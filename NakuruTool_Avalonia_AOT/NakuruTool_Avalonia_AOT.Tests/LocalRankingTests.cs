using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// ローカルランキング (ScoresDbParser / LocalScoreStore / FillLocalRanking / FindBestPlay) の単体テスト
/// </summary>
public class LocalRankingTests
{
    private const int DT = OsuMods.DoubleTime;
    private const int NC = OsuMods.Nightcore | OsuMods.DoubleTime;
    private const int HT = OsuMods.HalfTime;
    private const int V2 = OsuMods.ScoreV2;
    private const int TargetPractice = 1 << 23;

    // ───────────────────────────────────────────
    // scores.db 合成ヘルパー
    // ───────────────────────────────────────────

    private sealed record TestScore(
        byte Ruleset, int Score, int Mods, long Ticks,
        ushort Geki = 0, ushort C300 = 0, ushort Katu = 0, ushort C100 = 0, ushort C50 = 0, ushort Miss = 0,
        string Player = "player");

    private static byte[] BuildScoresDb(params (string Md5, TestScore[] Scores)[] entries)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(20240101);
        w.Write(entries.Length);
        foreach (var (md5, scores) in entries)
        {
            WriteString(w, md5);
            w.Write(scores.Length);
            foreach (var s in scores)
            {
                w.Write(s.Ruleset);
                w.Write(20240101);
                WriteString(w, md5);
                WriteString(w, s.Player);
                WriteString(w, "replaymd5replaymd5replaymd5repla");
                w.Write(s.C300);
                w.Write(s.C100);
                w.Write(s.C50);
                w.Write(s.Geki);
                w.Write(s.Katu);
                w.Write(s.Miss);
                w.Write(s.Score);
                w.Write((ushort)123); // max combo
                w.Write((byte)0);     // perfect
                w.Write(s.Mods);
                WriteString(w, string.Empty); // life bar
                w.Write(s.Ticks);
                w.Write(-1);
                w.Write(987654321L); // online score id
                if ((s.Mods & TargetPractice) != 0)
                    w.Write(1.0d);
            }
        }
        w.Flush();
        return ms.ToArray();
    }

    private static void WriteString(BinaryWriter w, string value)
    {
        if (value.Length == 0)
        {
            w.Write((byte)0x00);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        w.Write((byte)0x0b);
        uint len = (uint)bytes.Length;
        do
        {
            byte b = (byte)(len & 0x7F);
            len >>= 7;
            if (len != 0) b |= 0x80;
            w.Write(b);
        } while (len != 0);
        w.Write(bytes);
    }

    // ───────────────────────────────────────────
    // ScoresDbParser
    // ───────────────────────────────────────────

    [Fact]
    public void Parse_KeepsOnlyManiaScores_SortedByScoreDescending()
    {
        var bytes = BuildScoresDb(
            ("aaaa", new[]
            {
                new TestScore(3, 500_000, 0, 1000),
                new TestScore(0, 999_999, 0, 2000), // osu!std → 除外
                new TestScore(3, 900_000, DT, 3000),
                new TestScore(3, 700_000, 0, 4000),
            }),
            ("bbbb", new[]
            {
                new TestScore(1, 100, 0, 1), // taiko のみ → エントリごと除外
            }),
            ("cccc", new[]
            {
                new TestScore(3, 10, 0, 5),
            }));

        var result = ScoresDbParser.Parse(bytes);

        Assert.Equal(new[] { "aaaa", "cccc" }, result.BeatmapMd5s);
        Assert.Equal(new[] { 0, 3, 4 }, result.EntryStarts);
        Assert.Equal(4, result.Scores.Length);
        Assert.Equal(new[] { 900_000, 700_000, 500_000 },
            new[] { result.Scores[0].ReplayScore, result.Scores[1].ReplayScore, result.Scores[2].ReplayScore });
        Assert.Equal(10, result.Scores[3].ReplayScore);
    }

    [Fact]
    public void Parse_ReadsJudgementCountsModsAndTimestamp()
    {
        var bytes = BuildScoresDb(
            ("aaaa", new[]
            {
                new TestScore(3, 812_345, HT | V2, 638_000_000_000_000_000,
                    Geki: 1000, C300: 200, Katu: 30, C100: 4, C50: 5, Miss: 6, Player: "日本語ユーザー"),
            }));

        var score = Assert.Single(ScoresDbParser.Parse(bytes).Scores);

        Assert.Equal(812_345, score.ReplayScore);
        Assert.Equal(HT | V2, score.Mods);
        Assert.Equal(638_000_000_000_000_000, score.TimestampTicks);
        Assert.Equal(1000, score.CountGeki);
        Assert.Equal(200, score.Count300);
        Assert.Equal(30, score.CountKatu);
        Assert.Equal(4, score.Count100);
        Assert.Equal(5, score.Count50);
        Assert.Equal(6, score.CountMiss);
    }

    [Fact]
    public void Parse_SkipsTargetPracticeExtraField()
    {
        // TP 付き非 mania スコアの追加 double を読み飛ばせないと後続がずれる
        var bytes = BuildScoresDb(
            ("aaaa", new[]
            {
                new TestScore(0, 1, TargetPractice, 1),
                new TestScore(3, 42, 0, 2),
            }),
            ("bbbb", new[] { new TestScore(3, 43, 0, 3) }));

        var result = ScoresDbParser.Parse(bytes);

        Assert.Equal(new[] { "aaaa", "bbbb" }, result.BeatmapMd5s);
        Assert.Equal(42, result.Scores[0].ReplayScore);
        Assert.Equal(43, result.Scores[1].ReplayScore);
    }

    [Fact]
    public void Parse_NoManiaScores_ReturnsEmpty()
    {
        var bytes = BuildScoresDb(("aaaa", new[] { new TestScore(0, 1, 0, 1) }));

        var result = ScoresDbParser.Parse(bytes);

        Assert.Equal(0, result.EntryCount);
        Assert.Empty(result.Scores);
    }

    // ───────────────────────────────────────────
    // LocalScoreStore
    // ───────────────────────────────────────────

    [Fact]
    public void Store_MapsEntriesToBeatmapIndex()
    {
        var parsed = ScoresDbParser.Parse(BuildScoresDb(
            ("aaaa", new[] { new TestScore(3, 1, 0, 1), new TestScore(3, 2, 0, 2) }),
            ("zzzz", new[] { new TestScore(3, 3, 0, 3) }), // osu!.db に無い譜面
            ("cccc", new[] { new TestScore(3, 4, 0, 4) })));

        var index = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["aaaa"] = 2,
            ["bbbb"] = 0,
            ["cccc"] = 1,
        };

        var store = LocalScoreStore.Build(parsed, index, 3);

        Assert.True(store.GetScores(0).IsEmpty);
        Assert.Equal(4, store.GetScores(1)[0].ReplayScore);
        Assert.Equal(2, store.GetScores(2).Length);
        Assert.Equal(2, store.GetScores(2)[0].ReplayScore);
        Assert.True(store.GetScores(-1).IsEmpty);
        Assert.True(store.GetScores(3).IsEmpty);
    }

    // ───────────────────────────────────────────
    // BuildScoreView
    // ───────────────────────────────────────────

    private static readonly LocalScore[] SortedScores =
    {
        new(1, 990_000, DT | V2, CountGeki: 10),
        new(2, 950_000, 0, CountGeki: 9),
        new(3, 940_000, NC, CountGeki: 8),
        new(4, 900_000, DT, CountGeki: 7),
        new(5, 800_000, 0, CountGeki: 6),
        new(6, 700_000, HT, CountGeki: 5),
    };

    private static List<LocalRankingEntry> Ranking(ScoreSystemCategory system, ModCategory mod)
    {
        var list = new List<LocalRankingEntry>();
        BeatmapListViewModelBase.FillLocalRanking(SortedScores, system, mod, list);
        return list;
    }

    [Fact]
    public void FillLocalRanking_FiltersBySystemAndMod_KeepsDescendingOrder()
    {
        // DT と NC はどちらも DoubleTime 扱い。ScoreV2 付きは除外
        Assert.Equal(new[] { 940_000, 900_000 }, Ranking(ScoreSystemCategory.Default, ModCategory.DoubleTime).ConvertAll(e => e.Score));
        Assert.Equal(new[] { 950_000, 800_000 }, Ranking(ScoreSystemCategory.Default, ModCategory.NoMod).ConvertAll(e => e.Score));
        Assert.Equal(new[] { 990_000 }, Ranking(ScoreSystemCategory.ScoreV2, ModCategory.DoubleTime).ConvertAll(e => e.Score));
        Assert.Empty(Ranking(ScoreSystemCategory.ScoreV2, ModCategory.HalfTime));
    }

    [Fact]
    public void FindBestPlay_ReturnsTopMatchingScore()
    {
        var best = BeatmapListViewModelBase.FindBestPlay(SortedScores, ScoreSystemCategory.Default, ModCategory.DoubleTime);

        Assert.NotNull(best);
        Assert.Equal(940_000, best!.Score);
        Assert.Equal(8, best.Count320);
    }

    [Fact]
    public void FindBestPlay_NoMatch_ReturnsNull()
    {
        Assert.Null(BeatmapListViewModelBase.FindBestPlay(SortedScores, ScoreSystemCategory.ScoreV2, ModCategory.HalfTime));
        Assert.Null(BeatmapListViewModelBase.FindBestPlay(ReadOnlySpan<LocalScore>.Empty, ScoreSystemCategory.Default, ModCategory.NoMod));
    }

    [Fact]
    public void LocalRankingEntry_ConvertsTimestampAndAccuracy()
    {
        var utc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var entry = new LocalRankingEntry(new LocalScore(utc.Ticks, 1, 0, CountGeki: 10));

        Assert.Equal(utc.ToLocalTime(), entry.PlayedAt);
        Assert.Equal(100.0, entry.Accuracy, precision: 6);
        Assert.Equal("SS", entry.Grade);
        Assert.Null(new LocalRankingEntry(new LocalScore(0, 1, 0)).PlayedAt);
    }
}
