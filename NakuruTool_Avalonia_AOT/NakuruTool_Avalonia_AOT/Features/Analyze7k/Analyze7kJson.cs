using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

// analyze_7k.dll の入出力 DTO（native/nakuru_7k_analyze/src/analyze.rs）。

public sealed class Analyze7kRequest
{
    [JsonPropertyName("od")] public double Od { get; set; }
}

public sealed class Analyze7kResponse
{
    [JsonPropertyName("prism")] public PrismChartResult? Prism { get; set; }
    [JsonPropertyName("dan")] public DanVerdict? Dan { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
}

/// <summary>
/// Prism の譜面値（等速・精度 96.5%）。genres / tendency のキーは Prism の値名
/// （Jumpstream / Handstream / Speed / Technical / Chordjack）。
/// </summary>
public sealed class PrismChartResult
{
    [JsonPropertyName("overall")] public double Overall { get; set; }
    /// <summary>LN 基準Overall。LN と判定されない譜面は 0。</summary>
    [JsonPropertyName("ln")] public double Ln { get; set; }
    [JsonPropertyName("genres")] public Dictionary<string, double>? Genres { get; set; }
    /// <summary>ジャンル傾向（全ジャンル共通の物差しで 0〜100）</summary>
    [JsonPropertyName("tendency")] public Dictionary<string, double>? Tendency { get; set; }
}

/// <summary>
/// 譜面段位エンジンの判定（huuugeosu の chart_dan と同じ形の一部）。
/// </summary>
public sealed class DanVerdict
{
    [JsonPropertyName("regular")] public DanEstimate? Regular { get; set; }
    [JsonPropertyName("ln")] public DanEstimate? Ln { get; set; }
    [JsonPropertyName("primary_family")] public string? PrimaryFamily { get; set; }
    [JsonPropertyName("eligible")] public bool Eligible { get; set; }
    /// <summary>huuugeosu は欠落時に true 扱い（vibro とみなす）</summary>
    [JsonPropertyName("vibro")] public bool Vibro { get; set; } = true;
    [JsonPropertyName("bucketFeatures")] public BucketFeatures? BucketFeatures { get; set; }
}

public sealed class DanEstimate
{
    [JsonPropertyName("raw_dan")] public double RawDan { get; set; }
}

public sealed class PatternInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("score")] public double Score { get; set; }
}

/// <summary>段位ジャンルの判定材料（bucketFeatures.version = 1）</summary>
public sealed class BucketFeatures
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("lnRatio")] public double? LnRatio { get; set; }
    [JsonPropertyName("patterns")] public List<PatternInfo>? Patterns { get; set; }
    [JsonPropertyName("clusters")] public List<ClusterInfo>? Clusters { get; set; }
    [JsonPropertyName("clusterCategory")] public string? ClusterCategory { get; set; }
    [JsonPropertyName("riceShape")] public RiceShape? RiceShape { get; set; }
}

public sealed class ClusterInfo
{
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("pattern")] public string Pattern { get; set; } = string.Empty;
    [JsonPropertyName("importance")] public double Importance { get; set; }
    [JsonPropertyName("mixed")] public bool? Mixed { get; set; }
}

public sealed class RiceShape
{
    [JsonPropertyName("twoBack")] public double? TwoBack { get; set; }
    [JsonPropertyName("techSections")] public double? TechSections { get; set; }
    [JsonPropertyName("motifRepeat")] public double? MotifRepeat { get; set; }
    [JsonPropertyName("speedPeak")] public double? SpeedPeak { get; set; }
    [JsonPropertyName("rollSections")] public double? RollSections { get; set; }
}

/// <summary>
/// キャッシュファイル（JSON Lines）の1行。reason があれば解析できなかった記録、なければ解析結果。
/// </summary>
public sealed class Analyze7kCacheRecord
{
    [JsonPropertyName("md5")] public string Md5 { get; set; } = string.Empty;
    /// <summary>Prism の Reg 基準Overall</summary>
    [JsonPropertyName("p")] public double? PrismOverall { get; set; }
    /// <summary>Prism の LN 基準Overall（なければ省略）</summary>
    [JsonPropertyName("pl")] public double? PrismLnOverall { get; set; }
    /// <summary>5ジャンルの基準スキル値（PrismGenre の順）</summary>
    [JsonPropertyName("gs")] public float[]? GenreSkills { get; set; }
    /// <summary>5ジャンルのジャンル傾向（PrismGenre の順）。ジャンル濃度はここから計算する。</summary>
    [JsonPropertyName("gc")] public float[]? GenreTendencies { get; set; }
    /// <summary>Regular 段位</summary>
    [JsonPropertyName("rd")] public double? RegularDan { get; set; }
    /// <summary>LN 段位</summary>
    [JsonPropertyName("ld")] public double? LnDan { get; set; }
    /// <summary>主分類（regular / ln）</summary>
    [JsonPropertyName("pf")] public string? PrimaryFamily { get; set; }
    [JsonPropertyName("el")] public bool? Eligible { get; set; }
    [JsonPropertyName("vb")] public bool? Vibro { get; set; }
    /// <summary>Regular の段位ジャンル（RegularDanGenre のビット）</summary>
    [JsonPropertyName("rg")] public int? RegularGenres { get; set; }
    /// <summary>LN の段位ジャンル（LnDanGenre のビット）</summary>
    [JsonPropertyName("lg")] public int? LnGenres { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Analyze7kRequest))]
[JsonSerializable(typeof(Analyze7kResponse))]
[JsonSerializable(typeof(Analyze7kCacheRecord))]
public partial class Analyze7kJsonContext : JsonSerializerContext
{
}
