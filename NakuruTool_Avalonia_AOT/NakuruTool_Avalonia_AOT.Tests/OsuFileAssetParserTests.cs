using System;
using System.Collections.Generic;
using System.IO;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="OsuFileAssetParser"/> の回帰テスト。
/// .osu / .osb を一時フォルダに作成し、<c>Parse</c> の戻り値
/// <see cref="OsuReferencedAssets"/> を検証する。
/// </summary>
public class OsuFileAssetParserTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OsuFileAssetParser _sut;

    public OsuFileAssetParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_OsuFileAssetParserTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new OsuFileAssetParser();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    private string WriteOsuFile(IEnumerable<string>? eventsLines = null)
    {
        var lines = new List<string>
        {
            "osu file format v14",
            string.Empty,
            "[General]",
            "AudioFilename: audio.mp3",
            "Mode:3",
            string.Empty,
            "[Metadata]",
            "Title:t",
            "Artist:a",
            "Creator:c",
            "Version:v",
            string.Empty,
            "[Difficulty]",
            "HPDrainRate:5",
            "CircleSize:7",
            "OverallDifficulty:8",
            "ApproachRate:5",
            "SliderMultiplier:1.4",
            "SliderTickRate:1",
            string.Empty,
            "[Events]",
        };
        if (eventsLines is not null) lines.AddRange(eventsLines);
        lines.Add(string.Empty);
        lines.Add("[TimingPoints]");
        lines.Add(string.Empty);
        lines.Add("[HitObjects]");

        var path = Path.Combine(_tempDir, "test_" + Guid.NewGuid().ToString("N") + ".osu");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void Parse_NumericFiveSample_AddsToSampleAudioFiles()
    {
        var osu = WriteOsuFile(eventsLines: ["5,929,0,\"normal-hitnormal10.ogg\",100"]);

        var assets = _sut.Parse(osu);

        Assert.Contains("normal-hitnormal10.ogg", assets.SampleAudioFiles);
    }

    [Fact]
    public void Parse_StringSample_StillAddsToSampleAudioFiles()
    {
        var osu = WriteOsuFile(eventsLines: ["Sample,929,0,\"foo.ogg\",100"]);

        var assets = _sut.Parse(osu);

        Assert.Contains("foo.ogg", assets.SampleAudioFiles);
    }

    [Fact]
    public void Parse_NumericFourSprite_AddsToNonAudio()
    {
        var osu = WriteOsuFile(eventsLines: ["4,Foreground,Centre,\"sprite.png\",320,240"]);

        var assets = _sut.Parse(osu);

        Assert.Contains("sprite.png", assets.NonAudioFiles);
    }

    [Fact]
    public void Parse_NumericSixAnimation_ExpandsFrames()
    {
        var osu = WriteOsuFile(
            eventsLines: ["6,Foreground,Centre,\"frame.png\",320,240,3,100,LoopForever"]);

        var assets = _sut.Parse(osu);

        Assert.Contains("frame0.png", assets.NonAudioFiles);
        Assert.Contains("frame1.png", assets.NonAudioFiles);
        Assert.Contains("frame2.png", assets.NonAudioFiles);
    }

    [Fact]
    public void Parse_OsbWithVariables_ExpandsBeforeCollecting()
    {
        var osu = WriteOsuFile();
        var osbName = "test_" + Guid.NewGuid().ToString("N") + ".osb";
        var osb = Path.Combine(_tempDir, osbName);
        File.WriteAllLines(osb,
        [
            "[Variables]",
            "$f=\"custom-sample.ogg\"",
            "[Events]",
            "Sample,1000,0,$f,100",
        ]);

        var assets = _sut.Parse(osu);

        Assert.Contains("custom-sample.ogg", assets.SampleAudioFiles);
    }

    [Fact]
    public void Parse_OsbItself_AddedToNonAudioFiles()
    {
        var osu = WriteOsuFile();
        var osbName = "story_" + Guid.NewGuid().ToString("N") + ".osb";
        var osb = Path.Combine(_tempDir, osbName);
        File.WriteAllText(osb, "[Events]\n");

        var assets = _sut.Parse(osu);

        Assert.Contains(osbName, assets.NonAudioFiles);
    }
}
