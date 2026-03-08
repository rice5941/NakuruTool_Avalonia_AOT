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

        // v1 (Default) Mod別スコア・精度・グレード
        public int BestScoreNoMod { get; init; }
        public double BestAccuracyNoMod { get; init; }
        public string GradeNoMod { get; init; } = string.Empty;
        public int BestScoreHT { get; init; }
        public double BestAccuracyHT { get; init; }
        public string GradeHT { get; init; } = string.Empty;
        public int BestScoreDT { get; init; }
        public double BestAccuracyDT { get; init; }
        public string GradeDT { get; init; } = string.Empty;

        // ScoreV2 Mod別スコア・精度・グレード
        public int BestScoreV2NoMod { get; init; }
        public double BestAccuracyV2NoMod { get; init; }
        public string GradeV2NoMod { get; init; } = string.Empty;
        public int BestScoreV2HT { get; init; }
        public double BestAccuracyV2HT { get; init; }
        public string GradeV2HT { get; init; } = string.Empty;
        public int BestScoreV2DT { get; init; }
        public double BestAccuracyV2DT { get; init; }
        public string GradeV2DT { get; init; } = string.Empty;

        /// <summary>スコアシステム・mod 2軸で最高スコアを取得</summary>
        public int GetBestScore(ScoreSystemCategory system, ModCategory mod) => (system, mod) switch
        {
            (ScoreSystemCategory.ScoreV2, ModCategory.HalfTime)   => BestScoreV2HT,
            (ScoreSystemCategory.ScoreV2, ModCategory.DoubleTime)  => BestScoreV2DT,
            (ScoreSystemCategory.ScoreV2, _)                       => BestScoreV2NoMod,
            (_, ModCategory.HalfTime)                              => BestScoreHT,
            (_, ModCategory.DoubleTime)                            => BestScoreDT,
            _                                                      => BestScoreNoMod
        };

        /// <summary>スコアシステム・mod 2軸で最高精度を取得</summary>
        public double GetBestAccuracy(ScoreSystemCategory system, ModCategory mod) => (system, mod) switch
        {
            (ScoreSystemCategory.ScoreV2, ModCategory.HalfTime)   => BestAccuracyV2HT,
            (ScoreSystemCategory.ScoreV2, ModCategory.DoubleTime)  => BestAccuracyV2DT,
            (ScoreSystemCategory.ScoreV2, _)                       => BestAccuracyV2NoMod,
            (_, ModCategory.HalfTime)                              => BestAccuracyHT,
            (_, ModCategory.DoubleTime)                            => BestAccuracyDT,
            _                                                      => BestAccuracyNoMod
        };

        /// <summary>スコアシステム・mod 2軸でグレードを取得</summary>
        public string GetGrade(ScoreSystemCategory system, ModCategory mod) => (system, mod) switch
        {
            (ScoreSystemCategory.ScoreV2, ModCategory.HalfTime)   => GradeV2HT,
            (ScoreSystemCategory.ScoreV2, ModCategory.DoubleTime)  => GradeV2DT,
            (ScoreSystemCategory.ScoreV2, _)                       => GradeV2NoMod,
            (_, ModCategory.HalfTime)                              => GradeHT,
            (_, ModCategory.DoubleTime)                            => GradeDT,
            _                                                      => GradeNoMod
        };

        /// <summary>指定modの最高スコアを取得（後方互換: Default システム）</summary>
        public int GetBestScore(ModCategory mod) => GetBestScore(ScoreSystemCategory.Default, mod);

        /// <summary>指定modの最高精度を取得（後方互換: Default システム）</summary>
        public double GetBestAccuracy(ModCategory mod) => GetBestAccuracy(ScoreSystemCategory.Default, mod);

        /// <summary>指定modのグレードを取得（後方互換: Default システム）</summary>
        public string GetGrade(ModCategory mod) => GetGrade(ScoreSystemCategory.Default, mod);
    }
}