using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using R3;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// フィルタ条件の組（組の中は AND、組どうしは OR）の判定・編集・プリセット保存
/// </summary>
public class FilterGroupsTests
{
    private static Beatmap Map(
        string md5,
        int keyCount = 7,
        BeatmapStatus status = BeatmapStatus.Ranked,
        double bpm = 180,
        DateTime? lastPlayed = null) => new()
    {
        MD5Hash = md5,
        KeyCount = keyCount,
        Status = status,
        Title = md5,
        Artist = "Artist",
        Version = "Insane",
        Creator = "Mapper",
        BPM = bpm,
        LastPlayed = lastPlayed,
        FolderName = "f",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty,
    };

    private static FilterCondition Keys(int keyCount) => new() { Target = FilterTarget.KeyCount, Value = keyCount.ToString() };

    private static FilterCondition Status(BeatmapStatus status) => new() { Target = FilterTarget.Status, StatusValue = status };

    private static FilterGroupsEditorViewModel Editor(params FilterCondition[][] groups)
    {
        var editor = new FilterGroupsEditorViewModel(new MockDatabaseService());
        for (var i = 0; i < groups.Length; i++)
        {
            if (i > 0) editor.Groups.Add(new FilterConditionGroup());
            foreach (var condition in groups[i])
                editor.Groups[i].Conditions.Add(condition);
        }
        return editor;
    }

    private static string[] Matched(FilterGroupsEditorViewModel editor, params Beatmap[] maps) =>
        maps.Where(editor.Matches).Select(m => m.MD5Hash).ToArray();

    // ---- 判定 ----

    [Fact]
    public void Groups_AreOrOfAnds()
    {
        // (4K かつ Ranked) または 7K
        var editor = Editor([Keys(4), Status(BeatmapStatus.Ranked)], [Keys(7)]);

        var result = Matched(editor,
            Map("4k-ranked", keyCount: 4),
            Map("4k-loved", keyCount: 4, status: BeatmapStatus.Loved),
            Map("7k-loved", keyCount: 7, status: BeatmapStatus.Loved),
            Map("5k-ranked", keyCount: 5));

        Assert.Equal(["4k-ranked", "7k-loved"], result);
    }

    [Fact]
    public void EmptyCondition_IsIgnored_AndDoesNotMakeGroupMatchEverything()
    {
        // 2 つ目の組を追加しただけ（値が未入力）のときは、1 つ目の組だけで絞り込む
        var editor = Editor([Keys(4)], [new FilterCondition { Target = FilterTarget.KeyCount }]);

        Assert.Equal(["4k"], Matched(editor, Map("4k", keyCount: 4), Map("7k", keyCount: 7)));
        Assert.True(editor.Groups[1].IsIgnored);
        Assert.False(editor.Groups[0].IsIgnored);
    }

    [Fact]
    public void NoActiveCondition_MatchesEverything()
    {
        var editor = Editor([new FilterCondition { Target = FilterTarget.Title }]);

        Assert.Equal(2, Matched(editor, Map("a"), Map("b")).Length);
    }

    [Fact]
    public void NotOnEmptyCondition_IsIgnored()
    {
        // 以前は NOT を付けた未入力の条件で一覧が空になっていた
        var editor = Editor([new FilterCondition { Target = FilterTarget.KeyCount, IsNot = true }]);

        Assert.Equal(2, Matched(editor, Map("a"), Map("b")).Length);
    }

    [Fact]
    public void Exclude_InvertsActiveCondition()
    {
        var editor = Editor([new FilterCondition { Target = FilterTarget.KeyCount, Value = "7", IsNot = true }]);

        Assert.Equal(["4k"], Matched(editor, Map("4k", keyCount: 4), Map("7k", keyCount: 7)));
    }

    [Theory]
    [InlineData(RangeBoundaryType.Inclusive, new[] { "150", "160" })]
    [InlineData(RangeBoundaryType.Exclusive, new[] { "150" })]
    public void Range_WithOnlyMax_FiltersByMax(RangeBoundaryType maxBoundary, string[] expected)
    {
        var condition = new FilterCondition { Target = FilterTarget.BPM };
        condition.ComparisonType = ComparisonType.Range;
        condition.ValueMax = "160";
        condition.MaxBoundaryType = maxBoundary;
        var editor = Editor([condition]);

        Assert.True(condition.IsActive);
        Assert.Equal(expected, Matched(editor, Map("150", bpm: 150), Map("160", bpm: 160), Map("170", bpm: 170)));
    }

    [Theory]
    [InlineData(RangeBoundaryType.Inclusive, RangeBoundaryType.Exclusive, new[] { "150", "160" })]
    [InlineData(RangeBoundaryType.Exclusive, RangeBoundaryType.Inclusive, new[] { "160", "170" })]
    public void Range_Boundaries(RangeBoundaryType min, RangeBoundaryType max, string[] expected)
    {
        var condition = new FilterCondition { Target = FilterTarget.BPM };
        condition.ComparisonType = ComparisonType.Range;
        condition.Value = "150";
        condition.ValueMax = "170";
        condition.MinBoundaryType = min;
        condition.MaxBoundaryType = max;
        var editor = Editor([condition]);

        Assert.Equal(expected, Matched(editor, Map("150", bpm: 150), Map("160", bpm: 160), Map("170", bpm: 170)));
    }

    [Fact]
    public void EmptyDateCondition_DoesNotExcludeUnplayedMaps()
    {
        var editor = Editor([new FilterCondition { Target = FilterTarget.LastPlayed }]);

        Assert.Equal(2, Matched(editor, Map("played", lastPlayed: new DateTime(2025, 1, 1)), Map("unplayed")).Length);
    }

    [Fact]
    public void IsActive_DependsOnInput()
    {
        Assert.False(new FilterCondition { Target = FilterTarget.KeyCount }.IsActive);
        Assert.False(new FilterCondition { Target = FilterTarget.KeyCount, Value = "abc" }.IsActive);
        Assert.True(Keys(7).IsActive);
        Assert.True(Status(BeatmapStatus.Loved).IsActive);
        Assert.False(new FilterCondition { Target = FilterTarget.Collection }.IsActive);
        Assert.True(new FilterCondition { Target = FilterTarget.Collection, CollectionValue = "x" }.IsActive);
        Assert.True(new FilterCondition { Target = FilterTarget.ManiaDanRegularGenre }.IsActive); // 先頭の選択肢が入る
    }

    // ---- 編集 ----

    [Fact]
    public void RemovingLastConditionOfGroup_RemovesGroup_ButKeepsOneGroup()
    {
        var four = Keys(4);
        var seven = Keys(7);
        var editor = Editor([four], [seven]);
        Assert.True(editor.HasMultipleGroups);

        editor.RemoveConditionCommand.Execute(seven);
        Assert.Single(editor.Groups);
        Assert.False(editor.HasMultipleGroups);

        editor.RemoveConditionCommand.Execute(four);
        Assert.Single(editor.Groups);
        Assert.Empty(editor.Groups[0].Conditions);
        Assert.False(editor.HasConditions);
    }

    [Fact]
    public void DuplicateGroup_IsDeepCopy_InsertedAfter()
    {
        var editor = Editor([Keys(4), Status(BeatmapStatus.Ranked)], [Keys(5)]);
        var source = editor.Groups[0];

        editor.DuplicateGroupCommand.Execute(source);

        Assert.Equal(3, editor.Groups.Count);
        var copy = editor.Groups[1];
        Assert.Equal(2, copy.Number);
        Assert.Equal(3, editor.Groups[2].Number);
        Assert.Equal(2, copy.Conditions.Count);
        Assert.NotSame(source.Conditions[0], copy.Conditions[0]);

        copy.Conditions[0].Value = "7";
        Assert.Equal("4", source.Conditions[0].Value);
    }

    [Fact]
    public void AddGroup_OnEmptyEditor_AddsConditionToFirstGroup()
    {
        var editor = new FilterGroupsEditorViewModel(new MockDatabaseService());
        Assert.False(editor.DuplicateGroupCommand.CanExecute(editor.Groups[0])); // 空の組は複製しない

        editor.AddGroupCommand.Execute(null);

        Assert.Single(editor.Groups);
        Assert.Single(editor.Groups[0].Conditions);
        Assert.True(editor.DuplicateGroupCommand.CanExecute(editor.Groups[0]));

        editor.AddGroupCommand.Execute(null);
        Assert.Equal(2, editor.Groups.Count);
        Assert.Single(editor.Groups[1].Conditions);
    }

    [Fact]
    public void ConditionLimit_IsTotalAcrossGroups()
    {
        var editor = new FilterGroupsEditorViewModel(new MockDatabaseService());
        for (var i = 0; i < FilterGroupsEditorViewModel.MaxConditions - 1; i++)
            editor.AddConditionCommand.Execute(null);
        editor.AddGroupCommand.Execute(null);

        Assert.Equal(FilterGroupsEditorViewModel.MaxConditions, editor.ConditionCount);
        Assert.False(editor.AddConditionCommand.CanExecute(null));
        Assert.False(editor.AddGroupCommand.CanExecute(null));
        Assert.False(editor.DuplicateGroupCommand.CanExecute(editor.Groups[1]));
    }

    [Fact]
    public void Changed_FiresOnStructureAndValueChanges_AndOnceForLoad()
    {
        var editor = new FilterGroupsEditorViewModel(new MockDatabaseService());
        var count = 0;
        using var _ = editor.Changed.Subscribe(_ => count++);

        editor.AddConditionCommand.Execute(null);
        Assert.Equal(1, count);

        editor.Groups[0].Conditions[0].Value = "7";
        Assert.True(count >= 2);

        editor.AddGroupCommand.Execute(null);
        var beforeNewGroupEdit = count;
        editor.Groups[1].Conditions[0].Value = "4";
        Assert.True(count > beforeNewGroupEdit);

        var beforeLoad = count;
        editor.Load(FilterPreset.Create("p", "p", [
            [FilterConditionData.FromFilterCondition(Keys(4))],
            [FilterConditionData.FromFilterCondition(Keys(7))],
        ]));
        Assert.Equal(beforeLoad + 1, count);
    }

    [Fact]
    public void Clear_LeavesOneEmptyGroup()
    {
        var editor = Editor([Keys(4)], [Keys(7)]);

        editor.Clear();

        Assert.Single(editor.Groups);
        Assert.Empty(editor.Groups[0].Conditions);
        Assert.Equal(0, editor.ConditionCount);
    }

    // ---- プリセット ----

    private static FilterPreset LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Presets", name);
        var preset = JsonSerializer.Deserialize(File.ReadAllText(path), FilterPresetJsonContext.Default.FilterPreset);
        Assert.NotNull(preset);
        return preset;
    }

    [Theory]
    [InlineData("legacy.json", 3)]
    [InlineData("no_or_v3_1.json", 5)]
    public void OldPresets_LoadAsSingleGroup(string fixture, int conditionCount)
    {
        var preset = LoadFixture(fixture);
        var editor = new FilterGroupsEditorViewModel(new MockDatabaseService());

        editor.Load(preset);

        Assert.Single(editor.Groups);
        Assert.Equal(conditionCount, editor.Groups[0].Conditions.Count);
        Assert.Null(preset.Groups);
    }

    [Fact]
    public void V31Preset_KeepsConditionValues()
    {
        var editor = new FilterGroupsEditorViewModel(new MockDatabaseService());
        editor.Load(LoadFixture("no_or_v3_1.json"));

        var conditions = editor.Groups[0].Conditions;
        Assert.Equal(FilterTarget.LongNoteRate, conditions[1].Target);
        Assert.Equal(RangeBoundaryType.Exclusive, conditions[1].MaxBoundaryType);
        Assert.True(conditions[2].IsNot);
        Assert.Equal(BeatmapStatus.Loved, conditions[2].StatusValue);
        Assert.Equal("Tech", conditions[3].ChoiceValue);
        Assert.Equal(ModCategory.DoubleTime, conditions[4].ScoreModCategory);
        Assert.Equal(ScoreSystemCategory.ScoreV2, conditions[4].ScoreSystemCategory);
    }

    [Fact]
    public void SingleGroup_SavesInLegacyConditionsFormat()
    {
        var editor = Editor([Keys(7), Status(BeatmapStatus.Ranked)]);

        var json = JsonSerializer.Serialize(editor.ToPreset("p", "c"), FilterPresetJsonContext.Default.FilterPreset);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(2, doc.RootElement.GetProperty("conditions").GetArrayLength());
        Assert.False(doc.RootElement.TryGetProperty("groups", out _));
    }

    [Fact]
    public void MultipleGroups_SaveAsGroups_AndRoundTrip()
    {
        var editor = Editor([Keys(4), Status(BeatmapStatus.Ranked)], [Keys(7)]);

        var json = JsonSerializer.Serialize(editor.ToPreset("p", "c"), FilterPresetJsonContext.Default.FilterPreset);
        using (var doc = JsonDocument.Parse(json))
        {
            Assert.Equal(0, doc.RootElement.GetProperty("conditions").GetArrayLength());
            Assert.Equal(2, doc.RootElement.GetProperty("groups").GetArrayLength());
        }

        var restored = new FilterGroupsEditorViewModel(new MockDatabaseService());
        restored.Load(JsonSerializer.Deserialize(json, FilterPresetJsonContext.Default.FilterPreset));

        Assert.Equal(2, restored.Groups.Count);
        Assert.Equal(2, restored.Groups[0].Conditions.Count);
        Assert.Equal("7", restored.Groups[1].Conditions[0].Value);
        Assert.Equal(["4k-ranked", "7k"], Matched(restored,
            Map("4k-ranked", keyCount: 4),
            Map("4k-loved", keyCount: 4, status: BeatmapStatus.Loved),
            Map("7k", keyCount: 7, status: BeatmapStatus.Loved)));
    }

    [Fact]
    public void EmptyGroups_AreDroppedOnSave()
    {
        var editor = Editor([Keys(4)], []);

        var preset = editor.ToPreset("p", "c");

        Assert.Null(preset.Groups);
        Assert.Single(preset.Conditions);
    }

    [Fact]
    public void UsesAnalyze7k_LooksAtAllGroups()
    {
        var preset = FilterPreset.Create("p", "p", [
            [FilterConditionData.FromFilterCondition(Keys(7))],
            [FilterConditionData.FromFilterCondition(new FilterCondition { Target = FilterTarget.ManiaDanRegular, Value = "5" })],
        ]);

        Assert.True(preset.UsesAnalyze7k());
    }

    [Fact]
    public async Task BatchGenerate_UsesOrGroups()
    {
        var maps = new[]
        {
            Map("4k-ranked", keyCount: 4),
            Map("4k-loved", keyCount: 4, status: BeatmapStatus.Loved),
            Map("7k-loved", keyCount: 7, status: BeatmapStatus.Loved),
        };
        var presets = new MockFilterPresetService();
        presets.Presets.Add(FilterPreset.Create("p", "col", [
            [FilterConditionData.FromFilterCondition(Keys(4)), FilterConditionData.FromFilterCondition(Status(BeatmapStatus.Ranked))],
            [FilterConditionData.FromFilterCondition(Keys(7))],
        ]));
        var generator = new CapturingGenerateCollectionService();
        using var vm = new PresetEditorViewModel(presets, new MockDatabaseService(maps), generator);

        var (success, _) = await vm.ExecuteBatchGenerateCoreAsync();

        Assert.Equal(1, success);
        Assert.Equal(["4k-ranked", "7k-loved"], generator.Generated["col"].Select(m => m.MD5Hash).ToArray());
    }

    private sealed class CapturingGenerateCollectionService : IGenerateCollectionService
    {
        public Dictionary<string, Beatmap[]> Generated { get; } = new();
        public Observable<GenerationProgress> GenerationProgressObservable { get; } = Observable.Empty<GenerationProgress>();

        public Task<bool> GenerateCollection(string collectionName, Beatmap[] beatmaps)
        {
            Generated[collectionName] = beatmaps;
            return Task.FromResult(true);
        }

        public void Dispose() { }
    }
}
