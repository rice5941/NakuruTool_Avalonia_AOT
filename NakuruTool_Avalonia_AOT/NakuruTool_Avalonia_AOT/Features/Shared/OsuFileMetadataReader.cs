using System;
using System.IO;

namespace NakuruTool_Avalonia_AOT.Features.Shared;

/// <summary>
/// .osu ファイルの <c>[Metadata]</c> セクションから必要最小限の値を取り出す
/// AOT 安全な静的ヘルパー。リフレクション・動的コード生成は使用しない。
/// </summary>
internal static class OsuFileMetadataReader
{
    private const string MetadataSectionHeader = "[Metadata]";
    private const string BeatmapSetIdKey = "BeatmapSetID";

    /// <summary>
    /// 指定された .osu ファイルから <c>BeatmapSetID</c> を読み出す。
    /// 見つからない・パース失敗・I/O 例外などはすべて <c>false</c> を返す。
    /// </summary>
    /// <remarks>
    /// パフォーマンスのため <c>[Metadata]</c> セクションだけを読み、
    /// 次のセクションヘッダ (例: <c>[Difficulty]</c>) を検出した時点で打ち切る。
    /// </remarks>
    public static bool TryReadBeatmapSetId(string osuFilePath, out int beatmapSetId)
    {
        beatmapSetId = 0;
        if (string.IsNullOrEmpty(osuFilePath))
            return false;

        try
        {
            using var stream = new FileStream(
                osuFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            var inMetadata = false;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var span = line.AsSpan().Trim();
                if (span.IsEmpty)
                    continue;

                if (span[0] == '[' && span[^1] == ']')
                {
                    if (inMetadata)
                    {
                        // [Metadata] を抜けた → これ以上読む必要なし
                        return false;
                    }
                    inMetadata = span.SequenceEqual(MetadataSectionHeader.AsSpan());
                    continue;
                }

                if (!inMetadata)
                    continue;

                var colonIndex = span.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                var key = span[..colonIndex].Trim();
                if (!key.SequenceEqual(BeatmapSetIdKey.AsSpan()))
                    continue;

                var value = span[(colonIndex + 1)..].Trim();
                return int.TryParse(value, out beatmapSetId);
            }
        }
        catch (IOException)
        {
            // best effort: ファイルが消えた・ロックされた等は false
        }
        catch (UnauthorizedAccessException)
        {
        }

        return false;
    }
}
