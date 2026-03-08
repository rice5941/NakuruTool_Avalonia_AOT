using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// DatabaseService.CalculateAccuracy の単体テスト
/// </summary>
public class AccuracyCalculatorTests
{
    private const int ScoreV2Bit = 1 << 29; // 536870912

    // ───────────────────────────────────────────
    // Mania ScoreV1
    // ───────────────────────────────────────────

    [Fact]
    public void ManiaScoreV1_AllRainbow300_Returns100Percent()
    {
        // 全ノーツが CountGeki (レインボー300) の場合、精度 100%
        var score = new ScoreData
        {
            Ruleset = 3,
            Mods = 0, // ScoreV2 なし
            CountGeki = 100,
            Count300 = 0,
            CountKatu = 0,
            Count100 = 0,
            Count50 = 0,
            CountMiss = 0
        };

        var acc = DatabaseService.CalculateAccuracy(score);

        Assert.Equal(100.0, acc, precision: 6);
    }

    [Fact]
    public void ManiaScoreV1_Mixed_CorrectFormula()
    {
        // ScoreV1: (CountGeki + Count300)*300 + CountKatu*200 + Count100*100 + Count50*50
        // totalHits = 2+3+1+1+1+1 = 9
        // weighted = (3+2)*300 + 1*200 + 1*100 + 1*50 = 1500+200+100+50 = 1850
        // max = 9*300 = 2700
        // acc = 1850/2700 * 100 ≈ 68.5185...%
        var score = new ScoreData
        {
            Ruleset = 3,
            Mods = 0,
            CountGeki = 2,
            Count300 = 3,
            CountKatu = 1,
            Count100 = 1,
            Count50 = 1,
            CountMiss = 1
        };

        var acc = DatabaseService.CalculateAccuracy(score);

        double expected = 1850.0 / 2700.0 * 100.0;
        Assert.Equal(expected, acc, precision: 10);
    }

    // ───────────────────────────────────────────
    // Mania ScoreV2
    // ───────────────────────────────────────────

    [Fact]
    public void ManiaScoreV2_AllRainbow300_Returns100Percent()
    {
        // ScoreV2 でも全 CountGeki なら精度 100%
        // weighted = 100*305 = 30500, max = 100*305 = 30500
        var score = new ScoreData
        {
            Ruleset = 3,
            Mods = ScoreV2Bit,
            CountGeki = 100,
            Count300 = 0,
            CountKatu = 0,
            Count100 = 0,
            Count50 = 0,
            CountMiss = 0
        };

        var acc = DatabaseService.CalculateAccuracy(score);

        Assert.Equal(100.0, acc, precision: 6);
    }

    [Fact]
    public void ManiaScoreV2_AllCount300NoneGeki_IsLessThan100()
    {
        // ScoreV2: CountGeki=0, Count300=100 → weighted=100*300, max=100*305
        // acc = 30000/30500 * 100 ≈ 98.3606...% < 100%
        var score = new ScoreData
        {
            Ruleset = 3,
            Mods = ScoreV2Bit,
            CountGeki = 0,
            Count300 = 100,
            CountKatu = 0,
            Count100 = 0,
            Count50 = 0,
            CountMiss = 0
        };

        var acc = DatabaseService.CalculateAccuracy(score);

        double expected = 100 * 300.0 / (100 * 305.0) * 100.0;
        Assert.Equal(expected, acc, precision: 10);
        Assert.True(acc < 100.0);
    }

    [Fact]
    public void ManiaScoreV2_Mixed_Uses305WeightForGeki()
    {
        // ScoreV2: weighted = 2*305 + 3*300 + 1*200 + 1*100 + 1*50 + 0*0 = 610+900+200+100+50=1860
        // totalHits = 2+3+1+1+1+1=9, max = 9*305 = 2745
        // acc = 1860/2745 * 100
        var score = new ScoreData
        {
            Ruleset = 3,
            Mods = ScoreV2Bit,
            CountGeki = 2,
            Count300 = 3,
            CountKatu = 1,
            Count100 = 1,
            Count50 = 1,
            CountMiss = 1
        };

        var acc = DatabaseService.CalculateAccuracy(score);

        double expected = 1860.0 / 2745.0 * 100.0;
        Assert.Equal(expected, acc, precision: 10);
    }

    [Fact]
    public void ManiaScoreV2_DifferentFromV1_ForSameHits()
    {
        // 同じヒット内容でも ScoreV2 と ScoreV1 では精度が異なることを確認
        var scoreV1 = new ScoreData { Ruleset = 3, Mods = 0, CountGeki = 10, Count300 = 5, CountMiss = 5 };
        var scoreV2 = new ScoreData { Ruleset = 3, Mods = ScoreV2Bit, CountGeki = 10, Count300 = 5, CountMiss = 5 };

        var accV1 = DatabaseService.CalculateAccuracy(scoreV1);
        var accV2 = DatabaseService.CalculateAccuracy(scoreV2);

        // V1: (10+5)*300 / 20*300*100 = 75%
        // V2: (10*305+5*300) / 20*305*100 = (3050+1500)/(6100)*100 ≈ 74.59...%
        Assert.NotEqual(accV1, accV2);
        Assert.True(accV2 < accV1); // V2のほうが若干厳しい（305分母 vs 300分母）
    }

    // ───────────────────────────────────────────
    // 他ルールセット（ScoreV2 bit を持っても Mania 以外は従来通り）
    // ───────────────────────────────────────────

    [Fact]
    public void OsuStandard_ScoreV2Bit_IgnoresGeki()
    {
        // Ruleset=0 (osu!) のスコアは CountGeki を無視する従来ロジック
        var score = new ScoreData
        {
            Ruleset = 0,
            Mods = ScoreV2Bit,
            Count300 = 50,
            Count100 = 10,
            Count50 = 5,
            CountMiss = 5,
            CountGeki = 999 // 無視されるべき
        };

        var acc = DatabaseService.CalculateAccuracy(score);

        // totalHitsOther = 50+10+5+5=70, weighted = 50*300+10*100+5*50=16250, max=70*300=21000
        double expected = 16250.0 / 21000.0 * 100.0;
        Assert.Equal(expected, acc, precision: 10);
    }

    [Fact]
    public void ZeroHits_ReturnsZero()
    {
        var score = new ScoreData { Ruleset = 3, Mods = ScoreV2Bit };
        Assert.Equal(0.0, DatabaseService.CalculateAccuracy(score));
    }
}
