using NakuruTool_Avalonia_AOT.Features.OsuDatabase;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>単一 beatmap のレート変更生成結果</summary>
public sealed record RateGenerationResult
{
    /// <summary>生成が成功したか</summary>
    public required bool Success { get; init; }

    /// <summary>生成された .osu ファイルのフルパス（失敗時は null）</summary>
    public string? GeneratedOsuPath { get; init; }

    /// <summary>生成されたオーディオファイルのフルパス（失敗時・スキップ時は null）</summary>
    public string? GeneratedAudioPath { get; init; }

    /// <summary>オーディオ生成がスキップされたか（同一レートのファイルが既存）</summary>
    public bool AudioSkipped { get; init; }

    /// <summary>.osu生成がスキップされたか（同名ファイルが既存）</summary>
    public bool OsuSkipped { get; init; }

    /// <summary>適用されたレート倍率</summary>
    public double AppliedRate { get; init; }

    /// <summary>エラーメッセージ（成功時は null）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>元 beatmap の情報（ログ用）</summary>
    public required Beatmap SourceBeatmap { get; init; }
}
