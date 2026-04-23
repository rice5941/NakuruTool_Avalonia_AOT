using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// ffmpeg 引数組立用の純関数群。
/// 数値→文字列化は必ず <see cref="CultureInfo.InvariantCulture"/>。
/// </summary>
internal static class FfmpegArgumentsBuilder
{
    /// <summary>OGG 品質既定値（libvorbis -q:a）。</summary>
    internal const int DefaultOggQuality = 8;

    /// <summary>MP3 VBR 品質既定値（libmp3lame -q:a）。</summary>
    internal const int DefaultMp3VbrQuality = 4;

    /// <summary>
    /// rate を [0.5, 2.0] の範囲に収まる最小個数の等分チェーンへ分解する。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">rate &lt;= 0</exception>
    internal static IReadOnlyList<double> SplitAtempo(double rate)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "rate must be positive.");
        }

        if (rate >= 0.5 && rate <= 2.0)
        {
            return new ReadOnlyCollection<double>(new[] { rate });
        }

        double logRate = Math.Abs(Math.Log2(rate));
        int n = Math.Max(2, (int)Math.Ceiling(logRate));
        double step = Math.Pow(rate, 1.0 / n);
        var result = new double[n];
        for (int i = 0; i < n; i++)
        {
            result[i] = step;
        }
        return new ReadOnlyCollection<double>(result);
    }

    /// <summary>DT 用 filter:a 文字列（atempo チェーン、各要素は小数 6 桁）。</summary>
    internal static string BuildDtFilter(double rate)
    {
        var parts = SplitAtempo(rate);
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("atempo=");
            sb.Append(parts[i].ToString("F6", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>NC 用 filter:a 文字列（"asetrate=&lt;sr*rate&gt;,aresample=&lt;sr&gt;"）。</summary>
    internal static string BuildNcFilter(int originalSampleRate, double rate)
    {
        long newRate = (long)Math.Round(originalSampleRate * rate, MidpointRounding.AwayFromZero);
        return string.Concat(
            "asetrate=", newRate.ToString(CultureInfo.InvariantCulture),
            ",aresample=", originalSampleRate.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 出力拡張子と品質から -c:a 以降のエンコーダ引数列を生成。
    /// </summary>
    /// <exception cref="NotSupportedException">未対応拡張子</exception>
    internal static IReadOnlyList<string> BuildEncoderArgs(
        string outputExtensionLower,
        int mp3VbrQuality,
        int oggQuality)
    {
        return outputExtensionLower switch
        {
            ".wav" => new ReadOnlyCollection<string>(new[] { "-c:a", "pcm_s16le", "-f", "wav" }),
            ".mp3" => new ReadOnlyCollection<string>(new[]
            {
                "-c:a", "libmp3lame",
                "-q:a", mp3VbrQuality.ToString(CultureInfo.InvariantCulture),
            }),
            ".ogg" => new ReadOnlyCollection<string>(new[]
            {
                "-c:a", "libvorbis",
                "-q:a", oggQuality.ToString(CultureInfo.InvariantCulture),
            }),
            _ => throw new NotSupportedException(
                $"Unsupported output extension: {outputExtensionLower}"),
        };
    }

    /// <summary>完成した ffmpeg 引数列（ArgumentList にそのまま詰める順序）。</summary>
    internal static IReadOnlyList<string> BuildFfmpegArgs(
        string inputPath,
        string outputPath,
        string filterString,
        string outputExtensionLower,
        int mp3VbrQuality,
        int oggQuality)
    {
        var encoderArgs = BuildEncoderArgs(outputExtensionLower, mp3VbrQuality, oggQuality);
        var list = new List<string>(16 + encoderArgs.Count)
        {
            "-hide_banner",
            "-nostdin",
            "-y",
            "-loglevel", "error",
            "-i", inputPath,
            "-vn", "-sn", "-dn",
            "-map_metadata", "-1",
            "-filter:a", filterString,
        };
        for (int i = 0; i < encoderArgs.Count; i++)
        {
            list.Add(encoderArgs[i]);
        }
        list.Add(outputPath);
        return list.AsReadOnly();
    }
}
