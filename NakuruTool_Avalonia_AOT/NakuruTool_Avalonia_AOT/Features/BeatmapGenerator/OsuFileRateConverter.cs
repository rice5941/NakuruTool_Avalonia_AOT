using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// .osu ファイルのレート変換サービス。
/// 入力 .osu ファイルを読み込み、指定レートで時間軸を変換した新しい .osu ファイルを出力する。
/// </summary>
public interface IOsuFileRateConverter
{
    /// <summary>
    /// .osu ファイルをレート変換し、出力先パスに書き出す。
    /// </summary>
    /// <param name="sourceOsuPath">変換元 .osu ファイルの絶対パス</param>
    /// <param name="destinationOsuPath">変換先 .osu ファイルの絶対パス</param>
    /// <param name="options">変換パラメータ</param>
    void Convert(string sourceOsuPath, string destinationOsuPath, OsuFileConvertOptions options);
}

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

        using var reader = new StreamReader(sourceOsuPath, Encoding.UTF8);
        var outputDir = Path.GetDirectoryName(destinationOsuPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var tempPath = destinationOsuPath + ".tmp";
        try
        {
            var currentSection = OsuSection.None;
            var tagsFound = false;
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var section = DetectSection(line);
                    if (section.HasValue)
                    {
                        // Metadata セクション終了時に Tags 行が無ければ補完
                        if (currentSection == OsuSection.Metadata && !tagsFound)
                        {
                            writer.WriteLine("Tags:NakuruTool");
                            tagsFound = true;
                        }

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

                    if (currentSection == OsuSection.Metadata
                        && line.StartsWith("Tags:", StringComparison.Ordinal))
                    {
                        tagsFound = true;
                    }

                    var transformed = currentSection switch
                    {
                        OsuSection.General => TransformGeneralLine(line, options),
                        OsuSection.Metadata => TransformMetadataLine(line, options),
                        OsuSection.Difficulty => TransformDifficultyLine(line, options),
                        OsuSection.Events => TransformEventLine(line, options),
                        OsuSection.TimingPoints => TransformTimingPointLine(line, options.Rate),
                        OsuSection.HitObjects => TransformHitObjectLine(line, options),
                        _ => line
                    };

                    writer.WriteLine(transformed);
                }

                // ファイル末尾が Metadata セクションのまま終了した場合
                if (currentSection == OsuSection.Metadata && !tagsFound)
                {
                    writer.WriteLine("Tags:NakuruTool");
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
            "[Metadata]" => OsuSection.Metadata,
            "[Difficulty]" => OsuSection.Difficulty,
            "[Events]" => OsuSection.Events,
            "[TimingPoints]" => OsuSection.TimingPoints,
            "[HitObjects]" => OsuSection.HitObjects,
            _ => OsuSection.Other
        };
    }

    private static string TransformGeneralLine(string line, OsuFileConvertOptions options)
    {
        if (line.StartsWith("AudioFilename:", StringComparison.Ordinal))
            return $"AudioFilename: {options.NewAudioFilename}";

        if (line.StartsWith("AudioLeadIn:", StringComparison.Ordinal))
        {
            var valueStr = line["AudioLeadIn:".Length..].Trim();
            if (int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return $"AudioLeadIn: {ScaleTime(value, options.Rate).ToString(CultureInfo.InvariantCulture)}";
        }

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

        if (line.StartsWith("Tags:", StringComparison.Ordinal))
        {
            var existingTags = line["Tags:".Length..];
            if (!existingTags.Contains("NakuruTool", StringComparison.OrdinalIgnoreCase))
                return $"Tags:{existingTags} NakuruTool";
            return line;
        }

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

    private static string TransformEventLine(string line, OsuFileConvertOptions options)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
            return line;

        // .osu 内の indented storyboard command (`_F` 等) は触らない
        if (StoryboardSyntaxHelper.IsCommandLine(line))
            return line;

        // .osu の Animation の frameDelay は触らない→ scaleAnimationFrameDelay: false
        return StoryboardLineRateTransformer.TransformEventLine(
            line,
            options.Rate,
            options.SampleFilenameMap,
            scaleAnimationFrameDelay: false);
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

    private static string TransformHitObjectLine(string line, OsuFileConvertOptions options)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        var parts = line.Split(',');
        if (parts.Length < 5)
            return line;

        // time (index 2)
        if (!int.TryParse(parts[2], CultureInfo.InvariantCulture, out var time))
            return line;

        parts[2] = ScaleTime(time, options.Rate).ToString(CultureInfo.InvariantCulture);

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
                    var newEndTime = ScaleTime(endTime, options.Rate);
                    var scaledTime = ScaleTime(time, options.Rate);
                    newEndTime = Math.Max(scaledTime, newEndTime);
                    var hitSample = extras[(colonIndex + 1)..];
                    if (options.SampleFilenameMap is { Count: > 0 } map)
                    {
                        hitSample = ReplaceSampleFilename(hitSample, map);
                    }
                    parts[5] = newEndTime.ToString(CultureInfo.InvariantCulture) + ":" + hitSample;
                }
            }
            else if (int.TryParse(extras, CultureInfo.InvariantCulture, out var endTime))
            {
                var newEndTime = ScaleTime(endTime, options.Rate);
                var scaledTime = ScaleTime(time, options.Rate);
                newEndTime = Math.Max(scaledTime, newEndTime);
                parts[5] = newEndTime.ToString(CultureInfo.InvariantCulture);
            }
        }
        // 通常ノートの hitSample 処理
        else if (parts.Length >= 6 && options.SampleFilenameMap is { Count: > 0 } normalMap)
        {
            parts[5] = ReplaceSampleFilename(parts[5], normalMap);
        }

        // スピナー判定: type & 8
        if ((type & 8) != 0 && parts.Length >= 6)
        {
            if (int.TryParse(parts[5], CultureInfo.InvariantCulture, out var endTime))
            {
                var newEndTime = ScaleTime(endTime, options.Rate);
                var scaledTime = ScaleTime(time, options.Rate);
                newEndTime = Math.Max(scaledTime, newEndTime);
                parts[5] = newEndTime.ToString(CultureInfo.InvariantCulture);
            }
        }

        return string.Join(',', parts);
    }

    private static string ReplaceSampleFilename(
        string hitSample, IReadOnlyDictionary<string, string> map)
    {
        // hitSample = "normalSet:additionSet:index:volume:filename"
        var colonParts = hitSample.Split(':');
        if (colonParts.Length >= 5 && colonParts[4].Length > 0)
        {
            var normalized = colonParts[4].Replace('\\', '/').Trim();
            if (map.TryGetValue(normalized, out var renamed))
            {
                colonParts[4] = renamed;
                return string.Join(':', colonParts);
            }
        }
        return hitSample;
    }

    /// <summary>
    /// 時間値をレート倍率で変換する。四捨五入。
    /// </summary>
    private static int ScaleTime(int value, decimal rate)
        => (int)Math.Round(value / rate, MidpointRounding.AwayFromZero);

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
