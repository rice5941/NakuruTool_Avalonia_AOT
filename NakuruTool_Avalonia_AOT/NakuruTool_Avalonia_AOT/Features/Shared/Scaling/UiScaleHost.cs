using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Scaling;

/// <summary>
/// 中身を <see cref="UiScaleService.Scale"/> の倍率で拡大・縮小するホスト。各ウィンドウのルートに置く。
/// レイアウトごと変換する（<see cref="LayoutTransformControl"/>）ため、縮小すると中身はその分広い領域でレイアウトされる。
/// ポップアップ（ドロップダウン・コンテキストメニュー・ツールチップ等）は別の TopLevel に描画されるため、
/// App.axaml の Popup スタイル（InheritsTransform）で配置元の倍率を引き継がせている。
/// </summary>
public class UiScaleHost : LayoutTransformControl
{
    /// <summary>
    /// メインモニター（OS のプライマリディスプレイ）の論理解像度を追跡し、<see cref="UiScaleService"/> に報告するか。
    /// 報告は 1 か所で足りるため、メインウィンドウのホストだけ true にする。
    /// ウィンドウが今あるモニターではなくメインモニターを基準にするので、別のモニターへ移動しても倍率は変わらない。
    /// </summary>
    public static readonly StyledProperty<bool> TracksScreenProperty =
        AvaloniaProperty.Register<UiScaleHost, bool>(nameof(TracksScreen));

    private TopLevel? _trackedTopLevel;
    private Screens? _trackedScreens;
    private Size? _lastReportedScreenSize;

    public bool TracksScreen
    {
        get => GetValue(TracksScreenProperty);
        set => SetValue(TracksScreenProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        UiScaleService.Instance.ScaleChanged += OnScaleChanged;

        if (TracksScreen)
        {
            StartScreenTracking(TopLevel.GetTopLevel(this));
        }

        // 初回描画の前に倍率を反映する（自動モードでは直前の画面報告で倍率が確定している）
        ApplyScale();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UiScaleService.Instance.ScaleChanged -= OnScaleChanged;
        StopScreenTracking();

        base.OnDetachedFromVisualTree(e);
    }

    private void OnScaleChanged(object? sender, EventArgs e) => ApplyScale();

    private void ApplyScale()
    {
        var scale = UiScaleService.Instance.Scale;

        // 開発時の大きさ（Scale 1.0）では変換を外し、従来とまったく同じ描画にする
        LayoutTransform = Math.Abs(scale - 1.0) < 0.0001
            ? null
            : new ScaleTransform(scale, scale);
    }

    private void StartScreenTracking(TopLevel? topLevel)
    {
        StopScreenTracking();
        if (topLevel is null)
        {
            return;
        }

        _trackedTopLevel = topLevel;
        // メインモニターの解像度・拡大率の変更は Screens.Changed で拾う。
        // ScalingChanged は、ウィンドウがメインモニター上にあるときの拡大率変更を確実に拾うための保険
        // （別モニターへの移動でも発火するが、メインモニターの論理解像度は変わらないので報告されない）
        topLevel.ScalingChanged += OnScreenMayHaveChanged;
        if (topLevel is WindowBase window)
        {
            window.Opened += OnScreenMayHaveChanged;
        }

        _trackedScreens = topLevel.Screens;
        if (_trackedScreens is not null)
        {
            _trackedScreens.Changed += OnScreenMayHaveChanged;
        }

        ReportCurrentScreen();
    }

    private void StopScreenTracking()
    {
        if (_trackedTopLevel is { } topLevel)
        {
            topLevel.ScalingChanged -= OnScreenMayHaveChanged;
            if (topLevel is WindowBase window)
            {
                window.Opened -= OnScreenMayHaveChanged;
            }
        }

        if (_trackedScreens is not null)
        {
            _trackedScreens.Changed -= OnScreenMayHaveChanged;
        }

        _trackedTopLevel = null;
        _trackedScreens = null;
        _lastReportedScreenSize = null;
    }

    private void OnScreenMayHaveChanged(object? sender, EventArgs e) => ReportCurrentScreen();

    /// <summary>
    /// メインモニターの論理解像度を報告する。論理解像度が前回から変わったときだけ報告する。
    /// </summary>
    private void ReportCurrentScreen()
    {
        if (_trackedTopLevel is not { } topLevel || topLevel.Screens is not { } screens)
        {
            return;
        }

        // メインモニターが取得できない環境では、ウィンドウがあるモニターで代用する
        var screen = screens.Primary
            ?? (topLevel is WindowBase window
                ? screens.ScreenFromWindow(window)
                : screens.ScreenFromTopLevel(topLevel));
        if (screen is null)
        {
            return;
        }

        var size = UiScaleCalculator.ToLogicalSize(screen);
        if (_lastReportedScreenSize == size)
        {
            return;
        }

        _lastReportedScreenSize = size;
        UiScaleService.Instance.ReportScreen(size.Width, size.Height);
    }
}
