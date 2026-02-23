using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// collection.db への書き込みを担当する静的ユーティリティクラス。
/// GenerateCollectionService から抽出。
/// </summary>
public static class CollectionDbWriter
{
    // osu! collection.db フォーマットバージョン
    private const int CollectionDbVersion = 20210528;

    /// <summary>
    /// collection.db を書き込む
    /// </summary>
    public static async Task WriteAsync(List<OsuCollection> collections, string collectionDbPath)
    {
        using var fileStream = new FileStream(collectionDbPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fileStream, Encoding.UTF8);

        // バージョン情報を書き込み
        writer.Write(CollectionDbVersion);

        // コレクション数を書き込み
        writer.Write(collections.Count);

        // 各コレクションを書き込み
        foreach (var collection in collections)
        {
            // コレクション名を書き込み
            WriteOsuString(writer, collection.Name);

            // ビートマップ数を書き込み
            writer.Write(collection.BeatmapMd5s.Length);

            // MD5ハッシュリストを書き込み
            foreach (var md5Hash in collection.BeatmapMd5s)
            {
                WriteOsuString(writer, md5Hash);
            }
        }

        await fileStream.FlushAsync();
    }

    /// <summary>
    /// osu!形式の文字列を書き込み
    /// </summary>
    private static void WriteOsuString(BinaryWriter writer, string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            writer.Write((byte)0x00);
        }
        else
        {
            writer.Write((byte)0x0b);
            var bytes = Encoding.UTF8.GetBytes(str);
            WriteULEB128(writer, bytes.Length);
            writer.Write(bytes);
        }
    }

    /// <summary>
    /// ULEB128形式で整数を書き込み
    /// </summary>
    private static void WriteULEB128(BinaryWriter writer, int value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
                b |= 0x80;
            writer.Write(b);
        } while (value != 0);
    }
}
