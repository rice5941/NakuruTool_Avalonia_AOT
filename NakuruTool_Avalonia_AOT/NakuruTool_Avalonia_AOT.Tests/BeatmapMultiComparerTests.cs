using System;
using NakuruTool_Avalonia_AOT.Features.MapList.Sorting;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="BeatmapMultiComparer"/> の比較ロジック回帰テスト。
/// 設計書 §9.1〜9.5 を網羅 (§9.6 は MapListSortViewModelTests 側)。
/// </summary>
public class BeatmapMultiComparerTests
{
    // ---------------- helpers ----------------

    private static Beatmap MakeBeatmap(
        string md5,
        int keyCount = 7,
        BeatmapStatus status = BeatmapStatus.Ranked,
        string title = "T",
        string titleUnicode = "",
        string artist = "A",
        string artistUnicode = "",
        string version = "V",
        string creator = "C",
        double bpm = 120,
        double difficulty = 5.0,
        double longNoteRate = 0.0,
        DateTime? lastPlayed = null,
        DateTime? lastModifiedTime = null,
        int playCount = 0,
        double od = 8.0,
        double hp = 7.0,
        int drainTimeSeconds = 60,
        int bestScoreNoMod = 0,
        double bestAccuracyNoMod = 0,
        int bestScoreHT = 0,
        double bestAccuracyHT = 0,
        int bestScoreDT = 0,
        double bestAccuracyDT = 0,
        int bestScoreV2NoMod = 0,
        double bestAccuracyV2NoMod = 0,
        int bestScoreV2HT = 0,
        double bestAccuracyV2HT = 0,
        int bestScoreV2DT = 0,
        double bestAccuracyV2DT = 0)
    {
        return new Beatmap
        {
            MD5Hash = md5,
            KeyCount = keyCount,
            Status = status,
            Title = title,
            TitleUnicode = titleUnicode,
            Artist = artist,
            ArtistUnicode = artistUnicode,
            Version = version,
            Creator = creator,
            BPM = bpm,
            Difficulty = difficulty,
            LongNoteRate = longNoteRate,
            LastPlayed = lastPlayed,
            LastModifiedTime = lastModifiedTime,
            FolderName = "F",
            AudioFilename = "audio.mp3",
            OsuFileName = "x.osu",
            PlayCount = playCount,
            Grade = string.Empty,
            OD = od,
            HP = hp,
            DrainTimeSeconds = drainTimeSeconds,
            BestScoreNoMod = bestScoreNoMod,
            BestAccuracyNoMod = bestAccuracyNoMod,
            BestScoreHT = bestScoreHT,
            BestAccuracyHT = bestAccuracyHT,
            BestScoreDT = bestScoreDT,
            BestAccuracyDT = bestAccuracyDT,
            BestScoreV2NoMod = bestScoreV2NoMod,
            BestAccuracyV2NoMod = bestAccuracyV2NoMod,
            BestScoreV2HT = bestScoreV2HT,
            BestAccuracyV2HT = bestAccuracyV2HT,
            BestScoreV2DT = bestScoreV2DT,
            BestAccuracyV2DT = bestAccuracyV2DT,
        };
    }

    private static SortRule Rule(SortField field, SortDirection direction = SortDirection.Ascending)
        => new SortRule { Field = field, Direction = direction };

    private static BeatmapMultiComparer MakeComparer(
        SortRule[] rules,
        bool preferUnicode = false,
        ScoreSystemCategory scoreSystem = ScoreSystemCategory.Default,
        ModCategory mod = ModCategory.NoMod)
        => new BeatmapMultiComparer(rules, preferUnicode, scoreSystem, mod);

    // ---------------- §9.1 null/空文字は末尾固定 ----------------

    [Fact]
    public void LastPlayed_Ascending_PutsNullLast()
    {
        var withValue = MakeBeatmap("a", lastPlayed: new DateTime(2024, 1, 1));
        var withNull = MakeBeatmap("b", lastPlayed: null);

        var comparer = MakeComparer(new[] { Rule(SortField.LastPlayed, SortDirection.Ascending) });
        var arr = new[] { withNull, withValue };
        Array.Sort(arr, comparer);

        Assert.Same(withValue, arr[0]);
        Assert.Same(withNull, arr[1]);
    }

    [Fact]
    public void LastPlayed_Descending_PutsNullLast()
    {
        var newer = MakeBeatmap("a", lastPlayed: new DateTime(2025, 1, 1));
        var older = MakeBeatmap("b", lastPlayed: new DateTime(2024, 1, 1));
        var withNull = MakeBeatmap("c", lastPlayed: null);

        var comparer = MakeComparer(new[] { Rule(SortField.LastPlayed, SortDirection.Descending) });
        var arr = new[] { withNull, older, newer };
        Array.Sort(arr, comparer);

        Assert.Same(newer, arr[0]);
        Assert.Same(older, arr[1]);
        Assert.Same(withNull, arr[2]);
    }

    [Fact]
    public void LastModifiedTime_Ascending_PutsNullLast()
    {
        var withValue = MakeBeatmap("a", lastModifiedTime: new DateTime(2024, 1, 1));
        var withNull = MakeBeatmap("b", lastModifiedTime: null);

        var comparer = MakeComparer(new[] { Rule(SortField.LastModifiedTime, SortDirection.Ascending) });
        var arr = new[] { withNull, withValue };
        Array.Sort(arr, comparer);

        Assert.Same(withValue, arr[0]);
        Assert.Same(withNull, arr[1]);
    }

    [Fact]
    public void LastModifiedTime_Descending_PutsNullLast()
    {
        var newer = MakeBeatmap("a", lastModifiedTime: new DateTime(2025, 1, 1));
        var older = MakeBeatmap("b", lastModifiedTime: new DateTime(2024, 1, 1));
        var withNull = MakeBeatmap("c", lastModifiedTime: null);

        var comparer = MakeComparer(new[] { Rule(SortField.LastModifiedTime, SortDirection.Descending) });
        var arr = new[] { withNull, older, newer };
        Array.Sort(arr, comparer);

        Assert.Same(newer, arr[0]);
        Assert.Same(older, arr[1]);
        Assert.Same(withNull, arr[2]);
    }

    [Fact]
    public void Title_EmptyString_PutsLastInBothDirections()
    {
        var alpha = MakeBeatmap("a", title: "Alpha");
        var bravo = MakeBeatmap("b", title: "Bravo");
        var empty = MakeBeatmap("c", title: "");

        var asc = MakeComparer(new[] { Rule(SortField.Title, SortDirection.Ascending) });
        var arrAsc = new[] { empty, bravo, alpha };
        Array.Sort(arrAsc, asc);
        Assert.Same(alpha, arrAsc[0]);
        Assert.Same(bravo, arrAsc[1]);
        Assert.Same(empty, arrAsc[2]);

        var desc = MakeComparer(new[] { Rule(SortField.Title, SortDirection.Descending) });
        var arrDesc = new[] { empty, alpha, bravo };
        Array.Sort(arrDesc, desc);
        Assert.Same(bravo, arrDesc[0]);
        Assert.Same(alpha, arrDesc[1]);
        Assert.Same(empty, arrDesc[2]);
    }

    [Fact]
    public void Artist_EmptyString_PutsLastInBothDirections()
    {
        var alpha = MakeBeatmap("a", artist: "Alpha");
        var bravo = MakeBeatmap("b", artist: "Bravo");
        var empty = MakeBeatmap("c", artist: "");

        var asc = MakeComparer(new[] { Rule(SortField.Artist, SortDirection.Ascending) });
        var arrAsc = new[] { empty, bravo, alpha };
        Array.Sort(arrAsc, asc);
        Assert.Same(alpha, arrAsc[0]);
        Assert.Same(bravo, arrAsc[1]);
        Assert.Same(empty, arrAsc[2]);

        var desc = MakeComparer(new[] { Rule(SortField.Artist, SortDirection.Descending) });
        var arrDesc = new[] { empty, alpha, bravo };
        Array.Sort(arrDesc, desc);
        Assert.Same(bravo, arrDesc[0]);
        Assert.Same(alpha, arrDesc[1]);
        Assert.Same(empty, arrDesc[2]);
    }

    // ---------------- §9.2 Mod / ScoreSystem 反映 ----------------

    [Fact]
    public void BestScore_UsesDefaultNoModWhenDefaultAndNoModSelected()
    {
        var lo = MakeBeatmap("a", bestScoreNoMod: 100, bestScoreDT: 9999, bestScoreV2NoMod: 9999);
        var hi = MakeBeatmap("b", bestScoreNoMod: 500, bestScoreDT: 1, bestScoreV2NoMod: 1);

        var comparer = MakeComparer(
            new[] { Rule(SortField.BestScore, SortDirection.Ascending) },
            scoreSystem: ScoreSystemCategory.Default,
            mod: ModCategory.NoMod);

        var arr = new[] { hi, lo };
        Array.Sort(arr, comparer);
        Assert.Same(lo, arr[0]);
        Assert.Same(hi, arr[1]);
    }

    [Fact]
    public void BestScore_UsesScoreV2DoubleTimeWhenScoreV2AndDoubleTimeSelected()
    {
        var lo = MakeBeatmap("a", bestScoreV2DT: 100, bestScoreNoMod: 9999, bestScoreV2NoMod: 9999);
        var hi = MakeBeatmap("b", bestScoreV2DT: 500, bestScoreNoMod: 1, bestScoreV2NoMod: 1);

        var comparer = MakeComparer(
            new[] { Rule(SortField.BestScore, SortDirection.Ascending) },
            scoreSystem: ScoreSystemCategory.ScoreV2,
            mod: ModCategory.DoubleTime);

        var arr = new[] { hi, lo };
        Array.Sort(arr, comparer);
        Assert.Same(lo, arr[0]);
        Assert.Same(hi, arr[1]);
    }

    [Fact]
    public void BestAccuracy_UsesDefaultHalfTimeWhenDefaultAndHalfTimeSelected()
    {
        var lo = MakeBeatmap("a", bestAccuracyHT: 90.0, bestAccuracyNoMod: 99.9, bestAccuracyV2HT: 99.9);
        var hi = MakeBeatmap("b", bestAccuracyHT: 95.0, bestAccuracyNoMod: 0, bestAccuracyV2HT: 0);

        var comparer = MakeComparer(
            new[] { Rule(SortField.BestAccuracy, SortDirection.Ascending) },
            scoreSystem: ScoreSystemCategory.Default,
            mod: ModCategory.HalfTime);

        var arr = new[] { hi, lo };
        Array.Sort(arr, comparer);
        Assert.Same(lo, arr[0]);
        Assert.Same(hi, arr[1]);
    }

    [Fact]
    public void BestAccuracy_UsesScoreV2NoModWhenScoreV2AndNoModSelected()
    {
        var lo = MakeBeatmap("a", bestAccuracyV2NoMod: 90.0, bestAccuracyNoMod: 99.9, bestAccuracyV2DT: 99.9);
        var hi = MakeBeatmap("b", bestAccuracyV2NoMod: 95.0, bestAccuracyNoMod: 0, bestAccuracyV2DT: 0);

        var comparer = MakeComparer(
            new[] { Rule(SortField.BestAccuracy, SortDirection.Ascending) },
            scoreSystem: ScoreSystemCategory.ScoreV2,
            mod: ModCategory.NoMod);

        var arr = new[] { hi, lo };
        Array.Sort(arr, comparer);
        Assert.Same(lo, arr[0]);
        Assert.Same(hi, arr[1]);
    }

    // ---------------- §9.3 PreferUnicode 反映 ----------------

    [Fact]
    public void Title_UsesAsciiWhenPreferUnicodeFalse()
    {
        // ASCII で並べ替えると alpha→bravo、Unicode で並べると逆 (β=beta, α=alpha) になるよう細工
        var a = MakeBeatmap("a", title: "Alpha", titleUnicode: "ZZZ");
        var b = MakeBeatmap("b", title: "Bravo", titleUnicode: "AAA");

        var comparer = MakeComparer(
            new[] { Rule(SortField.Title, SortDirection.Ascending) },
            preferUnicode: false);

        var arr = new[] { b, a };
        Array.Sort(arr, comparer);
        Assert.Same(a, arr[0]);
        Assert.Same(b, arr[1]);
    }

    [Fact]
    public void Title_UsesUnicodeWhenPreferUnicodeTrue()
    {
        var a = MakeBeatmap("a", title: "Alpha", titleUnicode: "ZZZ");
        var b = MakeBeatmap("b", title: "Bravo", titleUnicode: "AAA");

        var comparer = MakeComparer(
            new[] { Rule(SortField.Title, SortDirection.Ascending) },
            preferUnicode: true);

        var arr = new[] { a, b };
        Array.Sort(arr, comparer);
        Assert.Same(b, arr[0]); // AAA
        Assert.Same(a, arr[1]); // ZZZ
    }

    [Fact]
    public void Title_FallsBackToAsciiWhenPreferUnicodeTrueAndTitleUnicodeEmpty()
    {
        var a = MakeBeatmap("a", title: "Alpha", titleUnicode: "");
        var b = MakeBeatmap("b", title: "Bravo", titleUnicode: "");

        var comparer = MakeComparer(
            new[] { Rule(SortField.Title, SortDirection.Ascending) },
            preferUnicode: true);

        var arr = new[] { b, a };
        Array.Sort(arr, comparer);
        Assert.Same(a, arr[0]);
        Assert.Same(b, arr[1]);
    }

    [Fact]
    public void Artist_FallsBackToAsciiWhenArtistUnicodeEmpty()
    {
        var a = MakeBeatmap("a", artist: "Alpha", artistUnicode: "");
        var b = MakeBeatmap("b", artist: "Bravo", artistUnicode: "");

        var comparer = MakeComparer(
            new[] { Rule(SortField.Artist, SortDirection.Ascending) },
            preferUnicode: true);

        var arr = new[] { b, a };
        Array.Sort(arr, comparer);
        Assert.Same(a, arr[0]);
        Assert.Same(b, arr[1]);
    }

    // ---------------- §9.4 複合ソート (ThenBy) ----------------

    [Fact]
    public void Compare_UsesSecondRuleWhenFirstRuleTies()
    {
        // 第1: KeyCount (タイ), 第2: Difficulty
        var a = MakeBeatmap("a", keyCount: 7, difficulty: 4.0);
        var b = MakeBeatmap("b", keyCount: 7, difficulty: 5.0);
        var c = MakeBeatmap("c", keyCount: 4, difficulty: 9.9);

        var comparer = MakeComparer(new[]
        {
            Rule(SortField.KeyCount, SortDirection.Ascending),
            Rule(SortField.Difficulty, SortDirection.Ascending),
        });

        var arr = new[] { b, a, c };
        Array.Sort(arr, comparer);
        Assert.Same(c, arr[0]); // KeyCount=4
        Assert.Same(a, arr[1]); // KeyCount=7, Diff 4.0
        Assert.Same(b, arr[2]); // KeyCount=7, Diff 5.0
    }

    [Fact]
    public void Compare_UsesThirdRuleWhenFirstAndSecondTie()
    {
        var a = MakeBeatmap("a", keyCount: 7, difficulty: 5.0, bpm: 100);
        var b = MakeBeatmap("b", keyCount: 7, difficulty: 5.0, bpm: 200);
        var c = MakeBeatmap("c", keyCount: 7, difficulty: 5.0, bpm: 150);

        var comparer = MakeComparer(new[]
        {
            Rule(SortField.KeyCount, SortDirection.Ascending),
            Rule(SortField.Difficulty, SortDirection.Ascending),
            Rule(SortField.BPM, SortDirection.Ascending),
        });

        var arr = new[] { b, c, a };
        Array.Sort(arr, comparer);
        Assert.Same(a, arr[0]);
        Assert.Same(c, arr[1]);
        Assert.Same(b, arr[2]);
    }

    [Fact]
    public void Compare_ReturnsZeroWhenAllRulesTie()
    {
        var a = MakeBeatmap("a", keyCount: 7, difficulty: 5.0);
        var b = MakeBeatmap("b", keyCount: 7, difficulty: 5.0);

        var comparer = MakeComparer(new[]
        {
            Rule(SortField.KeyCount, SortDirection.Ascending),
            Rule(SortField.Difficulty, SortDirection.Descending),
        });

        Assert.Equal(0, comparer.Compare(a, b));
        Assert.Equal(0, comparer.Compare(b, a));
    }

    // ---------------- §9.5 単一フィールド昇順/降順 ----------------

    [Fact]
    public void KeyCount_Ascending_OrdersAscending()
    {
        var k7 = MakeBeatmap("a", keyCount: 7);
        var k4 = MakeBeatmap("b", keyCount: 4);
        var k10 = MakeBeatmap("c", keyCount: 10);

        var comparer = MakeComparer(new[] { Rule(SortField.KeyCount, SortDirection.Ascending) });
        var arr = new[] { k7, k4, k10 };
        Array.Sort(arr, comparer);

        Assert.Same(k4, arr[0]);
        Assert.Same(k7, arr[1]);
        Assert.Same(k10, arr[2]);
    }

    [Fact]
    public void Difficulty_Descending_OrdersHighToLow()
    {
        var d3 = MakeBeatmap("a", difficulty: 3.0);
        var d5 = MakeBeatmap("b", difficulty: 5.0);
        var d8 = MakeBeatmap("c", difficulty: 8.0);

        var comparer = MakeComparer(new[] { Rule(SortField.Difficulty, SortDirection.Descending) });
        var arr = new[] { d3, d8, d5 };
        Array.Sort(arr, comparer);

        Assert.Same(d8, arr[0]);
        Assert.Same(d5, arr[1]);
        Assert.Same(d3, arr[2]);
    }

    [Fact]
    public void Status_Ascending_UsesEnumUnderlyingOrder()
    {
        // None=0, Ranked=1, Loved=2, Approved=3, Qualified=4, Pending=5
        var pending = MakeBeatmap("a", status: BeatmapStatus.Pending);
        var ranked = MakeBeatmap("b", status: BeatmapStatus.Ranked);
        var loved = MakeBeatmap("c", status: BeatmapStatus.Loved);
        var none = MakeBeatmap("d", status: BeatmapStatus.None);

        var comparer = MakeComparer(new[] { Rule(SortField.Status, SortDirection.Ascending) });
        var arr = new[] { pending, ranked, loved, none };
        Array.Sort(arr, comparer);

        Assert.Same(none, arr[0]);
        Assert.Same(ranked, arr[1]);
        Assert.Same(loved, arr[2]);
        Assert.Same(pending, arr[3]);
    }

    // ---------------- Beatmap 自体の null 末尾固定 ----------------

    [Fact]
    public void Compare_NullX_PutsLast()
    {
        var map = MakeBeatmap("a");
        var comparer = MakeComparer(new[] { Rule(SortField.KeyCount, SortDirection.Ascending) });

        Assert.True(comparer.Compare(null, map) > 0);
    }

    [Fact]
    public void Compare_NullY_PutsFirst()
    {
        var map = MakeBeatmap("a");
        var comparer = MakeComparer(new[] { Rule(SortField.KeyCount, SortDirection.Ascending) });

        Assert.True(comparer.Compare(map, null) < 0);
    }

    [Fact]
    public void Compare_BothNull_ReturnsZero()
    {
        var comparer = MakeComparer(new[] { Rule(SortField.KeyCount, SortDirection.Ascending) });

        Assert.Equal(0, comparer.Compare(null, null));
    }

    // ---------------- Artist PreferUnicode 正側 ----------------

    [Fact]
    public void Artist_UsesAsciiWhenPreferUnicodeFalse()
    {
        var a = MakeBeatmap("a", artist: "Alpha", artistUnicode: "ZZZ");
        var b = MakeBeatmap("b", artist: "Bravo", artistUnicode: "AAA");

        var comparer = MakeComparer(
            new[] { Rule(SortField.Artist, SortDirection.Ascending) },
            preferUnicode: false);

        var arr = new[] { b, a };
        Array.Sort(arr, comparer);
        Assert.Same(a, arr[0]);
        Assert.Same(b, arr[1]);
    }

    [Fact]
    public void Artist_UsesUnicodeWhenPreferUnicodeTrueAndArtistUnicodeNonEmpty()
    {
        var a = MakeBeatmap("a", artist: "Alpha", artistUnicode: "ZZZ");
        var b = MakeBeatmap("b", artist: "Bravo", artistUnicode: "AAA");

        var comparer = MakeComparer(
            new[] { Rule(SortField.Artist, SortDirection.Ascending) },
            preferUnicode: true);

        var arr = new[] { a, b };
        Array.Sort(arr, comparer);
        Assert.Same(b, arr[0]); // AAA
        Assert.Same(a, arr[1]); // ZZZ
    }
}
