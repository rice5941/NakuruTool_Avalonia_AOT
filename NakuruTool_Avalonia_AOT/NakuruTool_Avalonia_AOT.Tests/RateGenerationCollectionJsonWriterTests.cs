using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="RateGenerationCollectionJsonWriter"/> の writer 単体テスト。
/// AppBase 直下 <c>imports/rate-generation/</c> へファイルを生成するため、
/// 各テストケースは生成ファイルを後始末する。
/// </summary>
public sealed class RateGenerationCollectionJsonWriterTests : IDisposable
{
    private static readonly string OutputFolder =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "imports", "rate-generation");

    private readonly List<string> _filesBeforeTest;

    public RateGenerationCollectionJsonWriterTests()
    {
        _filesBeforeTest = Directory.Exists(OutputFolder)
            ? Directory.GetFiles(OutputFolder).ToList()
            : new List<string>();
    }

    public void Dispose()
    {
        // テスト中に新規生成されたファイルだけ掃除する
        if (!Directory.Exists(OutputFolder))
            return;

        var current = Directory.GetFiles(OutputFolder);
        foreach (var path in current)
        {
            if (_filesBeforeTest.Contains(path))
                continue;
            try { File.Delete(path); }
            catch { /* 後始末失敗は無視 */ }
        }
    }

    private static Beatmap MakeBeatmap(string md5) => new()
    {
        MD5Hash = md5,
        Title = "Title",
        Artist = "Artist",
        Version = "Version",
        Creator = "Creator",
        FolderName = "Folder",
        AudioFilename = "audio.mp3",
        OsuFileName = "x.osu",
        Grade = "F",
    };

    private static RateGenerationResult MakeSuccessResult(
        string md5,
        bool included = true,
        bool withJsonItem = true,
        int beatmapsetId = 1234,
        double cs = 7,
        string title = "MyTitle",
        string artist = "MyArtist")
    {
        return new RateGenerationResult
        {
            Success = true,
            GeneratedOszPath = "C:/fake/path.osz",
            AppliedRate = 1.25,
            SourceBeatmap = MakeBeatmap(md5),
            IncludedInOsz = included,
            JsonItem = withJsonItem
                ? new RateGenerationJsonItem
                {
                    Title = title,
                    Artist = artist,
                    Version = "Hard 1.25x",
                    Creator = "TestCreator",
                    Cs = cs,
                    BeatmapsetId = beatmapsetId,
                    Md5 = md5,
                }
                : null,
        };
    }

    [Fact]
    public async Task WriteBatchAsync_NoEligibleResults_DoesNotWriteFile()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await writer.WriteBatchAsync(
            "MyCollection",
            options,
            new List<RateGenerationResult>());

        Assert.False(result.FileWritten);
        Assert.Null(result.OutputFilePath);
        Assert.Equal(0, result.WrittenBeatmapCount);
    }

    [Fact]
    public async Task WriteBatchAsync_OnlyFailedResults_DoesNotCountAsSkipped()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var results = new List<RateGenerationResult>
        {
            new()
            {
                Success = false,
                ErrorMessage = "boom",
                SourceBeatmap = MakeBeatmap("md5fail"),
            },
        };

        var result = await writer.WriteBatchAsync("MyCollection", options, results);

        Assert.False(result.FileWritten);
        Assert.Equal(0, result.WrittenBeatmapCount);
        Assert.Equal(0, result.SkippedBeatmapCount);
    }

    [Fact]
    public async Task WriteBatchAsync_WithJsonItemAndIncluded_WritesReadableJson()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions
        {
            Rate = 1.25,
            ChangePitch = false,
            HpOverride = 8,
            OdOverride = 5,
        };
        var results = new List<RateGenerationResult>
        {
            MakeSuccessResult("a1b2c3", beatmapsetId: 999, cs: 7),
        };

        var result = await writer.WriteBatchAsync("Sample Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.NotNull(result.OutputFilePath);
        Assert.True(File.Exists(result.OutputFilePath));
        Assert.Equal("Sample Coll [1.25x DT HP8 OD5]", result.OutputCollectionName);
        Assert.Equal(1, result.WrittenBeatmapCount);

        // ImportExportJsonContext.Default で読み戻せること
        await using var stream = File.OpenRead(result.OutputFilePath!);
        var data = await JsonSerializer.DeserializeAsync(
            stream,
            ImportExportJsonContext.Default.CollectionExchangeData);

        Assert.NotNull(data);
        Assert.Equal("Sample Coll [1.25x DT HP8 OD5]", data!.Name);
        Assert.Single(data.Beatmaps);
        var bm = data.Beatmaps[0];
        Assert.Equal("MyTitle", bm.Title);
        Assert.Equal("MyArtist", bm.Artist);
        Assert.Equal("a1b2c3", bm.Md5);
        Assert.Equal(999, bm.BeatmapsetId);
        Assert.Equal(7, bm.Cs);
    }

    [Fact]
    public async Task WriteBatchAsync_WithJsonItem_FileNameContainsRateLabel()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5one") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        var fileName = Path.GetFileName(result.OutputFilePath!);
        Assert.Contains("1.25x", fileName);
        Assert.Contains("DT", fileName);
        Assert.StartsWith("Coll_", fileName);
        Assert.EndsWith(".json", fileName);
    }

    [Fact]
    public async Task WriteBatchAsync_RawSerializedJson_ContainsBeatmapsetIdAndCsKeys()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.5, ChangePitch = true };
        var results = new List<RateGenerationResult>
        {
            MakeSuccessResult("md5cs", beatmapsetId: 4242, cs: 4),
        };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        var jsonText = await File.ReadAllTextAsync(result.OutputFilePath!);

        Assert.Contains("\"beatmapset_id\": 4242", jsonText);
        Assert.Contains("\"cs\": 4", jsonText);
        Assert.Contains("\"md5\": \"md5cs\"", jsonText);
        // [1.5x NC] (HP/OD なし)
        Assert.Contains("[1.5x NC]", jsonText);
    }

    [Fact]
    public async Task WriteBatchAsync_TargetBpm_ProducesBpmRateLabel()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { TargetBpm = 200, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5bpm") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal("Coll [200bpm DT]", result.OutputCollectionName);
        Assert.Contains("200bpm", Path.GetFileName(result.OutputFilePath!));
    }

    [Fact]
    public async Task WriteBatchAsync_NotIncludedInOsz_StillIncludedInJson()
    {
        // 仕様変更: 既存 .osz 内の同名 entry 衝突 (IncludedInOsz=false) であっても
        // JsonItem があれば JSON 出力対象に含める。
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var results = new List<RateGenerationResult>
        {
            MakeSuccessResult("md5collide", included: false),
        };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal(1, result.WrittenBeatmapCount);
        Assert.Equal(0, result.SkippedBeatmapCount);

        await using var stream = File.OpenRead(result.OutputFilePath!);
        var data = await JsonSerializer.DeserializeAsync(
            stream,
            ImportExportJsonContext.Default.CollectionExchangeData);
        Assert.NotNull(data);
        Assert.Single(data!.Beatmaps);
        Assert.Equal("md5collide", data.Beatmaps[0].Md5);
    }

    [Fact]
    public async Task WriteBatchAsync_NullJsonItem_IsCountedAsSkipped()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var results = new List<RateGenerationResult>
        {
            MakeSuccessResult("md5nojson", withJsonItem: false),
        };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.False(result.FileWritten);
        Assert.Equal(1, result.SkippedBeatmapCount);
    }

    [Fact]
    public async Task WriteBatchAsync_DuplicateMd5_IsDedupedAndCountedAsSkipped()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var results = new List<RateGenerationResult>
        {
            MakeSuccessResult("dup-md5", title: "First"),
            MakeSuccessResult("dup-md5", title: "Second"),
            MakeSuccessResult("unique", title: "Third"),
        };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal(2, result.WrittenBeatmapCount);
        Assert.Equal(1, result.SkippedBeatmapCount);

        await using var stream = File.OpenRead(result.OutputFilePath!);
        var data = await JsonSerializer.DeserializeAsync(
            stream,
            ImportExportJsonContext.Default.CollectionExchangeData);
        Assert.NotNull(data);
        Assert.Equal(2, data!.Beatmaps.Count);
        Assert.Contains(data.Beatmaps, b => b.Md5 == "dup-md5" && b.Title == "First");
        Assert.Contains(data.Beatmaps, b => b.Md5 == "unique");
    }

    [Fact]
    public async Task WriteBatchAsync_RateOne_ProducesOneLabel()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.0, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5one") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal("Coll [1x DT]", result.OutputCollectionName);
    }

    [Fact]
    public async Task WriteBatchAsync_RateTwo_ProducesTwoLabel()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 2.0, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5two") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal("Coll [2x DT]", result.OutputCollectionName);
    }

    [Fact]
    public async Task WriteBatchAsync_RateNotSet_FallsBackToOneDotZero()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { ChangePitch = false }; // Rate / TargetBpm 共に未設定
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5fallback") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal("Coll [1.0x DT]", result.OutputCollectionName);
    }

    [Fact]
    public async Task WriteBatchAsync_FractionalTargetBpm_PreservesDecimals()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { TargetBpm = 199.75, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5bpmfrac") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal("Coll [199.75bpm DT]", result.OutputCollectionName);
        Assert.Contains("199.75bpm", Path.GetFileName(result.OutputFilePath!));
    }

    [Fact]
    public async Task WriteBatchAsync_FractionalHpOd_PreservesDecimals()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions
        {
            Rate = 1.25,
            ChangePitch = false,
            HpOverride = 8.25,
            OdOverride = 5.5,
        };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5hpod") };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal("Coll [1.25x DT HP8.25 OD5.5]", result.OutputCollectionName);
    }

    [Fact]
    public async Task WriteBatchAsync_NullJsonItemAndNotIncluded_IsCountedAsSkipped()
    {
        // 仕様: JsonItem が null の場合は IncludedInOsz の値に関わらず Skipped として扱う。
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var results = new List<RateGenerationResult>
        {
            MakeSuccessResult("md5composite", included: false, withJsonItem: false),
        };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.False(result.FileWritten);
        Assert.Equal(0, result.WrittenBeatmapCount);
        Assert.Equal(1, result.SkippedBeatmapCount);
    }

    [Fact]
    public async Task WriteBatchAsync_MixedComposite_CountsAreOrderedCorrectly()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var results = new List<RateGenerationResult>
        {
            // 失敗 -> カウント外
            new()
            {
                Success = false,
                ErrorMessage = "boom",
                SourceBeatmap = MakeBeatmap("md5fail"),
            },
            // GeneratedOszPath 欠落 -> Skipped
            new()
            {
                Success = true,
                GeneratedOszPath = string.Empty,
                SourceBeatmap = MakeBeatmap("md5nopath"),
            },
            // JsonItem null -> Skipped (IncludedInOsz=true)
            MakeSuccessResult("md5nojson", included: true, withJsonItem: false),
            // JsonItem null かつ IncludedInOsz=false -> Skipped (JsonItem 欠落理由)
            MakeSuccessResult("md5composite", included: false, withJsonItem: false),
            // IncludedInOsz=false かつ JsonItem あり -> JSON に含める (仕様変更)
            MakeSuccessResult("md5collide", included: false, withJsonItem: true),
            // 正常
            MakeSuccessResult("md5ok", included: true, withJsonItem: true),
        };

        var result = await writer.WriteBatchAsync("Coll", options, results);

        Assert.True(result.FileWritten);
        Assert.Equal(2, result.WrittenBeatmapCount);
        Assert.Equal(3, result.SkippedBeatmapCount);
    }

    [Fact]
    public async Task WriteBatchAsync_AfterAtomicMove_FinalFileIsReadableAndTmpIsGone()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5atomic") };

        var result = await writer.WriteBatchAsync("AtomicColl", options, results);

        Assert.True(result.FileWritten);
        Assert.NotNull(result.OutputFilePath);
        Assert.True(File.Exists(result.OutputFilePath));

        // .tmp が残っていないこと
        var tmpPath = result.OutputFilePath + ".tmp";
        Assert.False(File.Exists(tmpPath), $".tmp file should not remain: {tmpPath}");

        // ディレクトリ内に *.tmp が一切残っていないこと (このテストで生成された物のみ確認)
        var leftoverTmp = Directory.GetFiles(OutputFolder, "*.tmp")
            .Where(p => !_filesBeforeTest.Contains(p))
            .ToList();
        Assert.Empty(leftoverTmp);

        // 最終ファイルが読み戻せること
        await using var stream = File.OpenRead(result.OutputFilePath!);
        var data = await JsonSerializer.DeserializeAsync(
            stream,
            ImportExportJsonContext.Default.CollectionExchangeData);
        Assert.NotNull(data);
        Assert.Single(data!.Beatmaps);
        Assert.Equal("md5atomic", data.Beatmaps[0].Md5);
    }

    [Fact]
    public async Task WriteBatchAsync_PreCancelledToken_ThrowsAndLeavesNoFiles()
    {
        var writer = new RateGenerationCollectionJsonWriter();
        var options = new RateGenerationOptions { Rate = 1.25, ChangePitch = false };
        var results = new List<RateGenerationResult> { MakeSuccessResult("md5cancel") };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // 実行前後のファイルスナップショット差分を取り、新規生成が無いことを確認する
        var snapshotBefore = Directory.Exists(OutputFolder)
            ? Directory.GetFiles(OutputFolder).ToHashSet()
            : new HashSet<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await writer.WriteBatchAsync("CancelColl", options, results, cts.Token));

        var snapshotAfter = Directory.Exists(OutputFolder)
            ? Directory.GetFiles(OutputFolder)
            : Array.Empty<string>();

        var newFiles = snapshotAfter.Where(p => !snapshotBefore.Contains(p)).ToList();
        Assert.Empty(newFiles);
    }
}
