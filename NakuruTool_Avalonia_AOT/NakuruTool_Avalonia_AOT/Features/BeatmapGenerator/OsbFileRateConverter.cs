using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// .osb ファイルの行ベースレート変換実装。
/// 1 パス目で <c>[Variables]</c> を収集し、2 パス目で <c>[Events]</c> 行を変数展開しつつ
/// rate に応じて時間軸変換し、UTF-8 (BOM なし) で書き出す。
/// </summary>
public sealed class OsbFileRateConverter : IOsbFileRateConverter
{
    public void Convert(string sourceOsbPath, string destinationOsbPath, OsbFileConvertOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceOsbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationOsbPath);
        ArgumentNullException.ThrowIfNull(options);
        if (string.Equals(Path.GetFullPath(sourceOsbPath), Path.GetFullPath(destinationOsbPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("変換元と変換先が同じパスです。", nameof(destinationOsbPath));
        if (!File.Exists(sourceOsbPath))
            throw new FileNotFoundException(".osb ファイルが見つかりません。", sourceOsbPath);
        if (options.Rate <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.Rate, "Rate は正の値である必要があります。");

        var outputDir = Path.GetDirectoryName(destinationOsbPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // 1 パス目: [Variables] 収集
        var variables = CollectVariables(sourceOsbPath);

        // 2 パス目: 行ごとに変換しつつ書き出し
        var tempPath = destinationOsbPath + ".tmp";
        try
        {
            using (var reader = new StreamReader(sourceOsbPath, Encoding.UTF8))
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                var section = OsbSection.None;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var newSection = DetectSection(line);
                    if (newSection.HasValue)
                    {
                        section = newSection.Value;
                        writer.WriteLine(line);
                        continue;
                    }

                    if (section == OsbSection.Events && !string.IsNullOrEmpty(line) && !line.StartsWith("//", StringComparison.Ordinal))
                    {
                        // Variables 展開
                        var expanded = StoryboardSyntaxHelper.ExpandVariables(line, variables);
                        if (expanded is null)
                        {
                            // 未定義変数を含む → 警告して原文出力
                            Debug.WriteLine($"[OsbFileRateConverter] 警告: 未定義変数を含む行のためスケールせず原文出力: {line}");
                            writer.WriteLine(line);
                            continue;
                        }

                        // command 行 / event 行を判定
                        if (StoryboardSyntaxHelper.IsCommandLine(expanded))
                        {
                            writer.WriteLine(StoryboardLineRateTransformer.TransformCommandLine(expanded, options.Rate));
                        }
                        else
                        {
                            // .osb は Animation の frameDelay もスケールする (設計 Q2)
                            writer.WriteLine(StoryboardLineRateTransformer.TransformEventLine(
                                expanded, options.Rate, options.SampleFilenameMap, scaleAnimationFrameDelay: true));
                        }
                    }
                    else
                    {
                        // [Variables] / [その他] / 空行 / コメント はそのまま
                        writer.WriteLine(line);
                    }
                }
            }

            File.Move(tempPath, destinationOsbPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static Dictionary<string, string> CollectVariables(string sourceOsbPath)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StreamReader(sourceOsbPath, Encoding.UTF8);
        var section = OsbSection.None;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var newSection = DetectSection(line);
            if (newSection.HasValue)
            {
                section = newSection.Value;
                continue;
            }
            if (section != OsbSection.Variables) continue;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith('$')) continue;
            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                var name = line[..eqIndex];
                var value = line[(eqIndex + 1)..];
                variables[name] = value;
            }
        }
        return variables;
    }

    private enum OsbSection { None, Variables, Events, Other }

    private static OsbSection? DetectSection(string line)
    {
        if (line.Length < 2 || line[0] != '[' || line[^1] != ']') return null;
        return line switch
        {
            "[Variables]" => OsbSection.Variables,
            "[Events]" => OsbSection.Events,
            _ => OsbSection.Other,
        };
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
