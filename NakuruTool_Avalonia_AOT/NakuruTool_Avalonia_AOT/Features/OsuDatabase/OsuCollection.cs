using System;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase
{
    /// <summary>
    /// osu!のコレクション情報を表すモデルクラス
    /// </summary>
    public class OsuCollection
    {
        /// <summary>
        /// コレクション名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// このコレクションに含まれるBeatmapのMD5ハッシュ配列
        /// </summary>
        public string[] BeatmapMd5s { get; set; } = Array.Empty<string>();
    }
}