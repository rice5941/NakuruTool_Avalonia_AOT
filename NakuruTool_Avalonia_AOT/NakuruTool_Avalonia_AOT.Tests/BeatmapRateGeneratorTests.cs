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

    private sealed class FakeAudioRateChanger : IAudioRateChanger
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

    private sealed class FakeOsuFileRateConverter : IOsuFileRateConverter
    {
        public void Convert(string sourceOsuPath, string destinationOsuPath, OsuFileConvertOptions options)
        {
            File.WriteAllText(destinationOsuPath, "osu file format v14\n[General]\nMode:3\n");
        }
    }

    private sealed class FakeOsuFileAssetParser : IOsuFileAssetParser
    {
        private readonly OsuReferencedAssets _assets;
        public FakeOsuFileAssetParser(OsuReferencedAssets assets) => _assets = assets;
        public OsuReferencedAssets Parse(string osuFilePath) => _assets;
    }

    private sealed class FakeSettingsService : ISettingsService
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