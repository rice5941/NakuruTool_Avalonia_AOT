namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>.osu ファイルレート変換のパラメータ</summary>
public sealed record OsuFileConvertOptions
{
    /// <summary>レート倍率（decimal で精度確保）</summary>
    public required decimal Rate { get; init; }

    /// <summary>変換後の AudioFilename（例: "audio_1.10x.wav"）</summary>
    public required string NewAudioFilename { get; init; }

    /// <summary>新しい難易度名（Version）。null の場合はサービス側で自動生成</summary>
    public string? NewDifficultyName { get; init; }

    /// <summary>HP の上書き値。null の場合は元の値を維持</summary>
    public double? HpOverride { get; init; }

    /// <summary>OD の上書き値。null の場合は元の値を維持</summary>
    public double? OdOverride { get; init; }

    /// <summary>
    /// DT/NC 判別用。現時点では .osu 変換ロジックに影響しないが、
    /// 将来の拡張ポイントとして保持。
    /// </summary>
    public bool ChangePitch { get; init; }
}
