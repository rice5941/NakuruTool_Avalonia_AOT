using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>
/// 7K 段位の表示名。huuugeosu app/usecases/mania_dan.py（Mania Tracker player/dan-credit.ts）の
/// ラベル部分だけを移植したもの。
/// </summary>
public static partial class DanLabels
{
    private static readonly string[] Levels =
        ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "gamma", "azimuth", "zenith", "stellium"];

    /// <summary>段位の内部ラベル（例: "9 Mid", "Gamma High"）</summary>
    public static string DanLabel(double raw)
    {
        int level = LevelIndex(raw);
        double offset = Math.Floor((raw - level) * 1e6 + 0.5) / 1e6;
        string tier = offset < -0.3 ? "Low"
            : offset < -0.1 ? "Mid/Low"
            : offset < 0.1 ? "Mid"
            : offset < 0.3 ? "Mid/High"
            : "High";
        return $"{TitleCase(Levels[level])} {tier}";
    }

    private static int LevelIndex(double raw) => (int)Math.Min(14, Math.Max(0, Math.Floor(raw + 0.5)));

    [GeneratedRegex(@"^(\d+|gamma|azimuth|zenith|stellium)( (?:Low|Mid/Low|Mid|Mid/High|High))?$", RegexOptions.IgnoreCase)]
    private static partial Regex LabelPattern();

    /// <summary>表示用の段位名（例: "9th Dan Mid", "Gamma Dan High"）</summary>
    public static string DisplayDanLabel(string label)
    {
        var match = LabelPattern().Match(label);
        if (!match.Success)
            return label;

        var level = match.Groups[1].Value;
        var suffix = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
        if (int.TryParse(level, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            var ordinal = number % 100 is >= 10 and <= 20
                ? "th"
                : (number % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return $"{number}{ordinal} Dan{suffix}";
        }
        return $"{TitleCase(level)} Dan{suffix}";
    }

    /// <summary>raw 値からそのまま表示名を作る</summary>
    public static string DisplayLabelOf(double raw) => DisplayDanLabel(DanLabel(raw));

    /// <summary>Python の str.title() 相当（先頭のみ大文字、残りは小文字）</summary>
    private static string TitleCase(string value)
    {
        if (string.IsNullOrEmpty(value) || !char.IsLetter(value[0]))
            return value;
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
