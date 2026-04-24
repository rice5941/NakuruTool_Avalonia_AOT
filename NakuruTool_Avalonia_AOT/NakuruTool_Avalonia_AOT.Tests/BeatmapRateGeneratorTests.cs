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
}