using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;
using OggVorbisEncoder;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// オーディオレート変更の結果。
/// </summary>
/// <param name="Success">変換成功/失敗</param>
/// <param name="ActualOutputPath">実際に出力されたファイルパス。3chフォールバック時は要求と異なるパスになる。失敗時は null</param>
public readonly record struct AudioRateChangeResult(
    bool Success,
    string? ActualOutputPath = null);

/// <summary>
/// オーディオファイルのレート（速度）変更を行うサービス。
/// </summary>
public interface IAudioRateChanger
{
    /// <summary>
    /// オーディオファイルのレートを変更して出力する。
    /// 入力フォーマットと同じフォーマットで出力する（MP3→MP3, OGG→OGG, WAV→WAV）。
    /// 3ch以上のMP3出力要求時はOGGにフォールバックし、ActualOutputPathで通知する。
    /// </summary>
    /// <param name="inputPath">入力ファイルのフルパス（.mp3, .ogg, .wav）</param>
    /// <param name="outputPath">出力ファイルのフルパス</param>
    /// <param name="rate">レート倍率（例: 1.1 = 1.1倍速）</param>
    /// <param name="changePitch">true = NC方式（ピッチ+速度を同時変更）、false = DT方式（速度のみ変更、ピッチ維持）</param>
    /// <param name="mp3VbrQuality">MP3出力時のVBR品質（0=最高, 9=最低）。nullの場合はデフォルト（4）</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>変換結果</returns>
    Task<AudioRateChangeResult> ChangeRateAsync(
        string inputPath,
        string outputPath,
        double rate,
        bool changePitch,
        int? mp3VbrQuality = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// NAudio によるオーディオレート変更実装。
/// 入力フォーマットと同じフォーマットで出力する（MP3→MP3, OGG→OGG, WAV→WAV）。
/// 3ch以上のMP3出力要求時はOGGにフォールバックする。
/// </summary>
public sealed class AudioRateChanger : IAudioRateChanger
{
    private const float OggQuality = 0.95f;

    public async Task<AudioRateChangeResult> ChangeRateAsync(
        string inputPath, string outputPath, double rate,
        bool changePitch, int? mp3VbrQuality = null,
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

        var vbrQuality = mp3VbrQuality ?? 4;

        return await Task.Run(() =>
        {
            var actualOutputPath = outputPath;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 3ch以上のMP3出力要求時はOGGにフォールバック
                var outputExt = Path.GetExtension(outputPath).ToLowerInvariant();

                if (changePitch)
                {
                    // NC方式: リーダーを1回だけ作成し、プローブと実処理で共用する
                    var reader = CreateReader(inputPath, extension);
                    if (outputExt == ".mp3" && reader.WaveFormat.Channels > 2)
                        actualOutputPath = Path.ChangeExtension(outputPath, ".ogg");

                    var ncExt = Path.GetExtension(actualOutputPath).ToLowerInvariant();
                    var success = ncExt switch
                    {
                        ".wav" => ProcessToWav(reader, actualOutputPath, rate),
                        ".mp3" => ProcessToMp3(reader, actualOutputPath, rate, vbrQuality),
                        _ => ProcessToOgg(reader, actualOutputPath, rate),
                    };

                    return new AudioRateChangeResult(
                        success,
                        actualOutputPath != outputPath ? actualOutputPath : null);
                }
                else
                {
                    // DT方式: 3ch以上のMP3出力はOGGにフォールバック
                    if (outputExt == ".mp3")
                    {
                        using var probeReader = CreateReader(inputPath, extension);
                        if (probeReader.WaveFormat.Channels > 2)
                            actualOutputPath = Path.ChangeExtension(outputPath, ".ogg");
                    }
                    var success = ProcessWithTimeStretch(inputPath, extension, actualOutputPath, rate, vbrQuality);

                    return new AudioRateChangeResult(
                        success,
                        actualOutputPath != outputPath ? actualOutputPath : null);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                TryDeleteIncompleteOutput(actualOutputPath);
                if (actualOutputPath != outputPath)
                    TryDeleteIncompleteOutput(outputPath);
                return new AudioRateChangeResult(false);
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

            using var outputStream = File.Create(outputPath);
            EncodeOggStreaming(outputStream, source, sampleRate, channels);
            return true;
        }
    }

    /// <summary>PCMデータをMP3としてエンコード・出力する。</summary>
    private static bool ProcessToMp3(WaveStream reader, string outputPath, double rate, int vbrQuality)
    {
        using (reader)
        {
            var sampleProvider = reader.ToSampleProvider();
            ISampleProvider source = rate == 1.0 ? sampleProvider : ApplyRateChange(sampleProvider, rate);

            var sampleRate = source.WaveFormat.SampleRate;
            var channels = source.WaveFormat.Channels;

            using var encoder = new LameMp3Encoder(channels, sampleRate, vbrQuality);
            using var outputStream = File.Create(outputPath);

            const int blockFrames = 4096;
            var pcmBuffer = new float[blockFrames * channels];
            // LAME 推奨: mp3 バッファサイズ = 1.25 * num_samples + 7200
            var mp3Buffer = new byte[(int)(1.25 * blockFrames) + 7200];

            int samplesRead;
            while ((samplesRead = source.Read(pcmBuffer, 0, pcmBuffer.Length)) > 0)
            {
                var frames = samplesRead / channels;
                var bytesEncoded = encoder.Encode(pcmBuffer, frames, mp3Buffer);
                if (bytesEncoded > 0)
                    outputStream.Write(mp3Buffer, 0, bytesEncoded);
            }

            var flushed = encoder.Flush(mp3Buffer);
            if (flushed > 0)
                outputStream.Write(mp3Buffer, 0, flushed);

            // VBR Xing ヘッダーを更新（正確な再生時間情報をファイル先頭に書き込む）
            var tagBuffer = new byte[4096];
            var tagSize = encoder.GetLameTagFrame(tagBuffer);
            if (tagSize > 0)
            {
                outputStream.Seek(0, SeekOrigin.Begin);
                outputStream.Write(tagBuffer, 0, tagSize);
            }

            return true;
        }
    }

    /// <summary>ISampleProvider からストリーミングで OGG Vorbis にエンコードする。</summary>
    private static void EncodeOggStreaming(Stream output, ISampleProvider source, int sampleRate, int channels)
    {
        var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, OggQuality);
        var serial = Random.Shared.Next();
        var oggStream = new OggStream(serial);

        // ヘッダー書き出し
        var comments = new Comments();
        oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(comments));
        oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        FlushPages(oggStream, output, force: true);

        // ストリーミングエンコード
        var processingState = ProcessingState.Create(info);
        const int chunkFrames = 4096;
        var interleavedBuf = new float[chunkFrames * channels];
        var channelBufs = new float[channels][];
        for (var ch = 0; ch < channels; ch++)
            channelBufs[ch] = new float[chunkFrames];

        int samplesRead;
        while ((samplesRead = source.Read(interleavedBuf, 0, interleavedBuf.Length)) > 0)
        {
            var frames = samplesRead / channels;
            DeinterleaveChunk(interleavedBuf, samplesRead, channels, channelBufs);
            processingState.WriteData(channelBufs, frames, 0);

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

    private static void DeinterleaveChunk(float[] interleaved, int samplesRead, int channels, float[][] channelBuffers)
    {
        var frames = samplesRead / channels;
        for (var frame = 0; frame < frames; frame++)
            for (var ch = 0; ch < channels; ch++)
                channelBuffers[ch][frame] = interleaved[frame * channels + ch];
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
    /// SoundTouch によるDT方式タイムストレッチ（ピッチ維持テンポ変更）。
    /// ストリーミング処理で入力を読み込み → SoundTouch で処理 → エンコード出力。
    /// </summary>
    private static bool ProcessWithTimeStretch(
        string inputPath, string extension, string outputPath, double rate, int vbrQuality)
    {
        using var reader = CreateReader(inputPath, extension);
        var sp = reader.ToSampleProvider();
        var sampleRate = sp.WaveFormat.SampleRate;
        var channels = sp.WaveFormat.Channels;

        using var st = new soundtouch.SoundTouch();
        st.Channels = (uint)channels;
        st.SampleRate = (uint)sampleRate;
        st.Tempo = (float)rate;

        const int blockFrames = 4096;
        var inBuf = new float[blockFrames * channels];
        var outBuf = new float[blockFrames * channels];

        var outputExt = Path.GetExtension(outputPath).ToLowerInvariant();
        switch (outputExt)
        {
            case ".wav":
                WriteTimeStretchedStreamingWav(sp, st, sampleRate, channels, outputPath, inBuf, outBuf);
                break;
            case ".mp3":
                WriteTimeStretchedStreamingMp3(sp, st, sampleRate, channels, outputPath, vbrQuality, inBuf, outBuf);
                break;
            default:
                WriteTimeStretchedStreamingOgg(sp, st, sampleRate, channels, outputPath, inBuf, outBuf);
                break;
        }
        return true;
    }

    /// <summary>
    /// ISampleProvider → SoundTouch → コールバックのパイプラインを駆動する共通ループ。
    /// </summary>
    private static void FeedAndDrain(
        ISampleProvider source, soundtouch.SoundTouch st,
        int channels, float[] inBuf, float[] outBuf,
        Action<float[], int> onChunkReceived)
    {
        var maxOutFrames = (uint)(outBuf.Length / channels);
        int samplesRead;
        while ((samplesRead = source.Read(inBuf, 0, inBuf.Length)) > 0)
        {
            st.PutSamples(inBuf, (uint)(samplesRead / channels));
            uint received;
            while ((received = st.ReceiveSamples(outBuf, maxOutFrames)) > 0)
                onChunkReceived(outBuf, (int)(received * channels));
        }
        st.Flush();
        uint flushed;
        while ((flushed = st.ReceiveSamples(outBuf, maxOutFrames)) > 0)
            onChunkReceived(outBuf, (int)(flushed * channels));
    }

    private static void WriteTimeStretchedStreamingWav(
        ISampleProvider source, soundtouch.SoundTouch st,
        int sampleRate, int channels, string outputPath,
        float[] inBuf, float[] outBuf)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        using var writer = new WaveFileWriter(outputPath, format);
        FeedAndDrain(source, st, channels, inBuf, outBuf,
            (buf, count) => writer.WriteSamples(buf, 0, count));
    }

    private static void WriteTimeStretchedStreamingMp3(
        ISampleProvider source, soundtouch.SoundTouch st,
        int sampleRate, int channels, string outputPath, int vbrQuality,
        float[] inBuf, float[] outBuf)
    {
        using var encoder = new LameMp3Encoder(channels, sampleRate, vbrQuality);
        using var outputStream = File.Create(outputPath);

        const int blockFrames = 4096;
        var mp3Buffer = new byte[(int)(1.25 * blockFrames) + 7200];

        FeedAndDrain(source, st, channels, inBuf, outBuf, (buf, count) =>
        {
            var frames = count / channels;
            var bytesEncoded = encoder.Encode(buf, frames, mp3Buffer);
            if (bytesEncoded > 0)
                outputStream.Write(mp3Buffer, 0, bytesEncoded);
        });

        var flushed = encoder.Flush(mp3Buffer);
        if (flushed > 0)
            outputStream.Write(mp3Buffer, 0, flushed);

        var tagBuffer = new byte[4096];
        var tagSize = encoder.GetLameTagFrame(tagBuffer);
        if (tagSize > 0)
        {
            outputStream.Seek(0, SeekOrigin.Begin);
            outputStream.Write(tagBuffer, 0, tagSize);
        }
    }

    private static void WriteTimeStretchedStreamingOgg(
        ISampleProvider source, soundtouch.SoundTouch st,
        int sampleRate, int channels, string outputPath,
        float[] inBuf, float[] outBuf)
    {
        using var outputStream = File.Create(outputPath);

        var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, OggQuality);
        var serial = Random.Shared.Next();
        var oggStream = new OggStream(serial);

        oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(new Comments()));
        oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        FlushPages(oggStream, outputStream, force: true);

        var processingState = ProcessingState.Create(info);
        var maxOutFrames = outBuf.Length / channels;
        var channelBufs = new float[channels][];
        for (var ch = 0; ch < channels; ch++)
            channelBufs[ch] = new float[maxOutFrames];

        FeedAndDrain(source, st, channels, inBuf, outBuf, (buf, count) =>
        {
            var frames = count / channels;
            DeinterleaveChunk(buf, count, channels, channelBufs);
            processingState.WriteData(channelBufs, frames, 0);

            while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
            {
                oggStream.PacketIn(packet);
                FlushPages(oggStream, outputStream, force: false);
            }
        });

        processingState.WriteEndOfStream();
        while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
        {
            oggStream.PacketIn(packet);
            FlushPages(oggStream, outputStream, force: false);
        }
        FlushPages(oggStream, outputStream, force: true);
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
