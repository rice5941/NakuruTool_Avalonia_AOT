using System;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// ローカルランキング1件の表示用モデル。
/// 選択中譜面のランキング、および表示中ページの行の最高スコアについてのみ
/// <see cref="LocalScore"/> から生成する（全件は常駐させない）。
/// </summary>
public sealed class LocalRankingEntry
{
    /// <summary>登録日時（ローカル時刻）</summary>
    public DateTime? PlayedAt { get; }
    public double Accuracy { get; }
    public int Score { get; }
    public string Grade { get; }

    // 判定内訳（ツールチップ用）
    public int Count320 { get; }
    public int Count300 { get; }
    public int Count200 { get; }
    public int Count100 { get; }
    public int Count50 { get; }
    public int Count0 { get; }

    public LocalRankingEntry(in LocalScore score)
    {
        PlayedAt = ToLocalDateTime(score.TimestampTicks);
        Accuracy = ScoreCalculator.CalculateAccuracy(score);
        Score = score.ReplayScore;
        Grade = ScoreCalculator.CalculateGrade(Accuracy);
        Count320 = score.CountGeki;
        Count300 = score.Count300;
        Count200 = score.CountKatu;
        Count100 = score.Count100;
        Count50 = score.Count50;
        Count0 = score.CountMiss;
    }

    private static DateTime? ToLocalDateTime(long ticks)
    {
        if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks)
            return null;

        return new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
    }
}
