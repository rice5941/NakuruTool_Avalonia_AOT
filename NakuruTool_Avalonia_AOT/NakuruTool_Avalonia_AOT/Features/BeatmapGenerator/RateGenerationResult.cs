using System.Collections.Generic;
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

    /// <summary>参照欠落でスキップしたファイルの名前一覧（ログ用）</summary>
    public IReadOnlyList<string> SkippedFiles { get; init; } = [];

    /// <summary>元 beatmap の情報（ログ用）</summary>
    public required Beatmap SourceBeatmap { get; init; }

    /// <summary>
    /// tempDir 上に生成された <c>.osu</c> の <c>.osz</c> 内エントリ名 (相対パス、'/' 区切り)。
    /// 失敗時や <c>.osu</c> 変換失敗時は <c>null</c>。
    /// </summary>
    public string? GeneratedOsuEntryName { get; init; }

    /// <summary>
    /// tempDir 上の生成 <c>.osu</c> から構築した JSON 1 アイテム。
    /// メタ抽出に失敗した場合や生成結果が失敗の場合は <c>null</c>。
    /// </summary>
    public RateGenerationJsonItem? JsonItem { get; init; }

    /// <summary>
    /// 生成 <c>.osu</c> が最終 <c>.osz</c> に実際に収録されたか。
    /// 既存 <c>.osz</c> 更新時に同名 entry が既に存在しスキップされた場合は <c>false</c>。
    /// </summary>
    public bool IncludedInOsz { get; init; }
}
