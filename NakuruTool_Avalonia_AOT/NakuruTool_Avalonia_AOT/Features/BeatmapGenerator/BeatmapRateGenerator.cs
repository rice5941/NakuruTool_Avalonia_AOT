using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// beatmap レート変更生成のオーケストレーションサービス。
/// オーディオ変換と .osu 変換を統合し、.osz ファイルとして出力する。
/// </summary>
public interface IBeatmapRateGenerator
{
    /// <summary>単一 beatmap のレート変更生成</summary>
    Task<RateGenerationResult> GenerateAsync(
        Beatmap beatmap,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>複数 beatmap の一括レート変更生成</summary>
    Task<BatchGenerationResult> GenerateBatchAsync(
        ReadOnlyMemory<Beatmap> beatmaps,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}


public sealed class BeatmapRateGenerator : IBeatmapRateGenerator
{
    private readonly IAudioRateChanger _audioRateChanger;
    private readonly IOsuFileRateConverter _osuFileRateConverter;
    private readonly IOsuFileAssetParser _osuFileAssetParser;
    private readonly ISettingsService _settingsService;

    private static readonly char[] InvalidFileNameChars = ['"', '*', '\\', '/', '?', '<', '>', '|', ':'];

    private static readonly HashSet<string> s_defaultHitsoundNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "normal-hitnormal", "normal-hitclap", "normal-hitfinish", "normal-hitwhistle",
        "normal-slidertick", "normal-sliderslide", "normal-sliderwhistle",
        "soft-hitnormal", "soft-hitclap", "soft-hitfinish", "soft-hitwhistle",
        "soft-slidertick", "soft-sliderslide", "soft-sliderwhistle",
        "drum-hitnormal", "drum-hitclap", "drum-hitfinish", "drum-hitwhistle",
        "drum-slidertick", "drum-sliderslide", "drum-sliderwhistle",
    };

    /// <summary>
    /// osu! 標準ヒットサウンドファイルかどうかを判定する。
    /// 拡張子を除いたファイル名で比較する。
    /// </summary>
    internal static bool IsDefaultHitsoundFile(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        return s_defaultHitsoundNames.Contains(nameWithoutExt);
    }

    public BeatmapRateGenerator(
        IAudioRateChanger audioRateChanger,
        IOsuFileRateConverter osuFileRateConverter,
        IOsuFileAssetParser osuFileAssetParser,
        ISettingsService settingsService)
    {
        _audioRateChanger = audioRateChanger;
        _osuFileRateConverter = osuFileRateConverter;
        _osuFileAssetParser = osuFileAssetParser;
        _settingsService = settingsService;
    }

    public async Task<RateGenerationResult> GenerateAsync(
        Beatmap beatmap,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = await GenerateOszForGroupAsync(
            [beatmap], beatmap.FolderName, options, progress, cancellationToken);
        return results[0];
    }

    public async Task<BatchGenerationResult> GenerateBatchAsync(
        ReadOnlyMemory<Beatmap> beatmaps,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var groups = GroupByFolderName(beatmaps);
        var allResults = new List<RateGenerationResult>();
        var processedCount = 0;
        var totalBeatmapCount = beatmaps.Length;
        var wasCancelled = false;

        foreach (var (folderName, groupBeatmaps) in groups)
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

            var groupProgress = CreateGroupProgress(
                progress, processedCount, totalBeatmapCount, folderName);

            RateGenerationResult[] groupResults;
            try
            {
                groupResults = await GenerateOszForGroupAsync(
                    groupBeatmaps, folderName, options, groupProgress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
                break;
            }
            catch (Exception ex)
            {
                foreach (var bm in groupBeatmaps)
                {
                    allResults.Add(new RateGenerationResult
                    {
                        Success = false,
                        AppliedRate = 0,
                        ErrorMessage = ex.Message,
                        SourceBeatmap = bm,
                    });
                }
                processedCount += groupBeatmaps.Length;
                continue;
            }

            allResults.AddRange(groupResults);
            processedCount += groupBeatmaps.Length;
        }

        progress?.Report(new RateGenerationProgress(
            wasCancelled ? "Cancelled" : "Completed",
            allResults.Count,
            totalBeatmapCount,
            100));

        var successCount = 0;
        var failureCount = 0;
        foreach (var r in allResults)
        {
            if (r.Success) successCount++;
            else failureCount++;
        }

        return new BatchGenerationResult
        {
            Results = [.. allResults],
            SuccessCount = successCount,
            FailureCount = failureCount,
            WasCancelled = wasCancelled,
        };
    }

    private async Task<RateGenerationResult[]> GenerateOszForGroupAsync(
        Beatmap[] beatmapsInGroup,
        string folderName,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var total = beatmapsInGroup.Length;
        var results = new RateGenerationResult[total];
        var generatedOsuEntryNames = new string?[total];
        var jsonItems = new RateGenerationJsonItem?[total];

        // レート確定（各beatmapで個別に算出）
        var rates = new double[total];
        for (var i = 0; i < total; i++)
        {
            try
            {
                rates[i] = ResolveAppliedRate(beatmapsInGroup[i], options);
            }
            catch (InvalidOperationException ex)
            {
                results[i] = new RateGenerationResult
                {
                    Success = false,
                    AppliedRate = 0,
                    ErrorMessage = ex.Message,
                    SourceBeatmap = beatmapsInGroup[i],
                };
            }
        }

        // 全てエラーの場合は早期リターン
        var hasValidBeatmap = false;
        for (var i = 0; i < total; i++)
        {
            if (results[i] is null) { hasValidBeatmap = true; break; }
        }
        if (!hasValidBeatmap)
            return results;

        cancellationToken.ThrowIfCancellationRequested();

        progress?.Report(new RateGenerationProgress("解析中...", 0, total, 0));

        var beatmapFolder = ResolveBeatmapFolder(folderName);

        // === 1. 全.osuの参照アセットを収集 ===
        var allSampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allNonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var audioNameMap = new Dictionary<string, (string NewName, double Rate)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < total; i++)
        {
            if (results[i] is not null) continue;

            var beatmap = beatmapsInGroup[i];
            var inputOsuPath = Path.Combine(beatmapFolder, beatmap.OsuFileName);

            if (!File.Exists(inputOsuPath))
            {
                results[i] = new RateGenerationResult
                {
                    Success = false,
                    AppliedRate = rates[i],
                    ErrorMessage = $"元の.osuファイルが見つかりません: {beatmap.OsuFileName}",
                    SourceBeatmap = beatmap,
                };
                continue;
            }

            var isVirtualAudio = string.Equals(beatmap.AudioFilename, "virtual", StringComparison.OrdinalIgnoreCase);

            if (!isVirtualAudio)
            {
                var inputAudioPath = Path.Combine(beatmapFolder, beatmap.AudioFilename);
                if (!File.Exists(inputAudioPath))
                {
                    results[i] = new RateGenerationResult
                    {
                        Success = false,
                        AppliedRate = rates[i],
                        ErrorMessage = $"元のオーディオファイルが見つかりません: {beatmap.AudioFilename}",
                        SourceBeatmap = beatmap,
                    };
                    continue;
                }
            }

            try
            {
                var assets = _osuFileAssetParser.Parse(inputOsuPath);

                foreach (var sample in assets.SampleAudioFiles)
                    allSampleAudioFiles.Add(NormalizeAssetRelativePath(sample));
                foreach (var nonAudio in assets.NonAudioFiles)
                    allNonAudioFiles.Add(NormalizeAssetRelativePath(nonAudio));

                if (!isVirtualAudio)
                {
                    var normalizedAudio = NormalizeAssetRelativePath(beatmap.AudioFilename);
                    if (!audioNameMap.ContainsKey(normalizedAudio))
                    {
                        var newAudioName = BuildAudioFileName(beatmap.AudioFilename, rates[i], options.ChangePitch);
                        audioNameMap[normalizedAudio] = (newAudioName, rates[i]);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results[i] = new RateGenerationResult
                {
                    Success = false,
                    AppliedRate = rates[i],
                    ErrorMessage = $"アセット解析に失敗しました: {ex.Message}",
                    SourceBeatmap = beatmap,
                };
            }
        }

        // 有効なbeatmapが残っているか再チェック
        hasValidBeatmap = false;
        for (var i = 0; i < total; i++)
        {
            if (results[i] is null) { hasValidBeatmap = true; break; }
        }
        if (!hasValidBeatmap)
            return results;

        // sampleNameMap構築（代表レートを取得）
        var representativeRate = 0.0;
        for (var i = 0; i < total; i++)
        {
            if (results[i] is null) { representativeRate = rates[i]; break; }
        }

        var sampleNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sampleFile in allSampleAudioFiles)
        {
            var nameOnly = Path.GetFileName(sampleFile);
            var normalizedForCheck = NormalizeAssetRelativePath(sampleFile);
            // デフォルトヒットサウンドはファイルが存在しない場合のみ変換対象外
            // 実ファイルが存在する場合は通常通り変換する
            if (IsDefaultHitsoundFile(nameOnly) &&
                !File.Exists(Path.Combine(beatmapFolder, ToFileSystemRelativePath(normalizedForCheck))))
                continue;
            var dir = Path.GetDirectoryName(sampleFile);
            var renamed = BuildAudioFileName(nameOnly, representativeRate, options.ChangePitch);
            var renamedPath = string.IsNullOrEmpty(dir)
                ? renamed
                : NormalizeAssetRelativePath(Path.Combine(dir, renamed));
            sampleNameMap[normalizedForCheck] = renamedPath;
        }

        progress?.Report(new RateGenerationProgress("パス解決完了", 0, total, 5));

        // === 2. 一時ディレクトリ作成 ===
        var tempDir = Path.Combine(Path.GetTempPath(), $"NakuruTool_osz_{Guid.NewGuid():N}");
        var oszPath = ResolveOszOutputPath(folderName);
        var oszTmpPath = oszPath + ".tmp";

        try
        {
            Directory.CreateDirectory(tempDir);

            cancellationToken.ThrowIfCancellationRequested();

            // === 3. メインオーディオのレート変換 ===
            var processedAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var audioEntryIndex = 0;
            var audioEntryCount = audioNameMap.Count;

            foreach (var (originalAudio, (newAudioName, audioRate)) in audioNameMap)
            {
                if (processedAudioFiles.Contains(originalAudio))
                    continue;

                processedAudioFiles.Add(originalAudio);

                var inputAudioPath = Path.Combine(beatmapFolder, ToFileSystemRelativePath(originalAudio));
                var outputAudioPath = Path.Combine(tempDir, newAudioName);

                var audioPercent = audioEntryCount > 0
                    ? 5 + (audioEntryIndex * 25 / audioEntryCount)
                    : 5;
                progress?.Report(new RateGenerationProgress(
                    $"オーディオ変換中: {originalAudio}", 0, total, audioPercent));

                var audioResult = await _audioRateChanger.ChangeRateAsync(
                    inputAudioPath, outputAudioPath, audioRate, options.ChangePitch,
                    options.Mp3VbrQuality, cancellationToken);

                if (!audioResult.Success)
                {
                    for (var i = 0; i < total; i++)
                    {
                        results[i] ??= new RateGenerationResult
                        {
                            Success = false,
                            AppliedRate = rates[i],
                            ErrorMessage = $"オーディオ変換に失敗しました: {originalAudio}",
                            SourceBeatmap = beatmapsInGroup[i],
                        };
                    }
                    return results;
                }

                // 3chフォールバック時: audioNameMapを実際の出力ファイル名で更新
                if (audioResult.ActualOutputPath is not null)
                {
                    var actualFileName = NormalizeAssetRelativePath(
                        Path.GetFileName(audioResult.ActualOutputPath));
                    var dir = Path.GetDirectoryName(originalAudio);
                    var updatedName = string.IsNullOrEmpty(dir)
                        ? actualFileName
                        : NormalizeAssetRelativePath(Path.Combine(dir, actualFileName));
                    audioNameMap[originalAudio] = (updatedName, audioRate);
                }

                audioEntryIndex++;
            }

            progress?.Report(new RateGenerationProgress("オーディオ変換完了", 0, total, 30));

            // === 4. サンプル音声のレート変換 ===
            var convertedSampleCount = 0;
            var skippedFileCount = 0;
            var skippedFiles = new List<string>();
            var sampleIndex = 0;
            var sampleTotal = allSampleAudioFiles.Count;

            foreach (var sampleFile in allSampleAudioFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalizedSample = NormalizeAssetRelativePath(sampleFile);
                var inputSamplePath = Path.Combine(beatmapFolder, ToFileSystemRelativePath(normalizedSample));

                if (!File.Exists(inputSamplePath))
                {
                    Debug.WriteLine($"[BeatmapRateGenerator] 警告: サンプル音声が見つかりません: {sampleFile}");
                    skippedFileCount++;
                    skippedFiles.Add(sampleFile);
                    sampleIndex++;
                    continue;
                }

                var renamedFile = sampleNameMap[normalizedSample];
                var renamedOutputPath = Path.Combine(tempDir, ToFileSystemRelativePath(renamedFile));
                var originalOutputPath = Path.Combine(tempDir, ToFileSystemRelativePath(normalizedSample));

                // 出力先ディレクトリを作成
                var renamedDir = Path.GetDirectoryName(renamedOutputPath);
                if (!string.IsNullOrEmpty(renamedDir))
                    Directory.CreateDirectory(renamedDir);

                // デフォルトヒットサウンドは元ファイル名でも参照されるため原音をそのままコピー
                if (IsDefaultHitsoundFile(Path.GetFileName(normalizedSample)))
                {
                    var originalDir = Path.GetDirectoryName(originalOutputPath);
                    if (!string.IsNullOrEmpty(originalDir))
                        Directory.CreateDirectory(originalDir);
                    File.Copy(inputSamplePath, originalOutputPath, overwrite: true);
                }

                var samplePercent = sampleTotal > 0
                    ? 30 + (sampleIndex * 35 / sampleTotal)
                    : 30;
                progress?.Report(new RateGenerationProgress(
                    $"サンプル音声変換中: {sampleFile}", 0, total, samplePercent));

                try
                {
                    var sampleResult = await _audioRateChanger.ChangeRateAsync(
                        inputSamplePath, renamedOutputPath, representativeRate, options.ChangePitch,
                        options.Mp3VbrQuality, cancellationToken);

                    if (sampleResult.Success)
                    {
                        // 3chフォールバック時: sampleNameMapを実際の出力ファイル名で更新
                        if (sampleResult.ActualOutputPath is not null)
                        {
                            var actualFileName = Path.GetFileName(sampleResult.ActualOutputPath);
                            var dir = Path.GetDirectoryName(normalizedSample);
                            var updatedPath = string.IsNullOrEmpty(dir)
                                ? actualFileName
                                : NormalizeAssetRelativePath(Path.Combine(dir, actualFileName));
                            sampleNameMap[normalizedSample] = updatedPath;
                        }

                        convertedSampleCount++;
                    }
                    else
                    {
                        Debug.WriteLine($"[BeatmapRateGenerator] 警告: サンプル音声変換に失敗、原音でフォールバック: {sampleFile}");
                        File.Copy(inputSamplePath, renamedOutputPath, overwrite: true);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Debug.WriteLine($"[BeatmapRateGenerator] 警告: サンプル音声変換例外、原音でフォールバック: {sampleFile} - {ex.Message}");
                    File.Copy(inputSamplePath, renamedOutputPath, overwrite: true);
                }

                sampleIndex++;
            }

            progress?.Report(new RateGenerationProgress("サンプル音声変換完了", 0, total, 65));

            // === 4.5 osuで参照されていないがフォルダ内に存在するデフォルトヒットサウンドをコピー ===
            // osuエンジンはデフォルトヒットサウンドを.osuファイルで参照せずとも同フォルダの同名ファイルを使用するため
            string[] hitsoundExtensions = [".wav", ".ogg", ".mp3"];
            foreach (var ext in hitsoundExtensions)
            {
                foreach (var defaultName in s_defaultHitsoundNames)
                {
                    var fileName = defaultName + ext;
                    var normalizedKey = NormalizeAssetRelativePath(fileName);
                    if (allSampleAudioFiles.Contains(normalizedKey)) continue; // 既に変換対象として処理済み
                    var sourcePath = Path.Combine(beatmapFolder, fileName);
                    if (!File.Exists(sourcePath)) continue;
                    var destPath = Path.Combine(tempDir, fileName);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }

            // === 5. 非音声ファイルのコピー ===
            foreach (var nonAudioFile in allNonAudioFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalized = NormalizeAssetRelativePath(nonAudioFile);
                var srcPath = Path.Combine(beatmapFolder, ToFileSystemRelativePath(normalized));
                var destPath = Path.Combine(tempDir, ToFileSystemRelativePath(normalized));

                if (!File.Exists(srcPath))
                {
                    Debug.WriteLine($"[BeatmapRateGenerator] 警告: 非音声ファイルが見つかりません: {nonAudioFile}");
                    skippedFileCount++;
                    skippedFiles.Add(nonAudioFile);
                    continue;
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                // 非音声アセット（.osb 含む）は raw コピー
                File.Copy(srcPath, destPath, overwrite: true);
            }

            progress?.Report(new RateGenerationProgress("ファイルコピー完了", 0, total, 75));

            // === 6. 各.osu変換 → tempDir に出力 ===
            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (results[i] is not null) continue;

                var beatmap = beatmapsInGroup[i];
                var rate = rates[i];
                var inputOsuPath = Path.Combine(beatmapFolder, beatmap.OsuFileName);
                var newDiffName = BuildDifficultyName(beatmap, rate, options);
                var newOsuName = BuildOsuFileName(beatmap, newDiffName);
                var osuOutputPath = Path.Combine(tempDir, newOsuName);

                var normalizedAudioKey = NormalizeAssetRelativePath(beatmap.AudioFilename);
                var newAudioFilename = audioNameMap.TryGetValue(normalizedAudioKey, out var audioEntry)
                    ? audioEntry.NewName
                    : beatmap.AudioFilename;

                var convertOptions = new OsuFileConvertOptions
                {
                    Rate = (decimal)rate,
                    NewAudioFilename = newAudioFilename,
                    NewDifficultyName = newDiffName,
                    HpOverride = options.HpOverride,
                    OdOverride = options.OdOverride,
                    SampleFilenameMap = sampleNameMap,
                };

                try
                {
                    _osuFileRateConverter.Convert(inputOsuPath, osuOutputPath, convertOptions);

                    // 生成 .osu のエントリ名 (zip 内 rel) を保存
                    var entryName = newOsuName.Replace('\\', '/');
                    generatedOsuEntryNames[i] = entryName;

                    // MD5 + メタ抽出。失敗しても .osz 生成は続行する。
                    try
                    {
                        string md5;
                        using (var fs = File.OpenRead(osuOutputPath))
                        {
                            md5 = Convert.ToHexString(MD5.HashData(fs)).ToLowerInvariant();
                        }

                        if (OsuFileMetadataReader.TryReadBasicMetadata(osuOutputPath, out var meta))
                        {
                            jsonItems[i] = new RateGenerationJsonItem
                            {
                                Title = meta.Title,
                                Artist = meta.Artist,
                                Version = meta.Version,
                                Creator = meta.Creator,
                                Cs = meta.CircleSize,
                                BeatmapsetId = meta.BeatmapSetId,
                                Md5 = md5,
                            };
                        }
                        else
                        {
                            Debug.WriteLine($"[BeatmapRateGenerator] 警告: 生成 .osu のメタ抽出に失敗: {osuOutputPath}");
                        }
                    }
                    catch (Exception metaEx) when (metaEx is not OperationCanceledException)
                    {
                        Debug.WriteLine($"[BeatmapRateGenerator] 警告: 生成 .osu の MD5/メタ抽出例外: {metaEx.Message}");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    results[i] = new RateGenerationResult
                    {
                        Success = false,
                        AppliedRate = rate,
                        ErrorMessage = $".osu 変換に失敗しました: {ex.Message}",
                        SourceBeatmap = beatmap,
                    };
                }

                var osuPercent = total > 0 ? 75 + ((i + 1) * 10 / total) : 85;
                progress?.Report(new RateGenerationProgress(
                    $".osu変換中: [{i + 1}/{total}]", i + 1, total, osuPercent));
            }

            progress?.Report(new RateGenerationProgress("ZIP化中...", total, total, 85));

            // === 7. .osz作成（原子的置換） ===
            cancellationToken.ThrowIfCancellationRequested();

            var oszDir = Path.GetDirectoryName(oszPath);
            if (!string.IsNullOrEmpty(oszDir) && !Directory.Exists(oszDir))
                Directory.CreateDirectory(oszDir);

            var includedOsuEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(oszPath))
            {
                ZipFile.CreateFromDirectory(tempDir, oszTmpPath, CompressionLevel.Fastest, false, Encoding.UTF8);
                File.Move(oszTmpPath, oszPath, overwrite: true);

                // 新規作成: 成功した全ての .osu は収録済み
                for (var i = 0; i < total; i++)
                {
                    if (generatedOsuEntryNames[i] is { } name)
                        includedOsuEntries.Add(name);
                }
            }
            else
            {
                // 既存 .osz をベースに、未収録エントリのみ追加（衝突時は既存優先）
                File.Copy(oszPath, oszTmpPath, overwrite: true);

                try
                {
                    using (var archive = ZipFile.Open(oszTmpPath, ZipArchiveMode.Update, Encoding.UTF8))
                    {
                        var existingEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var e in archive.Entries)
                            existingEntries.Add(e.FullName.Replace('\\', '/'));

                        foreach (var absPath in Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories))
                        {
                            var rel = Path.GetRelativePath(tempDir, absPath).Replace('\\', '/');
                            if (existingEntries.Contains(rel))
                                continue;

                            archive.CreateEntryFromFile(absPath, rel, CompressionLevel.Fastest);
                            includedOsuEntries.Add(rel);
                        }
                    }

                    File.Move(oszTmpPath, oszPath, overwrite: true);
                }
                catch (InvalidDataException ex)
                {
                    Debug.WriteLine($"[BeatmapRateGenerator] 既存 .osz を開けないため新規作成にフォールバック: {oszPath} - {ex.Message}");
                    TryDeleteFile(oszTmpPath);

                    ZipFile.CreateFromDirectory(tempDir, oszTmpPath, CompressionLevel.Fastest, false, Encoding.UTF8);
                    File.Move(oszTmpPath, oszPath, overwrite: true);

                    // フォールバックで新規作成された場合、成功した全ての .osu は収録済み
                    includedOsuEntries.Clear();
                    for (var i = 0; i < total; i++)
                    {
                        if (generatedOsuEntryNames[i] is { } name)
                            includedOsuEntries.Add(name);
                    }
                }
            }

            progress?.Report(new RateGenerationProgress("完了", total, total, 100));

            // 成功結果を設定
            for (var i = 0; i < total; i++)
            {
                var entryName = generatedOsuEntryNames[i];
                var included = entryName is not null && includedOsuEntries.Contains(entryName);
                results[i] ??= new RateGenerationResult
                {
                    Success = true,
                    GeneratedOszPath = oszPath,
                    AppliedRate = rates[i],
                    ConvertedSampleCount = convertedSampleCount,
                    SkippedFileCount = skippedFileCount,
                    SkippedFiles = skippedFiles,
                    SourceBeatmap = beatmapsInGroup[i],
                    GeneratedOsuEntryName = entryName,
                    JsonItem = jsonItems[i],
                    IncludedInOsz = included,
                };
            }

            return results;
        }
        finally
        {
            // === 8. cleanup ===
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BeatmapRateGenerator] 一時ディレクトリの削除に失敗: {ex.Message}");
            }

            TryDeleteFile(oszTmpPath);
        }
    }

    private static List<(string FolderName, Beatmap[] Beatmaps)> GroupByFolderName(
        ReadOnlyMemory<Beatmap> beatmaps)
    {
        var dict = new Dictionary<string, List<Beatmap>>(StringComparer.Ordinal);
        var span = beatmaps.Span;
        for (var i = 0; i < span.Length; i++)
        {
            var bm = span[i];
            if (!dict.TryGetValue(bm.FolderName, out var list))
            {
                list = [];
                dict[bm.FolderName] = list;
            }
            list.Add(bm);
        }
        var result = new List<(string, Beatmap[])>(dict.Count);
        foreach (var (key, list) in dict)
            result.Add((key, [.. list]));
        return result;
    }

    private string ResolveOszOutputPath(string folderName)
    {
        var songsFolder = Path.Combine(_settingsService.SettingsData.OsuFolderPath, "Songs");
        // folderName にパス区切り文字が含まれる場合（Songs 直下ではなくサブフォルダ構成）、
        // osz は Songs 直下に置くため、末尾のフォルダ名のみをファイル名として使用する
        var safeFileName = Path.GetFileName(folderName.TrimEnd('\\', '/'));
        var candidate = Path.Combine(songsFolder, $"{safeFileName}.osz");
        if (candidate.Length <= 240)
            return candidate;

        var hash = ComputeStableHashSuffix(folderName);
        var shortened = safeFileName.Length > 180 ? safeFileName[..180] : safeFileName;
        return Path.Combine(songsFolder, $"{shortened}_{hash}.osz");
    }

    private static string ComputeStableHashSuffix(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes, 0, 4).ToLowerInvariant();
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

    private string ResolveBeatmapFolder(string folderName)
    {
        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        return Path.Combine(osuFolderPath, "Songs", folderName);
    }

    private string ResolveBeatmapFolder(Beatmap beatmap)
        => ResolveBeatmapFolder(beatmap.FolderName);

    internal static string BuildAudioFileName(string originalName, double rate, bool changePitch)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalName);
        var inputExt = Path.GetExtension(originalName).ToLowerInvariant();
        var outputExt = inputExt switch
        {
            ".mp3" => ".mp3",
            ".wav" => ".wav",
            _ => ".ogg",
        };
        var modeTag = changePitch ? "nc" : "dt";
        return string.Create(CultureInfo.InvariantCulture,
            $"{nameWithoutExt}_{rate:0.000}x_{modeTag}{outputExt}");
    }

    private static string BuildDifficultyName(Beatmap beatmap, double rate, RateGenerationOptions options)
    {
        var rateStr = FormatRate(rate);
        var modeTag = options.ChangePitch ? "NC" : "DT";
        var bpm = Math.Round(beatmap.BPM * rate, 0);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{beatmap.Version} {rateStr}x");

        if (beatmap.BPM > 0)
            sb.Append(CultureInfo.InvariantCulture, $" ({bpm:0}bpm)");

        sb.Append(CultureInfo.InvariantCulture, $" {modeTag}");

        if (options.HpOverride.HasValue)
            sb.Append(CultureInfo.InvariantCulture, $" HP{options.HpOverride.Value:0.#}");
        if (options.OdOverride.HasValue)
            sb.Append(CultureInfo.InvariantCulture, $" OD{options.OdOverride.Value:0.#}");

        return sb.ToString();
    }

    private static string BuildOsuFileName(Beatmap beatmap, string newDiffName)
    {
        var raw = $"{beatmap.Artist} - {beatmap.Title} ({beatmap.Creator}) [{newDiffName}].osu";
        return SanitizeFileName(raw);
    }

    internal static string SanitizeFileName(string name)
    {
        foreach (var c in InvalidFileNameChars)
        {
            name = name.Replace(c.ToString(), string.Empty);
        }
        return name;
    }

    private static string NormalizeAssetRelativePath(string path)
        => path.Replace('\\', '/').Trim();

    private static string ToFileSystemRelativePath(string canonicalPath)
        => canonicalPath.Replace('/', Path.DirectorySeparatorChar);

    private static string FormatRate(double rate)
    {
        if (Math.Abs(rate % 1.0) < 0.001)
            return ((int)rate).ToString(CultureInfo.InvariantCulture);

        var formatted = rate.ToString("0.##", CultureInfo.InvariantCulture);
        return formatted;
    }

    private static IProgress<RateGenerationProgress>? CreateGroupProgress(
        IProgress<RateGenerationProgress>? batchProgress,
        int processedCount,
        int totalBeatmapCount,
        string folderName)
    {
        if (batchProgress is null)
            return null;

        return new Progress<RateGenerationProgress>(item =>
        {
            var mappedPercent = totalBeatmapCount <= 0
                ? item.ProgressPercent
                : (processedCount * 100 + item.ProgressPercent) / totalBeatmapCount;

            batchProgress.Report(new RateGenerationProgress(
                $"[{folderName}] {item.Message}",
                processedCount + item.CurrentIndex,
                totalBeatmapCount,
                mappedPercent));
        });
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BeatmapRateGenerator] ファイルの削除に失敗: {path} - {ex.Message}");
        }
    }
}
