using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Translate;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// beatmap一覧の Prism 列・段位推定列・解析の進み具合のスクリーンショット（ジャンル濃度の五角形 / 基準スキル値の横棒の両モード）
/// </summary>
public class Analyze7kScreenshotTests
{
    private static string ScreenshotsDirectory => Path.Combine(AppContext.BaseDirectory, "Screenshots");

    private static Beatmap Map(string md5, string title, ManiaChartAnalysis? analysis, int keyCount = 7) => new()
    {
        MD5Hash = md5,
        KeyCount = keyCount,
        Status = BeatmapStatus.Ranked,
        Title = title,
        Artist = "Artist",
        Version = "7K Insane",
        Creator = "Mapper",
        BPM = 180,
        Difficulty = 6.2,
        LongNoteRate = analysis?.PrismLnOverall is null ? 0.05 : 0.62,
        FolderName = "f",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty,
        ChartAnalysis = analysis,
    };

    private static List<Beatmap> TestBeatmaps() =>
    [
        Map("jack", "Jack heavy chart", new ManiaChartAnalysis
        {
            PrismOverall = 37.95,
            GenreSkills = new GenreValues(30.1f, 28.4f, 27.0f, 33.2f, 37.95f),
            GenreTendencies = new GenreValues(20, 14, 10, 30, 52),
            RegularDan = 9.18,
            // LN 段位はあるが LN ジャンルなし（主分類は Regular）→ 参考値として薄く表示
            LnDan = 9.8,
            PrimaryFamily = DanSide.Regular,
            RegularGenres = RegularDanGenre.Jack,
            Eligible = true,
        }),
        Map("ln", "LN chart", new ManiaChartAnalysis
        {
            PrismOverall = 31.2,
            PrismLnOverall = 41.6,
            GenreSkills = new GenreValues(35.4f, 29.0f, 26.1f, 31.0f, 22.3f),
            GenreTendencies = new GenreValues(40, 18, 12, 24, 3),
            RegularDan = 10.4,
            LnDan = 12.6,
            PrimaryFamily = DanSide.Ln,
            RegularGenres = RegularDanGenre.Stream | RegularDanGenre.Tech,
            LnGenres = LnDanGenre.General | LnDanGenre.Inverse,
            Eligible = true,
        }),
        Map("vibro", "Vibro chart", new ManiaChartAnalysis
        {
            PrismOverall = 44.0,
            GenreSkills = new GenreValues(20, 18, 19, 25, 44),
            GenreTendencies = new GenreValues(5, 0, 3, 12, 85),
            RegularDan = 14.5,
            PrimaryFamily = DanSide.Regular,
            RegularGenres = RegularDanGenre.Jack,
            Eligible = true,
            Vibro = true,
        }),
        Map("4k", "4K chart (no analysis)", null, keyCount: 4),
    ];

    [AvaloniaTheory]
    [InlineData(false, "Analyze7k_Concentration.png")]
    [InlineData(true, "Analyze7k_SkillValues.png")]
    public void CaptureAnalyze7kColumns(bool showSkillValues, string fileName)
    {
        var vm = new MockMapListViewModel
        {
            IsAnalysisMode = true,
            ShowGenreSkillValues = showSkillValues,
            Analyze7kStatus = Analyze7kProgressDisplay.From(
                new Analyze7kProgress(Analyze7kState.Running, 12_345, 43_673, 3, TimeSpan.FromMinutes(5.4)),
                LanguageService.Instance.GetString),
        };
        vm.SetTestData(TestBeatmaps());
        var view = new MapListView { DataContext = vm };
        var window = new Window { Width = 1920, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var grid = view.FindControl<DataGrid>("MapListDataGrid")!;
        if (grid.ItemsSource is null)
        {
            grid.ItemsSource = vm.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Assert.True(grid.Columns.First(c => (c.Tag as string) == "PrismColumn").IsVisible);
        Assert.True(grid.Columns.First(c => (c.Tag as string) == "DanEstimateColumn").IsVisible);
        Assert.Equal(showSkillValues, view.ShowGenreSkillValues);
        Assert.False(grid.Columns.First(c => (c.Tag as string) == "HistoryColumn").IsVisible);
        Assert.False(grid.Columns.First(c => (c.Tag as string) == "IsPlayedColumn").IsVisible);
        Assert.Equal(!showSkillValues, view.FindControl<RadioButton>("GenreConcentrationRadio")!.IsChecked);
        Assert.Equal(showSkillValues, view.FindControl<RadioButton>("GenreSkillValueRadio")!.IsChecked);
        Assert.True(view.FindControl<Border>("Analyze7kProgressPanel")!.IsVisible);

        // 段位推定の参考値（ジャンルなし・主分類ではない側）だけ薄く表示する
        var lnDans = grid.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "LnDanText" && t.IsVisible).ToList();
        var reference = Assert.Single(lnDans, t => t.Text!.Contains("(9.80)"));
        Assert.True(reference.Classes.Contains("a7k-reference"));
        Assert.True(reference.Opacity < 1);
        var genuine = Assert.Single(lnDans, t => t.Text!.Contains("(12.60)"));
        Assert.False(genuine.Classes.Contains("a7k-reference"));
        Assert.Equal(1, genuine.Opacity);

        var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory(ScreenshotsDirectory);
        var path = Path.Combine(ScreenshotsDirectory, fileName);
        frame?.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(path));
        window.Close();
    }

    /// <summary>フィルタ: ジャンル別の基準スキル値・ジャンル濃度は「比較対象」でジャンルを選び、範囲で絞り込む</summary>
    [AvaloniaFact]
    public void CaptureGenreFilters()
    {
        var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());
        vm.Conditions.Clear();
        var skill = new FilterCondition { Target = FilterTarget.ManiaPrismGenreSkill };
        skill.CompareValue = "LN";
        skill.Value = "30";
        skill.ValueMax = "40";
        var concentration = new FilterCondition { Target = FilterTarget.ManiaPrismGenreConcentration };
        concentration.CompareValue = "Jack";
        concentration.Value = "40";
        var top = new FilterCondition { Target = FilterTarget.ManiaPrismTopGenre };
        top.ChoiceValue = "LN";
        vm.Conditions.Add(skill);
        vm.Conditions.Add(concentration);
        vm.Conditions.Add(top);

        var view = new MapFilterView { DataContext = vm };
        var window = new Window { Width = 420, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var compareBoxes = view.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.IsEffectivelyVisible && (ReferenceEquals(c.ItemsSource, skill.CompareOptions) || ReferenceEquals(c.ItemsSource, concentration.CompareOptions)))
            .ToList();
        Assert.Contains(compareBoxes, c => Equals(c.SelectedItem, "LN"));
        Assert.Contains(compareBoxes, c => Equals(c.SelectedItem, "Jack"));

        var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory(ScreenshotsDirectory);
        var path = Path.Combine(ScreenshotsDirectory, "Analyze7k_GenreFilters.png");
        frame?.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(path));
        window.Close();
    }
}
