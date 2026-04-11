using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public sealed class BeatmapRateGenerator : IBeatmapRateGenerator
{
    private readonly IAudioRateChanger _audioRateChanger;
    private readonly IOsuFileRateConverter _osuFileRateConverter;
    private readonly ISettingsService _settingsService;

    private static readonly char[] InvalidFileNameChars = ['"', '*', '\\', '/', '?', '<', '>', '|', ':'];

    public BeatmapRateGenerator(
        IAudioRateChanger audioRateChanger,
        IOsuFileRateConverter osuFileRateConverter,
        ISettingsService settingsService)
    {
        _audioRateChanger = audioRateChanger;
        _osuFileRateConverter = osuFileRateConverter;
        _settingsService = settingsService;
    }

    public async Task<RateGenerationResult> GenerateAsync(
        Beatmap beatmap,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // 1. レート確定・検証
        double rate;
        try
        {
            rate = ResolveAppliedRate(beatmap, options);
        }
        catch (InvalidOperationException ex)
        {
            return new RateGenerationResult
            {
                Success = false,
                AppliedRate = 0,
                ErrorMessage = ex.Message,
                SourceBeatmap = beatmap,
            };
        }


        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new RateGenerationProgress("パス解決中...", 1, 1, 0));

        // 2. パス解決
        var beatmapFolder = ResolveBeatmapFolder(beatmap);
        var inputOsuPath = Path.Combine(beatmapFolder, beatmap.OsuFileName);
        var inputAudioPath = Path.Combine(beatmapFolder, beatmap.AudioFilename);
        if (!File.Exists(inputOsuPath))
        {
            return new RateGenerationResult
            {
                Success = false,
                AppliedRate = rate,
                ErrorMessage = $"元の.osuファイルが見つかりません: {beatmap.OsuFileName}",
                SourceBeatmap = beatmap,
            };
        }

        if (!File.Exists(inputAudioPath))
        {
            return new RateGenerationResult
            {
                Success = false,
                AppliedRate = rate,
                ErrorMessage = $"元のオーディオファイルが見つかりません: {beatmap.AudioFilename}",
                SourceBeatmap = beatmap,
            };
        }

        var newAudioName = BuildAudioFileName(beatmap.AudioFilename, rate, options.ChangePitch);
        var newDiffName = BuildDifficultyName(beatmap, rate, options);
        var newOsuName = BuildOsuFileName(beatmap, newDiffName);
        var audioOutputPath = Path.Combine(beatmapFolder, newAudioName);
        var osuOutputPath = Path.Combine(beatmapFolder, newOsuName);

        // 3. オーディオ変換（スキップ判定）
        bool audioSkipped;
        if (File.Exists(audioOutputPath))
        {
            audioSkipped = true;
            progress?.Report(new RateGenerationProgress("オーディオ変換をスキップしました", 1, 1, 50));
        }
        else
        {
            progress?.Report(new RateGenerationProgress("オーディオ変換中...", 1, 1, 50));

            bool audioSuccess;
            try
            {
                audioSuccess = await _audioRateChanger.ChangeRateAsync(
                    inputAudioPath, audioOutputPath, rate, options.ChangePitch, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new RateGenerationResult
                {
                    Success = false,
                    AppliedRate = rate,
                    ErrorMessage = $"オーディオ変換に失敗しました: {ex.Message}",
                    SourceBeatmap = beatmap,
                };
            }

            if (!audioSuccess)
            {
                return new RateGenerationResult
                {
                    Success = false,
                    AppliedRate = rate,
                    ErrorMessage = "オーディオ変換に失敗しました",
                    SourceBeatmap = beatmap,
                };
            }

            audioSkipped = false;
        }

        // 4. .osu変換
        cancellationToken.ThrowIfCancellationRequested();
        osuOutputPath = ResolveUniqueOutputPath(osuOutputPath);

        var convertOptions = new OsuFileConvertOptions
        {
            Rate = (decimal)rate,
            NewAudioFilename = newAudioName,
            NewDifficultyName = newDiffName,
            HpOverride = options.HpOverride,
            OdOverride = options.OdOverride,
        };

        try
        {
            _osuFileRateConverter.Convert(inputOsuPath, osuOutputPath, convertOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RateGenerationResult
            {
                Success = false,
                GeneratedAudioPath = audioSkipped ? null : audioOutputPath,
                AudioSkipped = audioSkipped,
                AppliedRate = rate,
                ErrorMessage = $".osu 変換に失敗しました: {ex.Message}",
                SourceBeatmap = beatmap,
            };
        }

        progress?.Report(new RateGenerationProgress("完了", 1, 1, 100));

        // 5. Result返却
        return new RateGenerationResult
        {
            Success = true,
            GeneratedOsuPath = osuOutputPath,
            GeneratedAudioPath = audioSkipped ? null : audioOutputPath,
            AudioSkipped = audioSkipped,
            AppliedRate = rate,
            SourceBeatmap = beatmap,
        };
    }

    public async Task<BatchGenerationResult> GenerateBatchAsync(
        ReadOnlyMemory<Beatmap> beatmaps,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var total = beatmaps.Length;
        var results = new List<RateGenerationResult>(total);
        var wasCancelled = false;

        for (var i = 0; i < total; i++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                break;
            }

            var beatmap = beatmaps.Span[i];
            progress?.Report(new RateGenerationProgress(
                $"[{i + 1}/{total}] {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}] の生成を開始します",
                i + 1,
                total,
                total > 0 ? i * 100 / total : 100));

            var itemProgress = CreateBatchItemProgress(progress, beatmap, i, total);

            RateGenerationResult result;
            try
            {
                result = await GenerateAsync(beatmap, options, itemProgress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                break;
            }
            catch (Exception ex)
            {
                result = new RateGenerationResult
                {
                    Success = false,
                    AppliedRate = 0,
                    ErrorMessage = ex.Message,
                    SourceBeatmap = beatmap,
                };
            }

            results.Add(result);
        }

        progress?.Report(new RateGenerationProgress(
            wasCancelled ? "Cancelled" : "Completed",
            results.Count,
            total,
            100));

        var successCount = 0;
        var failureCount = 0;
        foreach (var r in results)
        {
            if (r.Success) successCount++;
            else failureCount++;
        }

        return new BatchGenerationResult
        {
            Results = [.. results],
            SuccessCount = successCount,
            FailureCount = failureCount,
            WasCancelled = wasCancelled,
        };
    }

    private static double ResolveAppliedRate(Beatmap beatmap, RateGenerationOptions options)
    {
        if (options.Rate.HasValue == options.TargetBpm.HasValue)
            throw new InvalidOperationException("Rate または TargetBpm のいずれか一方のみを指定してください");

        if (options.TargetBpm.HasValue)
        {
            if (beatmap.BPM <= 0)
                throw new InvalidOperationException(
                    $"BPMが0以下のため目標BPMからレートを算出できません: {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}]");

            return options.TargetBpm.Value / beatmap.BPM;
        }

        if (options.Rate.HasValue)
            return options.Rate.Value;

        throw new InvalidOperationException("Rate または TargetBpm のいずれか一方を指定してください");
    }

    private string ResolveBeatmapFolder(Beatmap beatmap)
    {
        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        return Path.Combine(osuFolderPath, "Songs", beatmap.FolderName);
    }

    private static string BuildAudioFileName(string originalName, double rate, bool changePitch)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalName);
        var inputExt = Path.GetExtension(originalName).ToLowerInvariant();
        // WAV入力はWAV出力、それ以外（MP3/OGG）はOGG出力
        var outputExt = inputExt == ".wav" ? ".wav" : ".ogg";
        var modeTag = changePitch ? "nc" : "dt";
        return string.Create(CultureInfo.InvariantCulture,
            $"{nameWithoutExt}_{rate:0.00}x_{modeTag}{outputExt}");
    }

    private static string BuildDifficultyName(Beatmap beatmap, double rate, RateGenerationOptions options)
    {
        var rateStr = FormatRate(rate);
        var modeTag = options.ChangePitch ? "NC" : "DT";

        if (options.TargetBpm.HasValue)
        {
            var bpm = Math.Round(beatmap.BPM * rate, 0);
            return string.Create(CultureInfo.InvariantCulture,
                $"{beatmap.Version} x{rateStr} ({bpm:0}bpm) {modeTag}");
        }

        return $"{beatmap.Version} x{rateStr} {modeTag}";
    }

    private static string BuildOsuFileName(Beatmap beatmap, string newDiffName)
    {
        var raw = $"{beatmap.Artist} - {beatmap.Title} ({beatmap.Creator}) [{newDiffName}].osu";
        return SanitizeFileName(raw);
    }

    internal static string ResolveUniqueOutputPath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{nameWithoutExt} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return path;
    }

    internal static string SanitizeFileName(string name)
    {
        foreach (var c in InvalidFileNameChars)
        {
            name = name.Replace(c.ToString(), string.Empty);
        }
        return name;
    }

    private static string FormatRate(double rate)
    {
        if (Math.Abs(rate % 1.0) < 0.001)
            return ((int)rate).ToString(CultureInfo.InvariantCulture);

        // 小数点以下の不要な0を除去
        var formatted = rate.ToString("0.##", CultureInfo.InvariantCulture);
        return formatted;
    }

    private static IProgress<RateGenerationProgress>? CreateBatchItemProgress(
        IProgress<RateGenerationProgress>? batchProgress,
        Beatmap beatmap,
        int index,
        int total)
    {
        if (batchProgress is null)
            return null;

        return new Progress<RateGenerationProgress>(item =>
        {
            var mappedPercent = total <= 0
                ? item.ProgressPercent
                : ((index * 100) + item.ProgressPercent) / total;

            batchProgress.Report(new RateGenerationProgress(
                $"[{index + 1}/{total}] {beatmap.Artist} - {beatmap.Title} [{beatmap.Version}] - {item.Message}",
                index + 1,
                total,
                mappedPercent));
        });
    }
}
