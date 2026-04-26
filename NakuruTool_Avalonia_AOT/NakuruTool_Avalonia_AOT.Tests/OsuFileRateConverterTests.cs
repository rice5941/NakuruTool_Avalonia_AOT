using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="OsuFileRateConverter"/> の回帰テスト。
/// 一時ディレクトリを <see cref="Path.GetTempPath"/> 配下に作成し、
/// 入力 .osu を生成 → <c>Convert</c> 呼び出し → 出力を <see cref="File.ReadAllLines"/>
/// で取得し、対象行のみアサートする。
/// </summary>
public class OsuFileRateConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OsuFileRateConverter _sut;

    public OsuFileRateConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_OsuFileRateConverterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new OsuFileRateConverter();
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

    private string[] Convert(
        decimal rate,
        IEnumerable<string>? generalExtra = null,
        IEnumerable<string>? metadataExtra = null,
        IEnumerable<string>? eventsLines = null,
        IEnumerable<string>? timingPointsLines = null,
        IEnumerable<string>? hitObjectsLines = null,
        IReadOnlyDictionary<string, string>? sampleFilenameMap = null)
    {
        var input = new List<string>
        {
            "osu file format v14",
            string.Empty,
            "[General]",
            "AudioFilename: audio.mp3",
            "Mode:3",
        };
        if (generalExtra is not null) input.AddRange(generalExtra);

        input.Add(string.Empty);
        input.Add("[Metadata]");
        input.Add("Title:t");
        input.Add("Artist:a");
        input.Add("Creator:c");
        input.Add("Version:v");
        input.Add("Tags:foo");
        if (metadataExtra is not null) input.AddRange(metadataExtra);

        input.Add(string.Empty);
        input.Add("[Difficulty]");
        input.Add("HPDrainRate:5");
        input.Add("CircleSize:7");
        input.Add("OverallDifficulty:8");
        input.Add("ApproachRate:5");
        input.Add("SliderMultiplier:1.4");
        input.Add("SliderTickRate:1");

        input.Add(string.Empty);
        input.Add("[Events]");
        if (eventsLines is not null) input.AddRange(eventsLines);

        input.Add(string.Empty);
        input.Add("[TimingPoints]");
        if (timingPointsLines is not null) input.AddRange(timingPointsLines);

        input.Add(string.Empty);
        input.Add("[HitObjects]");
        if (hitObjectsLines is not null) input.AddRange(hitObjectsLines);

        var src = Path.Combine(_tempDir, "in_" + Guid.NewGuid().ToString("N") + ".osu");
        var dst = Path.Combine(_tempDir, "out_" + Guid.NewGuid().ToString("N") + ".osu");
        File.WriteAllLines(src, input);

        var options = new OsuFileConvertOptions
        {
            Rate = rate,
            NewAudioFilename = "audio.mp3",
            SampleFilenameMap = sampleFilenameMap,
        };

        _sut.Convert(src, dst, options);
        return File.ReadAllLines(dst);
    }

    // ---- [Events] ----

    [Fact]
    public void Events_NumericFiveSample_ScalesTimeAndRenamesFile()
    {
        var map = new Dictionary<string, string>
        {
            ["normal-hitnormal10.ogg"] = "normal-hitnormal10_1.250x_dt.ogg",
        };

        var output = Convert(
            rate: 1.25m,
            eventsLines: ["5,929,0,\"normal-hitnormal10.ogg\",100"],
            sampleFilenameMap: map);

        Assert.Contains("5,743,0,\"normal-hitnormal10_1.250x_dt.ogg\",100", output);
    }

    [Fact]
    public void Events_StringSample_StillScalesTimeAndRenamesFile()
    {
        var map = new Dictionary<string, string>
        {
            ["normal-hitnormal10.ogg"] = "normal-hitnormal10_1.250x_dt.ogg",
        };

        var output = Convert(
            rate: 1.25m,
            eventsLines: ["Sample,929,0,\"normal-hitnormal10.ogg\",100"],
            sampleFilenameMap: map);

        Assert.Contains("Sample,743,0,\"normal-hitnormal10_1.250x_dt.ogg\",100", output);
    }

    [Fact]
    public void Events_NumericThreeColour_ScalesStartTime()
    {
        var output = Convert(rate: 1.25m, eventsLines: ["3,100,163,162,255"]);
        Assert.Contains("3,80,163,162,255", output);
    }

    [Fact]
    public void Events_NumericTwoBreak_ScalesBothTimes()
    {
        var output = Convert(rate: 1.25m, eventsLines: ["2,1000,2000"]);
        Assert.Contains("2,800,1600", output);
    }

    [Fact]
    public void Events_NumericOneVideo_ScalesStartTime()
    {
        var output = Convert(rate: 2.0m, eventsLines: ["1,1000,\"video.mp4\""]);
        Assert.Contains("1,500,\"video.mp4\"", output);
    }

    [Fact]
    public void Events_NumericFive_WithoutMap_StillScalesTimeOnly()
    {
        var output = Convert(rate: 1.25m, eventsLines: ["5,929,0,\"x.ogg\",100"]);
        Assert.Contains("5,743,0,\"x.ogg\",100", output);
    }

    [Fact]
    public void Events_UnknownEventKind_PassesThrough()
    {
        var output = Convert(rate: 2.0m, eventsLines: ["7,foo,bar"]);
        Assert.Contains("7,foo,bar", output);
    }

    [Fact]
    public void Events_IndentedFadeCommand_PassesThrough()
    {
        // .osu 内の indented storyboard command は素通し（設計 Q1）
        var output = Convert(rate: 2.0m, eventsLines: ["_F,0,1000,2000,0,1"]);
        Assert.Contains("_F,0,1000,2000,0,1", output);
    }

    [Fact]
    public void Events_NumericSixAnimation_FrameDelayUnchanged()
    {
        // .osu の Animation の frameDelay は変更しない（設計 Q2）。
        // numeric 6 で先頭が "6" の Animation 行は他フィールドも変化しないため不変。
        var line = "6,Foreground,Centre,\"a.png\",320,240,5,100,LoopForever";
        var output = Convert(rate: 1.25m, eventsLines: [line]);
        Assert.Contains(line, output);
    }

    // ---- [General] ----

    [Fact]
    public void General_AudioLeadIn_ScalesValue()
    {
        var output = Convert(rate: 1.25m, generalExtra: ["AudioLeadIn: 1500"]);
        Assert.Contains("AudioLeadIn: 1200", output);
    }

    [Fact]
    public void General_AudioLeadInZero_RemainsZero()
    {
        var output = Convert(rate: 1.25m, generalExtra: ["AudioLeadIn: 0"]);
        Assert.Contains("AudioLeadIn: 0", output);
    }

    [Fact]
    public void General_PreviewTime_StillScales()
    {
        var output = Convert(rate: 1.25m, generalExtra: ["PreviewTime: 1000"]);
        Assert.Contains("PreviewTime: 800", output);
    }

    // ---- [Metadata] ----

    [Fact]
    public void Metadata_Tags_AppendsNakuruTool()
    {
        var output = Convert(rate: 1.25m);
        var tagsLine = output.First(l => l.StartsWith("Tags:", StringComparison.Ordinal));
        Assert.Contains("NakuruTool", tagsLine);
    }

    // ---- [TimingPoints] ----

    [Fact]
    public void TimingPoints_ScalesTimeAndBeatLength()
    {
        var output = Convert(
            rate: 2.0m,
            timingPointsLines: ["1000,500,4,2,0,100,1,0"]);

        var tpLine = output.First(l => l.StartsWith("500,", StringComparison.Ordinal));
        var parts = tpLine.Split(',');
        Assert.Equal("500", parts[0]);
        // beatLength: 500 / 2 = 250（小数点表記は decimal の振る舞いに依存するため数値比較）
        Assert.Equal(250m, decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("4", parts[2]);
        Assert.Equal("2", parts[3]);
        Assert.Equal("0", parts[4]);
        Assert.Equal("100", parts[5]);
        Assert.Equal("1", parts[6]);
        Assert.Equal("0", parts[7]);
    }

    // ---- [HitObjects] ----

    [Fact]
    public void HitObject_LongNote_ScalesEndTime()
    {
        var output = Convert(
            rate: 2.0m,
            hitObjectsLines: ["448,192,1000,128,0,2000:0:0:0:0:"]);

        Assert.Contains("448,192,500,128,0,1000:0:0:0:0:", output);
    }
}
