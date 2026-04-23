using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// ffmpeg subprocess を用いたオーディオレート変更実装。
/// 入力メタデータは純 C# パーサー <see cref="AudioInputMetadataReader"/> で抽出する。
/// </summary>
public sealed class FfmpegAudioRateChanger : IAudioRateChanger
{
    private static readonly string[] s_supportedExtensions = { ".mp3", ".ogg", ".wav" };

    public FfmpegAudioRateChanger()
    {
    }

    public async Task<AudioRateChangeResult> ChangeRateAsync(
        string inputPath,
        string outputPath,
        double rate,
        bool changePitch,
        int? mp3VbrQuality = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "rate must be positive.");
        }

        string inputExt = Path.GetExtension(inputPath).ToLowerInvariant();
        string outputExt = Path.GetExtension(outputPath).ToLowerInvariant();
        if (Array.IndexOf(s_supportedExtensions, inputExt) < 0)
        {
            throw new NotSupportedException($"Unsupported input extension: {inputExt}");
        }
        if (Array.IndexOf(s_supportedExtensions, outputExt) < 0)
        {
            throw new NotSupportedException($"Unsupported output extension: {outputExt}");
        }
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}", inputPath);
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // rate == 1.0 ショートカット（入出力拡張子一致時のみ）
        if (Math.Abs(rate - 1.0) < 1e-9 && inputExt == outputExt)
        {
            File.Copy(inputPath, outputPath, overwrite: true);
            return new AudioRateChangeResult(true);
        }

        string actualOutputPath = outputPath;
        string actualOutputExt = outputExt;

        try
        {
            // 純 C# パーサーで入力メタデータを取得
            AudioInputMetadata metadata = AudioInputMetadataReader.ReadFromFile(inputPath);

            // 3ch 以上 MP3 → OGG フォールバック
            if (outputExt == ".mp3" && metadata.Channels >= 3)
            {
                actualOutputPath = Path.ChangeExtension(outputPath, ".ogg");
                actualOutputExt = ".ogg";
            }

            // フィルタ組立
            string filter = changePitch
                ? FfmpegArgumentsBuilder.BuildNcFilter(metadata.SampleRate, rate)
                : FfmpegArgumentsBuilder.BuildDtFilter(rate);

            var args = FfmpegArgumentsBuilder.BuildFfmpegArgs(
                inputPath,
                actualOutputPath,
                filter,
                actualOutputExt,
                mp3VbrQuality ?? FfmpegArgumentsBuilder.DefaultMp3VbrQuality,
                FfmpegArgumentsBuilder.DefaultOggQuality);

            await FfmpegProcessRunner.RunFfmpegAsync(args, cancellationToken).ConfigureAwait(false);

            return new AudioRateChangeResult(
                true,
                actualOutputPath != outputPath ? actualOutputPath : null);
        }
        catch (OperationCanceledException)
        {
            TryDeleteIncompleteOutput(actualOutputPath);
            if (actualOutputPath != outputPath) TryDeleteIncompleteOutput(outputPath);
            throw;
        }
        catch (TimeoutException)
        {
            TryDeleteIncompleteOutput(actualOutputPath);
            if (actualOutputPath != outputPath) TryDeleteIncompleteOutput(outputPath);
            throw;
        }
        catch (FfmpegExecutionException ex)
        {
            Debug.WriteLine(
                $"[ffmpeg] exit={ex.ExitCode} tail={ex.StderrTail}");
            TryDeleteIncompleteOutput(actualOutputPath);
            if (actualOutputPath != outputPath) TryDeleteIncompleteOutput(outputPath);
            return new AudioRateChangeResult(false);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is InvalidDataException ||
            ex is NotSupportedException ||
            ex is FormatException ||
            ex is OverflowException)
        {
            Debug.WriteLine($"[ffmpeg] {ex.GetType().Name}: {ex.Message}");
            TryDeleteIncompleteOutput(actualOutputPath);
            if (actualOutputPath != outputPath) TryDeleteIncompleteOutput(outputPath);
            return new AudioRateChangeResult(false);
        }
    }

    private static void TryDeleteIncompleteOutput(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // swallow
        }
    }
}
