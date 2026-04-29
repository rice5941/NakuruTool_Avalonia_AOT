using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.Shared;

/// <summary>
/// .osu ファイルの <c>[Metadata]</c> / <c>[Difficulty]</c> セクションから必要最小限の値を取り出す
/// AOT 安全な静的ヘルパー。リフレクション・動的コード生成は使用しない。
/// </summary>
internal static class OsuFileMetadataReader
{
    private const string MetadataSectionHeader = "[Metadata]";
    private const string DifficultySectionHeader = "[Difficulty]";
    private const string BeatmapSetIdKey = "BeatmapSetID";
    private const string TitleKey = "Title";
    private const string ArtistKey = "Artist";
    private const string VersionKey = "Version";
    private const string CreatorKey = "Creator";
    private const string CircleSizeKey = "CircleSize";

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

    /// <summary>
    /// 指定された .osu ファイルから <c>[Metadata]</c>/<c>[Difficulty]</c> の必要項目を読み出す。
    /// 必須項目 (Title/Artist/Version/Creator) が揃わない場合は <c>false</c> を返す。
    /// I/O 例外などはすべて握りつぶし <c>false</c> を返す best effort 動作。
    /// </summary>
    public static bool TryReadBasicMetadata(string osuFilePath, out OsuFileBasicMetadata metadata)
    {
        metadata = default;
        if (string.IsNullOrEmpty(osuFilePath))
            return false;

        try
        {
            using var stream = new FileStream(
                osuFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return TryReadBasicMetadata(stream, out metadata);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return false;
    }

    /// <summary>
    /// 指定された <see cref="Stream"/> から .osu の必要項目を読み出す。
    /// 呼び出し元 stream は閉じない (<c>leaveOpen: true</c>)。
    /// </summary>
    public static bool TryReadBasicMetadata(Stream stream, out OsuFileBasicMetadata metadata)
    {
        metadata = default;
        if (stream is null)
            return false;

        try
        {
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);

            string? title = null;
            string? artist = null;
            string? version = null;
            string? creator = null;
            var circleSize = 0.0;
            var beatmapSetId = -1;

            // 0=未到達, 1=[Metadata]内, 2=[Difficulty]内, 3=その他
            var section = 0;
            var metadataSeen = false;
            var difficultySeen = false;

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var span = line.AsSpan().Trim();
                if (span.IsEmpty)
                    continue;

                if (span[0] == '[' && span[^1] == ']')
                {
                    if (span.SequenceEqual(MetadataSectionHeader.AsSpan()))
                    {
                        section = 1;
                        metadataSeen = true;
                    }
                    else if (span.SequenceEqual(DifficultySectionHeader.AsSpan()))
                    {
                        section = 2;
                        difficultySeen = true;
                    }
                    else
                    {
                        // 必要な 2 セクションを通過済みなら早期終了
                        if (metadataSeen && difficultySeen)
                            break;
                        section = 3;
                    }
                    continue;
                }

                if (section != 1 && section != 2)
                    continue;

                var colonIndex = span.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                var key = span[..colonIndex].Trim();
                var value = span[(colonIndex + 1)..].Trim();

                if (section == 1)
                {
                    if (key.SequenceEqual(TitleKey.AsSpan()))
                        title ??= value.ToString();
                    else if (key.SequenceEqual(ArtistKey.AsSpan()))
                        artist ??= value.ToString();
                    else if (key.SequenceEqual(VersionKey.AsSpan()))
                        version ??= value.ToString();
                    else if (key.SequenceEqual(CreatorKey.AsSpan()))
                        creator ??= value.ToString();
                    else if (key.SequenceEqual(BeatmapSetIdKey.AsSpan()))
                    {
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                            beatmapSetId = parsed;
                    }
                }
                else
                {
                    if (key.SequenceEqual(CircleSizeKey.AsSpan()))
                    {
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                            circleSize = parsed;
                    }
                }
            }

            if (title is null || artist is null || version is null || creator is null)
                return false;

            metadata = new OsuFileBasicMetadata(title, artist, version, creator, circleSize, beatmapSetId);
            return true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (FormatException)
        {
        }
        catch (DecoderFallbackException)
        {
        }

        return false;
    }
}
