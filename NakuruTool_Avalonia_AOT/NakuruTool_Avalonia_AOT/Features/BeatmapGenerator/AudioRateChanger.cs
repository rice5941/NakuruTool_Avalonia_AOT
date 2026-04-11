using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// nakuru_rate_audio ネイティブライブラリによるオーディオレート変更実装。
/// </summary>
public sealed class AudioRateChanger : IAudioRateChanger
{
    public async Task<bool> ChangeRateAsync(
        string inputPath, string outputPath, double rate,
        bool changePitch,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("入力ファイルが見つかりません。", inputPath);

        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension is not ".mp3" and not ".ogg" and not ".wav")
            throw new NotSupportedException($"サポートされていない拡張子です: {extension}");

        var outputExt = Path.GetExtension(outputPath).ToLowerInvariant();
        if (outputExt is not ".mp3" and not ".ogg" and not ".wav")
            throw new NotSupportedException($"サポートされていない出力拡張子です: {outputExt}");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var mode = changePitch
                    ? RateAudioInterop.ConvertMode.TempoPitch
                    : RateAudioInterop.ConvertMode.TempoOnly;

                var format = outputExt switch
                {
                    ".wav" => RateAudioInterop.OutputFormat.Wav,
                    ".mp3" => RateAudioInterop.OutputFormat.Mp3,
                    ".ogg" => RateAudioInterop.OutputFormat.OggVorbis,
                    _ => throw new NotSupportedException($"サポートされていない出力拡張子です: {outputExt}"),
                };

                Action<float>? onProgress = progress is not null
                    ? v => progress.Report(v)
                    : null;

                RateAudioInterop.Convert(inputPath, outputPath, rate, mode, format,
                    onProgress: onProgress);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                TryDeleteIncompleteOutput(outputPath);
                throw;
            }
        }, cancellationToken);
    }

    private static void TryDeleteIncompleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* 削除失敗は無視 */ }
    }
}
