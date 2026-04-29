using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
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

/// <summary>
/// <see cref="BeatmapRateGenerator"/> の非音声アセット（<c>.osb</c> を含む）コピー挙動の
/// オーケストレーションテスト。実 DI を使わず手書き fake を渡して <c>.osz</c> 出力エントリを検証する。
/// </summary>
public class BeatmapRateGeneratorNonAudioCopyTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _osuFolderPath;
    private readonly string _songsFolderPath;
    private readonly string _beatmapFolderPath;
    private const string FolderName = "TestBeatmapFolder";
    private const string OsuFileName = "test.osu";
    private const string AudioFileName = "audio.mp3";

    public BeatmapRateGeneratorNonAudioCopyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_BeatmapRateGeneratorNonAudioCopyTests_" + Guid.NewGuid().ToString("N"));
        _osuFolderPath = Path.Combine(_tempDir, "osu");
        _songsFolderPath = Path.Combine(_osuFolderPath, "Songs");
        _beatmapFolderPath = Path.Combine(_songsFolderPath, FolderName);
        Directory.CreateDirectory(_beatmapFolderPath);
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
        GC.SuppressFinalize(this);
    }

    private Beatmap CreateBeatmap()
    {
        File.WriteAllText(Path.Combine(_beatmapFolderPath, OsuFileName),
            "osu file format v14\n[General]\nAudioFilename: " + AudioFileName + "\nMode:3\n");
        File.WriteAllBytes(Path.Combine(_beatmapFolderPath, AudioFileName), [0x49, 0x44, 0x33]);

        return new Beatmap
        {
            MD5Hash = "00000000000000000000000000000000",
            Title = "Title",
            Artist = "Artist",
            Version = "Hard",
            Creator = "Creator",
            FolderName = FolderName,
            AudioFilename = AudioFileName,
            OsuFileName = OsuFileName,
            Grade = string.Empty,
            BPM = 120,
        };
    }

    private BeatmapRateGenerator CreateSut(OsuReferencedAssets assets)
    {
        var audio = new FakeAudioRateChanger();
        var osu = new FakeOsuFileRateConverter();
        var parser = new FakeOsuFileAssetParser(assets);
        var settings = new FakeSettingsService(_osuFolderPath);
        return new BeatmapRateGenerator(audio, osu, parser, settings);
    }

    [Fact]
    public async Task NonAudioOsb_IsRawCopiedIntoOsz()
    {
        const string osbName = "story.osb";
        var osbContent = "ORIGINAL_OSB_BYTES_\u00ff\u0001\u0002"u8.ToArray();
        File.WriteAllBytes(Path.Combine(_beatmapFolderPath, osbName), osbContent);

        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { osbName },
        };

        var sut = CreateSut(assets);
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗しました: {result.ErrorMessage}");

        var osz = result.GeneratedOszPath!;
        using var archive = ZipFile.OpenRead(osz);
        var entry = archive.GetEntry(osbName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        Assert.Equal(osbContent, ms.ToArray());
    }

    [Fact]
    public async Task NonAudioOsb_RawCopied_DoesNotApplySampleFilenameMap()
    {
        const string osbName = "story.osb";
        const string sampleName = "s.mp3";
        // .osb 内に sample 参照を含む（変換削除に伴いリネーム置換されないことを検証）
        var osbContent =
            "[Events]\n//Storyboard Sound Samples\nSample,1000,0,\"" + sampleName + "\",100\n";
        File.WriteAllText(Path.Combine(_beatmapFolderPath, osbName), osbContent);
        File.WriteAllBytes(Path.Combine(_beatmapFolderPath, sampleName), [0x49, 0x44, 0x33]);

        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sampleName },
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { osbName },
        };

        var sut = CreateSut(assets);
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗しました: {result.ErrorMessage}");

        var osz = result.GeneratedOszPath!;
        using var archive = ZipFile.OpenRead(osz);
        var entry = archive.GetEntry(osbName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var reader = new StreamReader(stream);
        var written = reader.ReadToEnd();

        // .osb は raw コピーされ、sample 参照名が書き換わっていないこと
        Assert.Contains("\"" + sampleName + "\"", written);
        Assert.Equal(osbContent, written);
    }

    [Fact]
    public async Task NonAudioAsset_OtherThanOsb_IsRawCopiedIntoOsz()
    {
        const string assetName = "bg.png";
        var sourceContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        File.WriteAllBytes(Path.Combine(_beatmapFolderPath, assetName), sourceContent);

        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { assetName },
        };

        var sut = CreateSut(assets);
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗しました: {result.ErrorMessage}");

        var osz = result.GeneratedOszPath!;
        using var archive = ZipFile.OpenRead(osz);
        var entry = archive.GetEntry(assetName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        Assert.Equal(sourceContent, ms.ToArray());
    }

    // ====== Fakes ======

    internal sealed class FakeAudioRateChanger : IAudioRateChanger
    {
        public Task<AudioRateChangeResult> ChangeRateAsync(
            string inputPath,
            string outputPath,
            double rate,
            bool changePitch,
            int? mp3VbrQuality = null,
            CancellationToken cancellationToken = default)
        {
            // .mp3 出力要求は .ogg にフォールバックを模擬
            var actualOutputPath = outputPath;
            if (string.Equals(Path.GetExtension(outputPath), ".mp3", StringComparison.OrdinalIgnoreCase))
                actualOutputPath = Path.ChangeExtension(outputPath, ".ogg");

            var dir = Path.GetDirectoryName(actualOutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(inputPath, actualOutputPath, overwrite: true);

            var reportedActual = actualOutputPath != outputPath ? actualOutputPath : null;
            return Task.FromResult(new AudioRateChangeResult(true, reportedActual));
        }
    }

    internal sealed class FakeOsuFileRateConverter : IOsuFileRateConverter
    {
        public void Convert(string sourceOsuPath, string destinationOsuPath, OsuFileConvertOptions options)
        {
            // テスト用: 生成 .osu の MD5/メタ抽出を成立させるため、必須メタを含む最小内容を書き出す。
            var diffName = options.NewDifficultyName ?? "Hard";
            var content =
                "osu file format v14\n" +
                "[General]\nMode:3\n" +
                "[Metadata]\n" +
                "Title:FakeTitle\n" +
                "Artist:FakeArtist\n" +
                "Creator:FakeCreator\n" +
                $"Version:{diffName}\n" +
                "BeatmapSetID:1234\n" +
                "[Difficulty]\n" +
                "CircleSize:7\n";
            File.WriteAllText(destinationOsuPath, content);
        }
    }

    internal sealed class FakeOsuFileAssetParser : IOsuFileAssetParser
    {
        private readonly OsuReferencedAssets _assets;
        public FakeOsuFileAssetParser(OsuReferencedAssets assets) => _assets = assets;
        public OsuReferencedAssets Parse(string osuFilePath) => _assets;
    }

    internal sealed class FakeSettingsService : ISettingsService
    {
        public ISettingsData SettingsData { get; }
        public FakeSettingsService(string osuFolderPath)
        {
            SettingsData = new SettingsData { OsuFolderPath = osuFolderPath };
        }

        public bool SaveSettings(SettingsData settings) => true;
        public bool CheckSettingsPath() => true;
        public string GetSettingsPath() => string.Empty;
        public void Dispose() { }
    }
}

/// <summary>
/// 生成 <c>.osu</c> 由来の <see cref="RateGenerationJsonItem"/> 構築および
/// <c>IncludedInOsz</c> 判定のテスト。
/// </summary>
public class BeatmapRateGeneratorJsonItemTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _osuFolderPath;
    private readonly string _songsFolderPath;
    private readonly string _beatmapFolderPath;
    private const string FolderName = "TestBeatmapFolder";
    private const string OsuFileName = "test.osu";
    private const string AudioFileName = "audio.mp3";

    public BeatmapRateGeneratorJsonItemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_BeatmapRateGeneratorJsonItemTests_" + Guid.NewGuid().ToString("N"));
        _osuFolderPath = Path.Combine(_tempDir, "osu");
        _songsFolderPath = Path.Combine(_osuFolderPath, "Songs");
        _beatmapFolderPath = Path.Combine(_songsFolderPath, FolderName);
        Directory.CreateDirectory(_beatmapFolderPath);
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
        GC.SuppressFinalize(this);
    }

    private Beatmap CreateBeatmap()
    {
        File.WriteAllText(Path.Combine(_beatmapFolderPath, OsuFileName),
            "osu file format v14\n[General]\nAudioFilename: " + AudioFileName + "\nMode:3\n");
        File.WriteAllBytes(Path.Combine(_beatmapFolderPath, AudioFileName), [0x49, 0x44, 0x33]);

        return new Beatmap
        {
            MD5Hash = "00000000000000000000000000000000",
            Title = "Title",
            Artist = "Artist",
            Version = "Hard",
            Creator = "Creator",
            FolderName = FolderName,
            AudioFilename = AudioFileName,
            OsuFileName = OsuFileName,
            Grade = string.Empty,
            BPM = 120,
        };
    }

    private BeatmapRateGenerator CreateSut()
    {
        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var audio = new BeatmapRateGeneratorNonAudioCopyTests.FakeAudioRateChanger();
        var osu = new BeatmapRateGeneratorNonAudioCopyTests.FakeOsuFileRateConverter();
        var parser = new BeatmapRateGeneratorNonAudioCopyTests.FakeOsuFileAssetParser(assets);
        var settings = new BeatmapRateGeneratorNonAudioCopyTests.FakeSettingsService(_osuFolderPath);
        return new BeatmapRateGenerator(audio, osu, parser, settings);
    }

    private static string ComputeMd5Hex(byte[] bytes)
        => System.Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task NewOsz_PopulatesJsonItemAndIncludedInOsz()
    {
        var sut = CreateSut();
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗: {result.ErrorMessage}");
        Assert.NotNull(result.GeneratedOsuEntryName);
        Assert.NotNull(result.JsonItem);
        Assert.True(result.IncludedInOsz);

        var item = result.JsonItem!;
        Assert.Equal("FakeTitle", item.Title);
        Assert.Equal("FakeArtist", item.Artist);
        Assert.Equal("FakeCreator", item.Creator);
        Assert.Equal(7.0, item.Cs);
        Assert.Equal(1234, item.BeatmapsetId);
        Assert.False(string.IsNullOrEmpty(item.Md5));

        // .osz 内の生成 .osu エントリ MD5 と一致することを確認
        using var archive = ZipFile.OpenRead(result.GeneratedOszPath!);
        var entry = archive.GetEntry(result.GeneratedOsuEntryName!);
        Assert.NotNull(entry);
        using var es = entry!.Open();
        using var ms = new MemoryStream();
        es.CopyTo(ms);
        var entryMd5 = ComputeMd5Hex(ms.ToArray());
        Assert.Equal(entryMd5, item.Md5);
    }

    [Fact]
    public async Task ExistingOsz_WithSameOsuEntry_SetsIncludedInOszFalse()
    {
        // まず一度生成して .osz を作る
        var sut1 = CreateSut();
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };
        var first = await sut1.GenerateAsync(beatmap, options);
        Assert.True(first.Success, $"first GenerateAsync が失敗: {first.ErrorMessage}");
        Assert.True(first.IncludedInOsz);
        Assert.NotNull(first.GeneratedOsuEntryName);

        // 同じ条件でもう一度生成 → 既存 .osz に同名 .osu entry が既にあるためスキップされるはず
        var sut2 = CreateSut();
        var second = await sut2.GenerateAsync(beatmap, options);

        Assert.True(second.Success, $"second GenerateAsync が失敗: {second.ErrorMessage}");
        Assert.Equal(first.GeneratedOsuEntryName, second.GeneratedOsuEntryName);
        Assert.NotNull(second.JsonItem);
        Assert.False(second.IncludedInOsz);
    }

    [Fact]
    public async Task FailedResult_HasNullJsonItemAndFalseIncluded()
    {
        // 元 .osu が存在しない beatmap を渡して失敗させる
        var sut = CreateSut();
        var beatmap = new Beatmap
        {
            MD5Hash = "00000000000000000000000000000000",
            Title = "Title",
            Artist = "Artist",
            Version = "Hard",
            Creator = "Creator",
            FolderName = FolderName,
            AudioFilename = AudioFileName,
            OsuFileName = "missing.osu",
            Grade = string.Empty,
            BPM = 120,
        };
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.False(result.Success);
        Assert.Null(result.GeneratedOsuEntryName);
        Assert.Null(result.JsonItem);
        Assert.False(result.IncludedInOsz);
    }

    [Fact]
    public async Task ExistingCorruptOsz_FallsBackToNewCreation_IncludedInOszTrue()
    {
        // 既存 .osz が壊れていて ZipArchiveMode.Update で開けないケース。
        // BeatmapRateGenerator は新規作成にフォールバックし、IncludedInOsz==true となる。
        var oszPath = Path.Combine(_songsFolderPath, FolderName + ".osz");
        Directory.CreateDirectory(_songsFolderPath);
        // ZIP として無効なバイト列を書き込む（ZipFile.Open(..., Update) は InvalidDataException を投げる）
        File.WriteAllBytes(oszPath, "this is not a zip archive"u8.ToArray());

        var sut = CreateSut();
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗: {result.ErrorMessage}");
        Assert.NotNull(result.GeneratedOsuEntryName);
        Assert.True(result.IncludedInOsz, "フォールバックで新規作成された場合 IncludedInOsz は true となるべき");

        // .osz が ZIP として有効に上書きされ、生成 .osu エントリを含むことを確認
        using var archive = ZipFile.OpenRead(result.GeneratedOszPath!);
        Assert.NotNull(archive.GetEntry(result.GeneratedOsuEntryName!));
    }

    [Fact]
    public async Task MetadataExtractionFails_StillSucceedsAndIncludedInOsz()
    {
        // FakeOsuFileRateConverterWithoutMetadata は [Metadata] を含まない .osu を出力するため、
        // OsuFileMetadataReader.TryReadBasicMetadata は false を返す。
        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        var audio = new BeatmapRateGeneratorNonAudioCopyTests.FakeAudioRateChanger();
        var osu = new FakeOsuFileRateConverterWithoutMetadata();
        var parser = new BeatmapRateGeneratorNonAudioCopyTests.FakeOsuFileAssetParser(assets);
        var settings = new BeatmapRateGeneratorNonAudioCopyTests.FakeSettingsService(_osuFolderPath);
        var sut = new BeatmapRateGenerator(audio, osu, parser, settings);

        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗: {result.ErrorMessage}");
        Assert.NotNull(result.GeneratedOsuEntryName);
        Assert.Null(result.JsonItem);
        Assert.True(result.IncludedInOsz);

        // .osz 生成自体が成功し、生成 .osu エントリが含まれていることを確認
        Assert.NotNull(result.GeneratedOszPath);
        Assert.True(File.Exists(result.GeneratedOszPath!));
        using var archive = ZipFile.OpenRead(result.GeneratedOszPath!);
        Assert.NotNull(archive.GetEntry(result.GeneratedOsuEntryName!));
    }

    [Fact]
    public async Task ExistingOszWithNonConflictingEntry_UpdateAddsEntry_IncludedInOszTrue()
    {
        // 既存 .osz は存在するが、生成される .osu entry とは衝突しない (無関係なダミーエントリのみ)
        var oszPath = Path.Combine(_songsFolderPath, FolderName + ".osz");
        Directory.CreateDirectory(_songsFolderPath);
        using (var fs = File.Create(oszPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("preexisting_dummy.txt");
            using var es = entry.Open();
            using var sw = new StreamWriter(es);
            sw.Write("existing");
        }

        var sut = CreateSut();
        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗: {result.ErrorMessage}");
        Assert.NotNull(result.GeneratedOsuEntryName);
        Assert.True(result.IncludedInOsz, "衝突しない既存 .osz への追加では IncludedInOsz は true となるべき");

        // 既存ダミーエントリと新規生成 .osu エントリの両方が存在することを確認
        using var resultArchive = ZipFile.OpenRead(result.GeneratedOszPath!);
        Assert.NotNull(resultArchive.GetEntry("preexisting_dummy.txt"));
        Assert.NotNull(resultArchive.GetEntry(result.GeneratedOsuEntryName!));
    }

    /// <summary>
    /// テスト用: 生成 .osu に [Metadata] セクションを含めず、
    /// <c>OsuFileMetadataReader.TryReadBasicMetadata</c> を false にさせる converter。
    /// </summary>
    private sealed class FakeOsuFileRateConverterWithoutMetadata : IOsuFileRateConverter
    {
        public void Convert(string sourceOsuPath, string destinationOsuPath, OsuFileConvertOptions options)
        {
            // 必須メタ (Title/Artist/Version/Creator) を含まないため TryReadBasicMetadata は false。
            var content =
                "osu file format v14\n" +
                "[General]\nMode:3\n" +
                "[Difficulty]\nCircleSize:7\n";
            File.WriteAllText(destinationOsuPath, content);
        }
    }
}