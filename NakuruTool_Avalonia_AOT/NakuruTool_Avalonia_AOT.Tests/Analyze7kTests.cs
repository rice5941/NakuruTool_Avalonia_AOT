using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.MapList.Sorting;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// 7K 譜面解析（Prism・段位推定）のテスト。
/// fixture は原典 Mania Tracker の TypeScript が出力した値。
/// </summary>
public class Analyze7kTests
{
    private static JsonElement Fixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Analyze7k", name);
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    // ───────────────────────────────────────────
    // 段位の表示名
    // ───────────────────────────────────────────

    [Theory]
    [InlineData(9, "9th")]
    [InlineData(10, "10th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    public void DisplayDanLabel_UsesOrdinals(int number, string ordinal)
    {
        Assert.Equal($"{ordinal} Dan Mid", DanLabels.DisplayDanLabel($"{number} Mid"));
    }

    [Fact]
    public void DanLabels_MatchManiaTracker()
    {
        Assert.Equal("Gamma Dan High", DanLabels.DisplayDanLabel("Gamma High"));
        Assert.Equal("Zenith Dan Mid/High", DanLabels.DisplayDanLabel("Zenith Mid/High"));
        Assert.Equal("9th Dan Mid", DanLabels.DisplayDanLabel("9th Dan Mid"));
        Assert.Equal("Unrated", DanLabels.DisplayDanLabel("Unrated"));
        Assert.Equal("9 Mid", DanLabels.DanLabel(9));
        Assert.Equal("Zenith High", DanLabels.DanLabel(13.4));
        Assert.Equal("Stellium Low", DanLabels.DanLabel(13.6));
    }

    [Fact]
    public void FormatDan_ShowsLabelAndNumber()
    {
        Assert.Equal("9th Dan Mid/High (9.20)", ManiaChartAnalysis.FormatDan(9.2));
        Assert.Equal(string.Empty, ManiaChartAnalysis.FormatDan(null));
    }

    [Fact]
    public void DanReference_IsTheSideWithoutGenreThatIsNotPrimary()
    {
        // Regular の譜面の LN 段位でジャンルなし → LN 側だけ参考値（Finding shiners [Lvl 79] と同じ形）
        var regularChart = new ManiaChartAnalysis
        {
            PrismOverall = 29.94, RegularDan = 9.4, LnDan = 9.8, PrimaryFamily = DanSide.Regular,
            RegularGenres = RegularDanGenre.Stream,
        };
        Assert.False(regularChart.IsRegularDanReference);
        Assert.True(regularChart.IsLnDanReference);

        // LN の譜面の Reg 段位でジャンルなし → Reg 側だけ参考値
        var lnChart = new ManiaChartAnalysis
        {
            PrismOverall = 25, RegularDan = 8, LnDan = 9, PrimaryFamily = DanSide.Ln, LnGenres = LnDanGenre.General,
        };
        Assert.True(lnChart.IsRegularDanReference);
        Assert.False(lnChart.IsLnDanReference);

        // 主分類の側はジャンルがなくても参考値にしない
        var primaryWithoutGenre = regularChart with { RegularGenres = RegularDanGenre.None };
        Assert.False(primaryWithoutGenre.IsRegularDanReference);

        // 主分類ではない側でもジャンルがあれば参考値にしない・段位がなければ参考値にしない
        Assert.False((regularChart with { LnGenres = LnDanGenre.Tech }).IsLnDanReference);
        Assert.False((regularChart with { LnDan = null }).IsLnDanReference);
    }

    // ───────────────────────────────────────────
    // 段位推定のジャンル（mania_dan_buckets.py）
    // ───────────────────────────────────────────

    private static BucketFeatures? ParseFeatures(JsonElement parsed)
        => JsonSerializer.Deserialize(parsed.GetRawText(), TestJsonContext.Default.BucketFeatures);

    [Fact]
    public void RiceTiles_MatchUpstreamTypeScript()
    {
        foreach (var c in Fixture("mania_player_dan.json").GetProperty("rice").EnumerateArray())
        {
            var verdict = new DanVerdict { BucketFeatures = ParseFeatures(c.GetProperty("parsed")) };
            var expected = c.GetProperty("expected").EnumerateArray().Select(e => e.GetString()!).ToArray();
            Assert.Equal(expected, DanBuckets.BucketIds(verdict, DanSide.Regular)!.ToArray());

            // Flags への変換も同じ組み合わせになる
            var flags = DanBuckets.RegularGenres(verdict);
            Assert.Equal(expected.Contains("jack"), flags.HasFlag(RegularDanGenre.Jack));
            Assert.Equal(expected.Contains("tech"), flags.HasFlag(RegularDanGenre.Tech));
            Assert.Equal(expected.Contains("speed"), flags.HasFlag(RegularDanGenre.Speed));
            Assert.Equal(expected.Contains("stream"), flags.HasFlag(RegularDanGenre.Stream));
        }
    }

    [Fact]
    public void LnGenres_NeedScoreAndHoldShare()
    {
        DanVerdict Verdict(double lnRatio) => new()
        {
            BucketFeatures = new BucketFeatures
            {
                Version = 1,
                LnRatio = lnRatio,
                Patterns =
                [
                    new PatternInfo { Id = "lninverse", Score = 0.7 },
                    new PatternInfo { Id = "lnrelease", Score = 0.4 },
                    new PatternInfo { Id = "ln", Score = 0.55 },
                ],
            },
        };

        Assert.Equal(LnDanGenre.General | LnDanGenre.Inverse, DanBuckets.LnGenres(Verdict(0.5)));
        Assert.Equal(LnDanGenre.None, DanBuckets.LnGenres(Verdict(0.3)));
        Assert.Equal(LnDanGenre.None, DanBuckets.LnGenres(new DanVerdict()));
    }

    // ───────────────────────────────────────────
    // ジャンル濃度
    // ───────────────────────────────────────────

    [Fact]
    public void TopGenre_PicksLargestSkillValue()
    {
        Assert.Equal(PrismGenre.Jack, new GenreValues(10, 20, 5, 30, 40).Top());
        Assert.Equal(PrismGenre.Bracket, new GenreValues(10, 40, 5, 30, 40).Top()); // 同じ値なら表示順で先
        Assert.Null(default(GenreValues).Top());

        // 最大ジャンルは基準スキル値で決める
        var analysis = Analysis(skills: new GenreValues(25, 20, 31, 28, 30), tendencies: new GenreValues(30, 10, 45, 35, 40));
        Assert.Equal(PrismGenre.Delay, analysis.TopGenre);
    }

    [Fact]
    public void SkillBars_ShareOneScaleWithLn()
    {
        // 通常の譜面: Reg 基準Overall が満幅
        Assert.Equal(30, PrismGenreChart.SkillBarMaximum(Analysis(overall: 30)));
        // LN 譜面: LN が Reg を超えたら LN が満幅
        Assert.Equal(41.6, PrismGenreChart.SkillBarMaximum(Analysis(overall: 31.2, ln: 41.6)), 6);
        // ジャンルの値が Overall を超えた場合（LN の多い譜面の補正）もはみ出さない
        Assert.Equal(35, PrismGenreChart.SkillBarMaximum(Analysis(overall: 30, skills: new GenreValues(35, 20, 20, 20, 20))), 6);
    }

    [Fact]
    public void Concentration_IsShareOfTendencies()
    {
        var analysis = Analysis(tendencies: new GenreValues(30, 0, 10, 20, 40));
        var shares = analysis.GenreConcentrations;
        Assert.Equal(30, shares.Chordstream, 4);
        Assert.Equal(0, shares.Bracket);
        Assert.Equal(10, shares.Delay, 4);
        Assert.Equal(20, shares.Technical, 4);
        Assert.Equal(40, shares.Jack, 4);

        // 合計 100・傾向なしは 0・大小は傾向（＝基準スキル値）と同じ
        var tendencies = new GenreValues(12.5f, 0, 40, 7, 66);
        var other = tendencies.ToShares();
        Assert.Equal(100, other.Sum(), 3);
        Assert.Equal(0, other.Bracket);
        Assert.Equal(tendencies.Top(), other.Top());
        Assert.True(other.Jack > other.Delay && other.Delay > other.Chordstream && other.Chordstream > other.Technical);

        // 傾向がすべて 0 なら濃度もすべて 0
        Assert.Equal(default(GenreValues), default(GenreValues).ToShares());
    }

    [Fact]
    public void RoundedConcentration_AddsUpToExactlyHundred()
    {
        // 33.3 / 33.3 / 33.3 → 端数の同じものは表示順で先に 1 を足す
        Assert.Equal([34, 33, 33, 0, 0], new GenreValues(1, 1, 1, 0, 0).ToRoundedShares());
        // 12.5 / 0 / 40 / 7 / 66 → 9.96 / 0 / 31.87 / 5.58 / 52.59 → 切り捨てで 97、端数の大きい 3 つに足して 10 / 0 / 32 / 5 / 53
        Assert.Equal([10, 0, 32, 5, 53], new GenreValues(12.5f, 0, 40, 7, 66).ToRoundedShares());
        Assert.Equal(100, new GenreValues(3, 7, 11, 13, 17).ToRoundedShares().Sum());
        Assert.Equal([0, 0, 0, 0, 0], default(GenreValues).ToRoundedShares());
    }

    // ───────────────────────────────────────────
    // .osu の確認
    // ───────────────────────────────────────────

    private static string SevenKeyChart(int notes = 600, string od = "8", string mode = "3", string cs = "7")
    {
        var sb = new StringBuilder();
        sb.Append("osu file format v14\n\n[General]\nAudioFilename: audio.mp3\nMode: ").Append(mode).Append('\n');
        sb.Append("\n[Metadata]\nTitle:Test\nVersion:Test\n");
        sb.Append("\n[Difficulty]\nHPDrainRate:8\nCircleSize:").Append(cs).Append("\nOverallDifficulty:").Append(od).Append('\n');
        sb.Append("\n[TimingPoints]\n0,300,4,2,0,50,1,0\n\n[HitObjects]\n");
        int[] pattern = [0, 2, 4, 6, 1, 3, 5, 6, 4, 2, 0, 3];
        for (int i = 0; i < notes; i++)
        {
            int column = pattern[i % pattern.Length];
            int x = (int)Math.Floor((column + 0.5) * 512 / 7);
            sb.Append(x).Append(",192,").Append(1000 + i * 110).Append(",1,0,0:0:0:0:\n");
        }
        return sb.ToString();
    }

    [Fact]
    public void ChartHeader_AcceptsOnlyNativeSevenKeyMania()
    {
        Assert.True(Analyze7kRules.TryReadChartHeader(Encoding.UTF8.GetBytes(SevenKeyChart(od: "8.5")), out var od, out _));
        Assert.Equal(8.5, od);
        Assert.True(Analyze7kRules.TryReadChartHeader([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(SevenKeyChart())], out _, out _));
        Assert.False(Analyze7kRules.TryReadChartHeader(Encoding.UTF8.GetBytes(SevenKeyChart(cs: "4")), out _, out var reason));
        Assert.Equal(Analyze7kFailure.NotSevenKey, reason);
        Assert.False(Analyze7kRules.TryReadChartHeader(Encoding.UTF8.GetBytes(SevenKeyChart(mode: "0")), out _, out _));
        Assert.False(Analyze7kRules.TryReadChartHeader("not an osu file"u8, out _, out reason));
        Assert.Equal(Analyze7kFailure.InvalidFile, reason);
    }

    [Theory]
    [InlineData("7", true)]
    [InlineData("7.021", true)] // 実データ: Camellia - Hello (BPM) 2021 [20.21]
    [InlineData("7.49", true)]  // 実データ: M2U - Myosotis
    [InlineData("6.5", false)]  // 偶数丸めで 6（解析エンジンと同じ）
    [InlineData("7.5", false)]  // 偶数丸めで 8
    [InlineData("NaN", false)]
    public void ChartHeader_RoundsCircleSizeLikeTheEngine(string cs, bool sevenKey)
    {
        Assert.Equal(sevenKey, Analyze7kRules.TryReadChartHeader(Encoding.UTF8.GetBytes(SevenKeyChart(cs: cs)), out _, out var reason));
        if (!sevenKey)
            Assert.Equal(Analyze7kFailure.NotSevenKey, reason);
    }

    [Theory]
    [InlineData("no dan estimate for this chart", Analyze7kFailure.NoDanEstimate)]
    [InlineData("only 7K supported", Analyze7kFailure.NotSevenKey)]
    [InlineData("unsupported chart", Analyze7kFailure.NotSevenKey)]
    [InlineData("NaN column or note time", Analyze7kFailure.EngineError)]
    [InlineData("panic in analysis", Analyze7kFailure.EngineError)]
    public void Failure_FromEngineMessage(string message, string expected)
        => Assert.Equal(expected, Analyze7kFailure.FromEngineMessage(message));

    [Fact]
    public void Failure_RetriesAllButInvalidFilesAndChartsWithoutDanEstimate()
    {
        Assert.True(Analyze7kFailure.IsRetryable(Analyze7kFailure.FileMissing));
        Assert.True(Analyze7kFailure.IsRetryable(Analyze7kFailure.Md5Mismatch));
        Assert.True(Analyze7kFailure.IsRetryable(Analyze7kFailure.EngineError));
        // 以前の版で空の難易度・CS 7.021 などに付けた not_7k は解析し直す
        Assert.True(Analyze7kFailure.IsRetryable(Analyze7kFailure.NotSevenKey));
        Assert.False(Analyze7kFailure.IsRetryable(Analyze7kFailure.NoDanEstimate));
        Assert.False(Analyze7kFailure.IsRetryable(Analyze7kFailure.InvalidFile));
    }

    [Fact]
    public void ChartHeader_SkipsLeadingBlankLines()
    {
        // osu! 本体と同じく、"osu file format" の前の空行・空白は読み飛ばす
        Assert.True(Analyze7kRules.TryReadChartHeader(Encoding.UTF8.GetBytes("\r\n\r\n" + SevenKeyChart(od: "7")), out var od, out _));
        Assert.Equal(7, od);
        Assert.True(Analyze7kRules.TryReadChartHeader([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(" \t\n" + SevenKeyChart())], out _, out _));
        Assert.False(Analyze7kRules.TryReadChartHeader("\r\n\r\nnot an osu file"u8, out _, out var reason));
        Assert.Equal(Analyze7kFailure.InvalidFile, reason);
    }

    [Fact]
    public void AlternateChartPath_TrimsTrailingSpacesAndDotsOfFolder()
    {
        // osu!.db のフォルダ名 "2536025  - " に対し、Windows 上のフォルダは "2536025  -"
        Assert.Equal(Path.Combine("Songs", "2536025  -", "a.osu"), Analyze7kRules.AlternateChartPath("Songs", "2536025  - ", "a.osu"));
        Assert.Equal(Path.Combine("Songs", "abc", "a.osu"), Analyze7kRules.AlternateChartPath("Songs", "abc. .", "a.osu"));
        Assert.Null(Analyze7kRules.AlternateChartPath("Songs", "123 Artist - Title", "a.osu"));
        Assert.Null(Analyze7kRules.AlternateChartPath("Songs", " . ", "a.osu"));
    }

    // ───────────────────────────────────────────
    // キャッシュ
    // ───────────────────────────────────────────

    private static ManiaChartAnalysis Analysis(
        double overall = 30,
        double? ln = null,
        double? regularDan = 9,
        double? lnDan = null,
        GenreValues tendencies = default,
        RegularDanGenre regularGenres = RegularDanGenre.None,
        LnDanGenre lnGenres = LnDanGenre.None,
        GenreValues? skills = null) => new()
    {
        PrismOverall = overall,
        PrismLnOverall = ln,
        GenreSkills = skills ?? new GenreValues(28, 20, 22, 25, 30),
        GenreTendencies = tendencies,
        RegularDan = regularDan,
        LnDan = lnDan,
        PrimaryFamily = lnDan is null ? DanSide.Regular : DanSide.Ln,
        RegularGenres = regularGenres,
        LnGenres = lnGenres,
        Eligible = true,
        Vibro = false,
    };

    [Fact]
    public void Cache_RoundTripsAndDropsOtherVersions()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nakuru-analyze7k-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "results-oldversion.jsonl"), "{}\n");
            var saved = Analysis(ln: 33.5, lnDan: 11.2, tendencies: new GenreValues(12.5f, 0, 40, 7, 66),
                regularGenres: RegularDanGenre.Jack | RegularDanGenre.Speed, lnGenres: LnDanGenre.Release);
            using (var cache = new Analyze7kCache(dir, "v1"))
            {
                cache.Load();
                cache.Add("md5", saved);
                cache.AddFailure("missing", Analyze7kFailure.FileMissing);
                cache.AddFailure("other", Analyze7kFailure.NoDanEstimate);
            }
            File.AppendAllText(Path.Combine(dir, "results-v1.jsonl"), "{\"md5\":\"broken"); // 書きかけの行

            using var reloaded = new Analyze7kCache(dir, "v1");
            reloaded.Load();
            Assert.False(File.Exists(Path.Combine(dir, "results-oldversion.jsonl")));
            Assert.True(reloaded.TryGet("md5", out var loaded));
            Assert.Equal(saved, loaded);
            Assert.Equal(Analyze7kFailure.FileMissing, reloaded.GetFailure("missing"));
            reloaded.ForgetRetryableFailures();
            Assert.Null(reloaded.GetFailure("missing"));
            Assert.Equal(Analyze7kFailure.NoDanEstimate, reloaded.GetFailure("other"));

            // 書きかけの行の後でも、次の結果は別の行として読める
            reloaded.Add("after-broken", saved);
            reloaded.Dispose();
            using var third = new Analyze7kCache(dir, "v1");
            third.Load();
            Assert.True(third.TryGet("after-broken", out _));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Cache_DoesNotRepeatTheSameFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nakuru-analyze7k-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var cache = new Analyze7kCache(dir, "v1"))
            {
                cache.Load();
                cache.AddFailure("a", Analyze7kFailure.Md5Mismatch);   // 書く
                cache.ForgetRetryableFailures();
                cache.AddFailure("a", Analyze7kFailure.Md5Mismatch);   // 再試行して同じ失敗 → 書かない
                cache.AddFailure("a", Analyze7kFailure.FileMissing);   // 理由が変わった → 書く
                cache.Add("b", Analysis());                            // 書く
                cache.AddFailure("b", Analyze7kFailure.EngineError);   // 結果の後の失敗 → 書く
            }

            // 次の起動（読み込み → 再試行）でも、同じ失敗は書き足さない
            using (var cache = new Analyze7kCache(dir, "v1"))
            {
                cache.Load();
                cache.ForgetRetryableFailures();
                Assert.Null(cache.GetFailure("a"));
                cache.AddFailure("a", Analyze7kFailure.FileMissing);
                cache.AddFailure("b", Analyze7kFailure.EngineError);
                Assert.Equal(Analyze7kFailure.FileMissing, cache.GetFailure("a"));
                Assert.Equal(Analyze7kFailure.EngineError, cache.GetFailure("b"));
            }

            var lines = File.ReadAllLines(Path.Combine(dir, "results-v1.jsonl")).Where(l => l.Length > 0).ToArray();
            Assert.Equal(4, lines.Length);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ───────────────────────────────────────────
    // フィルタ・ソート
    // ───────────────────────────────────────────

    private static Beatmap Map(string md5, ManiaChartAnalysis? analysis, int keyCount = 7) => new()
    {
        MD5Hash = md5,
        KeyCount = keyCount,
        Status = BeatmapStatus.Ranked,
        Title = "T",
        TitleUnicode = "",
        Artist = "A",
        ArtistUnicode = "",
        Version = "V",
        Creator = "C",
        FolderName = "F",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = string.Empty,
        ChartAnalysis = analysis,
    };

    private static FilterCondition Condition(FilterTarget target, string? choice = null, string value = "", string max = "")
    {
        var condition = new FilterCondition { Target = target };
        if (choice is not null)
            condition.ChoiceValue = choice;
        condition.Value = value;
        condition.ValueMax = max;
        return condition;
    }

    [Fact]
    public void ChoiceFilters_MatchTopGenreAndDanGenres()
    {
        var jackHeavy = Map("a", Analysis(skills: new GenreValues(24, 20, 20, 27, 30), tendencies: new GenreValues(10, 5, 5, 20, 60),
            regularGenres: RegularDanGenre.Jack | RegularDanGenre.Tech));
        var lnChart = Map("b", Analysis(lnDan: 10, skills: new GenreValues(30, 20, 20, 26, 22), tendencies: new GenreValues(50, 5, 5, 20, 10),
            lnGenres: LnDanGenre.Inverse));
        var notAnalyzed = Map("c", null, keyCount: 4);

        var top = Condition(FilterTarget.ManiaPrismTopGenre, "Jack");
        Assert.True(top.IsChoiceType);
        Assert.Equal(["Chordstream", "Bracket", "Delay", "Technical", "Jack", "LN"], top.ChoiceOptions);
        Assert.True(top.Matches(jackHeavy));
        Assert.False(top.Matches(lnChart));
        Assert.False(top.Matches(notAnalyzed));

        var tech = Condition(FilterTarget.ManiaDanRegularGenre, "Tech");
        Assert.True(tech.Matches(jackHeavy));
        Assert.False(tech.Matches(lnChart));

        var inverse = Condition(FilterTarget.ManiaDanLnGenre, "Inverse");
        Assert.True(inverse.Matches(lnChart));
        Assert.False(inverse.Matches(jackHeavy));
    }

    [Fact]
    public void TopGenreFilter_IncludesLn()
    {
        var regular = Map("a", Analysis(overall: 30, skills: new GenreValues(24, 20, 20, 27, 30)));
        // LN が5ジャンルの最大を超える（LN の多い譜面の補正でジャンル値は LN×0.85 寄り）
        var lnLead = Map("b", Analysis(overall: 28, ln: 35, skills: new GenreValues(25, 20, 20, 26, 29.75f)));
        // LN があっても5ジャンルの最大以下なら、そのジャンル
        var lnBelow = Map("c", Analysis(overall: 30, ln: 25, skills: new GenreValues(24, 20, 20, 27, 30)));
        // 同じ値ならジャンルを優先
        var tie = Map("d", Analysis(overall: 30, ln: 30, skills: new GenreValues(24, 20, 20, 27, 30)));

        Assert.Equal("Jack", regular.ChartAnalysis!.TopSkillAxis);
        Assert.Equal("LN", lnLead.ChartAnalysis!.TopSkillAxis);
        Assert.Equal("Jack", lnBelow.ChartAnalysis!.TopSkillAxis);
        Assert.Equal("Jack", tie.ChartAnalysis!.TopSkillAxis);

        var ln = Condition(FilterTarget.ManiaPrismTopGenre, "LN");
        Assert.True(ln.Matches(lnLead));
        Assert.False(ln.Matches(regular));
        Assert.False(ln.Matches(lnBelow));
        Assert.False(ln.Matches(Map("e", null, keyCount: 4)));
        Assert.True(Condition(FilterTarget.ManiaPrismTopGenre, "Jack").Matches(lnBelow));
    }

    [Fact]
    public void GenreSkillFilter_ComparesChosenGenreInRange()
    {
        var a = Map("a", Analysis(overall: 30, ln: 33, skills: new GenreValues(24, 20, 20, 27, 30)));
        var b = Map("b", Analysis(overall: 30, skills: new GenreValues(29, 20, 20, 25, 18)));
        var condition = Condition(FilterTarget.ManiaPrismGenreSkill, value: "28", max: "35");

        Assert.True(condition.HasCompareTarget);
        Assert.True(condition.IsRangeComparison);
        Assert.False(condition.SupportsEquals);
        Assert.Equal(["Chordstream", "Bracket", "Delay", "Technical", "Jack", "LN"], condition.CompareOptions);
        Assert.Equal("Chordstream", condition.CompareValue); // 既定は先頭

        Assert.False(condition.Matches(a)); // Chordstream 24
        Assert.True(condition.Matches(b));  // Chordstream 29
        condition.CompareValue = "Jack";
        Assert.True(condition.Matches(a));  // 30
        Assert.False(condition.Matches(b)); // 18
        condition.CompareValue = "LN";
        Assert.True(condition.Matches(a));  // 33
        Assert.False(condition.Matches(b)); // LN 測定不能
        Assert.False(condition.Matches(Map("c", null, keyCount: 4)));

        // 範囲が空なら絞り込まない
        Assert.True(Condition(FilterTarget.ManiaPrismGenreSkill).Matches(b));
    }

    [Fact]
    public void GenreConcentrationFilter_ComparesShareInPercent()
    {
        // 傾向 30 / 0 / 10 / 20 / 40 → 濃度 30% / 0% / 10% / 20% / 40%
        var chart = Map("a", Analysis(tendencies: new GenreValues(30, 0, 10, 20, 40)));
        var condition = Condition(FilterTarget.ManiaPrismGenreConcentration, value: "35");

        Assert.Equal(Enum.GetNames<PrismGenre>(), condition.CompareOptions); // LN はない
        condition.CompareValue = "Jack";
        Assert.True(condition.Matches(chart));
        condition.CompareValue = "Technical";
        Assert.False(condition.Matches(chart));
        condition.Value = "0";
        condition.ValueMax = "0";
        condition.CompareValue = "Bracket";
        Assert.True(condition.Matches(chart)); // 傾向なし = 0%
        Assert.False(condition.Matches(Map("b", null, keyCount: 4)));
    }

    [Fact]
    public void CompareValue_SurvivesPresets()
    {
        var condition = new FilterCondition { Target = FilterTarget.ManiaPrismGenreSkill };
        condition.CompareValue = "LN";
        condition.Value = "20";
        condition.ValueMax = "40";

        var restored = FilterConditionData.FromFilterCondition(condition).ToFilterCondition();
        Assert.Equal(FilterTarget.ManiaPrismGenreSkill, restored.Target);
        Assert.Equal("LN", restored.CompareValue);
        Assert.Equal(ComparisonType.Range, restored.ComparisonType);
        Assert.Equal("20", restored.Value);
        Assert.Equal("40", restored.ValueMax);
    }

    [Fact]
    public void ChoiceValue_DefaultsToFirstOptionAndSurvivesPresets()
    {
        var condition = new FilterCondition { Target = FilterTarget.ManiaDanLnGenre };
        Assert.Equal("General", condition.ChoiceValue);
        condition.ChoiceValue = "Release";

        var restored = FilterConditionData.FromFilterCondition(condition).ToFilterCondition();
        Assert.Equal(FilterTarget.ManiaDanLnGenre, restored.Target);
        Assert.Equal("Release", restored.ChoiceValue);
    }

    [Fact]
    public void RangeFilters_ExcludeChartsWithoutValue()
    {
        var withLn = Map("a", Analysis(ln: 35, lnDan: 11));
        var withoutLn = Map("b", Analysis());

        var lnOverall = Condition(FilterTarget.ManiaPrismLnOverall, value: "30", max: "40");
        Assert.Equal(ComparisonType.Range, lnOverall.ComparisonType);
        Assert.True(lnOverall.Matches(withLn));
        Assert.False(lnOverall.Matches(withoutLn));

        var regDan = Condition(FilterTarget.ManiaDanRegular, value: "8.5");
        Assert.True(regDan.Matches(withLn));
        Assert.False(regDan.Matches(Map("c", null, keyCount: 4)));

        // 条件が空なら絞り込まない
        Assert.True(Condition(FilterTarget.ManiaDanLn).Matches(withoutLn));
    }

    [Fact]
    public void Sort_ByPrismOverall_PutsUnanalyzedLast()
    {
        var low = Map("low", Analysis(overall: 20));
        var high = Map("high", Analysis(overall: 40));
        var none = Map("none", null);
        var comparer = new BeatmapMultiComparer(
            [new SortRule { Field = SortField.ManiaPrismOverall, Direction = SortDirection.Descending }],
            false, ScoreSystemCategory.Default, ModCategory.NoMod);

        var sorted = new[] { none, low, high };
        Array.Sort(sorted, comparer);
        Assert.Equal(["high", "low", "none"], sorted.Select(b => b.MD5Hash).ToArray());
    }

    // ───────────────────────────────────────────
    // 進み具合の表示（進捗率・件数・残り時間）
    // ───────────────────────────────────────────

    private static string TestTranslate(string key) => key switch
    {
        "MapList.Analyze7k.Count" => "{0} / {1} charts",
        "MapList.Analyze7k.RemainingEstimating" => "estimating",
        "MapList.Analyze7k.RemainingUnderMinute" => "<1 min",
        "MapList.Analyze7k.RemainingMinutes" => "{0} min",
        "MapList.Analyze7k.RemainingHours" => "{0} h {1} min",
        "MapList.Analyze7k.Progress" => "about",
        "MapList.Analyze7k.Failed" => "failed {0}",
        _ => key,
    };

    private static TimeSpan Seconds(double s) => TimeSpan.FromSeconds(s);

    [Theory]
    [InlineData(0, 24, 12)] // 既定: 論理コア数の半分
    [InlineData(0, 8, 4)]
    [InlineData(0, 1, 1)]   // 1コアでも 1
    [InlineData(3, 24, 3)]  // 設定があればその値
    public void Parallelism_DefaultsToHalfTheLogicalCores(int configured, int processors, int expected)
    {
        Assert.Equal(expected, Analyze7kService.ResolveParallelism(configured, processors));
    }

    [Fact]
    public void RemainingTime_WaitsForEnoughSamplesThenUsesRate()
    {
        var estimator = new Analyze7kRemainingTimeEstimator();
        Assert.Null(estimator.Update(Seconds(0), 0, 1000));
        // 3 秒未満は推定しない
        Assert.Null(estimator.Update(Seconds(2), 200, 800));
        // 毎秒 100 件 → 残り 700 件は 7 秒
        Assert.Equal(Seconds(7), estimator.Update(Seconds(3), 300, 700));
        // 残りがなければ 0
        Assert.Equal(TimeSpan.Zero, estimator.Update(Seconds(4), 1000, 0));
    }

    [Fact]
    public void RemainingTime_FollowsRecentRate()
    {
        var estimator = new Analyze7kRemainingTimeEstimator();
        // 最初の 60 秒は毎秒 10 件
        for (var t = 0; t <= 60; t++)
            estimator.Update(Seconds(t), t * 10, 10_000 - t * 10);
        // その後の 40 秒は毎秒 50 件（直近 30 秒の窓には速い区間だけが残る）
        TimeSpan? remaining = null;
        for (var t = 61; t <= 100; t++)
        {
            var processed = 600 + (t - 60) * 50;
            remaining = estimator.Update(Seconds(t), processed, 10_000 - processed);
        }
        // 残り 7,400 件 ÷ 毎秒 50 件 = 148 秒（開始からの平均 毎秒 26 件なら約 285 秒）
        Assert.Equal(148, remaining!.Value.TotalSeconds, 6);
    }

    [Fact]
    public void RemainingTime_StalledWorkIsUnknown()
    {
        var estimator = new Analyze7kRemainingTimeEstimator();
        estimator.Update(Seconds(0), 0, 100);
        Assert.Null(estimator.Update(Seconds(10), 0, 100));
    }

    [Theory]
    [InlineData(null, "estimating")]
    [InlineData(59.0, "<1 min")]
    [InlineData(60.0, "1 min")]
    [InlineData(61.0, "2 min")]
    [InlineData(59 * 60.0, "59 min")]
    [InlineData(60 * 60.0, "1 h 0 min")]
    [InlineData(125 * 60.0 - 30, "2 h 5 min")]
    public void RemainingText_RoundsUpToMinutes(double? seconds, string expected)
    {
        TimeSpan? remaining = seconds is { } s ? Seconds(s) : null;
        Assert.Equal(expected, Analyze7kProgressDisplay.FormatRemaining(remaining, TestTranslate));
    }

    [Fact]
    public void ProgressDisplay_ShowsPercentCountAndRemainingOnlyWhileRunning()
    {
        var display = Analyze7kProgressDisplay.From(
            new Analyze7kProgress(Analyze7kState.Running, 43_672, 43_673, 0, Seconds(5)), TestTranslate)!;
        // 終わるまで 100% にしない
        Assert.Equal("99%", display.PercentText);
        Assert.Equal(43_672 * 100.0 / 43_673, display.Percent, 9);
        Assert.Equal($"{43_672:N0} / {43_673:N0} charts", display.CountText);
        Assert.Equal("<1 min", display.RemainingText);
        Assert.Equal("about", display.ToolTip);

        var withFailures = Analyze7kProgressDisplay.From(
            new Analyze7kProgress(Analyze7kState.Running, 10, 100, 3), TestTranslate)!;
        Assert.Equal("10%", withFailures.PercentText);
        Assert.Equal("estimating", withFailures.RemainingText);
        Assert.Equal($"about{Environment.NewLine}failed 3", withFailures.ToolTip);

        // 解析中でない・対象が決まる前は表示しない
        Assert.Null(Analyze7kProgressDisplay.From(new Analyze7kProgress(Analyze7kState.Running, 0, 0, 0), TestTranslate));
        Assert.Null(Analyze7kProgressDisplay.From(new Analyze7kProgress(Analyze7kState.Completed, 100, 100, 0), TestTranslate));
        Assert.Null(Analyze7kProgressDisplay.From(Analyze7kProgress.Initial, TestTranslate));
    }

    // ───────────────────────────────────────────
    // ネイティブライブラリ（analyze_7k.dll）
    // ───────────────────────────────────────────

    [Fact]
    public void Native_AnalyzesSevenKeyChart()
    {
        Assert.SkipUnless(Analyze7kNative.IsAvailable, "analyze_7k.dll not built");
        Assert.StartsWith("prism-", Analyze7kNative.EngineVersion);
        Assert.Contains("analyze7k-", Analyze7kNative.EngineVersion);

        Analyze7kResponse response;
        try
        {
            response = Analyze7kNative.Analyze(Encoding.UTF8.GetBytes(SevenKeyChart()), 8);
        }
        catch (Analyze7kEngineException ex)
        {
            // 合成譜面が段位エンジンの対象外と判定された場合も、理由付きの例外になること
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
            return;
        }

        var analysis = Analyze7kNative.ToAnalysis(response);
        Assert.True(analysis.PrismOverall > 0);
        Assert.NotNull(analysis.RegularDan);
        foreach (var genre in GenreValues.Genres)
        {
            Assert.InRange(analysis.GenreTendencies[genre], 0, 100);
            Assert.InRange(analysis.GenreConcentrations[genre], 0, 100);
            Assert.True(analysis.GenreSkills[genre] >= 0);
        }
        // 単押しだけの譜面に Jack（縦連）・Bracket は含まれない
        Assert.Equal(0, analysis.GenreTendencies.Jack, 3);
        Assert.Equal(0, analysis.GenreConcentrations.Jack, 3);
        Assert.Equal(100, analysis.GenreConcentrations.Sum(), 3);
        Assert.Null(analysis.PrismLnOverall);
    }

    [Fact]
    public void Native_RejectsNonSevenKeyAndInvalidInput()
    {
        Assert.SkipUnless(Analyze7kNative.IsAvailable, "analyze_7k.dll not built");
        var fourKey = Assert.Throws<Analyze7kEngineException>(
            () => Analyze7kNative.Analyze(Encoding.UTF8.GetBytes(SevenKeyChart(cs: "4")), 8));
        Assert.Contains("only 7K", fourKey.Message);
        Assert.Throws<Analyze7kEngineException>(() => Analyze7kNative.Analyze(Encoding.UTF8.GetBytes(SevenKeyChart()), 11));
        Assert.Throws<Analyze7kEngineException>(() => Analyze7kNative.Analyze([0xFF, 0xFE], 8));

        // ノーツのない空の難易度は「段位推定なし」（7K 以外とは分ける）
        var empty = Assert.Throws<Analyze7kEngineException>(
            () => Analyze7kNative.Analyze(Encoding.UTF8.GetBytes(SevenKeyChart(notes: 0)), 8));
        Assert.Equal(Analyze7kFailure.NoDanEstimate, Analyze7kFailure.FromEngineMessage(empty.Message));
    }

    [Fact]
    public void Native_IgnoresNotesWithoutTime()
    {
        // 時刻が NaN のノーツ（実データ: remu - generic future funk remix [pop]）があっても、それを除いた譜面として解析する
        Assert.SkipUnless(Analyze7kNative.IsAvailable, "analyze_7k.dll not built");
        var chart = SevenKeyChart();
        var withNaN = chart + "109,192,NaN,128,0,116219:0:0:0:0:\n256,192,NaN,128,0,116218:0:0:0:0:\n";

        Analyze7kResponse expected;
        try
        {
            expected = Analyze7kNative.Analyze(Encoding.UTF8.GetBytes(chart), 8);
        }
        catch (Analyze7kEngineException)
        {
            Assert.Skip("synthetic chart has no dan estimate");
            return;
        }

        var actual = Analyze7kNative.Analyze(Encoding.UTF8.GetBytes(withNaN), 8);
        Assert.Equal(Analyze7kNative.ToAnalysis(expected), Analyze7kNative.ToAnalysis(actual));
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(BucketFeatures))]
internal partial class TestJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
