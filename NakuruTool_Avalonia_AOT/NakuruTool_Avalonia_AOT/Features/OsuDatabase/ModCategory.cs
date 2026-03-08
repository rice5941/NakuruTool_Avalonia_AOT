namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// スピードMod区分（表示・フィルタ軸）
/// </summary>
public enum ModCategory
{
    NoMod,
    HalfTime,
    DoubleTime,
}

/// <summary>
/// スコアシステム区分（表示・フィルタ軸）
/// Default = legacy v1 スコア（ScoreV2 bit なし）
/// ScoreV2 = ScoreV2 mod bit (1 << 29) ありのスコア
/// </summary>
public enum ScoreSystemCategory
{
    Default,
    ScoreV2,
}

public static class OsuMods
{
    public const int DoubleTime = 0x40;   // 64
    public const int HalfTime  = 0x100;   // 256
    public const int Nightcore = 0x200;   // 512
    public const int ScoreV2   = 1 << 29; // 536870912

    public static ModCategory Categorize(int mods)
    {
        if ((mods & HalfTime) != 0)
            return ModCategory.HalfTime;

        if ((mods & (DoubleTime | Nightcore)) != 0)
            return ModCategory.DoubleTime;

        return ModCategory.NoMod;
    }

    public static ScoreSystemCategory CategorizeScoreSystem(int mods)
        => (mods & ScoreV2) != 0 ? ScoreSystemCategory.ScoreV2 : ScoreSystemCategory.Default;
}
