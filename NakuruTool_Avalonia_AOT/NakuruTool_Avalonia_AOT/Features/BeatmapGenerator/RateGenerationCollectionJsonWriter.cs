using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// <see cref="IRateGenerationCollectionJsonWriter"/> の実装。
/// レート生成済みの <c>JsonItem</c> から <c>CollectionExchangeData</c> 互換 JSON を出力する。
/// .osz / .osu の再読み込みは行わない。
/// </summary>
public sealed class RateGenerationCollectionJsonWriter : IRateGenerationCollectionJsonWriter
{
    private static readonly string OutputFolder =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imports", "rate-generation");

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly ImportExportJsonContext s_context = new(s_jsonOptions);

    public async Task<RateGenerationCollectionJsonWriteResult> WriteBatchAsync(
        string sourceCollectionName,
        RateGenerationOptions options,
        IReadOnlyList<RateGenerationResult> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCollectionName);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(results);

        cancellationToken.ThrowIfCancellationRequested();

        var rateLabelDisplay = BuildRateLabelDisplay(options);
        var outputCollectionName = $"{sourceCollectionName} [{rateLabelDisplay}]";

        var beatmaps = new List<CollectionExchangeBeatmap>(results.Count);
        var seenMd5 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedCount = 0;

        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 失敗結果は通常スキップ件数に含めない
            if (!result.Success)
                continue;

            if (string.IsNullOrEmpty(result.GeneratedOszPath))
            {
                skippedCount++;
                continue;
            }

            // JsonItem 欠落のみ Skipped 扱い。IncludedInOsz は JSON 出力可否に影響させない
            // (既存 .osz と同名 entry が衝突しても、レート生成済み .osu の MD5/メタは
            //  通常同一なため、JSON 取込み上は問題にならない)。
            if (result.JsonItem is null)
            {
                skippedCount++;
                continue;
            }

            if (string.IsNullOrEmpty(result.JsonItem.Md5) || !seenMd5.Add(result.JsonItem.Md5))
            {
                // Md5 欠落 / 既出は dedupe としてスキップ
                skippedCount++;
                continue;
            }

            beatmaps.Add(ToExchangeBeatmap(result.JsonItem));
        }

        if (beatmaps.Count == 0)
        {
            return new RateGenerationCollectionJsonWriteResult
            {
                FileWritten = false,
                OutputFilePath = null,
                OutputCollectionName = outputCollectionName,
                WrittenBeatmapCount = 0,
                SkippedBeatmapCount = skippedCount,
                WarningMessage = BuildWarningMessage(skippedCount, fileWritten: false),
            };
        }

        Directory.CreateDirectory(OutputFolder);

        var rateLabelForFile = SanitizeForFileName(rateLabelDisplay.Replace(' ', '_'));
        var sanitizedCollectionName = SanitizeForFileName(sourceCollectionName);
        var fileName = $"{sanitizedCollectionName}_{rateLabelForFile}.json";
        var outputFilePath = Path.Combine(OutputFolder, fileName);
        var tempPath = outputFilePath + ".tmp";

        var data = new CollectionExchangeData
        {
            Name = outputCollectionName,
            Beatmaps = beatmaps,
        };

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    data,
                    s_context.CollectionExchangeData,
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, outputFilePath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }

        return new RateGenerationCollectionJsonWriteResult
        {
            FileWritten = true,
            OutputFilePath = outputFilePath,
            OutputCollectionName = outputCollectionName,
            WrittenBeatmapCount = beatmaps.Count,
            SkippedBeatmapCount = skippedCount,
            WarningMessage = BuildWarningMessage(skippedCount, fileWritten: true),
        };
    }

    private static CollectionExchangeBeatmap ToExchangeBeatmap(RateGenerationJsonItem item) => new()
    {
        Title = item.Title,
        Artist = item.Artist,
        Version = item.Version,
        Creator = item.Creator,
        Cs = item.Cs,
        BeatmapsetId = item.BeatmapsetId,
        Md5 = item.Md5,
    };

    /// <summary>
    /// JSON 内 <c>Name</c> 末尾に付与するレートラベル表示文字列を構築する。
    /// 例: <c>1.25x DT HP8 OD5</c>, <c>2x NC</c>, <c>200bpm DT</c>, <c>1.0x DT</c> (Rate/TargetBpm 共に未指定時のフォールバック)。
    /// </summary>
    internal static string BuildRateLabelDisplay(RateGenerationOptions options)
    {
        var sb = new StringBuilder();

        if (options.TargetBpm.HasValue)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{options.TargetBpm.Value:0.##}bpm");
        }
        else if (options.Rate.HasValue)
        {
            // 明示 Rate は .osu 命名と同様 "0.##" 表記 (2.0 -> "2", 1.25 -> "1.25")
            sb.Append(CultureInfo.InvariantCulture, $"{options.Rate.Value:0.##}x");
        }
        else
        {
            // Rate / TargetBpm 共に未指定のフォールバックのみ "1.0x"
            sb.Append("1.0x");
        }

        sb.Append(options.ChangePitch ? " NC" : " DT");

        if (options.HpOverride.HasValue)
            sb.Append(CultureInfo.InvariantCulture, $" HP{options.HpOverride.Value:0.##}");
        if (options.OdOverride.HasValue)
            sb.Append(CultureInfo.InvariantCulture, $" OD{options.OdOverride.Value:0.##}");

        return sb.ToString();
    }

    private static string SanitizeForFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }

    private static string? BuildWarningMessage(int skipped, bool fileWritten)
    {
        if (skipped == 0)
            return null;

        var sb = new StringBuilder();
        if (!fileWritten)
            sb.Append("No beatmaps eligible for JSON export.");

        if (skipped > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(CultureInfo.InvariantCulture, $"Skipped {skipped} beatmap(s).");
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 後始末の失敗は無視
        }
    }
}
