namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

public enum ModCategory
{
    NoMod,
    HalfTime,
    DoubleTime,
}

public static class OsuMods
{
    public const int DoubleTime = 0x40;   // 64
    public const int HalfTime  = 0x100;   // 256
    public const int Nightcore = 0x200;   // 512

    public static ModCategory Categorize(int mods)
    {
        if ((mods & HalfTime) != 0)
            return ModCategory.HalfTime;

        if ((mods & (DoubleTime | Nightcore)) != 0)
            return ModCategory.DoubleTime;

        return ModCategory.NoMod;
    }
}
