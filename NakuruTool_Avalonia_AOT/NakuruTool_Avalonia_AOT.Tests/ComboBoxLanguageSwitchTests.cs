using System.Linq;
using Avalonia.Controls;
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
/// LanguageService はプロセス全体で共有されるため、言語を切り替えるテストは他のテストと並列に走らせない
/// </summary>
[CollectionDefinition(nameof(LanguageSwitchCollection), DisableParallelization = true)]
public sealed class LanguageSwitchCollection;

/// <summary>
/// ComboBox のドロップダウン項目・選択中表示が言語切替に追従することを確認する。
/// ドロップダウン幅を固定するために ItemsPanel を非仮想化の StackPanel にしているため、
/// 項目コンテナが作り直されず、バインディング側で言語変更を検知できないと古い言語のまま残る。
/// </summary>
[Collection(nameof(LanguageSwitchCollection))]
public sealed class ComboBoxLanguageSwitchTests
{
    [AvaloniaFact]
    public void FilterComboBoxes_FollowLanguageChange()
    {
        var languageService = LanguageService.Instance;
        var originalLanguage = languageService.CurrentLanguage;
        languageService.ChangeLanguage("ja-JP");

        var vm = new MapFilterViewModel(new MockFilterPresetService(), new MockDatabaseService());
        vm.Editor.Clear();
        var condition = new FilterCondition { Target = FilterTarget.BestScore, ScoreModCategory = ModCategory.NoMod };
        vm.Editor.Groups[0].Conditions.Add(condition);

        var view = new MapFilterView { DataContext = vm };
        var window = new Window { Width = 420, Height = 900, Content = view };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();

            var targetCombo = FindComboBox<FilterTarget>(view);
            var modCombo = FindComboBox<ModCategory>(view);

            AssertComboTexts(targetCombo, FilterTarget.BestScore, "MapFilter.Target.BestScore", "MapFilter.Target.");
            AssertComboTexts(modCombo, ModCategory.NoMod, "MapList.ModCategory.NoMod", "MapList.ModCategory.");
            var japaneseTarget = languageService.GetString("MapFilter.Target.BestScore");
            var japaneseMod = languageService.GetString("MapList.ModCategory.NoMod");

            languageService.ChangeLanguage("en-US");
            Dispatcher.UIThread.RunJobs();

            // 前提: 日本語と英語で表示文字列が異なること（同じだと切替を検証できない）
            Assert.NotEqual(japaneseTarget, languageService.GetString("MapFilter.Target.BestScore"));
            Assert.NotEqual(japaneseMod, languageService.GetString("MapList.ModCategory.NoMod"));

            AssertComboTexts(targetCombo, FilterTarget.BestScore, "MapFilter.Target.BestScore", "MapFilter.Target.");
            AssertComboTexts(modCombo, ModCategory.NoMod, "MapList.ModCategory.NoMod", "MapList.ModCategory.");
        }
        finally
        {
            window.Close();
            languageService.ChangeLanguage(originalLanguage);
        }
    }

    private static ComboBox FindComboBox<TItem>(Control root)
    {
        return root.GetVisualDescendants().OfType<ComboBox>()
            // 列挙型配列は基底型が同じだと is で相互に一致してしまうため、型を厳密に比較する
            .First(c => c.IsEffectivelyVisible && c.ItemsSource?.GetType() == typeof(TItem[]));
    }

    /// <summary>
    /// ドロップダウンを開いて全項目の表示と、閉じた状態の選択中表示が現在の言語になっていることを確認する
    /// </summary>
    private static void AssertComboTexts<TItem>(ComboBox combo, TItem selected, string selectedKey, string keyPrefix)
        where TItem : struct, System.Enum
    {
        var languageService = LanguageService.Instance;

        combo.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();

        var items = (TItem[])combo.ItemsSource!;
        var containers = combo.GetRealizedContainers().OfType<ComboBoxItem>().ToList();
        Assert.Equal(items.Length, containers.Count); // 非仮想化で全項目が実体化されている前提
        foreach (var container in containers)
        {
            var item = (TItem)container.DataContext!;
            var text = container.GetVisualDescendants().OfType<TextBlock>().First().Text;
            Assert.Equal(languageService.GetString($"{keyPrefix}{item}"), text);
        }

        combo.IsDropDownOpen = false;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(selected, combo.SelectedItem);
        var expectedSelected = languageService.GetString(selectedKey);
        Assert.Contains(combo.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == expectedSelected);
    }
}
