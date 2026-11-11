namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// scores.db の mania スコア1件分（ローカルランキング・ベスト値集計用）。
/// 全スコアを常駐させるため文字列（プレイヤー名・MD5）は一切持たない 32 バイトの値型。
/// </summary>
/// <param name="TimestampTicks">プレイ日時 (UTC ticks)</param>
/// <param name="ReplayScore">スコア</param>
/// <param name="Mods">Mod ビットフラグ（ModCategory / ScoreSystemCategory の判定にのみ使用）</param>
/// <param name="CountGeki">320 (レインボー300)</param>
/// <param name="Count300">300</param>
/// <param name="CountKatu">200</param>
/// <param name="Count100">100</param>
/// <param name="Count50">50</param>
/// <param name="CountMiss">0 (Miss)</param>
public readonly record struct LocalScore(
    long TimestampTicks,
    int ReplayScore,
    int Mods,
    ushort CountGeki = 0,
    ushort Count300 = 0,
    ushort CountKatu = 0,
    ushort Count100 = 0,
    ushort Count50 = 0,
    ushort CountMiss = 0);
