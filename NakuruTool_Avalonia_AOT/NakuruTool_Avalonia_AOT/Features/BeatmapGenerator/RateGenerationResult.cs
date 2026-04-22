using NakuruTool_Avalonia_AOT.Features.OsuDatabase;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>単一 beatmap のレート変更生成結果</summary>
public sealed record RateGenerationResult
{
    /// <summary>生成が成功したか</summary>
    public required bool Success { get; init; }

    /// <summary>生成された .osz ファイルのフルパス（失敗時は null）</summary>
    public string? GeneratedOszPath { get; init; }

    /// <summary>適用されたレート倍率</summary>
    public double AppliedRate { get; init; }

    /// <summary>エラーメッセージ（成功時は null）</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>変換したサンプル音声ファイル数（ログ・進捗表示用）</summary>
    public int ConvertedSampleCount { get; init; }

    /// <summary>参照欠落で警告をスキップしたファイル数</summary>
    public int SkippedFileCount { get; init; }

    /// <summary>元 beatmap の情報（ログ用）</summary>
    public required Beatmap SourceBeatmap { get; init; }
}
