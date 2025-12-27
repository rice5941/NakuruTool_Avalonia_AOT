using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// collection.dbパーサー（アンマネージドメモリ使用）
/// </summary>
public sealed class CollectionDbParser : IDisposable
{
    /// <summary>
    /// collection.dbファイルを読み込む
    /// </summary>
    public List<OsuCollection> ReadCollectionDb(string filePath, Action<string, int>? progressCallback = null)
    {
        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.CollectionLoading"), 0);

        var fileInfo = new FileInfo(filePath);
        int fileSize = (int)fileInfo.Length;

        using var buffer = new UnmanagedBuffer(fileSize);

        // ファイル全体を一括読み込み
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.None))
        {
            int totalRead = 0;
            while (totalRead < fileSize)
            {
                int bytesRead = buffer.ReadFromStream(fileStream, totalRead, fileSize - totalRead);
                if (bytesRead == 0)
                    throw new InvalidDataException("Unexpected end of file during read");
                totalRead += bytesRead;
            }
        }

        var bufferSpan = buffer.GetBufferSpan();
        int pos = 0;

        // ヘッダー読み込み
        int version = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.CollectionVersion"), version), 10);

        int collectionCount = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.CollectionCount"), collectionCount), 20);

        var collections = new List<OsuCollection>(collectionCount);

        // 各コレクションを読み込み
        for (int i = 0; i < collectionCount; i++)
        {
            string name = ReadStringFromSpan(bufferSpan, ref pos);
            int beatmapCount = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
            pos += 4;

            var md5Array = new string[beatmapCount];
            int validCount = 0;

            for (int j = 0; j < beatmapCount; j++)
            {
                var md5Hash = ReadStringFromSpan(bufferSpan, ref pos);
                if (!string.IsNullOrEmpty(md5Hash))
                {
                    md5Array[validCount++] = md5Hash;
                }
            }

            if (validCount < beatmapCount)
            {
                Array.Resize(ref md5Array, validCount);
            }

            var collection = new OsuCollection
            {
                Name = name,
                BeatmapMd5s = md5Array
            };

            collections.Add(collection);

            var progress = 20 + (int)((double)(i + 1) / collectionCount * 30);
            var message = string.Format(LanguageService.Instance.GetString("Loading.CollectionItemCompleted"), collection.Name, validCount);
            progressCallback?.Invoke(message, progress);
        }

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.CollectionCompleted"), 100);

        return collections;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadStringFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
        => BinaryReaderHelper.ReadStringFromSpan(buffer, ref pos);

    public void Dispose()
    {
        // 現在はリソース解放不要（UnmanagedBufferはusingで管理）
    }
}
