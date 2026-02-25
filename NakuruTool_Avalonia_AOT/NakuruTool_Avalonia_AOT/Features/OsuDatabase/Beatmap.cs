using System;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase
{
    /// <summary>
    /// Beatmapのランクステータス
    /// </summary>
    public enum BeatmapStatus
    {
        None,
        Ranked,
        Loved,
        Approved,
        Qualified,
        Pending
    }

    /// <summary>
    /// Beatmap情報を表すイミュータブルなレコード型
    /// </summary>
    public sealed record Beatmap
    {
        public required string MD5Hash { get; init; }
        public int KeyCount { get; init; }
        public BeatmapStatus Status { get; init; }
        public required string Title { get; init; }
        public string TitleUnicode { get; init; } = string.Empty;
        public required string Artist { get; init; }
        public string ArtistUnicode { get; init; } = string.Empty;
        public required string Version { get; init; }
        public required string Creator { get; init; }
        public double BPM { get; init; }
        public double Difficulty { get; init; }
        public double LongNoteRate { get; init; }
        public bool IsPlayed { get; init; }
        public DateTime? LastPlayed { get; init; }
        public DateTime? LastModifiedTime { get; init; }
        public required string FolderName { get; init; }
        public required string AudioFilename { get; init; }
        public int BeatmapSetId { get; init; }
        public int BeatmapId { get; init; }
        public int BestScore { get; init; }
        public double BestAccuracy { get; init; }
        public int PlayCount { get; init; }
        public required string Grade { get; init; }
    }
}