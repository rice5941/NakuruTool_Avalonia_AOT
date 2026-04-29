using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// レート一括生成の結果から、既存コレクションインポート互換 JSON
/// (<c>CollectionExchangeData</c>) を 1 ファイル出力する writer。
/// </summary>
public interface IRateGenerationCollectionJsonWriter
{
    Task<RateGenerationCollectionJsonWriteResult> WriteBatchAsync(
        string sourceCollectionName,
        RateGenerationOptions options,
        IReadOnlyList<RateGenerationResult> results,
        CancellationToken cancellationToken = default);
}

/// <summary>writer の実行結果。</summary>
public sealed record RateGenerationCollectionJsonWriteResult
{
    /// <summary>JSON ファイルが書き出されたか。</summary>
    public required bool FileWritten { get; init; }

    /// <summary>出力された JSON ファイルのフルパス。書き出されなかった場合は <c>null</c>。</summary>
    public string? OutputFilePath { get; init; }

    /// <summary>JSON 内 <c>Name</c> として書き出されたコレクション名。</summary>
    public string? OutputCollectionName { get; init; }

    /// <summary>JSON に書き出された beatmap 件数。</summary>
    public int WrittenBeatmapCount { get; init; }

    /// <summary>JSON 化対象から除外された件数 (JsonItem 欠落・dedupe など)。</summary>
    public int SkippedBeatmapCount { get; init; }

    /// <summary>UI 等に表示するための簡易警告メッセージ (i18n は呼び出し側責務)。</summary>
    public string? WarningMessage { get; init; }
}
