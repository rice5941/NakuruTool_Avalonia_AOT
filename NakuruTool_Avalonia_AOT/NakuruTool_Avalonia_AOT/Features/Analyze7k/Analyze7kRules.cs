using System;
using System.Globalization;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>.osu の事前確認（huuugeosu analyze_7k とほぼ同じ判定）</summary>
public static class Analyze7kRules
{
    /// <summary>.osu の最大サイズ（huuugeosu と同じ 16MiB）</summary>
    public const int MaxOsuFileBytes = 16 * 1024 * 1024;

    /// <summary>
    /// osu!.db のフォルダ名の末尾に空白・ピリオドがあるときの、もう一つの .osu のパス（なければ null）。
    /// Windows はフォルダを作るときに名前の末尾の空白・ピリオドを取り除くため、osu!.db の名前のままでは見つからないことがある。
    /// </summary>
    public static string? AlternateChartPath(string songsDirectory, string folderName, string osuFileName)
    {
        var trimmed = folderName.TrimEnd(' ', '.');
        return trimmed.Length > 0 && trimmed.Length != folderName.Length
            ? System.IO.Path.Combine(songsDirectory, trimmed, osuFileName)
            : null;
    }

    /// <summary>
    /// .osu の内容が native 7K mania として評価できるかを確認し、OD を返す（huuugeosu analyze_7k と同じ判定。先頭の空行を許す点だけ違う）。
    /// </summary>
    public static bool TryReadChartHeader(ReadOnlySpan<byte> osu, out double od, out string? reason)
    {
        od = 5.0;
        if (osu.Length > MaxOsuFileBytes)
        {
            reason = Analyze7kFailure.InvalidFile;
            return false;
        }

        var body = osu.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? osu[3..] : osu;
        // 先頭は "osu file format v"。osu! 本体と同じく、その前の空行・空白は読み飛ばす
        // （huuugeosu は BOM だけを読み飛ばす。解析エンジンは先頭に空行があっても読める）
        if (!osu.TrimStart((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]).TrimStart(" \t\r\n"u8).StartsWith("osu file format v"u8))
        {
            reason = Analyze7kFailure.InvalidFile;
            return false;
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(body);
        }
        catch (DecoderFallbackException)
        {
            reason = Analyze7kFailure.InvalidFile;
            return false;
        }

        string section = string.Empty;
        string? mode = null;
        string? circleSize = null;
        string? overall = null;
        foreach (var rawLine in text.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.StartsWith("["))
            {
                section = line.ToString();
                if (section == "[HitObjects]")
                    break;
                continue;
            }

            if (section is not ("[General]" or "[Difficulty]"))
                continue;

            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().ToString();
            if (key.SequenceEqual("Mode")) mode = value;
            else if (key.SequenceEqual("CircleSize")) circleSize = value;
            else if (key.SequenceEqual("OverallDifficulty")) overall = value;
        }

        if (mode != "3" ||
            !double.TryParse(circleSize ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out var cs) ||
            cs != 7)
        {
            reason = Analyze7kFailure.NotSevenKey;
            return false;
        }

        if (overall is not null &&
            (!double.TryParse(overall, NumberStyles.Float, CultureInfo.InvariantCulture, out od) ||
             !double.IsFinite(od) || od < 0 || od > 10))
        {
            reason = Analyze7kFailure.InvalidFile;
            return false;
        }

        reason = null;
        return true;
    }
}

/// <summary>解析できなかった理由（キャッシュに記録する）</summary>
public static class Analyze7kFailure
{
    public const string FileMissing = "file_missing";
    public const string Md5Mismatch = "md5_mismatch";
    public const string InvalidFile = "invalid_file";
    public const string NotSevenKey = "not_7k";
    public const string EngineError = "engine_error";

    /// <summary>解析を始めるたびに再試行する理由（ファイルの補完・修正で解消し得るもの）</summary>
    public static bool IsRetryable(string reason)
        => reason is FileMissing or Md5Mismatch or EngineError;
}
