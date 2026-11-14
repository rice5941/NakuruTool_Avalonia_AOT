using System;
using System.Globalization;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>段位の側（Regular / LN）</summary>
public enum DanSide
{
    Regular,
    Ln,
}

/// <summary>Prism の5ジャンル（表示順）。表示名は huuugeosu と同じ。</summary>
public enum PrismGenre
{
    /// <summary>Prism の Jumpstream（Stream / JumpstreamOnly の大きい方）</summary>
    Chordstream,
    /// <summary>Prism の Handstream</summary>
    Bracket,
    /// <summary>Prism の Speed</summary>
    Delay,
    Technical,
    /// <summary>Prism の Chordjack（JackSpeed / ChordjackOnly の大きい方）</summary>
    Jack,
}

/// <summary>段位推定の Regular ジャンル（有効なものの組み合わせ）</summary>
[Flags]
public enum RegularDanGenre
{
    None = 0,
    Jack = 1,
    Tech = 2,
    Speed = 4,
    Stream = 8,
}

/// <summary>段位推定の LN ジャンル（有効なものの組み合わせ）</summary>
[Flags]
public enum LnDanGenre
{
    None = 0,
    General = 1,
    Tech = 2,
    Inverse = 4,
    Release = 8,
}

/// <summary>Prism の5ジャンルの値（<see cref="PrismGenre"/> の順）</summary>
public readonly record struct GenreValues(float Chordstream, float Bracket, float Delay, float Technical, float Jack)
{
    public static readonly PrismGenre[] Genres = Enum.GetValues<PrismGenre>();

    public float this[PrismGenre genre] => genre switch
    {
        PrismGenre.Chordstream => Chordstream,
        PrismGenre.Bracket => Bracket,
        PrismGenre.Delay => Delay,
        PrismGenre.Technical => Technical,
        PrismGenre.Jack => Jack,
        _ => 0,
    };

    public float[] ToArray() => [Chordstream, Bracket, Delay, Technical, Jack];

    public static GenreValues FromArray(ReadOnlySpan<float> values)
        => values.Length < 5 ? default : new(values[0], values[1], values[2], values[3], values[4]);

    /// <summary>5ジャンルの合計（0 以下の値は数えない）</summary>
    public float Sum()
    {
        float sum = 0;
        foreach (var genre in Genres)
            sum += Math.Max(0, this[genre]);
        return sum;
    }

    /// <summary>各ジャンルが合計に占める割合（%、合計 100）。合計が 0 ならすべて 0。</summary>
    public GenreValues ToShares()
    {
        var sum = Sum();
        if (!(sum > 0))
            return default;
        float Share(float value) => Math.Max(0, value) * 100f / sum;
        return new(Share(Chordstream), Share(Bracket), Share(Delay), Share(Technical), Share(Jack));
    }

    /// <summary>
    /// <see cref="ToShares"/> を整数に丸める（最大剰余方式。表示の合計がちょうど 100 になる）。
    /// 端数の大きい順に 1 ずつ足し、同じ端数なら表示順で先のジャンルに足す。合計が 0 ならすべて 0。
    /// </summary>
    public int[] ToRoundedShares()
    {
        var result = new int[Genres.Length];
        var sum = Sum();
        if (!(sum > 0))
            return result;

        Span<double> remainders = stackalloc double[Genres.Length];
        int total = 0;
        for (int i = 0; i < Genres.Length; i++)
        {
            double exact = Math.Max(0, this[Genres[i]]) * 100.0 / sum;
            result[i] = (int)Math.Floor(exact);
            remainders[i] = exact - result[i];
            total += result[i];
        }
        for (; total < 100; total++)
        {
            int best = 0;
            for (int i = 1; i < remainders.Length; i++)
            {
                if (remainders[i] > remainders[best])
                    best = i;
            }
            result[best]++;
            remainders[best] = -1;
        }
        return result;
    }

    /// <summary>値が最大のジャンル（すべて 0 なら null。同じ値なら表示順で先のもの）</summary>
    public PrismGenre? Top()
    {
        PrismGenre? best = null;
        float bestValue = 0;
        foreach (var genre in Genres)
        {
            if (this[genre] > bestValue)
            {
                bestValue = this[genre];
                best = genre;
            }
        }
        return best;
    }
}

/// <summary>
/// 7K 譜面1件の解析結果（等速）。仕様: docs/ANALYZE_7K_SPEC.md
/// </summary>
public sealed record ManiaChartAnalysis
{
    /// <summary>Prism の Reg 基準Overall（精度 96.5% を基準にした譜面の難易度）</summary>
    public required double PrismOverall { get; init; }

    /// <summary>Prism の LN 基準Overall。LN と判定されない譜面は null（測定不能）。</summary>
    public double? PrismLnOverall { get; init; }

    /// <summary>5ジャンルの基準スキル値（huuugeosu と同じ値。LN の多い譜面の補正を含む）</summary>
    public GenreValues GenreSkills { get; init; }

    /// <summary>
    /// 5ジャンルのジャンル傾向（0〜100、全ジャンル共通の物差し。0 は傾向なし）。
    /// 1譜面の中での大小は <see cref="GenreSkills"/> と同じ。
    /// </summary>
    public GenreValues GenreTendencies { get; init; }

    /// <summary>5ジャンルのジャンル濃度（ジャンル傾向の内訳、%。合計 100）</summary>
    public GenreValues GenreConcentrations => GenreTendencies.ToShares();

    /// <summary>段位推定の Regular 段位（raw）</summary>
    public double? RegularDan { get; init; }

    /// <summary>段位推定の LN 段位（raw）。値がなければ測定不能。</summary>
    public double? LnDan { get; init; }

    /// <summary>段位エンジンの主分類</summary>
    public DanSide? PrimaryFamily { get; init; }

    public RegularDanGenre RegularGenres { get; init; }

    public LnDanGenre LnGenres { get; init; }

    /// <summary>段位エンジンの対象判定（同じ列に大量のノーツが重なる譜面などは false）</summary>
    public bool Eligible { get; init; } = true;

    /// <summary>縦連打（vibro）と判定された譜面</summary>
    public bool Vibro { get; init; }

    /// <summary>基準スキル値の6本目（LN 基準Overall）の軸名</summary>
    public const string LnAxis = "LN";

    /// <summary>基準スキル値の軸名（5ジャンル＋LN。huuugeosu の6軸と同じ並び）</summary>
    public static readonly string[] SkillAxes = [.. Enum.GetNames<PrismGenre>(), LnAxis];

    /// <summary>基準スキル値が最大のジャンル（5ジャンルの中で。ジャンル濃度が最大のジャンルとも一致する）</summary>
    public PrismGenre? TopGenre => GenreSkills.Top();

    /// <summary>
    /// 基準スキル値（5ジャンルと LN）が最大の軸名。LN は5ジャンルの最大を超えたときだけ選ぶ
    /// （同じ値なら表示順で先のジャンル）。値がすべて 0 なら null。
    /// </summary>
    public string? TopSkillAxis
    {
        get
        {
            var top = TopGenre;
            double best = top is { } genre ? GenreSkills[genre] : 0;
            return PrismLnOverall is { } ln && ln > best ? LnAxis : top?.ToString();
        }
    }

    /// <summary>軸名（ジャンル名か <see cref="LnAxis"/>）の基準スキル値。LN が測定不能・軸名が不明なら null。</summary>
    public double? SkillValueOf(string axis)
        => axis == LnAxis ? PrismLnOverall : TryParseGenre(axis, out var genre) ? GenreSkills[genre] : null;

    /// <summary>ジャンル名のジャンル濃度（%）。ジャンル名が不明なら null。</summary>
    public double? ConcentrationOf(string genre)
        => TryParseGenre(genre, out var g) ? GenreConcentrations[g] : null;

    private static bool TryParseGenre(string name, out PrismGenre genre)
        => Enum.TryParse(name, ignoreCase: false, out genre) && Enum.IsDefined(genre) && !int.TryParse(name, out _);

    public bool IsRegularPrimary => PrimaryFamily == DanSide.Regular;

    public bool IsLnPrimary => PrimaryFamily == DanSide.Ln;

    public bool HasLnOverall => PrismLnOverall is not null;

    public bool HasLnDan => LnDan is not null;

    /// <summary>縦連打・対象外の印を出すか</summary>
    public bool HasWarning => Vibro || !Eligible;

    public string PrismOverallText => PrismOverall.ToString("F2", CultureInfo.InvariantCulture);

    public string PrismLnOverallText => PrismLnOverall is { } v ? v.ToString("F2", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>例: "9th Dan Mid (9.42)"</summary>
    public string RegularDanText => FormatDan(RegularDan);

    public string LnDanText => FormatDan(LnDan);

    public static string FormatDan(double? raw)
        => raw is { } value
            ? $"{DanLabels.DisplayLabelOf(value)} ({value.ToString("F2", CultureInfo.InvariantCulture)})"
            : string.Empty;

    public bool HasRegularGenre(RegularDanGenre genre) => (RegularGenres & genre) != 0;

    public bool HasLnGenre(LnDanGenre genre) => (LnGenres & genre) != 0;

    // 段位推定列のジャンルラベル（有効なものを強調する）用
    public bool IsRegularJack => HasRegularGenre(RegularDanGenre.Jack);
    public bool IsRegularTech => HasRegularGenre(RegularDanGenre.Tech);
    public bool IsRegularSpeed => HasRegularGenre(RegularDanGenre.Speed);
    public bool IsRegularStream => HasRegularGenre(RegularDanGenre.Stream);
    public bool IsLnGeneral => HasLnGenre(LnDanGenre.General);
    public bool IsLnTech => HasLnGenre(LnDanGenre.Tech);
    public bool IsLnInverse => HasLnGenre(LnDanGenre.Inverse);
    public bool IsLnRelease => HasLnGenre(LnDanGenre.Release);
}
