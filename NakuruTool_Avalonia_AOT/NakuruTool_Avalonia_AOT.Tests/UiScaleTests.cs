using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.Shared.Scaling;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// ポップアップ（別の TopLevel に描画される）が UI 表示倍率を引き継ぐことのテスト。
/// App.axaml / TestApp.axaml の Popup スタイル（InheritsTransform=True）が、
/// テンプレート内の Popup（ComboBox）とコードで生成される Popup（ToolTip / ContextMenu）の両方に効くことを確認する。
/// </summary>
public class UiScalePopupTests
{
    [AvaloniaFact]
    public void Popups_InheritUiScale()
    {
        UiScaleService.Instance.SetConfiguredPercent(100); // FHD 相当（Scale 0.8）
        try
        {
            var comboBox = new ComboBox { ItemsSource = new[] { "A", "B" } };
            var toolTip = new ToolTip { Content = "tip" };
            var contextMenu = new ContextMenu { ItemsSource = new[] { "Menu" } };
            var button = new Button { Content = "button", ContextMenu = contextMenu };
            ToolTip.SetTip(button, toolTip);

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = new UiScaleHost
                {
                    Child = new StackPanel { Children = { comboBox, button } },
                },
            };
            window.Show();
            PumpUi();

            // テンプレート内の Popup（ComboBox のドロップダウン）
            comboBox.IsDropDownOpen = true;
            PumpUi();
            var dropDown = comboBox.GetVisualDescendants().OfType<Popup>().First();
            AssertPopupHostScale(dropDown.Child!, 0.8);
            comboBox.IsDropDownOpen = false;
            PumpUi();

            // コードで生成される Popup（ToolTip）
            ToolTip.SetIsOpen(button, true);
            PumpUi();
            AssertPopupHostScale(toolTip, 0.8);
            ToolTip.SetIsOpen(button, false);
            PumpUi();

            // コードで生成される Popup（ContextMenu）
            contextMenu.Open(button);
            PumpUi();
            AssertPopupHostScale(contextMenu, 0.8);
            contextMenu.Close();
            PumpUi();

            window.Close();
        }
        finally
        {
            // 静的な倍率を他のテストに持ち越さない
            UiScaleService.Instance.SetConfiguredPercent(UiScaleService.DefaultPercent);
        }
    }

    /// <summary>
    /// ポップアップの中身が、描画先のルートに対して指定の倍率で描画されることを確認する。
    /// 実機では別ウィンドウ（PopupRoot）、ヘッドレスではウィンドウのオーバーレイ層（OverlayPopupHost）に描画されるが、
    /// どちらも UiScaleHost の外なので、倍率がかかっていれば InheritsTransform が効いている。
    /// </summary>
    private static void AssertPopupHostScale(Visual popupContent, double expected)
    {
        var root = TopLevel.GetTopLevel(popupContent);
        Assert.NotNull(root);
        Assert.DoesNotContain(popupContent.GetVisualAncestors(), v => v is UiScaleHost);
        var matrix = popupContent.TransformToVisual(root);
        Assert.NotNull(matrix);
        Assert.Equal(expected, matrix.Value.M11, 6);
        Assert.Equal(expected, matrix.Value.M22, 6);
    }

    private static void PumpUi()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}

/// <summary>
/// UI 表示倍率の自動計算（<see cref="UiScaleCalculator"/>）のテスト。
/// 倍率（%）は FHD が 100%、開発時の大きさ（WQHD・拡大率 100%）が 125%。
/// </summary>
public class UiScaleCalculatorTests
{
    [Theory]
    [InlineData(2560, 1440, 125)]   // WQHD 100%（開発時の大きさ）/ 4K 150%
    [InlineData(3440, 1440, 125)]   // UWQHD 100%
    [InlineData(3840, 2160, 125)]   // 4K 100%（上限で止める）
    [InlineData(2304, 1296, 120)]   // ちょうど 120%
    [InlineData(2200, 1300, 110)]   // 114.5… → 5% 刻みで切り捨て
    [InlineData(2048, 1152, 105)]   // WQHD 125%
    [InlineData(1920, 1080, 100)]   // FHD 100% / 4K 200%
    [InlineData(1920, 1200, 100)]   // WUXGA 100%（幅で決まる）
    [InlineData(2560, 1080, 100)]   // UWFHD 100%（高さで決まる）
    [InlineData(1536, 864, 95)]     // FHD 125%（80% → 下限で止める）
    [InlineData(1366, 768, 95)]     // HD 100%（下限で止める）
    [InlineData(1280, 720, 95)]     // HD 100%（下限で止める）
    public void ComputeAutoPercent_ReturnsSteppedAndClampedPercent(double width, double height, int expected)
    {
        Assert.Equal(expected, UiScaleCalculator.ComputeAutoPercent(width, height));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(double.NaN, 1080)]
    [InlineData(double.PositiveInfinity, 1080)]
    public void ComputeAutoPercent_InvalidSize_ReturnsMaxPercent(double width, double height)
    {
        Assert.Equal(UiScaleCalculator.MaxAutoPercent, UiScaleCalculator.ComputeAutoPercent(width, height));
    }

    [Theory]
    [InlineData(125, 1.0)]   // 開発時の大きさ
    [InlineData(100, 0.8)]   // FHD
    [InlineData(95, 0.76)]
    [InlineData(150, 1.2)]
    public void PercentToScale_UsesDesignPercentAsOne(int percent, double expected)
    {
        Assert.Equal(expected, UiScaleCalculator.PercentToScale(percent), 6);
        Assert.Equal(percent, UiScaleCalculator.ScaleToPercent(expected));
    }
}

/// <summary>
/// UI 表示倍率の状態管理（<see cref="UiScaleService"/>）のテスト。
/// 静的インスタンスを汚さないよう、テストごとに新しいインスタンスを使う。
/// </summary>
public class UiScaleServiceTests
{
    [Fact]
    public void Default_IsDesignSize()
    {
        var service = new UiScaleService();

        Assert.Equal(UiScaleService.DefaultPercent, service.ConfiguredPercent);
        Assert.Equal(125, service.EffectivePercent);
        Assert.False(service.IsAuto);
        Assert.Equal(1.0, service.Scale);

        // 固定倍率のときは画面の報告で倍率が変わらない
        service.ReportScreen(1920, 1080);
        Assert.Equal(1.0, service.Scale);
    }

    [Fact]
    public void Auto_UsesReportedScreen()
    {
        var service = new UiScaleService();
        service.SetConfiguredPercent(UiScaleService.AutoPercent);

        // 画面が分からないうちは開発時の大きさ
        Assert.True(service.IsAuto);
        Assert.Equal(1.0, service.Scale);
        Assert.Equal(125, service.EffectivePercent);

        service.ReportScreen(1920, 1080);
        Assert.Equal(0.8, service.Scale, 6);
        Assert.Equal(100, service.EffectivePercent);

        service.ReportScreen(2560, 1440);
        Assert.Equal(1.0, service.Scale);
        Assert.Equal(125, service.EffectivePercent);
    }

    [Fact]
    public void Auto_RemembersScreenReportedWhileFixed()
    {
        var service = new UiScaleService();
        service.ReportScreen(1920, 1080);

        service.SetConfiguredPercent(UiScaleService.AutoPercent);

        Assert.Equal(0.8, service.Scale, 6);
    }

    [Fact]
    public void Manual_OverridesAutoScale()
    {
        var service = new UiScaleService();
        service.SetConfiguredPercent(UiScaleService.AutoPercent);
        service.ReportScreen(1920, 1080);

        service.SetConfiguredPercent(150);

        Assert.False(service.IsAuto);
        Assert.Equal(150, service.EffectivePercent);
        Assert.Equal(1.2, service.Scale, 6);
    }

    [Fact]
    public void ScaleChanged_RaisedOnlyWhenScaleChanges()
    {
        var service = new UiScaleService();
        var count = 0;
        service.ScaleChanged += (_, _) => count++;

        service.SetConfiguredPercent(125); // 変化なし
        Assert.Equal(0, count);

        service.SetConfiguredPercent(UiScaleService.AutoPercent); // 画面未報告の自動は開発時の大きさのまま
        Assert.Equal(0, count);

        service.ReportScreen(1920, 1080); // 100% に変化
        Assert.Equal(1, count);

        service.ReportScreen(1366, 768); // 下限の 95% に変化
        Assert.Equal(2, count);

        service.ReportScreen(1280, 720); // 下限で 95% のまま
        Assert.Equal(2, count);

        service.SetConfiguredPercent(95); // 手動でも同じ 95%
        Assert.Equal(2, count);

        service.SetConfiguredPercent(150);
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, UiScaleService.MinPercent)]
    [InlineData(-5, UiScaleService.MinPercent)]
    [InlineData(85, 85)]
    [InlineData(999, UiScaleService.MaxPercent)]
    public void NormalizePercent_ClampsOutOfRangeValues(int input, int expected)
    {
        Assert.Equal(expected, UiScaleService.NormalizePercent(input));
    }

    [Fact]
    public void ReportScreen_IgnoresInvalidSize()
    {
        var service = new UiScaleService();
        service.SetConfiguredPercent(UiScaleService.AutoPercent);
        service.ReportScreen(1920, 1080);

        service.ReportScreen(0, 0);

        Assert.Equal(0.8, service.Scale, 6);
    }
}
