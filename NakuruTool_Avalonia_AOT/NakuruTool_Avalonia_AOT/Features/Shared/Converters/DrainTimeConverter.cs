using System;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Converters;

/// <summary>
/// mm:ss 形式 ⇔ 秒数の変換ユーティリティ
/// </summary>
public static class DrainTimeConverter
{
    /// <summary>
    /// mm:ss 形式または秒数の文字列を秒数に変換する。
    /// "2:30" → 150, "90" → 90
    /// </summary>
    public static bool TryParseToSeconds(string input, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();

        // mm:ss 形式
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
        {
            var minutePart = trimmed.AsSpan(0, colonIndex);
            var secondPart = trimmed.AsSpan(colonIndex + 1);

            if (!int.TryParse(minutePart, out var minutes) || minutes < 0)
                return false;
            if (!int.TryParse(secondPart, out var secs) || secs < 0 || secs >= 60)
                return false;

            seconds = minutes * 60 + secs;
            return true;
        }

        // 秒数のみ（フォールバック）
        if (int.TryParse(trimmed, out var rawSeconds) && rawSeconds >= 0)
        {
            seconds = rawSeconds;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 秒数を mm:ss 形式に変換する。
    /// 150 → "2:30"
    /// </summary>
    public static string FormatToMinuteSecond(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return $"{minutes}:{secs:D2}";
    }
}
