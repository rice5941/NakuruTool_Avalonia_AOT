using System;
using System.Collections.Generic;
using System.IO;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="OsbFileRateConverter"/> の回帰テスト。
/// 一時ディレクトリで .osb を生成 → <c>Convert</c> 呼び出し → 出力を全行読み込んでアサート。
/// </summary>
public class OsbFileRateConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly OsbFileRateConverter _sut;

    public OsbFileRateConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_OsbFileRateConverterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new OsbFileRateConverter();
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
        IEnumerable<string> lines,
        IReadOnlyDictionary<string, string>? sampleFilenameMap = null)
    {
        var src = Path.Combine(_tempDir, "in_" + Guid.NewGuid().ToString("N") + ".osb");
        var dst = Path.Combine(_tempDir, "out_" + Guid.NewGuid().ToString("N") + ".osb");
        File.WriteAllLines(src, lines);

        var options = new OsbFileConvertOptions
        {
            Rate = rate,
            SampleFilenameMap = sampleFilenameMap,
        };

        _sut.Convert(src, dst, options);
        return File.ReadAllLines(dst);
    }

    // ---- Sample / Event ----

    [Fact]
    public void Sample_NumericFive_ScalesTimeAndRenamesFile()
    {
        var output = Convert(
            rate: 2.0m,
            lines: ["[Events]", "5,1000,0,\"foo.ogg\",100"],
            sampleFilenameMap: new Dictionary<string, string>
            {
                ["foo.ogg"] = "foo_2.000x_dt.ogg",
            });

        Assert.Contains("5,500,0,\"foo_2.000x_dt.ogg\",100", output);
    }

    [Fact]
    public void Sample_StringSample_ScalesTimeAndRenamesFile()
    {
        var output = Convert(
            rate: 2.0m,
            lines: ["[Events]", "Sample,1000,0,\"foo.ogg\",100"],
            sampleFilenameMap: new Dictionary<string, string>
            {
                ["foo.ogg"] = "foo_2.000x_dt.ogg",
            });

        Assert.Contains("Sample,500,0,\"foo_2.000x_dt.ogg\",100", output);
    }

    [Fact]
    public void Sprite_FadeCommand_ScalesBothTimes()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "_F,0,1000,2000,0,1"]);
        Assert.Contains("_F,0,500,1000,0,1", output);
    }

    [Fact]
    public void Sprite_MoveCommand_ScalesTimes_LeavesCoordsUnchanged()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "_M,0,1000,2000,100,200,300,400"]);
        Assert.Contains("_M,0,500,1000,100,200,300,400", output);
    }

    [Fact]
    public void Sprite_LoopCommand_ScalesStartTimeOnly_NotLoopCount()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "_L,1000,5"]);
        Assert.Contains("_L,500,5", output);
    }

    [Fact]
    public void Sprite_TriggerCommand_ScalesTimes_LeavesTriggerString()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "_T,HitSoundClap,1000,2000"]);
        Assert.Contains("_T,HitSoundClap,500,1000", output);
    }

    [Fact]
    public void Sprite_NestedLoopChild_ScalesRelativeTimes()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "__F,0,100,200,0,1"]);
        Assert.Contains("__F,0,50,100,0,1", output);
    }

    [Fact]
    public void Animation_FrameDelay_ScalesByRate()
    {
        var output = Convert(
            rate: 2.0m,
            lines: ["[Events]", "Animation,Foreground,Centre,\"a.png\",320,240,5,100,LoopForever"]);

        Assert.Contains("Animation,Foreground,Centre,\"a.png\",320,240,5,50,LoopForever", output);
    }

    [Fact]
    public void Animation_NumericSix_FrameDelay_ScalesByRate()
    {
        // numeric `6,...` 形式での frameDelay スケール（.osb 側のみ /rate）。
        // 実装が numeric 6 を Unknown 経由で素通しした場合に検知する。
        var output = Convert(
            rate: 2.0m,
            lines: ["[Events]", "6,Foreground,Centre,\"a.png\",320,240,5,100,LoopForever"]);

        Assert.Contains("6,Foreground,Centre,\"a.png\",320,240,5,50,LoopForever", output);
    }

    [Fact]
    public void Event_NumericThreeColour_ScalesStartTime()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "3,100,163,162,255"]);
        Assert.Contains("3,50,163,162,255", output);
    }

    // ---- [Variables] ----

    [Fact]
    public void Variables_ExpandedThenScaled()
    {
        var output = Convert(
            rate: 2.0m,
            lines:
            [
                "[Variables]",
                "$st=1000",
                "[Events]",
                "_F,0,$st,2000,0,1",
            ]);

        Assert.Contains("_F,0,500,1000,0,1", output);
        // [Variables] セクションは原文のまま
        Assert.Contains("$st=1000", output);
    }

    [Fact]
    public void Variables_SampleLine_ExpandedThenScaledAndRenamed()
    {
        var output = Convert(
            rate: 2.0m,
            lines:
            [
                "[Variables]",
                "$t=1000",
                "$f=\"foo.ogg\"",
                "[Events]",
                "5,$t,0,$f,100",
            ],
            sampleFilenameMap: new Dictionary<string, string>
            {
                ["foo.ogg"] = "foo_2.000x_dt.ogg",
            });

        Assert.Contains("5,500,0,\"foo_2.000x_dt.ogg\",100", output);
    }

    [Fact]
    public void Variables_LongestMatch_UsesLongestAlias()
    {
        var output = Convert(
            rate: 2.0m,
            lines:
            [
                "[Variables]",
                "$a=1000",
                "$ab=2000",
                "[Events]",
                "_F,0,$ab,3000,0,1",
            ]);

        // $ab → 2000（最長一致）→ scale 2.0 → 1000、3000 → 1500
        Assert.Contains("_F,0,1000,1500,0,1", output);
    }

    [Fact]
    public void Variables_Undefined_PreservesOriginalLine()
    {
        var input = new[]
        {
            "[Events]",
            "_F,0,$undef,2000,0,1",
        };

        var output = Convert(rate: 2.0m, lines: input);

        Assert.Contains("_F,0,$undef,2000,0,1", output);
    }

    [Fact]
    public void Variables_SectionPreservedVerbatim()
    {
        var output = Convert(
            rate: 2.0m,
            lines:
            [
                "[Variables]",
                "$f=\"foo.ogg\"",
                "[Events]",
            ]);

        Assert.Contains("$f=\"foo.ogg\"", output);
    }

    // ---- 特殊ケース ----

    [Fact]
    public void EmptyEndTime_PreservesEmptyField()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "_F,0,1000,,0,1"]);
        Assert.Contains("_F,0,500,,0,1", output);
    }

    [Fact]
    public void Comments_PreservedVerbatim()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "// comment"]);
        Assert.Contains("// comment", output);
    }

    [Fact]
    public void UnknownCommand_PassesThrough()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "_X,foo,bar"]);
        Assert.Contains("_X,foo,bar", output);
    }

    [Fact]
    public void IndentationPrefix_Preserved()
    {
        var output = Convert(rate: 2.0m, lines: ["[Events]", "__F,0,100,200,0,1"]);
        // prefix "__" が保持されている（"_F" に正規化されていない）
        Assert.Contains("__F,0,50,100,0,1", output);
        Assert.DoesNotContain("_F,0,50,100,0,1", output);
    }
}
