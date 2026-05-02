using System.Linq;
using NakuruTool_Avalonia_AOT.Features.MapList.Sorting;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// 設計書 §3.4 / §9.6 — active rule が 0 件のとき
/// <see cref="BeatmapListViewModelBase"/> が基準配列 (DB 順 / 派生順) を維持することを固定する。
/// </summary>
public class BeatmapListViewModelBaseSortIntegrationTests
{
    private static Beatmap[] CreateBeatmaps()
    {
        // タイトルがインサート順と逆になるよう設定し、Title ソートで順序が変わることを保証する。
        return new[]
        {
            CreateBeatmap("Beatmap0", "Echo"),
            CreateBeatmap("Beatmap1", "Delta"),
            CreateBeatmap("Beatmap2", "Charlie"),
            CreateBeatmap("Beatmap3", "Bravo"),
            CreateBeatmap("Beatmap4", "Alpha"),
        };
    }

    private static Beatmap CreateBeatmap(string md5, string title) => new()
    {
        MD5Hash = md5,
        KeyCount = 7,
        Title = title,
        Artist = "artist",
        Version = "version",
        Creator = "creator",
        FolderName = "folder",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty
    };

    [Fact]
    public void Reset_RestoresOriginalInsertionOrder()
    {
        var settings = new SettingsData();
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        vm.SetTestSource(CreateBeatmaps());

        // ソート発動: Title 昇順
        vm.SortViewModel.Primary.Field = SortField.Title;
        var sortedTitles = vm.ShowBeatmaps.Select(b => b.Title).ToArray();
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" }, sortedTitles);

        // Reset で元順序に戻る
        vm.SortViewModel.ResetCommand.Execute(null);
        var resetMd5 = vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray();
        Assert.Equal(new[] { "Beatmap0", "Beatmap1", "Beatmap2", "Beatmap3", "Beatmap4" }, resetMd5);
    }

    [Theory]
    [InlineData("Mod")]
    [InlineData("ScoreSystem")]
    [InlineData("PreferUnicode")]
    public void NoActiveRule_OrderRemainsStable_AcrossModeChanges(string trigger)
    {
        var settings = new SettingsData();
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        vm.SetTestSource(CreateBeatmaps());

        // 全 None のまま (active rule 0 件)
        Assert.Empty(vm.SortViewModel.GetActiveRules());

        switch (trigger)
        {
            case "Mod":
                vm.SelectedModCategory = ModCategory.DoubleTime;
                break;
            case "ScoreSystem":
                vm.SelectedScoreSystemCategory = ScoreSystemCategory.ScoreV2;
                break;
            case "PreferUnicode":
                settings.PreferUnicode = !settings.PreferUnicode;
                break;
        }

        var md5 = vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray();
        Assert.Equal(new[] { "Beatmap0", "Beatmap1", "Beatmap2", "Beatmap3", "Beatmap4" }, md5);
    }

    [Fact]
    public void SourceBeatmapsArrayInstance_StaysStable_AcrossSortChanges()
    {
        // AudioPlayerPanelViewModel が SourceBeatmapsRaw 参照を保持する仕様のため、
        // ソート変更後も同一インスタンスで中身だけが並べ替わる必要がある。
        var settings = new SettingsData();
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        vm.SetTestSource(CreateBeatmaps());

        var capturedRef = vm.GetRawForTest();
        Assert.Equal("Beatmap0", capturedRef[0].MD5Hash);

        // Title 昇順ソート
        vm.SortViewModel.Primary.Field = SortField.Title;

        Assert.Same(capturedRef, vm.GetRawForTest());
        Assert.Equal("Alpha", capturedRef[0].Title);
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" },
            capturedRef.Select(b => b.Title).ToArray());

        // Reset で元順序に戻る
        vm.SortViewModel.ResetCommand.Execute(null);

        Assert.Same(capturedRef, vm.GetRawForTest());
        Assert.Equal("Beatmap0", capturedRef[0].MD5Hash);
        Assert.Equal(new[] { "Beatmap0", "Beatmap1", "Beatmap2", "Beatmap3", "Beatmap4" },
            capturedRef.Select(b => b.MD5Hash).ToArray());
    }

    // ---------------- §9.8 / §9.9 — 依存変更で active rule のソートが再適用される ----------------

    /// <summary>
    /// §9.8-1: BestScore ソート中に SelectedModCategory が切り替わると順序が更新される。
    /// </summary>
    [Fact]
    public void ApplyFilter_ReSortsWhenSelectedModCategoryChanges_WithBestScoreRule()
    {
        var settings = new SettingsData();
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        // NoMod と DT の BestScore が逆順になるよう仕込む。
        var beatmaps = new[]
        {
            CreateScoreBeatmap("B0", bestScoreNoMod: 100, bestScoreDT: 500),
            CreateScoreBeatmap("B1", bestScoreNoMod: 200, bestScoreDT: 400),
            CreateScoreBeatmap("B2", bestScoreNoMod: 300, bestScoreDT: 300),
            CreateScoreBeatmap("B3", bestScoreNoMod: 400, bestScoreDT: 200),
            CreateScoreBeatmap("B4", bestScoreNoMod: 500, bestScoreDT: 100),
        };
        vm.SetTestSource(beatmaps);

        vm.SortViewModel.Primary.Field = SortField.BestScore;
        vm.SortViewModel.Primary.Direction = SortDirection.Ascending;

        // 既定 (NoMod) では NoMod スコアの昇順
        Assert.Equal(new[] { "B0", "B1", "B2", "B3", "B4" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());

        // Mod を DT に変更 → DT スコアの昇順 (= 元の逆順)
        vm.SelectedModCategory = ModCategory.DoubleTime;

        Assert.Equal(new[] { "B4", "B3", "B2", "B1", "B0" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());
    }

    /// <summary>
    /// §9.8-2: BestAccuracy ソート中に SelectedScoreSystemCategory が切り替わると順序が更新される。
    /// </summary>
    [Fact]
    public void ApplyFilter_ReSortsWhenSelectedScoreSystemCategoryChanges_WithBestAccuracyRule()
    {
        var settings = new SettingsData();
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        // Default(v1) と ScoreV2 の BestAccuracy(NoMod) が逆順になるよう仕込む。
        var beatmaps = new[]
        {
            CreateScoreBeatmap("B0", bestAccuracyNoMod: 0.90, bestAccuracyV2NoMod: 0.99),
            CreateScoreBeatmap("B1", bestAccuracyNoMod: 0.92, bestAccuracyV2NoMod: 0.97),
            CreateScoreBeatmap("B2", bestAccuracyNoMod: 0.94, bestAccuracyV2NoMod: 0.95),
            CreateScoreBeatmap("B3", bestAccuracyNoMod: 0.96, bestAccuracyV2NoMod: 0.93),
            CreateScoreBeatmap("B4", bestAccuracyNoMod: 0.98, bestAccuracyV2NoMod: 0.91),
        };
        vm.SetTestSource(beatmaps);

        vm.SortViewModel.Primary.Field = SortField.BestAccuracy;
        vm.SortViewModel.Primary.Direction = SortDirection.Ascending;

        // 既定 (Default) では v1 アキュラシーの昇順
        Assert.Equal(new[] { "B0", "B1", "B2", "B3", "B4" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());

        // ScoreV2 に切替 → v2 アキュラシーの昇順 (= 元の逆順)
        vm.SelectedScoreSystemCategory = ScoreSystemCategory.ScoreV2;

        Assert.Equal(new[] { "B4", "B3", "B2", "B1", "B0" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());
    }

    /// <summary>
    /// §9.8-3: Title ソート中に PreferUnicode を切り替えると Unicode タイトル順で再ソートされる。
    /// </summary>
    [Fact]
    public void ApplyFilter_ReSortsWhenPreferUnicodeChanges_WithTitleRule()
    {
        var settings = new SettingsData { PreferUnicode = false };
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        // ASCII Title と Unicode Title が逆順になるよう設定
        var beatmaps = new[]
        {
            CreateTitleBeatmap("B0", title: "Alpha", titleUnicode: "Echo"),
            CreateTitleBeatmap("B1", title: "Bravo", titleUnicode: "Delta"),
            CreateTitleBeatmap("B2", title: "Charlie", titleUnicode: "Charlie"),
            CreateTitleBeatmap("B3", title: "Delta", titleUnicode: "Bravo"),
            CreateTitleBeatmap("B4", title: "Echo", titleUnicode: "Alpha"),
        };
        vm.SetTestSource(beatmaps);

        vm.SortViewModel.Primary.Field = SortField.Title;
        vm.SortViewModel.Primary.Direction = SortDirection.Ascending;

        // PreferUnicode=false: ASCII Title 昇順
        Assert.Equal(new[] { "B0", "B1", "B2", "B3", "B4" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());

        // PreferUnicode=true に切替 → Unicode Title 昇順 (= 逆順)
        settings.PreferUnicode = true;

        Assert.Equal(new[] { "B4", "B3", "B2", "B1", "B0" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());
    }

    /// <summary>
    /// §9.9-2: SetSourceBeatmaps 後に SortRule を変更すると既存ソースを再ソートする。
    /// </summary>
    [Fact]
    public void SortViewModel_ChangedAfterSetSource_ReSortsExistingSource()
    {
        var settings = new SettingsData();
        using var vm = new TestBeatmapListViewModel(new StubSettingsService(settings));

        vm.SetTestSource(CreateBeatmaps());

        // 初期は active rule なしなので DB 順
        Assert.Equal(new[] { "Beatmap0", "Beatmap1", "Beatmap2", "Beatmap3", "Beatmap4" },
            vm.ShowBeatmaps.Select(b => b.MD5Hash).ToArray());

        // ソースを差し替えずに SortRule を変更 → 再ソート (Title 昇順)
        vm.SortViewModel.Primary.Field = SortField.Title;

        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" },
            vm.ShowBeatmaps.Select(b => b.Title).ToArray());

        // 方向反転 → 同じソース上で再ソート (Title 降順)
        vm.SortViewModel.Primary.Direction = SortDirection.Descending;

        Assert.Equal(new[] { "Echo", "Delta", "Charlie", "Bravo", "Alpha" },
            vm.ShowBeatmaps.Select(b => b.Title).ToArray());
    }

    private static Beatmap CreateScoreBeatmap(
        string md5,
        int bestScoreNoMod = 0,
        int bestScoreDT = 0,
        double bestAccuracyNoMod = 0,
        double bestAccuracyV2NoMod = 0) => new()
    {
        MD5Hash = md5,
        KeyCount = 7,
        Title = md5,
        Artist = "artist",
        Version = "version",
        Creator = "creator",
        FolderName = "folder",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty,
        BestScoreNoMod = bestScoreNoMod,
        BestScoreDT = bestScoreDT,
        BestAccuracyNoMod = bestAccuracyNoMod,
        BestAccuracyV2NoMod = bestAccuracyV2NoMod,
    };

    private static Beatmap CreateTitleBeatmap(string md5, string title, string titleUnicode) => new()
    {
        MD5Hash = md5,
        KeyCount = 7,
        Title = title,
        TitleUnicode = titleUnicode,
        Artist = "artist",
        Version = "version",
        Creator = "creator",
        FolderName = "folder",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty,
    };

    private sealed class TestBeatmapListViewModel(ISettingsService settingsService)
        : BeatmapListViewModelBase(settingsService)
    {
        public void SetTestSource(Beatmap[] source) => SetSourceBeatmaps(source);
        internal Beatmap[] GetRawForTest() => SourceBeatmapsRaw;
    }

    private sealed class StubSettingsService(ISettingsData settingsData) : ISettingsService
    {
        public ISettingsData SettingsData { get; } = settingsData;
        public bool SaveSettings(SettingsData settings) => true;
        public bool CheckSettingsPath() => true;
        public string GetSettingsPath() => string.Empty;
        public void Dispose()
        {
        }
    }
}
