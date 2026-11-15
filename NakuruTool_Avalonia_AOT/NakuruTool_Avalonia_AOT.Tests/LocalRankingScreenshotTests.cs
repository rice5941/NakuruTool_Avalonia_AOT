using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// ローカルランキングモードの表示切替・スクリーンショットテスト
/// </summary>
public class LocalRankingScreenshotTests
{
    private static string ScreenshotsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Screenshots");

    [AvaloniaFact]
    public void LocalRankingMode_TogglesColumns_AndProjectsRanking()
    {
        var db = new StubScoresDatabaseService(new Dictionary<string, LocalScore[]>
        {
            ["md5-a"] = Enumerable.Range(0, 8)
                .Select(i => new LocalScore(
                    new DateTime(2025, 1, 1 + i, 12, 0, 0, DateTimeKind.Utc).Ticks,
                    990_000 - i * 10_000, 0,
                    CountGeki: (ushort)(1500 - i * 20), Count300: (ushort)(300 + i), CountKatu: 20,
                    Count100: 5, Count50: 2, CountMiss: (ushort)i))
                .Append(new LocalScore(DateTime.UtcNow.Ticks, 999_999, OsuMods.DoubleTime, CountGeki: 1)) // DT は除外される
                .ToArray(),
            ["md5-b"] = [],
        });

        var vm = new TestListViewModel(new MockSettingsService(), db);
        vm.SetTestSource([CreateBeatmap("md5-a", "Ranking Song"), CreateBeatmap("md5-b", "No Score Song")]);

        var view = new MapListView { DataContext = vm };
        var window = new Window { Width = 1280, Height = 720, Content = view };
        window.Show();
        Render();

        var grid = view.FindControl<DataGrid>("MapListDataGrid")!;
        var rankingGrid = view.FindControl<DataGrid>("LocalRankingDataGrid")!;
        Assert.True(Column(grid, "BpmColumn").IsVisible);
        Assert.False(rankingGrid.IsEffectivelyVisible);

        // モード OFF でも既存スコアセル用の BestPlay は生成される。ランキングは生成しない
        Assert.Equal(990_000, vm.ShowBeatmaps[0].BestPlay!.Score);
        Assert.Null(vm.ShowBeatmaps[1].BestPlay);
        vm.SelectedBeatmap = vm.ShowBeatmaps[0];
        Assert.Empty(vm.LocalRanking);

        // トグルボタンでモード ON → 選択譜面のランキングが別 DataGrid に出る
        view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("LocalRankingToggleButton")!.IsChecked = true;
        Render();

        Assert.True(vm.IsLocalRankingMode);
        Assert.False(Column(grid, "BpmColumn").IsVisible);
        Assert.False(Column(grid, "LongNoteRateColumn").IsVisible);
        Assert.False(Column(grid, "HistoryColumn").IsVisible);
        Assert.False(Column(grid, "IsPlayedColumn").IsVisible);
        Assert.True(rankingGrid.IsEffectivelyVisible);
        Assert.True(vm.HasLocalRanking);
        Assert.Equal(8, vm.LocalRanking.Count);
        Assert.True(vm.LocalRanking.Zip(vm.LocalRanking.Skip(1)).All(p => p.First.Score >= p.Second.Score));

        Save(window, "MapListView_LocalRanking.png");

        // スコアセルの判定内訳ツールチップは廃止した（ランキング側・一覧側とも出さない）
        Assert.DoesNotContain(rankingGrid.GetVisualDescendants().OfType<Grid>(), g => ToolTip.GetTip(g) is LocalRankingEntry);
        Assert.DoesNotContain(grid.GetVisualDescendants().OfType<Grid>(), g => ToolTip.GetTip(g) is LocalRankingEntry);

        // Mod 切替でページ再投影され選択が外れても、対象譜面のランキングは追従する
        vm.SelectedModCategory = ModCategory.DoubleTime;
        Render();
        Assert.Equal(new[] { 999_999 }, vm.LocalRanking.Select(e => e.Score));
        vm.SelectedModCategory = ModCategory.NoMod;
        Render();
        Assert.Equal(8, vm.LocalRanking.Count);

        // スコアの無い譜面を選択すると空
        vm.SelectedBeatmap = vm.ShowBeatmaps[1];
        Render();
        Assert.False(vm.HasLocalRanking);
        Save(window, "MapListView_LocalRanking_Empty.png");

        // モード OFF で元に戻る
        vm.IsLocalRankingMode = false;
        Render();
        Assert.True(Column(grid, "BpmColumn").IsVisible);
        Assert.True(Column(grid, "HistoryColumn").IsVisible);
        Assert.False(rankingGrid.IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void AnalysisMode_ShowsAnalyze7kColumnsAndExcludesLocalRanking()
    {
        var vm = new TestListViewModel(new MockSettingsService(), new StubScoresDatabaseService([]));
        vm.SetTestSource([CreateBeatmap("md5-a", "Song")]);
        var view = new MapListView { DataContext = vm };
        var window = new Window { Width = 1280, Height = 720, Content = view };
        window.Show();
        Render();

        var grid = view.FindControl<DataGrid>("MapListDataGrid")!;
        var analysisToggle = view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("AnalysisToggleButton")!;
        var rankingToggle = view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("LocalRankingToggleButton")!;
        var modePanel = view.FindControl<Border>("GenreDisplayModePanel")!;

        // 既定: 解析結果の列は出さず、履歴・プレイ済みを出す
        Assert.True(analysisToggle.IsEffectivelyVisible);
        Assert.False(Column(grid, "PrismColumn").IsVisible);
        Assert.False(Column(grid, "DanEstimateColumn").IsVisible);
        Assert.True(Column(grid, "HistoryColumn").IsVisible);
        Assert.True(Column(grid, "IsPlayedColumn").IsVisible);
        Assert.False(modePanel.IsVisible);

        // 解析結果を表示: Prism・段位推定を出し、履歴・プレイ済みを隠す（BPM・LN率は残す）
        analysisToggle.IsChecked = true;
        Render();
        Assert.True(vm.IsAnalysisMode);
        Assert.True(Column(grid, "PrismColumn").IsVisible);
        Assert.True(Column(grid, "DanEstimateColumn").IsVisible);
        Assert.False(Column(grid, "HistoryColumn").IsVisible);
        Assert.False(Column(grid, "IsPlayedColumn").IsVisible);
        Assert.True(Column(grid, "BpmColumn").IsVisible);
        Assert.True(modePanel.IsVisible);

        // ラジオボタンで基準スキル値（既定）/ ジャンル濃度を切り替える
        var concentration = view.FindControl<RadioButton>("GenreConcentrationRadio")!;
        var skill = view.FindControl<RadioButton>("GenreSkillValueRadio")!;
        Assert.True(vm.ShowGenreSkillValues);
        Assert.True(skill.IsChecked);
        Assert.False(concentration.IsChecked);
        concentration.IsChecked = true;
        Render();
        Assert.False(vm.ShowGenreSkillValues);
        Assert.False(skill.IsChecked);
        skill.IsChecked = true;
        Render();
        Assert.True(vm.ShowGenreSkillValues);
        Assert.False(concentration.IsChecked);
        vm.ShowGenreSkillValues = false;
        Render();
        Assert.True(concentration.IsChecked);
        Assert.False(skill.IsChecked);

        // ローカルランキングと排他
        rankingToggle.IsChecked = true;
        Render();
        Assert.True(vm.IsLocalRankingMode);
        Assert.False(vm.IsAnalysisMode);
        Assert.False(analysisToggle.IsChecked);
        Assert.False(Column(grid, "PrismColumn").IsVisible);
        Assert.False(Column(grid, "HistoryColumn").IsVisible);
        Assert.False(modePanel.IsVisible);

        analysisToggle.IsChecked = true;
        Render();
        Assert.True(vm.IsAnalysisMode);
        Assert.False(vm.IsLocalRankingMode);
        Assert.False(rankingToggle.IsChecked);
        Assert.True(Column(grid, "PrismColumn").IsVisible);
        Assert.True(Column(grid, "BpmColumn").IsVisible);

        // 解除で元に戻る
        analysisToggle.IsChecked = false;
        Render();
        Assert.False(Column(grid, "PrismColumn").IsVisible);
        Assert.True(Column(grid, "HistoryColumn").IsVisible);
        Assert.True(Column(grid, "IsPlayedColumn").IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ShowAnalyze7kColumnsFalse_HidesAnalysisButtonAndColumns()
    {
        var vm = new TestListViewModel(new MockSettingsService(), new StubScoresDatabaseService([]));
        vm.IsAnalysisMode = true;
        var view = new MapListView { DataContext = vm, ShowAnalyze7kColumns = false };
        var window = new Window { Width = 1280, Height = 720, Content = view };
        window.Show();
        Render();

        var grid = view.FindControl<DataGrid>("MapListDataGrid")!;
        Assert.False(view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("AnalysisToggleButton")!.IsEffectivelyVisible);
        Assert.False(Column(grid, "PrismColumn").IsVisible);
        Assert.False(Column(grid, "DanEstimateColumn").IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ShowLocalRankingButtonFalse_HidesToggle()
    {
        var vm = new TestListViewModel(new MockSettingsService(), new StubScoresDatabaseService([]));
        var view = new MapListView { DataContext = vm, ShowLocalRankingButton = false };
        var window = new Window { Width = 1280, Height = 720, Content = view };
        window.Show();
        Render();

        var toggle = view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("LocalRankingToggleButton")!;
        Assert.False(toggle.IsEffectivelyVisible);

        view.ShowLocalRankingButton = true;
        Render();
        Assert.True(toggle.IsEffectivelyVisible);

        window.Close();
    }

    private static DataGridColumn Column(DataGrid grid, string tag)
        => grid.Columns.First(c => c.Tag as string == tag);

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Save(Window window, string fileName)
    {
        Directory.CreateDirectory(ScreenshotsDirectory);
        var path = Path.Combine(ScreenshotsDirectory, fileName);
        window.CaptureRenderedFrame()?.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(path));
    }

    private static Beatmap CreateBeatmap(string md5, string title) => new()
    {
        MD5Hash = md5,
        KeyCount = 7,
        Title = title,
        Artist = "Artist",
        Version = "Insane",
        Creator = "Mapper",
        BPM = 180,
        Difficulty = 4.2,
        LongNoteRate = 0.25,
        FolderName = "folder",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty,
        BestScoreNoMod = 990_000,
        BestAccuracyNoMod = 98.7,
        GradeNoMod = "S",
    };

    private sealed class TestListViewModel(ISettingsService settingsService, IDatabaseService databaseService)
        : BeatmapListViewModelBase(settingsService, databaseService)
    {
        public void SetTestSource(Beatmap[] source) => SetSourceBeatmaps(source);
    }

    private sealed class StubScoresDatabaseService(Dictionary<string, LocalScore[]> scores) : IDatabaseService
    {
        public Observable<DatabaseLoadProgress> CollectionDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
        public Observable<DatabaseLoadProgress> OsuDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
        public Observable<DatabaseLoadProgress> ScoresDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
        public List<OsuCollection> OsuCollections { get; } = [];
        public Beatmap[] Beatmaps { get; } = [];
        public Task LoadDatabasesAsync() => Task.CompletedTask;
        public Task ReloadDatabasesAsync() => Task.CompletedTask;
        public Task ReloadCollectionDbAsync() => Task.CompletedTask;
        public bool TryGetBeatmapByMd5(string md5Hash, out Beatmap? beatmap) { beatmap = null; return false; }
        public ReadOnlySpan<LocalScore> GetLocalScores(string md5Hash)
            => scores.TryGetValue(md5Hash, out var s) ? s : ReadOnlySpan<LocalScore>.Empty;
        public IReadOnlySet<string> ApplyChartAnalyses(Func<Beatmap, ManiaChartAnalysis?> resolver)
            => new HashSet<string>();
        public void Dispose() { }
    }
}
