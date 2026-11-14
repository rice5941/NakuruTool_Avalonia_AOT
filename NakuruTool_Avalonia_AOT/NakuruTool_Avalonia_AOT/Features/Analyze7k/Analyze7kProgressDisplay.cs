using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>
/// 解析の残り時間の推定。直近（最大 <see cref="Window"/>）の処理速度から求める。
/// ★の高い順に解析するため序盤と終盤で速度が変わるので、開始からの平均ではなく直近の速度を使う。
/// </summary>
public sealed class Analyze7kRemainingTimeEstimator
{
    /// <summary>速度を測る期間</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    /// <summary>推定を出すのに必要な最短の測定期間（開始直後の値は不安定なため）</summary>
    public static readonly TimeSpan MinimumSpan = TimeSpan.FromSeconds(3);

    private readonly Queue<(TimeSpan At, int Processed)> _samples = new();

    /// <param name="at">ジョブ開始からの経過時間</param>
    /// <param name="processed">このジョブで処理した件数</param>
    /// <param name="remaining">残りの件数</param>
    /// <returns>残り時間。測定がまだ足りない間は null</returns>
    public TimeSpan? Update(TimeSpan at, int processed, int remaining)
    {
        if (remaining <= 0)
            return TimeSpan.Zero;

        _samples.Enqueue((at, processed));
        while (_samples.Count > 2 && at - _samples.Peek().At > Window)
            _samples.Dequeue();

        var (oldestAt, oldestProcessed) = _samples.Peek();
        var span = at - oldestAt;
        var done = processed - oldestProcessed;
        if (span < MinimumSpan || done <= 0)
            return null;

        return TimeSpan.FromSeconds(remaining * span.TotalSeconds / done);
    }
}

/// <summary>
/// beatmap一覧のヘッダーに出す 7K 解析の進み具合。解析中だけ作る（<see cref="From"/>）。
/// </summary>
/// <param name="Percent">進捗率（0〜100。プログレスバー用）</param>
/// <param name="PercentText">進捗率の表示（切り捨て。終わるまで 100% にしない）</param>
/// <param name="CountText">解析済み / 対象の件数</param>
/// <param name="RemainingText">残り時間の目安</param>
/// <param name="ToolTip">説明（解析できなかった譜面があればその件数も）</param>
public sealed record Analyze7kProgressDisplay(
    double Percent,
    string PercentText,
    string CountText,
    string RemainingText,
    string ToolTip)
{
    /// <param name="translate">翻訳キー → 文字列（アプリでは LanguageService）</param>
    /// <returns>解析中でなければ null</returns>
    public static Analyze7kProgressDisplay? From(Analyze7kProgress progress, Func<string, string> translate)
    {
        if (progress.State != Analyze7kState.Running || progress.Total <= 0)
            return null;

        var processed = Math.Clamp(progress.Processed, 0, progress.Total);
        var percent = processed * 100.0 / progress.Total;
        var countText = string.Format(translate("MapList.Analyze7k.Count"), processed.ToString("N0"), progress.Total.ToString("N0"));
        var toolTip = translate("MapList.Analyze7k.Progress");
        if (progress.Failed > 0)
            toolTip += Environment.NewLine + string.Format(translate("MapList.Analyze7k.Failed"), progress.Failed.ToString("N0"));

        return new Analyze7kProgressDisplay(
            percent,
            $"{Math.Floor(percent):0}%",
            countText,
            FormatRemaining(progress.Remaining, translate),
            toolTip);
    }

    /// <summary>残り時間を分単位（切り上げ）で表す。1時間以上は「時間・分」</summary>
    public static string FormatRemaining(TimeSpan? remaining, Func<string, string> translate)
    {
        if (remaining is not { } value)
            return translate("MapList.Analyze7k.RemainingEstimating");
        if (value < TimeSpan.FromMinutes(1))
            return translate("MapList.Analyze7k.RemainingUnderMinute");

        var minutes = (int)Math.Ceiling(value.TotalMinutes);
        return minutes < 60
            ? string.Format(translate("MapList.Analyze7k.RemainingMinutes"), minutes)
            : string.Format(translate("MapList.Analyze7k.RemainingHours"), minutes / 60, minutes % 60);
    }
}
