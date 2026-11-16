using System;
using System.Globalization;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>.osu の事前確認</summary>
public static class Analyze7kRules
{
    /// <summary>.osu の最大サイズ（16MiB）</summary>
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
    /// .osu の内容が native 7K mania として評価できるかを確認し、OD を返す。
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
        // （解析エンジンは先頭に空行があっても読める）
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

        // キー数は CircleSize を偶数丸めした値（解析エンジンの Prism と同じ。osu! も CS 7.021・7.49 などを 7K として遊ぶ）
        if (mode != "3" ||
            !double.TryParse(circleSize ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out var cs) ||
            Math.Round(cs, MidpointRounding.ToEven) != 7)
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

    /// <summary>
    /// .osu の中身が osu!.db の MD5 と違い（譜面の更新後に osu! が読み直していない）、その中身も解析できなかった。
    /// 中身が違っても解析できれば、その結果を使う（osu! が遊ぶときに読むのはこのファイル）。
    /// </summary>
    public const string Md5Mismatch = "md5_mismatch";

    public const string InvalidFile = "invalid_file";

    /// <summary>
    /// osu!.db では 7K の譜面の .osu が 7K でない。
    /// 版 chart-1 までは段位推定のない空の難易度（<see cref="NoDanEstimate"/>）もこの理由だった。
    /// </summary>
    public const string NotSevenKey = "not_7k";

    /// <summary>段位エンジンが推定を返さない譜面（ほぼすべてノーツ 0〜9 個の空の難易度）</summary>
    public const string NoDanEstimate = "no_dan_estimate";

    public const string EngineError = "engine_error";

    /// <summary>
    /// 解析を始めるたびに再試行する理由（ファイルの補完・修正・osu!.db の更新、エンジンの更新で解消し得るもの）。
    /// not_7k は、以前の版が空の難易度や CircleSize が 7 ちょうどでない 7K 譜面（7.021 など）にも付けていたため、
    /// 再試行して解析し直す（空の難易度は no_dan_estimate に記録し直されて、次からは再試行しない）。
    /// </summary>
    public static bool IsRetryable(string reason)
        => reason is FileMissing or Md5Mismatch or NotSevenKey or EngineError;

    /// <summary>解析エンジンのエラーメッセージから理由を決める</summary>
    public static string FromEngineMessage(string message)
    {
        if (message.Contains("no dan estimate", StringComparison.OrdinalIgnoreCase))
            return NoDanEstimate;
        if (message.Contains("only 7K", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
            return NotSevenKey;
        return EngineError;
    }
}
