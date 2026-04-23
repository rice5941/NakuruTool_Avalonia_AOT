using System;
using System.Buffers.Binary;
using System.IO;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// 純 C# による WAV / MP3 / OGG (Vorbis / Opus) のメタデータ（チャンネル数・サンプルレート）抽出。
/// リフレクションや動的コード生成を一切用いないため NativeAOT 安全。
/// </summary>
internal static class AudioInputMetadataReader
{
    private const int MinChannels = 1;
    private const int MaxChannels = 8;
    private const int MinSampleRate = 8000;
    private const int MaxSampleRate = 192000;

    /// <summary>
    /// ファイルパスから <see cref="AudioInputMetadata"/> を読み取る。
    /// </summary>
    /// <exception cref="NotSupportedException">拡張子が未対応。</exception>
    /// <exception cref="InvalidDataException">データが破損・未対応フォーマット。</exception>
    /// <exception cref="IOException">I/O エラー。</exception>
    internal static AudioInputMetadata ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string extLower = Path.GetExtension(path).ToLowerInvariant();
        using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: false);
        return ReadFromStream(fs, extLower);
    }

    /// <summary>
    /// Seek 可能なストリームから <see cref="AudioInputMetadata"/> を読み取る。
    /// </summary>
    internal static AudioInputMetadata ReadFromStream(Stream stream, string extensionLower)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable.", nameof(stream));
        }
        return extensionLower switch
        {
            ".wav" => ReadWav(stream),
            ".mp3" => ReadMp3(stream),
            ".ogg" => ReadOgg(stream),
            _ => throw new NotSupportedException($"Unsupported audio extension: {extensionLower}"),
        };
    }

    // ─────────────────────────────────────────────────────────────
    // WAV
    // ─────────────────────────────────────────────────────────────
    private static AudioInputMetadata ReadWav(Stream stream)
    {
        Span<byte> header = stackalloc byte[12];
        ReadExact(stream, header);

        if (!(header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'))
        {
            throw new InvalidDataException("Not a RIFF file.");
        }
        if (!(header[8] == (byte)'W' && header[9] == (byte)'A' && header[10] == (byte)'V' && header[11] == (byte)'E'))
        {
            throw new InvalidDataException("Not a WAVE file.");
        }

        Span<byte> chunkHeader = stackalloc byte[8];
        // 破損した巨大サイズで無限ループにならないよう安全弁。
        const int MaxChunkScan = 64;
        for (int i = 0; i < MaxChunkScan; i++)
        {
            int read = stream.Read(chunkHeader);
            if (read < 8)
            {
                throw new InvalidDataException("WAV: fmt chunk not found.");
            }
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.Slice(4, 4));

            if (chunkHeader[0] == (byte)'f' && chunkHeader[1] == (byte)'m' &&
                chunkHeader[2] == (byte)'t' && chunkHeader[3] == (byte)' ')
            {
                if (chunkSize < 16)
                {
                    throw new InvalidDataException("WAV: fmt chunk too small.");
                }
                Span<byte> fmt = stackalloc byte[16];
                ReadExact(stream, fmt);
                ushort audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(0, 2));
                ushort numChannels = BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(2, 2));
                uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt.Slice(4, 4));

                if (audioFormat != 1 && audioFormat != 3 && audioFormat != 0xFFFE)
                {
                    throw new InvalidDataException($"WAV: unsupported audioFormat 0x{audioFormat:X4}.");
                }
                ValidateRanges(numChannels, (int)sampleRate, "WAV");
                return new AudioInputMetadata(numChannels, (int)sampleRate);
            }

            // スキップ（2 バイト境界パディング）
            long skip = chunkSize + (chunkSize & 1);
            SeekForward(stream, skip);
        }
        throw new InvalidDataException("WAV: fmt chunk not found within limit.");
    }

    // ─────────────────────────────────────────────────────────────
    // MP3
    // ─────────────────────────────────────────────────────────────
    private static readonly int[] s_mpeg1SampleRates = { 44100, 48000, 32000 };
    private static readonly int[] s_mpeg2SampleRates = { 22050, 24000, 16000 };
    private static readonly int[] s_mpeg25SampleRates = { 11025, 12000, 8000 };

    private static AudioInputMetadata ReadMp3(Stream stream)
    {
        // ID3v2 タグの検出とスキップ
        Span<byte> id3 = stackalloc byte[10];
        ReadExact(stream, id3);
        long frameSearchStart;
        if (id3[0] == (byte)'I' && id3[1] == (byte)'D' && id3[2] == (byte)'3')
        {
            // syncsafe 28-bit
            int tagSize = ((id3[6] & 0x7F) << 21)
                        | ((id3[7] & 0x7F) << 14)
                        | ((id3[8] & 0x7F) << 7)
                        | (id3[9] & 0x7F);
            bool hasFooter = (id3[5] & 0x10) != 0;
            long skip = tagSize + (hasFooter ? 10 : 0);
            SeekForward(stream, skip);
            frameSearchStart = 10 + skip;
        }
        else
        {
            // ID3 では無かったので巻き戻す
            stream.Seek(-id3.Length, SeekOrigin.Current);
            frameSearchStart = 0;
        }

        // ここから最大 64KB 前方スキャン。
        const int ScanLimit = 64 * 1024;
        // 4 バイトずつスライディングで読む
        byte[] buf = new byte[ScanLimit];
        int total = 0;
        while (total < buf.Length)
        {
            int r = stream.Read(buf, total, buf.Length - total);
            if (r <= 0) break;
            total += r;
        }
        if (total < 4)
        {
            throw new InvalidDataException("MP3: insufficient data to locate frame header.");
        }

        for (int i = 0; i + 4 <= total; i++)
        {
            byte b0 = buf[i];
            byte b1 = buf[i + 1];
            byte b2 = buf[i + 2];
            byte b3 = buf[i + 3];

            // sync word: 11 bits (0xFF, 0xE0 mask)
            if (b0 != 0xFF) continue;
            if ((b1 & 0xE0) != 0xE0) continue;

            int versionBits = (b1 >> 3) & 0x03;
            int layerBits = (b1 >> 1) & 0x03;
            if (versionBits == 0x01) continue; // reserved
            if (layerBits == 0x00) continue;    // reserved

            int bitrateIndex = (b2 >> 4) & 0x0F;
            if (bitrateIndex == 0 || bitrateIndex == 0x0F) continue;

            int sampleRateIndex = (b2 >> 2) & 0x03;
            if (sampleRateIndex == 3) continue;

            int sampleRate = versionBits switch
            {
                0b11 => s_mpeg1SampleRates[sampleRateIndex],  // MPEG1
                0b10 => s_mpeg2SampleRates[sampleRateIndex],  // MPEG2
                0b00 => s_mpeg25SampleRates[sampleRateIndex], // MPEG2.5
                _ => -1,
            };
            if (sampleRate <= 0) continue;

            int channelMode = (b3 >> 6) & 0x03;
            int channels = channelMode == 0b11 ? 1 : 2;

            ValidateRanges(channels, sampleRate, "MP3");
            return new AudioInputMetadata(channels, sampleRate);
        }

        _ = frameSearchStart; // 使わないが将来的な診断用
        throw new InvalidDataException("MP3: MPEG audio frame header not found.");
    }

    // ─────────────────────────────────────────────────────────────
    // OGG (Vorbis / Opus)
    // ─────────────────────────────────────────────────────────────
    private static AudioInputMetadata ReadOgg(Stream stream)
    {
        // OGG page header: 27 bytes.
        Span<byte> pageHeader = stackalloc byte[27];
        ReadExact(stream, pageHeader);

        if (!(pageHeader[0] == (byte)'O' && pageHeader[1] == (byte)'g' &&
              pageHeader[2] == (byte)'g' && pageHeader[3] == (byte)'S'))
        {
            throw new InvalidDataException("OGG: capture pattern mismatch.");
        }

        int segmentCount = pageHeader[26];
        if (segmentCount == 0)
        {
            throw new InvalidDataException("OGG: empty segment table.");
        }

        Span<byte> segmentTable = stackalloc byte[segmentCount]; // segmentCount <= 255
        ReadExact(stream, segmentTable);
        int payloadLength = 0;
        for (int i = 0; i < segmentCount; i++)
        {
            payloadLength += segmentTable[i];
        }
        if (payloadLength < 7)
        {
            throw new InvalidDataException("OGG: first page payload too small.");
        }

        // Vorbis ident は 30 バイト、Opus ident は 19 バイト以上読めれば OK。
        int readLen = Math.Min(payloadLength, 64);
        Span<byte> payload = stackalloc byte[64];
        payload = payload.Slice(0, readLen);
        ReadExact(stream, payload);

        // Vorbis identification header: packet_type=0x01 + "vorbis"
        if (payload[0] == 0x01 &&
            payload.Length >= 30 &&
            payload[1] == (byte)'v' && payload[2] == (byte)'o' && payload[3] == (byte)'r' &&
            payload[4] == (byte)'b' && payload[5] == (byte)'i' && payload[6] == (byte)'s')
        {
            // 7: vorbis_version u32 LE, 11: audio_channels u8, 12: audio_sample_rate u32 LE
            byte audioChannels = payload[11];
            uint audioSampleRate = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(12, 4));
            ValidateRanges(audioChannels, (int)audioSampleRate, "OGG Vorbis");
            return new AudioInputMetadata(audioChannels, (int)audioSampleRate);
        }

        // Opus identification header: "OpusHead"
        if (payload.Length >= 19 &&
            payload[0] == (byte)'O' && payload[1] == (byte)'p' && payload[2] == (byte)'u' && payload[3] == (byte)'s' &&
            payload[4] == (byte)'H' && payload[5] == (byte)'e' && payload[6] == (byte)'a' && payload[7] == (byte)'d')
        {
            // 8: version u8, 9: channel_count u8, 10: pre_skip u16 LE, 12: input_sample_rate u32 LE
            byte channelCount = payload[9];
            uint inputSampleRate = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(12, 4));
            ValidateRanges(channelCount, (int)inputSampleRate, "OGG Opus");
            return new AudioInputMetadata(channelCount, (int)inputSampleRate);
        }

        throw new InvalidDataException("OGG: unsupported codec (not Vorbis or Opus).");
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────
    private static void ValidateRanges(int channels, int sampleRate, string format)
    {
        if (channels < MinChannels || channels > MaxChannels)
        {
            throw new InvalidDataException(
                $"{format}: invalid channel count {channels} (must be {MinChannels}-{MaxChannels}).");
        }
        if (sampleRate < MinSampleRate || sampleRate > MaxSampleRate)
        {
            throw new InvalidDataException(
                $"{format}: invalid sample rate {sampleRate} (must be {MinSampleRate}-{MaxSampleRate}).");
        }
    }

    private static void ReadExact(Stream stream, Span<byte> buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = stream.Read(buffer.Slice(read));
            if (n <= 0)
            {
                throw new InvalidDataException("Unexpected end of stream.");
            }
            read += n;
        }
    }

    private static void SeekForward(Stream stream, long count)
    {
        if (count <= 0) return;
        if (stream.CanSeek)
        {
            long target = stream.Position + count;
            if (target > stream.Length)
            {
                throw new InvalidDataException("Attempted to seek beyond end of stream.");
            }
            stream.Seek(count, SeekOrigin.Current);
        }
        else
        {
            Span<byte> scratch = stackalloc byte[1024];
            long remaining = count;
            while (remaining > 0)
            {
                int take = (int)Math.Min(remaining, scratch.Length);
                int n = stream.Read(scratch.Slice(0, take));
                if (n <= 0) throw new InvalidDataException("Unexpected end of stream.");
                remaining -= n;
            }
        }
    }
}
