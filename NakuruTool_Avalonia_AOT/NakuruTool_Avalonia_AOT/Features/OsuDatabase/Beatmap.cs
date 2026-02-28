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
        /// <summary>.osuファイル名（例: "Artist - Title (Creator) [Difficulty].osu"）</summary>
        public required string OsuFileName { get; init; }
        public int BeatmapSetId { get; init; }
        public int BeatmapId { get; init; }
        public int BestScore { get; init; }
        public double BestAccuracy { get; init; }
        public int PlayCount { get; init; }
        public required string Grade { get; init; }
        /// <summary>Overall Difficulty</summary>
        public double OD { get; init; }
        /// <summary>HP Drain</summary>
        public double HP { get; init; }
        /// <summary>曲の長さ（秒単位）</summary>
        public int DrainTimeSeconds { get; init; }

        // Mod別スコア・精度・グレード
        public int BestScoreNoMod { get; init; }
        public double BestAccuracyNoMod { get; init; }
        public string GradeNoMod { get; init; } = string.Empty;
        public int BestScoreHT { get; init; }
        public double BestAccuracyHT { get; init; }
        public string GradeHT { get; init; } = string.Empty;
        public int BestScoreDT { get; init; }
        public double BestAccuracyDT { get; init; }
        public string GradeDT { get; init; } = string.Empty;

        /// <summary>指定modの最高スコアを取得</summary>
        public int GetBestScore(ModCategory mod) => mod switch
        {
            ModCategory.HalfTime => BestScoreHT,
            ModCategory.DoubleTime => BestScoreDT,
            _ => BestScoreNoMod
        };

        /// <summary>指定modの最高精度を取得</summary>
        public double GetBestAccuracy(ModCategory mod) => mod switch
        {
            ModCategory.HalfTime => BestAccuracyHT,
            ModCategory.DoubleTime => BestAccuracyDT,
            _ => BestAccuracyNoMod
        };

        /// <summary>指定modのグレードを取得</summary>
        public string GetGrade(ModCategory mod) => mod switch
        {
            ModCategory.HalfTime => GradeHT,
            ModCategory.DoubleTime => GradeDT,
            _ => GradeNoMod
        };
    }
}