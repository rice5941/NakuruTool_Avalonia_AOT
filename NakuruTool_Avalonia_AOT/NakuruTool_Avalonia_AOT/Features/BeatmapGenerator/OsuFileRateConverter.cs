using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// .osu ファイルの行ベースレート変換実装。
/// セクション判定しながら 1 行ずつ変換し、未知のセクション・行はそのまま通過させる。
/// </summary>
public sealed class OsuFileRateConverter : IOsuFileRateConverter
{
    private enum OsuSection
    {
        None,
        General,
        Editor,
        Metadata,
        Difficulty,
        Events,
        TimingPoints,
        HitObjects,
        Other
    }

    public void Convert(string sourceOsuPath, string destinationOsuPath, OsuFileConvertOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceOsuPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationOsuPath);
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(Path.GetFullPath(sourceOsuPath), Path.GetFullPath(destinationOsuPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("変換元と変換先が同じパスです。", nameof(destinationOsuPath));

        if (!File.Exists(sourceOsuPath))
            throw new FileNotFoundException("変換元 .osu ファイルが見つかりません。", sourceOsuPath);

        if (options.Rate <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.Rate, "Rate は正の値である必要があります。");

        var lines = File.ReadAllLines(sourceOsuPath);
        var outputDir = Path.GetDirectoryName(destinationOsuPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var tempPath = destinationOsuPath + ".tmp";
        try
        {
            var currentSection = OsuSection.None;
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                foreach (var line in lines)
                {
                    var section = DetectSection(line);
                    if (section.HasValue)
                    {
                        currentSection = section.Value;
                        writer.WriteLine(line);
                        continue;
                    }

                    if (currentSection == OsuSection.General
                        && line.StartsWith("Mode:", StringComparison.Ordinal))
                    {
                        var modeValue = line["Mode:".Length..].Trim();
                        if (!modeValue.Equals("3", StringComparison.Ordinal))
                            throw new InvalidDataException("osu!mania 以外の .osu ファイルは変換できません。");
                    }

                    var transformed = currentSection switch
                    {
                        OsuSection.General => TransformGeneralLine(line, options),
                        OsuSection.Editor => TransformEditorLine(line, options.Rate),
                        OsuSection.Metadata => TransformMetadataLine(line, options),
                        OsuSection.Difficulty => TransformDifficultyLine(line, options),
                        OsuSection.Events => TransformEventLine(line, options.Rate),
                        OsuSection.TimingPoints => TransformTimingPointLine(line, options.Rate),
                        OsuSection.HitObjects => TransformHitObjectLine(line, options.Rate),
                        _ => line
                    };

                    writer.WriteLine(transformed);
                }
            }

            File.Move(tempPath, destinationOsuPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static OsuSection? DetectSection(string line)
    {
        if (!line.StartsWith('[') || !line.EndsWith(']'))
            return null;

        return line switch
        {
            "[General]" => OsuSection.General,
            "[Editor]" => OsuSection.Editor,
            "[Metadata]" => OsuSection.Metadata,
            "[Difficulty]" => OsuSection.Difficulty,
            "[Events]" => OsuSection.Events,
            "[TimingPoints]" => OsuSection.TimingPoints,
            "[HitObjects]" => OsuSection.HitObjects,
            _ => OsuSection.Other
        };
    }

    private static string TransformEditorLine(string line, decimal rate)
    {
        if (!line.StartsWith("Bookmarks:", StringComparison.Ordinal))
            return line;

        var valueStr = line["Bookmarks:".Length..].Trim();
        if (string.IsNullOrWhiteSpace(valueStr))
            return line;

        var bookmarks = valueStr.Split(',');
        for (var i = 0; i < bookmarks.Length; i++)
        {
            if (int.TryParse(bookmarks[i].Trim(), CultureInfo.InvariantCulture, out var value))
                bookmarks[i] = ScaleTime(value, rate).ToString(CultureInfo.InvariantCulture);
        }

        return $"Bookmarks: {string.Join(',', bookmarks)}";
    }

    private static string TransformGeneralLine(string line, OsuFileConvertOptions options)
    {
        if (line.StartsWith("AudioFilename:", StringComparison.Ordinal))
            return $"AudioFilename: {options.NewAudioFilename}";

        if (line.StartsWith("PreviewTime:", StringComparison.Ordinal))
        {
            var valueStr = line["PreviewTime:".Length..].Trim();
            if (int.TryParse(valueStr, CultureInfo.InvariantCulture, out var value) && value != -1)
                return $"PreviewTime: {ScaleTime(value, options.Rate).ToString(CultureInfo.InvariantCulture)}";
        }

        return line;
    }

    private static string TransformMetadataLine(string line, OsuFileConvertOptions options)
    {
        if (line.StartsWith("Version:", StringComparison.Ordinal))
        {
            if (options.NewDifficultyName is not null)
                return $"Version:{options.NewDifficultyName}";

            var originalVersion = line["Version:".Length..];
            return $"Version:{originalVersion} x{options.Rate.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        if (line.StartsWith("BeatmapID:", StringComparison.Ordinal))
            return "BeatmapID:0";

        if (line.StartsWith("BeatmapSetID:", StringComparison.Ordinal))
            return "BeatmapSetID:-1";

        return line;
    }

    private static string TransformDifficultyLine(string line, OsuFileConvertOptions options)
    {
        if (options.HpOverride.HasValue && line.StartsWith("HPDrainRate:", StringComparison.Ordinal))
            return $"HPDrainRate:{options.HpOverride.Value.ToString(CultureInfo.InvariantCulture)}";

        if (options.OdOverride.HasValue && line.StartsWith("OverallDifficulty:", StringComparison.Ordinal))
            return $"OverallDifficulty:{options.OdOverride.Value.ToString(CultureInfo.InvariantCulture)}";

        return line;
    }

    private static string TransformEventLine(string line, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
            return line;

        var parts = line.Split(',');
        if (parts.Length < 2)
            return line;

        // ブレイクタイム: 2,startTime,endTime
        if (parts[0] == "2" && parts.Length >= 3)
        {
            if (int.TryParse(parts[1], CultureInfo.InvariantCulture, out var startTime)
                && int.TryParse(parts[2], CultureInfo.InvariantCulture, out var endTime))
            {
                parts[1] = ScaleTime(startTime, rate).ToString(CultureInfo.InvariantCulture);
                parts[2] = ScaleTime(endTime, rate).ToString(CultureInfo.InvariantCulture);
                return string.Join(',', parts);
            }
        }

        // Background: 0,startTime,"filename.ext",xOffset,yOffset
        if (parts[0] == "0" && parts.Length >= 2)
        {
            if (int.TryParse(parts[1], CultureInfo.InvariantCulture, out var bgStartTime) && bgStartTime != 0)
            {
                parts[1] = ScaleTime(bgStartTime, rate).ToString(CultureInfo.InvariantCulture);
                return string.Join(',', parts);
            }
        }

        // ビデオ: Video,startTime,... or 1,startTime,...
        if ((parts[0].Equals("Video", StringComparison.OrdinalIgnoreCase) || parts[0] == "1") && parts.Length >= 2)
        {
            if (int.TryParse(parts[1], CultureInfo.InvariantCulture, out var startTime))
            {
                parts[1] = ScaleTime(startTime, rate).ToString(CultureInfo.InvariantCulture);
                return string.Join(',', parts);
            }
        }

        return line;
    }

    private static string TransformTimingPointLine(string line, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        var parts = line.Split(',');
        if (parts.Length < 8)
            return line;

        // time (index 0)
        if (int.TryParse(parts[0], CultureInfo.InvariantCulture, out var time))
            parts[0] = ScaleTime(time, rate).ToString(CultureInfo.InvariantCulture);

        // uninherited (index 6): "1" → beatLength / rate
        if (parts[6].Trim() == "1"
            && decimal.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var beatLength))
        {
            parts[1] = (beatLength / rate).ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(',', parts);
    }

    private static string TransformHitObjectLine(string line, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        var parts = line.Split(',');
        if (parts.Length < 5)
            return line;

        // time (index 2)
        if (!int.TryParse(parts[2], CultureInfo.InvariantCulture, out var time))
            return line;

        parts[2] = ScaleTime(time, rate).ToString(CultureInfo.InvariantCulture);

        // type (index 3)
        if (!int.TryParse(parts[3], CultureInfo.InvariantCulture, out var type))
            return string.Join(',', parts);

        // LN判定: type & 128
        if ((type & 128) != 0 && parts.Length >= 6)
        {
            // extras: endTime:hitSample (endTimeは先頭部分)
            var extras = parts[5];
            var colonIndex = extras.IndexOf(':');
            if (colonIndex >= 0)
            {
                var endTimeStr = extras[..colonIndex];
                if (int.TryParse(endTimeStr, CultureInfo.InvariantCulture, out var endTime))
                {
                    var newEndTime = ScaleTime(endTime, rate);
                    var scaledTime = ScaleTime(time, rate);
                    newEndTime = Math.Max(scaledTime, newEndTime);
                    parts[5] = newEndTime.ToString(CultureInfo.InvariantCulture) + extras[colonIndex..];
                }
            }
            else if (int.TryParse(extras, CultureInfo.InvariantCulture, out var endTime))
            {
                var newEndTime = ScaleTime(endTime, rate);
                var scaledTime = ScaleTime(time, rate);
                newEndTime = Math.Max(scaledTime, newEndTime);
                parts[5] = newEndTime.ToString(CultureInfo.InvariantCulture);
            }
        }

        // スピナー判定: type & 8
        if ((type & 8) != 0 && parts.Length >= 6)
        {
            if (int.TryParse(parts[5], CultureInfo.InvariantCulture, out var endTime))
            {
                var newEndTime = ScaleTime(endTime, rate);
                var scaledTime = ScaleTime(time, rate);
                newEndTime = Math.Max(scaledTime, newEndTime);
                parts[5] = newEndTime.ToString(CultureInfo.InvariantCulture);
            }
        }

        return string.Join(',', parts);
    }

    /// <summary>
    /// 時間値をレート倍率で変換する。整数切り捨て。
    /// osu-trainer の divide 関数と同等: int(decimal(value) / rate)
    /// </summary>
    private static int ScaleTime(int value, decimal rate)
        => (int)(value / rate);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 削除失敗は無視
        }
    }
}
