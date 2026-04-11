namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// beatmapレート変更生成のオプション。
/// Rate または TargetBpm のいずれか一方を指定する。
/// </summary>
public sealed record RateGenerationOptions
{
    /// <summary>レート倍率（例: 1.1）。TargetBpm 指定時は無視される</summary>
    public double? Rate { get; init; }

    /// <summary>目標BPM。指定時は元 Beatmap.BPM からレート倍率を自動計算</summary>
    public double? TargetBpm { get; init; }

    /// <summary>HP 値の上書き。null の場合は元値を維持</summary>
    public double? HpOverride { get; init; }

    /// <summary>OD 値の上書き。null の場合は元値を維持</summary>
    public double? OdOverride { get; init; }

    /// <summary>
    /// ピッチを変更するか。
    /// true = NC方式（ピッチ+速度を同時変更）
    /// false = DT方式（速度のみ変更、ピッチ維持）— デフォルト
    /// </summary>
    public bool ChangePitch { get; init; }

    /// <summary>
    /// MP3出力時のVBR品質（0=最高品質, 9=最低品質）。
    /// デフォルト 4 MP3以外の出力時は無視される。
    /// </summary>
    public int Mp3VbrQuality { get; init; } = 4;
}
