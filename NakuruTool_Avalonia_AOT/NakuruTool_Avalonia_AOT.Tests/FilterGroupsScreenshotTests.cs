using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Translate;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// フィルタ条件パネル（幅 320px）の条件の組の表示
/// </summary>
public class FilterGroupsScreenshotTests
{
    private static string ScreenshotsDirectory => Path.Combine(AppContext.BaseDirectory, "Screenshots");

    private static FilterCondition Keys(int keyCount) => new() { Target = FilterTarget.KeyCount, Value = keyCount.ToString() };

    private static (Window Window, MapFilterView View) Show(MapFilterViewModel vm, int height = 900)
    {
        var view = new MapFilterView { DataContext = vm };
        var window = new Window { Width = 320, Height = height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, view);
    }

    private static void Save(Window window, string name)
    {
        Directory.CreateDirectory(ScreenshotsDirectory);
        var path = Path.Combine(ScreenshotsDirectory, name);
        window.CaptureRenderedFrame()?.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(path));
    }

    private static Control[] Visible(MapFilterView view, string className) =>
        view.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Classes.Contains(className) && c.IsEffectivelyVisible)
            .ToArray();

    /// <summary>組が 1 つでも枠と見出しを出す（組の削除と「または」は出さない）。条件の間に「かつ」を出す</summary>
    [AvaloniaFact]
    public void SingleGroup_ShowsCard()
    {
        LanguageService.Instance.ChangeLanguage("ja-JP");
        var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());
        vm.Editor.Groups[0].Conditions.Add(Keys(7));
        vm.Editor.Groups[0].Conditions.Add(new FilterCondition { Target = FilterTarget.Status, StatusValue = BeatmapStatus.Ranked });

        var (window, view) = Show(vm);

        Assert.Single(Visible(view, "group-card"));
        Assert.Single(Visible(view, "group-header"));
        Assert.Empty(Visible(view, "remove-group"));
        Assert.Empty(Visible(view, "or-connector"));
        Assert.Single(Visible(view, "and-connector"));
        Save(window, "FilterGroups_Single.png");
        window.Close();
    }

    /// <summary>条件がないときは組の枠に「未入力のため無視」を出さない</summary>
    [AvaloniaFact]
    public void EmptyEditor_DoesNotShowIgnoredLabel()
    {
        LanguageService.Instance.ChangeLanguage("ja-JP");
        var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());

        var (window, _) = Show(vm, height: 400);

        Assert.False(vm.Editor.Groups[0].IsIgnored);
        Save(window, "FilterGroups_Empty.png");
        window.Close();
    }

    /// <summary>組が 2 つ以上のときは「条件の組 N」の枠と、組の間に「または」を出す</summary>
    [AvaloniaFact]
    public void MultipleGroups_ShowCardsAndOrConnector()
    {
        LanguageService.Instance.ChangeLanguage("ja-JP");
        var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());
        vm.Editor.Groups[0].Conditions.Add(Keys(4));
        vm.Editor.Groups[0].Conditions.Add(new FilterCondition { Target = FilterTarget.Status, StatusValue = BeatmapStatus.Ranked });
        vm.Editor.AddGroupCommand.Execute(null);
        vm.Editor.Groups[1].Conditions[0].Value = "7";
        vm.Editor.AddGroupCommand.Execute(null); // 未入力のため無視される組

        var (window, view) = Show(vm, height: 1000);

        Assert.Equal(3, Visible(view, "group-header").Length);
        Assert.Equal(3, Visible(view, "remove-group").Length);
        Assert.Equal(2, Visible(view, "or-connector").Length);
        Assert.Single(Visible(view, "and-connector"));
        Save(window, "FilterGroups_Multiple.png");
        window.Close();
    }

    /// <summary>範囲（以上・未満の選択、最大値だけ）とスコア・日付の条件を、組の枠の中の狭い幅で表示する</summary>
    [AvaloniaFact]
    public void RangeConditions_FitInGroupCard()
    {
        LanguageService.Instance.ChangeLanguage("ja-JP");
        var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());

        var acc = new FilterCondition { Target = FilterTarget.BestAccuracy, ScoreSystemCategory = ScoreSystemCategory.ScoreV2, ScoreModCategory = ModCategory.DoubleTime };
        acc.ComparisonType = ComparisonType.Range;
        acc.Value = "90";
        acc.ValueMax = "91";
        acc.MaxBoundaryType = RangeBoundaryType.Exclusive;
        vm.Editor.Groups[0].Conditions.Add(acc);

        var played = new FilterCondition { Target = FilterTarget.LastPlayed, IsNot = true };
        played.DateValue = new DateTime(2025, 1, 1);
        played.DateValueMax = new DateTime(2025, 12, 31);
        vm.Editor.Groups[0].Conditions.Add(played);

        vm.Editor.AddGroupCommand.Execute(null);
        var bpm = vm.Editor.Groups[1].Conditions[0];
        bpm.Target = FilterTarget.BPM;
        bpm.ComparisonType = ComparisonType.Range;
        bpm.ValueMax = "160";

        var (window, _) = Show(vm, height: 1000);

        Save(window, "FilterGroups_Range.png");
        window.Close();
    }

    /// <summary>ダークテーマでも組の枠・「または」・セグメントが見分けられる</summary>
    [AvaloniaFact]
    public void MultipleGroups_DarkTheme()
    {
        LanguageService.Instance.ChangeLanguage("ja-JP");
        var app = Avalonia.Application.Current!;
        var original = app.RequestedThemeVariant;
        app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        try
        {
            var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());
            vm.Editor.Groups[0].Conditions.Add(Keys(4));
            vm.Editor.Groups[0].Conditions.Add(new FilterCondition { Target = FilterTarget.Status, StatusValue = BeatmapStatus.Ranked });
            vm.Editor.AddGroupCommand.Execute(null);
            vm.Editor.Groups[1].Conditions[0].Value = "7";

            var (window, _) = Show(vm);
            Save(window, "FilterGroups_Dark.png");
            window.Close();
        }
        finally
        {
            app.RequestedThemeVariant = original;
        }
    }
}
