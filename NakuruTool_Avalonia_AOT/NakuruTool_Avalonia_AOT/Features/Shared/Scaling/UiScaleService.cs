using System;
using System.Collections.Generic;
using Avalonia;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Scaling;

/// <summary>
/// アプリ全体の UI 表示倍率を管理する。
/// 倍率（%）は FHD にちょうど良い大きさを 100%、開発時の大きさ（WQHD）を 125% とする表記で扱い、
/// レイアウトに掛ける倍率 <see cref="Scale"/> は「倍率（%）÷ 125」になる（<see cref="UiScaleCalculator.PercentToScale"/>）。
/// 各ウィンドウのルートに置いた <see cref="UiScaleHost"/> が <see cref="ScaleChanged"/> を購読して倍率を反映する。
/// XAML から生成されるホストからも参照するため、<c>LanguageService.Instance</c> と同じく静的インスタンスで提供する。
/// UI スレッドからのみ操作すること。
/// </summary>
public sealed class UiScaleService
{
    /// <summary>アプリ全体で共有するインスタンス</summary>
    public static UiScaleService Instance { get; } = new();

    /// <summary>設定値「自動」（モニターの論理解像度から倍率を決める）</summary>
    public const int AutoPercent = 0;

    /// <summary>
    /// 未設定時の倍率（%）。開発時の大きさ（レイアウト上の倍率 1.0）。
    /// App から設定を渡されるまではこの大きさで表示する（ヘッドレステストもこの値のまま）。
    /// </summary>
    public const int DefaultPercent = UiScaleCalculator.DesignPercent;

    /// <summary>手動指定できる倍率の下限（%）。設定ファイルを直接書き換えた場合の異常値対策。</summary>
    public const int MinPercent = 50;

    /// <summary>手動指定できる倍率の上限（%）。設定ファイルを直接書き換えた場合の異常値対策。</summary>
    public const int MaxPercent = 250;

    /// <summary>設定画面で選べる倍率（%）。0 は「自動」。100% が FHD、125% が開発時（WQHD）の大きさ。</summary>
    public static IReadOnlyList<int> PercentOptions { get; } = [AutoPercent, 75, 100, 125, 150];

    private Size? _screenLogicalSize;

    /// <summary>設定された倍率（%）。<see cref="AutoPercent"/> は自動。</summary>
    public int ConfiguredPercent { get; private set; } = DefaultPercent;

    /// <summary>自動モードか</summary>
    public bool IsAuto => ConfiguredPercent == AutoPercent;

    /// <summary>実際に適用している倍率（%）。自動のときは計算結果。</summary>
    public int EffectivePercent { get; private set; } = DefaultPercent;

    /// <summary>レイアウトに掛ける倍率（1.0 = 開発時の大きさ）</summary>
    public double Scale { get; private set; } = 1.0;

    /// <summary><see cref="Scale"/> が変わったときに発火する</summary>
    public event EventHandler? ScaleChanged;

    /// <summary>
    /// 倍率の設定値（%）を変更する。<see cref="AutoPercent"/> で自動。
    /// </summary>
    public void SetConfiguredPercent(int percent)
    {
        ConfiguredPercent = NormalizePercent(percent);
        Recompute();
    }

    /// <summary>
    /// メインウィンドウがあるモニターの論理解像度を報告する。自動モードではこの値から倍率を計算する。
    /// </summary>
    public void ReportScreen(double logicalWidth, double logicalHeight)
    {
        if (!(logicalWidth > 0) || !(logicalHeight > 0))
        {
            return;
        }

        _screenLogicalSize = new Size(logicalWidth, logicalHeight);
        if (IsAuto)
        {
            Recompute();
        }
    }

    /// <summary>
    /// 設定ファイル由来の値を正規化する。0（自動）以外は [<see cref="MinPercent"/>, <see cref="MaxPercent"/>] に収める。
    /// </summary>
    public static int NormalizePercent(int percent)
        => percent == AutoPercent ? AutoPercent : Math.Clamp(percent, MinPercent, MaxPercent);

    private void Recompute()
    {
        int percent;
        if (IsAuto)
        {
            // モニターがまだ分からないうちは開発時の大きさ（ウィンドウ表示時に報告され次第、再計算される）
            percent = _screenLogicalSize is { } size
                ? UiScaleCalculator.ComputeAutoPercent(size.Width, size.Height)
                : UiScaleCalculator.MaxAutoPercent;
        }
        else
        {
            percent = ConfiguredPercent;
        }

        EffectivePercent = percent;

        var scale = UiScaleCalculator.PercentToScale(percent);
        if (Math.Abs(scale - Scale) < 0.0001)
        {
            return;
        }

        Scale = scale;
        ScaleChanged?.Invoke(this, EventArgs.Empty);
    }
}
