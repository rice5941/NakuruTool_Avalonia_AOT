using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;
using OggVorbisEncoder;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// NAudio によるオーディオレート変更実装。
/// SampleRate 偽装 + リサンプリング方式でテンポとピッチを同時に変化させる。
/// MP3/OGG 入力は OGG 出力、WAV 入力は WAV 出力。
/// </summary>
public sealed class AudioRateChanger : IAudioRateChanger
{
    private const int ReadBufferSize = 512;
    private const float OggQuality = 0.95f;

    public async Task<bool> ChangeRateAsync(
        string inputPath, string outputPath, double rate,
        bool changePitch,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("入力ファイルが見つかりません。", inputPath);

        var extension = Path.GetExtension(inputPath).ToLowerInvariant();
        if (extension is not ".mp3" and not ".ogg" and not ".wav")
            throw new NotSupportedException($"サポートされていない拡張子です: {extension}");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (changePitch)
                {
                    // NC方式（既存ロジック）
                    if (extension == ".wav")
                        return ProcessToWav(CreateReader(inputPath, extension), outputPath, rate);
                    else
                        return ProcessToOgg(CreateReader(inputPath, extension), outputPath, rate);
                }
                else
                {
                    // DT方式（新規）
                    return ProcessWithTimeStretch(inputPath, extension, outputPath, rate);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                TryDeleteIncompleteOutput(outputPath);
                return false;
            }
        }, cancellationToken);
    }

    /// <summary>入力形式に応じたWaveStreamを生成する。呼び出し元がDisposeすること。</summary>
    private static WaveStream CreateReader(string inputPath, string extension)
    {
        return extension switch
        {
            ".mp3" => new Mp3FileReaderBase(inputPath, wf => new Mp3FrameDecompressor(wf)),
            ".ogg" => new VorbisWaveReader(inputPath),
            ".wav" => new WaveFileReader(inputPath),
            _ => throw new NotSupportedException($"サポートされていない拡張子です: {extension}")
        };
    }

    /// <summary>PCMデータをWAVとして出力する。</summary>
    private static bool ProcessToWav(WaveStream reader, string outputPath, double rate)
    {
        using (reader)
        {
            var sampleProvider = reader.ToSampleProvider();

            if (rate == 1.0)
            {
                WaveFileWriter.CreateWaveFile16(outputPath, sampleProvider);
                return true;
            }

            var resampled = ApplyRateChange(sampleProvider, rate);
            WaveFileWriter.CreateWaveFile16(outputPath, resampled);
            return true;
        }
    }

    /// <summary>PCMデータをOgg Vorbisとしてエンコード・出力する。</summary>
    private static bool ProcessToOgg(WaveStream reader, string outputPath, double rate)
    {
        using (reader)
        {
            var sampleProvider = reader.ToSampleProvider();
            ISampleProvider source = rate == 1.0 ? sampleProvider : ApplyRateChange(sampleProvider, rate);

            var sampleRate = source.WaveFormat.SampleRate;
            var channels = source.WaveFormat.Channels;

            // 全サンプルをチャンネル別float配列に読み込む
            var allSamples = ReadAllSamples(source);
            var totalFrames = allSamples.Length / channels;

            var channelSamples = new float[channels][];
            for (var ch = 0; ch < channels; ch++)
            {
                channelSamples[ch] = new float[totalFrames];
            }

            for (var frame = 0; frame < totalFrames; frame++)
            {
                for (var ch = 0; ch < channels; ch++)
                {
                    channelSamples[ch][frame] = allSamples[frame * channels + ch];
                }
            }

            // OggVorbisEncoder でエンコード
            using var outputStream = File.Create(outputPath);
            EncodeOgg(outputStream, channelSamples, sampleRate, channels);
            return true;
        }
    }

    /// <summary>ISampleProviderから全サンプルを読み込む。</summary>
    private static float[] ReadAllSamples(ISampleProvider source)
    {
        const int bufferSize = 4096;
        var buffer = new float[bufferSize];

        using var ms = new MemoryStream();
        int samplesRead;
        while ((samplesRead = source.Read(buffer, 0, bufferSize)) > 0)
        {
            // float[] → byte[] でMemoryStreamに書く
            for (var i = 0; i < samplesRead; i++)
            {
                var bytes = BitConverter.GetBytes(buffer[i]);
                ms.Write(bytes, 0, bytes.Length);
            }
        }

        // byte[] → float[] に戻す
        var rawBytes = ms.ToArray();
        var result = new float[rawBytes.Length / sizeof(float)];
        Buffer.BlockCopy(rawBytes, 0, result, 0, rawBytes.Length);
        return result;
    }

    /// <summary>OggVorbisEncoder を使ってOGGファイルに書き出す。</summary>
    private static void EncodeOgg(Stream output, float[][] channelSamples, int sampleRate, int channels)
    {
        var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, OggQuality);
        var serial = Random.Shared.Next();
        var oggStream = new OggStream(serial);

        // ヘッダー書き出し
        var comments = new Comments();
        var infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
        var commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
        var booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

        oggStream.PacketIn(infoPacket);
        oggStream.PacketIn(commentsPacket);
        oggStream.PacketIn(booksPacket);

        FlushPages(oggStream, output, force: true);

        // オーディオデータ書き出し
        var processingState = ProcessingState.Create(info);
        var totalFrames = channelSamples[0].Length;

        for (var readIndex = 0; readIndex < totalFrames; readIndex += ReadBufferSize)
        {
            var length = Math.Min(ReadBufferSize, totalFrames - readIndex);
            processingState.WriteData(channelSamples, length, readIndex);

            while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
            {
                oggStream.PacketIn(packet);
                FlushPages(oggStream, output, force: false);
            }
        }

        processingState.WriteEndOfStream();
        while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
        {
            oggStream.PacketIn(packet);
            FlushPages(oggStream, output, force: false);
        }

        FlushPages(oggStream, output, force: true);
    }

    private static void FlushPages(OggStream oggStream, Stream output, bool force)
    {
        while (oggStream.PageOut(out OggPage page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }

    private static ISampleProvider ApplyRateChange(ISampleProvider source, double rate)
    {
        var originalRate = source.WaveFormat.SampleRate;
        var overriddenRate = (int)(originalRate * rate);
        var overridden = new SampleRateOverrideSampleProvider(source, overriddenRate);
        return new WdlResamplingSampleProvider(overridden, originalRate);
    }

    /// <summary>
    /// SignalsmithStretch によるDT方式タイムストレッチ。
    /// 入力PCM全量読み込み → Rust FFI → エンコード出力。
    /// </summary>
    private static bool ProcessWithTimeStretch(
        string inputPath, string extension, string outputPath, double rate)
    {
        using var reader = CreateReader(inputPath, extension);
        var sampleProvider = reader.ToSampleProvider();
        var sampleRate = sampleProvider.WaveFormat.SampleRate;
        var channels = sampleProvider.WaveFormat.Channels;

        // 1. 全サンプル読み込み（インターリーブfloat[]）— 既存ReadAllSamples再利用
        var inputSamples = ReadAllSamples(sampleProvider);

        // 2. Rust FFI呼び出し
        var outputSamples = StretchWithSignalsmith(inputSamples, sampleRate, channels, rate);

        // 3. エンコード出力
        var outputExt = Path.GetExtension(outputPath).ToLowerInvariant();
        if (outputExt == ".wav")
        {
            WriteStretchedToWav(outputSamples, sampleRate, channels, outputPath);
        }
        else
        {
            WriteStretchedToOgg(outputSamples, sampleRate, channels, outputPath);
        }

        return true;
    }

    /// <summary>
    /// Rust nakuru_stretch 経由でタイムストレッチを実行。
    /// Rustが出力バッファを確保して返す。C#側でコピー後、即座に解放。
    /// </summary>
    private static float[] StretchWithSignalsmith(
        float[] interleavedSamples, int sampleRate, int channels, double rate)
    {
        unsafe
        {
            fixed (float* inputPtr = interleavedSamples)
            {
                var result = NativeStretchMethods.nakuru_stretch_process(
                    inputPtr,
                    interleavedSamples.Length,
                    channels,
                    sampleRate,
                    rate);

                if (result.data == null || result.len <= 0)
                    throw new InvalidOperationException(
                        "SignalsmithStretchによるタイムストレッチに失敗しました。");

                try
                {
                    var managed = new float[result.len];
                    Marshal.Copy(
                        (nint)result.data, managed, 0, result.len);
                    return managed;
                }
                finally
                {
                    NativeStretchMethods.nakuru_stretch_free(result);
                }
            }
        }
    }

    private static void WriteStretchedToWav(
        float[] samples, int sampleRate, int channels, string outputPath)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(outputPath, format);
        writer.WriteSamples(samples, 0, samples.Length);
    }

    private static void WriteStretchedToOgg(
        float[] samples, int sampleRate, int channels, string outputPath)
    {
        var totalFrames = samples.Length / channels;
        var channelSamples = new float[channels][];
        for (var ch = 0; ch < channels; ch++)
            channelSamples[ch] = new float[totalFrames];

        for (var frame = 0; frame < totalFrames; frame++)
            for (var ch = 0; ch < channels; ch++)
                channelSamples[ch][frame] = samples[frame * channels + ch];

        using var outputStream = File.Create(outputPath);
        EncodeOgg(outputStream, channelSamples, sampleRate, channels);
    }

    private static void TryDeleteIncompleteOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch
        {
            // 削除失敗は無視
        }
    }
}
