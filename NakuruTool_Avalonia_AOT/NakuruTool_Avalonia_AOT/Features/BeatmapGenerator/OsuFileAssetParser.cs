using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public interface IOsuFileAssetParser
{
    /// <summary>
    /// .osu ファイルおよび関連 .osb ファイルを解析し、参照されているアセット一覧を返す。
    /// </summary>
    /// <param name="osuFilePath">.osu ファイルの絶対パス</param>
    /// <returns>参照アセットの分類済みデータ</returns>
    OsuReferencedAssets Parse(string osuFilePath);
}

public sealed class OsuFileAssetParser : IOsuFileAssetParser
{
    private enum OsuSection
    {
        None,
        General,
        Events,
        HitObjects,
        Variables,
        Other,
    }

    private static readonly HashSet<string> s_audioExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".wav", ".ogg", ".mp3" };

    public OsuReferencedAssets Parse(string osuFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(osuFilePath);

        if (!File.Exists(osuFilePath))
            throw new FileNotFoundException(".osu ファイルが見つかりません。", osuFilePath);

        var beatmapFolder = Path.GetDirectoryName(Path.GetFullPath(osuFilePath))!;
        var sampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string mainAudioFilename = string.Empty;

        // .osu ファイルの解析
        ParseOsuFile(osuFilePath, beatmapFolder, ref mainAudioFilename, sampleAudioFiles, nonAudioFiles);

        // 同一フォルダの .osb ファイルを解析
        ParseOsbFiles(beatmapFolder, sampleAudioFiles, nonAudioFiles);

        return new OsuReferencedAssets
        {
            MainAudioFilename = mainAudioFilename,
            SampleAudioFiles = sampleAudioFiles,
            NonAudioFiles = nonAudioFiles,
        };
    }

    private static void ParseOsuFile(
        string osuFilePath,
        string beatmapFolder,
        ref string mainAudioFilename,
        HashSet<string> sampleAudioFiles,
        HashSet<string> nonAudioFiles)
    {
        using var reader = new StreamReader(osuFilePath, Encoding.UTF8);
        var currentSection = OsuSection.None;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var section = DetectSection(line);
            if (section.HasValue)
            {
                currentSection = section.Value;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            switch (currentSection)
            {
                case OsuSection.General:
                    if (line.StartsWith("AudioFilename:", StringComparison.Ordinal))
                    {
                        var value = line["AudioFilename:".Length..].Trim();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            var normalized = NormalizeAssetRelativePath(StripQuotes(value));
                            if (!IsPathSafe(normalized, beatmapFolder))
                                throw new FileNotFoundException($"メインオーディオファイルのパスが不正です: {value}");
                            mainAudioFilename = normalized;
                        }
                    }
                    break;

                case OsuSection.Events:
                    ParseEventsLine(line, beatmapFolder, sampleAudioFiles, nonAudioFiles, variables: null);
                    break;

                case OsuSection.HitObjects:
                    ParseHitObjectLine(line, beatmapFolder, sampleAudioFiles);
                    break;
            }
        }
    }

    private static void ParseOsbFiles(
        string beatmapFolder,
        HashSet<string> sampleAudioFiles,
        HashSet<string> nonAudioFiles)
    {
        var osbFiles = Directory.GetFiles(beatmapFolder, "*.osb");
        Array.Sort(osbFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var osbFile in osbFiles)
        {
            // .osb ファイル自体を NonAudioFiles に追加
            var osbRelative = NormalizeAssetRelativePath(Path.GetFileName(osbFile));
            nonAudioFiles.Add(osbRelative);

            ParseOsbFile(osbFile, beatmapFolder, sampleAudioFiles, nonAudioFiles);
        }
    }

    private static void ParseOsbFile(
        string osbFilePath,
        string beatmapFolder,
        HashSet<string> sampleAudioFiles,
        HashSet<string> nonAudioFiles)
    {
        var lines = File.ReadAllLines(osbFilePath, Encoding.UTF8);
        var variables = new Dictionary<string, string>();

        // 1パス目: [Variables] セクションの変数を収集
        var currentSection = OsuSection.None;
        foreach (var line in lines)
        {
            var section = DetectSection(line);
            if (section.HasValue)
            {
                currentSection = section.Value;
                continue;
            }

            if (currentSection != OsuSection.Variables)
                continue;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith('$'))
            {
                var eqIndex = line.IndexOf('=');
                if (eqIndex > 0)
                {
                    var name = line[..eqIndex];
                    var value = line[(eqIndex + 1)..];
                    variables[name] = value;
                }
            }
        }

        // 2パス目: [Events] セクションを変数展開してパース
        currentSection = OsuSection.None;
        foreach (var line in lines)
        {
            var section = DetectSection(line);
            if (section.HasValue)
            {
                currentSection = section.Value;
                continue;
            }

            if (currentSection != OsuSection.Events)
                continue;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var expandedLine = ExpandVariables(line, variables);
            if (expandedLine is null)
                continue; // 未定義変数を含む行 → スキップ
            ParseEventsLine(expandedLine, beatmapFolder, sampleAudioFiles, nonAudioFiles, variables);
        }
    }

    private static void ParseEventsLine(
        string line,
        string beatmapFolder,
        HashSet<string> sampleAudioFiles,
        HashSet<string> nonAudioFiles,
        Dictionary<string, string>? variables)
    {
        // Storyboard コマンド行（スペースまたは _ で始まる行）はスキップ
        if (line.Length > 0 && (line[0] == ' ' || line[0] == '_'))
            return;

        // コメント行スキップ
        if (line.StartsWith("//", StringComparison.Ordinal))
            return;

        var parts = line.Split(',');
        if (parts.Length < 2)
            return;

        var eventType = parts[0].Trim();

        // 背景画像: 0,0,"filename",x,y
        if (eventType == "0" && parts.Length >= 3)
        {
            var filename = StripQuotes(parts[2].Trim());
            TryAddNonAudioFile(filename, beatmapFolder, nonAudioFiles);
            return;
        }

        // 動画: Video,time,"filename" or 1,time,"filename"
        if ((eventType.Equals("Video", StringComparison.OrdinalIgnoreCase) || eventType == "1") && parts.Length >= 3)
        {
            var filename = StripQuotes(parts[2].Trim());
            TryAddNonAudioFile(filename, beatmapFolder, nonAudioFiles);
            return;
        }

        // Sample: Sample,time,layer,"filename",volume
        if (eventType.Equals("Sample", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
        {
            var filename = StripQuotes(parts[3].Trim());
            TryAddSampleAudioFile(filename, beatmapFolder, sampleAudioFiles);
            return;
        }

        // Sprite: Sprite,layer,origin,"filename",x,y
        if (eventType.Equals("Sprite", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
        {
            var filename = StripQuotes(parts[3].Trim());
            TryAddNonAudioFile(filename, beatmapFolder, nonAudioFiles);
            return;
        }

        // Animation: Animation,layer,origin,"filename",x,y,frameCount,frameDelay,...
        if (eventType.Equals("Animation", StringComparison.OrdinalIgnoreCase) && parts.Length >= 7)
        {
            var filename = StripQuotes(parts[3].Trim());
            if (int.TryParse(parts[6].Trim(), out var frameCount) && frameCount > 0)
            {
                ExpandAnimationFrames(filename, frameCount, beatmapFolder, nonAudioFiles);
            }
            return;
        }
    }

    private static void ParseHitObjectLine(
        string line,
        string beatmapFolder,
        HashSet<string> sampleAudioFiles)
    {
        // osu!mania HitObject: x,y,time,type,hitSound,hitSample (or endTime:hitSample for LN)
        var parts = line.Split(',');
        if (parts.Length < 6)
            return;

        if (!int.TryParse(parts[3].Trim(), out var type))
            return;

        string hitSampleStr;

        if ((type & 128) != 0)
        {
            // LN: parts[5] = "endTime:hitSample"
            var colonIndex = parts[5].IndexOf(':');
            if (colonIndex < 0)
                return;
            hitSampleStr = parts[5][(colonIndex + 1)..];
        }
        else
        {
            // 通常ノート: parts[5] = hitSample
            hitSampleStr = parts[5];
        }

        // hitSample: normalSet:additionSet:index:volume:filename
        var hitSampleParts = hitSampleStr.Split(':');
        if (hitSampleParts.Length < 5)
            return;

        var filename = StripQuotes(hitSampleParts[4].Trim());
        TryAddSampleAudioFile(filename, beatmapFolder, sampleAudioFiles);
    }

    private static void ExpandAnimationFrames(
        string filename,
        int frameCount,
        string beatmapFolder,
        HashSet<string> nonAudioFiles)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return;

        var normalized = NormalizeAssetRelativePath(filename);
        var ext = Path.GetExtension(normalized);
        var pathWithoutExt = normalized[..^ext.Length];

        for (var i = 0; i < frameCount; i++)
        {
            var framePath = $"{pathWithoutExt}{i}{ext}";
            TryAddNonAudioFile(framePath, beatmapFolder, nonAudioFiles, alreadyNormalized: true);
        }
    }

    private static void TryAddSampleAudioFile(
        string filename,
        string beatmapFolder,
        HashSet<string> sampleAudioFiles)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return;

        var normalized = NormalizeAssetRelativePath(filename);

        if (!IsAudioExtension(normalized))
            return;

        if (!IsPathSafe(normalized, beatmapFolder))
            return;

        sampleAudioFiles.Add(normalized);
    }

    private static void TryAddNonAudioFile(
        string filename,
        string beatmapFolder,
        HashSet<string> nonAudioFiles,
        bool alreadyNormalized = false)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return;

        var normalized = alreadyNormalized ? filename : NormalizeAssetRelativePath(filename);

        if (!IsPathSafe(normalized, beatmapFolder))
            return;

        nonAudioFiles.Add(normalized);
    }

    private static bool IsPathSafe(string relativePath, string beatmapFolder)
    {
        // 1. ".." を含むパスを reject
        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            Debug.WriteLine($"パストラバーサル検出（..）: {relativePath}");
            return false;
        }

        // 2. 絶対パスを reject
        if (Path.IsPathRooted(relativePath))
        {
            Debug.WriteLine($"絶対パスを検出: {relativePath}");
            return false;
        }

        // 3. 正規化して beatmapFolder 配下であることを確認
        var fileSystemPath = ToFileSystemRelativePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(beatmapFolder, fileSystemPath));
        if (!fullPath.StartsWith(beatmapFolder, StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"beatmapFolder 外へのパス: {relativePath} → {fullPath}");
            return false;
        }

        return true;
    }

    private static bool IsAudioExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return s_audioExtensions.Contains(ext);
    }

    private static string? ExpandVariables(string line, Dictionary<string, string> variables)
    {
        if (!line.Contains('$'))
            return line;

        var result = line;
        // 変数名の長さ降順でソート（最長一致）
        var sortedVariables = variables.OrderByDescending(kvp => kvp.Key.Length);

        var index = 0;
        while (index < result.Length)
        {
            var dollarIndex = result.IndexOf('$', index);
            if (dollarIndex < 0)
                break;

            var matched = false;
            foreach (var kvp in sortedVariables)
            {
                if (result.AsSpan(dollarIndex).StartsWith(kvp.Key.AsSpan(), StringComparison.Ordinal))
                {
                    result = string.Concat(result.AsSpan(0, dollarIndex), kvp.Value, result.AsSpan(dollarIndex + kvp.Key.Length));
                    index = dollarIndex + kvp.Value.Length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                Debug.WriteLine($"未定義変数を検出: {line}");
                return null;
            }
        }

        return result;
    }

    private static OsuSection? DetectSection(string line)
    {
        if (!line.StartsWith('[') || !line.EndsWith(']'))
            return null;

        return line switch
        {
            "[General]" => OsuSection.General,
            "[Events]" => OsuSection.Events,
            "[HitObjects]" => OsuSection.HitObjects,
            "[Variables]" => OsuSection.Variables,
            _ => OsuSection.Other,
        };
    }

    private static string StripQuotes(string value)
        => value.Trim('"');

    private static string NormalizeAssetRelativePath(string path)
        => path.Replace('\\', '/').Trim();

    private static string ToFileSystemRelativePath(string canonicalPath)
        => canonicalPath.Replace('/', Path.DirectorySeparatorChar);
}
