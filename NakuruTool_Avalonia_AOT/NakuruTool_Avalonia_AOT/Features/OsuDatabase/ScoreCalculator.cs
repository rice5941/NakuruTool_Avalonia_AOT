namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// mania スコアの精度・グレード計算
/// </summary>
public static class ScoreCalculator
{
    /// <summary>
    /// mania スコアの精度 (0-100) を計算する。
    /// ScoreV2: CountGeki = レインボー300 (305点扱い)、分母も305基準。
    /// ScoreV1: CountGeki と Count300 は同等の300点扱い。
    /// </summary>
    public static double CalculateAccuracy(in LocalScore score)
    {
        var totalHits = score.Count300 + score.Count100 + score.Count50 +
                        score.CountGeki + score.CountKatu + score.CountMiss;

        if (totalHits == 0) return 0;

        if ((score.Mods & OsuMods.ScoreV2) != 0)
        {
            var weightedScore = score.CountGeki * 305.0 +
                                score.Count300 * 300.0 +
                                score.CountKatu * 200.0 +
                                score.Count100 * 100.0 +
                                score.Count50 * 50.0;
            return weightedScore / (totalHits * 305.0) * 100.0;
        }

        var weightedScoreV1 = (score.Count300 + score.CountGeki) * 300.0 +
                              score.CountKatu * 200.0 +
                              score.Count100 * 100.0 +
                              score.Count50 * 50.0;
        return weightedScoreV1 / (totalHits * 300.0) * 100.0;
    }

    /// <summary>
    /// 精度からグレード文字列を算出
    /// </summary>
    public static string CalculateGrade(double accuracy)
    {
        if (accuracy >= 100.0) return "SS";
        if (accuracy >= 95.0) return "S";
        if (accuracy >= 90.0) return "A";
        if (accuracy >= 80.0) return "B";
        if (accuracy >= 70.0) return "C";
        if (accuracy > 0.0) return "D";
        return string.Empty;
    }
}
