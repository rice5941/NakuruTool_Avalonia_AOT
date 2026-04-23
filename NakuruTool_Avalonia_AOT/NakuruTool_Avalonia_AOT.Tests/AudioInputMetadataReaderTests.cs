using System;
using System.Buffers.Binary;
using System.IO;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// AudioInputMetadataReader のユニットテスト。
/// 各フォーマットのバイナリをメモリ上で手組し、ReadFromStream を検証する。
/// </summary>
public class AudioInputMetadataReaderTests
{
    // ─────────────────────────────────────────────────────────────
    // WAV
    // ─────────────────────────────────────────────────────────────
    private static byte[] BuildWav(ushort audioFormat, ushort channels, uint sampleRate)
    {
        // fmt chunk 16 bytes + data chunk header 8 bytes + 4 dummy bytes of PCM
        // full file:
        // RIFF(4) size(4) WAVE(4)
        // fmt (4) chunkSize=16 (4) audioFormat(2) channels(2) sampleRate(4)
        //   byteRate(4) blockAlign(2) bitsPerSample(2)
        // data(4) dataSize(4) payload(4)
        const int dataSize = 4;
        const int fmtChunkSize = 16;
        int riffSize = 4 + (8 + fmtChunkSize) + (8 + dataSize);
        byte[] bytes = new byte[8 + riffSize];
        int i = 0;
        bytes[i++] = (byte)'R'; bytes[i++] = (byte)'I'; bytes[i++] = (byte)'F'; bytes[i++] = (byte)'F';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i, 4), (uint)riffSize); i += 4;
        bytes[i++] = (byte)'W'; bytes[i++] = (byte)'A'; bytes[i++] = (byte)'V'; bytes[i++] = (byte)'E';

        bytes[i++] = (byte)'f'; bytes[i++] = (byte)'m'; bytes[i++] = (byte)'t'; bytes[i++] = (byte)' ';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i, 4), fmtChunkSize); i += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i, 2), audioFormat); i += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i, 2), channels); i += 2;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i, 4), sampleRate); i += 4;
        uint byteRate = sampleRate * channels * 2u;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i, 4), byteRate); i += 4;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i, 2), (ushort)(channels * 2)); i += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i, 2), 16); i += 2;

        bytes[i++] = (byte)'d'; bytes[i++] = (byte)'a'; bytes[i++] = (byte)'t'; bytes[i++] = (byte)'a';
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i, 4), dataSize); i += 4;
        // payload zeros
        return bytes;
    }

    [Fact]
    public void Wav_44100_Stereo()
    {
        var bytes = BuildWav(1, 2, 44100);
        using var ms = new MemoryStream(bytes);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".wav");
        Assert.Equal(2, meta.Channels);
        Assert.Equal(44100, meta.SampleRate);
    }

    [Fact]
    public void Wav_48000_Mono()
    {
        var bytes = BuildWav(1, 1, 48000);
        using var ms = new MemoryStream(bytes);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".wav");
        Assert.Equal(1, meta.Channels);
        Assert.Equal(48000, meta.SampleRate);
    }

    [Fact]
    public void Wav_IeeeFloat_Accepted()
    {
        var bytes = BuildWav(3, 2, 44100);
        using var ms = new MemoryStream(bytes);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".wav");
        Assert.Equal(2, meta.Channels);
    }

    [Fact]
    public void Wav_UnsupportedFormat_Throws()
    {
        var bytes = BuildWav(7, 2, 44100); // 未対応 audioFormat
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() =>
            AudioInputMetadataReader.ReadFromStream(ms, ".wav"));
    }

    [Fact]
    public void Wav_NotRiff_Throws()
    {
        var bytes = new byte[12];
        // 全ゼロ → "RIFF" でない
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() =>
            AudioInputMetadataReader.ReadFromStream(ms, ".wav"));
    }

    // ─────────────────────────────────────────────────────────────
    // MP3
    // ─────────────────────────────────────────────────────────────
    private static byte[] BuildMp3(
        int mpegVersionBits,   // 11=MPEG1, 10=MPEG2, 00=MPEG2.5
        int sampleRateIndex,   // 0..2
        bool mono,
        bool withId3 = false)
    {
        // MPEG frame header 4 bytes:
        // 0: 0xFF
        // 1: 0b111_VV_LL_P where VV=versionBits, LL=layer (01=L3), P=protection bit
        // 2: bitrateIndex(4)=0b0001 layerRateIdx(2)=sampleRateIndex padding(1)=0 priv(1)=0
        // 3: channelMode(2) modeExt(2) copyright(1) original(1) emphasis(2)
        byte b0 = 0xFF;
        byte b1 = (byte)(0xE0 | ((mpegVersionBits & 0x3) << 3) | (0x01 << 1)); // Layer 3
        byte b2 = (byte)((0x01 << 4) | ((sampleRateIndex & 0x3) << 2)); // bitrate index 0001
        byte b3 = (byte)((mono ? 0b11 : 0b00) << 6);

        byte[] frame = { b0, b1, b2, b3, 0, 0, 0, 0 };

        if (!withId3)
        {
            return frame;
        }

        // ID3v2.3 header + 5 bytes padding
        const int tagPayload = 5;
        byte[] tag = new byte[10 + tagPayload];
        tag[0] = (byte)'I'; tag[1] = (byte)'D'; tag[2] = (byte)'3';
        tag[3] = 3; tag[4] = 0; tag[5] = 0; // version 2.3, no flags
        // syncsafe size = tagPayload (小さいので 1 バイト）
        tag[6] = 0; tag[7] = 0; tag[8] = 0; tag[9] = (byte)tagPayload;
        // tag[10..15] = zeros (padding)
        byte[] result = new byte[tag.Length + frame.Length];
        Buffer.BlockCopy(tag, 0, result, 0, tag.Length);
        Buffer.BlockCopy(frame, 0, result, tag.Length, frame.Length);
        return result;
    }

    [Fact]
    public void Mp3_Mpeg1_44100_Stereo_WithId3()
    {
        var bytes = BuildMp3(0b11, 0, mono: false, withId3: true);
        using var ms = new MemoryStream(bytes);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".mp3");
        Assert.Equal(2, meta.Channels);
        Assert.Equal(44100, meta.SampleRate);
    }

    [Fact]
    public void Mp3_Mpeg2_22050_Mono()
    {
        var bytes = BuildMp3(0b10, 0, mono: true);
        using var ms = new MemoryStream(bytes);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".mp3");
        Assert.Equal(1, meta.Channels);
        Assert.Equal(22050, meta.SampleRate);
    }

    [Fact]
    public void Mp3_NoSyncWord_Throws()
    {
        var bytes = new byte[64];
        // 全ゼロ → sync word 無し
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() =>
            AudioInputMetadataReader.ReadFromStream(ms, ".mp3"));
    }

    // ─────────────────────────────────────────────────────────────
    // OGG
    // ─────────────────────────────────────────────────────────────
    private static byte[] BuildOggPage(byte[] payload)
    {
        // segment_table: 255 バイトずつ + 最後のセグメント
        int segCount = payload.Length / 255 + 1;
        byte[] page = new byte[27 + segCount + payload.Length];
        int i = 0;
        page[i++] = (byte)'O'; page[i++] = (byte)'g'; page[i++] = (byte)'g'; page[i++] = (byte)'S';
        page[i++] = 0; // version
        page[i++] = 0x02; // header_type = first page of logical bitstream
        // granule position (8 bytes)
        for (int k = 0; k < 8; k++) page[i++] = 0;
        // serial number (4)
        for (int k = 0; k < 4; k++) page[i++] = 0;
        // page sequence (4)
        for (int k = 0; k < 4; k++) page[i++] = 0;
        // checksum (4) - not verified by parser
        for (int k = 0; k < 4; k++) page[i++] = 0;
        page[i++] = (byte)segCount;
        int remaining = payload.Length;
        for (int s = 0; s < segCount; s++)
        {
            byte lac = remaining >= 255 ? (byte)255 : (byte)remaining;
            page[i++] = lac;
            remaining -= lac;
        }
        Buffer.BlockCopy(payload, 0, page, i, payload.Length);
        return page;
    }

    private static byte[] BuildVorbisIdentPayload(byte channels, uint sampleRate)
    {
        // 30 bytes: 0x01 "vorbis" + u32 version + u8 channels + u32 rate + ...
        byte[] p = new byte[30];
        p[0] = 0x01;
        p[1] = (byte)'v'; p[2] = (byte)'o'; p[3] = (byte)'r';
        p[4] = (byte)'b'; p[5] = (byte)'i'; p[6] = (byte)'s';
        // 7..10: vorbis_version = 0
        p[11] = channels;
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(12, 4), sampleRate);
        // 残りは bitrate など、ここでは 0 で十分
        return p;
    }

    private static byte[] BuildOpusHeadPayload(byte channels, uint inputSampleRate)
    {
        // 19 bytes min: "OpusHead" + ver + ch + preSkip(u16) + inputRate(u32) + gain(i16) + mapping(u8)
        byte[] p = new byte[19];
        ReadOnlySpan<byte> magic = "OpusHead"u8;
        magic.CopyTo(p);
        p[8] = 1; // version
        p[9] = channels;
        BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(10, 2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(12, 4), inputSampleRate);
        return p;
    }

    [Fact]
    public void Ogg_Vorbis_44100_Stereo()
    {
        var payload = BuildVorbisIdentPayload(2, 44100);
        var page = BuildOggPage(payload);
        using var ms = new MemoryStream(page);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".ogg");
        Assert.Equal(2, meta.Channels);
        Assert.Equal(44100, meta.SampleRate);
    }

    [Fact]
    public void Ogg_Opus_48000_Stereo()
    {
        var payload = BuildOpusHeadPayload(2, 48000);
        var page = BuildOggPage(payload);
        using var ms = new MemoryStream(page);
        var meta = AudioInputMetadataReader.ReadFromStream(ms, ".ogg");
        Assert.Equal(2, meta.Channels);
        Assert.Equal(48000, meta.SampleRate);
    }

    [Fact]
    public void Ogg_NotOggS_Throws()
    {
        var bytes = new byte[64];
        using var ms = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() =>
            AudioInputMetadataReader.ReadFromStream(ms, ".ogg"));
    }

    [Fact]
    public void Ogg_UnsupportedCodec_Throws()
    {
        byte[] payload = new byte[30];
        // FLAC-in-OGG っぽいバイト列（0x7F + "FLAC"）
        payload[0] = 0x7F;
        payload[1] = (byte)'F'; payload[2] = (byte)'L'; payload[3] = (byte)'A'; payload[4] = (byte)'C';
        var page = BuildOggPage(payload);
        using var ms = new MemoryStream(page);
        Assert.Throws<InvalidDataException>(() =>
            AudioInputMetadataReader.ReadFromStream(ms, ".ogg"));
    }

    // ─────────────────────────────────────────────────────────────
    // その他
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void UnsupportedExtension_Throws()
    {
        using var ms = new MemoryStream(new byte[16]);
        Assert.Throws<NotSupportedException>(() =>
            AudioInputMetadataReader.ReadFromStream(ms, ".flac"));
    }
}
