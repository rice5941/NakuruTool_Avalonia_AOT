using System;
using Avalonia;
using Avalonia.Platform;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Scaling;

/// <summary>
/// UI 表示倍率の計算。
/// 倍率（%）は FHD（1920×1080・Windows の拡大率 100%）にちょうど良い大きさを 100% とする。
/// 画面は WQHD（2560×1440・拡大率 100%）で開発しており、その大きさ（レイアウト上の倍率 1.0）を <see cref="DesignPercent"/>（125%）とする。
/// 自動倍率は、モニターの論理解像度（物理解像度 ÷ Windows の拡大率）の FHD に対する比率から求める。
/// </summary>
public static class UiScaleCalculator
{
    /// <summary>倍率 100% とする論理解像度の幅（FHD）</summary>
    public const double ReferenceWidth = 1920;

    /// <summary>倍率 100% とする論理解像度の高さ（FHD）</summary>
    public const double ReferenceHeight = 1080;

    /// <summary>開発時の大きさ（WQHD・拡大率 100%、レイアウト上の倍率 1.0）に当たる倍率（%）</summary>
    public const int DesignPercent = 125;

    /// <summary>自動倍率の下限（%）。これより小さくすると文字が読みにくくなるため止める（12px の文字が約 9px）。</summary>
    public const int MinAutoPercent = 95;

    /// <summary>自動倍率の上限（%）。開発時の大きさより大きい画面では拡大しない（Windows の拡大率に任せる）。</summary>
    public const int MaxAutoPercent = DesignPercent;

    /// <summary>自動倍率の刻み（%）。半端な倍率で境界線や文字がにじむのを抑えるため切り捨てで丸める。</summary>
    public const int AutoPercentStep = 5;

    /// <summary>
    /// モニターの論理解像度から自動倍率（%）を求める。
    /// 幅・高さのうち FHD に対して小さい方の比率を使い、<see cref="AutoPercentStep"/> 刻みで切り捨てたうえで
    /// [<see cref="MinAutoPercent"/>, <see cref="MaxAutoPercent"/>] に収める。
    /// 例: WQHD → 125%、WQHD・拡大率 125% → 105%、FHD → 100%、FHD・拡大率 125% や HD → 95%（下限）。
    /// </summary>
    public static int ComputeAutoPercent(double logicalWidth, double logicalHeight)
    {
        if (!(logicalWidth > 0) || !(logicalHeight > 0)
            || double.IsInfinity(logicalWidth) || double.IsInfinity(logicalHeight))
        {
            return MaxAutoPercent;
        }

        var percent = 100.0 * Math.Min(logicalWidth / ReferenceWidth, logicalHeight / ReferenceHeight);

        // 浮動小数点の誤差（例: 120 が 119.99… になる）で一段下に切り捨てられないよう微小値を足してから丸める
        var stepped = (int)Math.Floor(percent / AutoPercentStep + 1e-9) * AutoPercentStep;
        return Math.Clamp(stepped, MinAutoPercent, MaxAutoPercent);
    }

    /// <summary>倍率（%）をレイアウト上の倍率に変換する（<see cref="DesignPercent"/> が 1.0）。</summary>
    public static double PercentToScale(int percent) => percent / (double)DesignPercent;

    /// <summary>レイアウト上の倍率を倍率（%）に変換する。</summary>
    public static int ScaleToPercent(double scale) => (int)Math.Round(scale * DesignPercent);

    /// <summary>
    /// モニターの論理解像度を求める。タスクバーの有無で値が揺れないよう、作業領域ではなく画面全体を使う。
    /// </summary>
    public static Size ToLogicalSize(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        var scaling = screen.Scaling > 0 ? screen.Scaling : 1.0;
        return new Size(screen.Bounds.Width / scaling, screen.Bounds.Height / scaling);
    }
}
