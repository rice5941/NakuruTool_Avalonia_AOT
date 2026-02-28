using System;
using System.IO;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer
{
    /// <summary>
    /// .osu ファイルの Events セクションから背景画像ファイル名を抽出する静的ユーティリティ。
    /// NativeAOT 互換: リフレクション不使用、純粋な文字列操作のみ。
    /// </summary>
    public static class OsuFileParser
    {
        /// <summary>
        /// .osu ファイルを解析し、背景画像のファイル名を返す。
        /// </summary>
        /// <param name="osuFilePath">.osu ファイルの絶対パス</param>
        /// <returns>背景画像ファイル名（例: "amekumizore.png"）。見つからない場合は null。</returns>
        public static string? GetBackgroundFilename(string osuFilePath)
        {
            if (!File.Exists(osuFilePath))
                return null;

            try
            {
                bool inEventsSection = false;

                foreach (var line in File.ReadLines(osuFilePath))
                {
                    // セクションヘッダーを検出
                    if (line.StartsWith('['))
                    {
                        if (line.StartsWith("[Events]", StringComparison.OrdinalIgnoreCase))
                        {
                            inEventsSection = true;
                            continue;
                        }
                        else if (inEventsSection)
                        {
                            // [Events] セクションを抜けた → 探索終了
                            break;
                        }
                        continue;
                    }

                    if (!inEventsSection)
                        continue;

                    // コメント行をスキップ
                    if (line.StartsWith("//"))
                        continue;

                    // 背景画像行: 0,0,"<filename>",0,0
                    if (line.StartsWith("0,0,\"", StringComparison.Ordinal))
                    {
                        // 最初の '"' の位置を取得
                        int start = line.IndexOf('"');
                        if (start < 0)
                            continue;

                        int end = line.IndexOf('"', start + 1);
                        if (end < 0)
                            continue;

                        string filename = line.Substring(start + 1, end - start - 1);
                        if (filename.Length > 0)
                            return filename;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
