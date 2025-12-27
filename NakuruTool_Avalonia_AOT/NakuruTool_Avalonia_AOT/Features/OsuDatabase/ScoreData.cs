using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// スコアデータ
/// </summary>
public record ScoreData
{
    public byte Ruleset { get; init; }
    public int OsuVersion { get; init; }
    public string BeatmapMD5Hash { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;
    public string ReplayMD5Hash { get; init; } = string.Empty;
    public ushort Count300 { get; init; }
    public ushort Count100 { get; init; }
    public ushort Count50 { get; init; }
    public ushort CountGeki { get; init; }
    public ushort CountKatu { get; init; }
    public ushort CountMiss { get; init; }
    public int ReplayScore { get; init; }
    public ushort Combo { get; init; }
    public bool PerfectCombo { get; init; }
    public int Mods { get; init; }
    public DateTime ScoreTimestamp { get; init; }
    public long ScoreId { get; init; }
}

/// <summary>
/// scores.dbファイル全体のデータ
/// </summary>
public class ScoresDatabase
{
    public int OsuVersion { get; set; }
    public Dictionary<string, List<ScoreData>> Scores { get; set; } = new();
}