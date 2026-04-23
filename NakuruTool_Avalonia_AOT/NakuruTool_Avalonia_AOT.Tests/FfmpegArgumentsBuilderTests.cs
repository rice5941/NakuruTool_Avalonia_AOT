using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// FfmpegArgumentsBuilder の純関数テスト（設計書 §10.2）。
/// </summary>
public class FfmpegArgumentsBuilderTests
{
    // ───────────────────────────────────────────
    // SplitAtempo
    // ───────────────────────────────────────────

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(-1.0)]
    public void SplitAtempo_NonPositive_Throws(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FfmpegArgumentsBuilder.SplitAtempo(rate));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void SplitAtempo_WithinRange_ReturnsSingleElement(double rate)
    {
        var result = FfmpegArgumentsBuilder.SplitAtempo(rate);
        Assert.Single(result);
        Assert.Equal(rate, result[0], precision: 12);
    }

    [Fact]
    public void SplitAtempo_Quarter_ReturnsTwoStepsProductMatches()
    {
        var result = FfmpegArgumentsBuilder.SplitAtempo(0.25);
        Assert.Equal(2, result.Count);
        double product = 1.0;
        foreach (var v in result) product *= v;
        Assert.InRange(Math.Abs(product - 0.25), 0.0, 1e-9);
    }

    [Fact]
    public void SplitAtempo_Four_ReturnsTwoStepsNearTwo()
    {
        var result = FfmpegArgumentsBuilder.SplitAtempo(4.0);
        Assert.Equal(2, result.Count);
        Assert.InRange(Math.Abs(result[0] - 2.0), 0.0, 1e-9);
        Assert.InRange(Math.Abs(result[1] - 2.0), 0.0, 1e-9);
        double product = result[0] * result[1];
        Assert.InRange(Math.Abs(product - 4.0), 0.0, 1e-9);
    }

    [Fact]
    public void SplitAtempo_Ten_ReturnsAtLeastFourStepsProductMatches()
    {
        var result = FfmpegArgumentsBuilder.SplitAtempo(10.0);
        Assert.True(result.Count >= 4, $"expected n>=4, got {result.Count}");
        double product = 1.0;
        foreach (var v in result) product *= v;
        Assert.InRange(Math.Abs(product - 10.0), 0.0, 1e-9);
    }

    // ───────────────────────────────────────────
    // BuildDtFilter
    // ───────────────────────────────────────────

    [Fact]
    public void BuildDtFilter_OnePointFive_SingleAtempo()
    {
        Assert.Equal("atempo=1.500000", FfmpegArgumentsBuilder.BuildDtFilter(1.5));
    }

    [Fact]
    public void BuildDtFilter_TwoPointFive_TwoAtempoChain()
    {
        var filter = FfmpegArgumentsBuilder.BuildDtFilter(2.5);
        // 2.5 は [0.5,2.0] の外、n=Max(2, ceil(|log2(2.5)|))=Max(2,2)=2、step=sqrt(2.5)≒1.581139
        Assert.Equal("atempo=1.581139,atempo=1.581139", filter);
    }

    [Fact]
    public void BuildDtFilter_LocaleIndependent()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var filter = FfmpegArgumentsBuilder.BuildDtFilter(1.5);
            Assert.Equal("atempo=1.500000", filter);
            Assert.Contains(".", filter);
            Assert.DoesNotContain(",", filter);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    // ───────────────────────────────────────────
    // BuildNcFilter
    // ───────────────────────────────────────────

    [Fact]
    public void BuildNcFilter_Speedup125()
    {
        Assert.Equal("asetrate=55125,aresample=44100",
            FfmpegArgumentsBuilder.BuildNcFilter(44100, 1.25));
    }

    [Fact]
    public void BuildNcFilter_Slowdown075()
    {
        Assert.Equal("asetrate=36000,aresample=48000",
            FfmpegArgumentsBuilder.BuildNcFilter(48000, 0.75));
    }

    [Fact]
    public void BuildNcFilter_FractionalRounded()
    {
        // 44100 * 1.1 = 48510
        Assert.Equal("asetrate=48510,aresample=44100",
            FfmpegArgumentsBuilder.BuildNcFilter(44100, 1.1));
    }

    // ───────────────────────────────────────────
    // BuildEncoderArgs
    // ───────────────────────────────────────────

    [Fact]
    public void BuildEncoderArgs_Wav()
    {
        var args = FfmpegArgumentsBuilder.BuildEncoderArgs(".wav", 4, 8);
        Assert.Equal(new[] { "-c:a", "pcm_s16le", "-f", "wav" }, args);
    }

    [Fact]
    public void BuildEncoderArgs_Mp3_Quality0()
    {
        var args = FfmpegArgumentsBuilder.BuildEncoderArgs(".mp3", 0, 8);
        Assert.Equal(new[] { "-c:a", "libmp3lame", "-q:a", "0" }, args);
    }

    [Fact]
    public void BuildEncoderArgs_Ogg_Quality7()
    {
        var args = FfmpegArgumentsBuilder.BuildEncoderArgs(".ogg", 4, 7);
        Assert.Equal(new[] { "-c:a", "libvorbis", "-q:a", "7" }, args);
    }

    [Fact]
    public void BuildEncoderArgs_Unsupported_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            FfmpegArgumentsBuilder.BuildEncoderArgs(".flac", 4, 8));
    }

    // ───────────────────────────────────────────
    // BuildFfmpegArgs
    // ───────────────────────────────────────────

    [Fact]
    public void BuildFfmpegArgs_PrefixAndOrder_Wav()
    {
        var args = FfmpegArgumentsBuilder.BuildFfmpegArgs(
            "in.wav", "out.wav", "atempo=1.500000", ".wav", 4, 8);
        var list = new List<string>(args);

        // 先頭プレフィックス
        Assert.Equal("-hide_banner", list[0]);
        Assert.Equal("-nostdin", list[1]);
        Assert.Equal("-y", list[2]);
        Assert.Equal("-loglevel", list[3]);
        Assert.Equal("error", list[4]);

        // -i <input>
        int iIdx = list.IndexOf("-i");
        Assert.True(iIdx >= 0);
        Assert.Equal("in.wav", list[iIdx + 1]);

        // -vn -sn -dn
        Assert.Equal("-vn", list[iIdx + 2]);
        Assert.Equal("-sn", list[iIdx + 3]);
        Assert.Equal("-dn", list[iIdx + 4]);

        // -map_metadata -1
        Assert.Equal("-map_metadata", list[iIdx + 5]);
        Assert.Equal("-1", list[iIdx + 6]);

        // -filter:a <filter>
        Assert.Equal("-filter:a", list[iIdx + 7]);
        Assert.Equal("atempo=1.500000", list[iIdx + 8]);

        // encoderArgs → 末尾 output
        int fIdx = iIdx + 8;
        Assert.Equal("-c:a", list[fIdx + 1]);
        Assert.Equal("pcm_s16le", list[fIdx + 2]);
        Assert.Equal("-f", list[fIdx + 3]);
        Assert.Equal("wav", list[fIdx + 4]);
        Assert.Equal("out.wav", list[list.Count - 1]);
    }

    [Fact]
    public void BuildFfmpegArgs_Mp3_UsesLameQuality()
    {
        var args = FfmpegArgumentsBuilder.BuildFfmpegArgs(
            "in.mp3", "out.mp3", "atempo=1.500000", ".mp3", 2, 8);
        var list = new List<string>(args);
        Assert.Contains("libmp3lame", list);
        int qIdx = list.IndexOf("-q:a");
        Assert.True(qIdx >= 0);
        Assert.Equal("2", list[qIdx + 1]);
        Assert.Equal("out.mp3", list[list.Count - 1]);
    }
}
