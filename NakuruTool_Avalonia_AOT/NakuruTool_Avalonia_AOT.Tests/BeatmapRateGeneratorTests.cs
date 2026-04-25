using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

public class BeatmapRateGeneratorTests
{
    [Fact]
    public void BuildAudioFileName_Mp3Input_PreservesMp3Extension()
    {
        var result = BeatmapRateGenerator.BuildAudioFileName("audio.mp3", 1.5, changePitch: false);

        Assert.Equal("audio_1.500x_dt.mp3", result);
    }

    [Fact]
    public void BuildAudioFileName_OggInput_RemainsOgg()
    {
        var result = BeatmapRateGenerator.BuildAudioFileName("audio.ogg", 1.5, changePitch: true);

        Assert.Equal("audio_1.500x_nc.ogg", result);
    }

    [Theory]
    [InlineData("normal-hitnormal.wav")]
    [InlineData("normal-hitclap.ogg")]
    [InlineData("normal-hitfinish.mp3")]
    [InlineData("normal-hitwhistle.wav")]
    [InlineData("normal-slidertick.wav")]
    [InlineData("normal-sliderslide.wav")]
    [InlineData("normal-sliderwhistle.wav")]
    [InlineData("soft-hitnormal.wav")]
    [InlineData("soft-hitclap.ogg")]
    [InlineData("soft-hitfinish.wav")]
    [InlineData("soft-hitwhistle.wav")]
    [InlineData("soft-slidertick.ogg")]
    [InlineData("soft-sliderslide.wav")]
    [InlineData("soft-sliderwhistle.wav")]
    [InlineData("drum-hitnormal.wav")]
    [InlineData("drum-hitclap.wav")]
    [InlineData("drum-hitfinish.wav")]
    [InlineData("drum-hitwhistle.wav")]
    [InlineData("drum-slidertick.wav")]
    [InlineData("drum-sliderslide.wav")]
    [InlineData("drum-sliderwhistle.wav")]
    [InlineData("NORMAL-HITNORMAL.WAV")]
    public void IsDefaultHitsoundFile_DefaultHitsound_ReturnsTrue(string fileName)
    {
        Assert.True(BeatmapRateGenerator.IsDefaultHitsoundFile(fileName));
    }

    [Theory]
    [InlineData("audio.mp3")]
    [InlineData("custom-hitnormal.wav")]
    [InlineData("hitnormal.wav")]
    [InlineData("normal-hit.wav")]
    [InlineData("soft-hitnormal2.ogg")]
    [InlineData("")]
    public void IsDefaultHitsoundFile_NonDefaultHitsound_ReturnsFalse(string fileName)
    {
        Assert.False(BeatmapRateGenerator.IsDefaultHitsoundFile(fileName));
    }
}