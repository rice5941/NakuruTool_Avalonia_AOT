using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="BeatmapRateGenerator"/> の <c>.osb</c> 周辺フローの軽量オーケストレーションテスト。
/// 実 DI を使わず手書き fake を渡し、<c>.osz</c> 出力エントリと
/// <see cref="IOsbFileRateConverter"/> 呼び出し有無を検証する。
/// </summary>
public class BeatmapRateGeneratorOsbFlowTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _osuFolderPath;
    private readonly string _songsFolderPath;
    private readonly string _beatmapFolderPath;
    private const string FolderName = "TestBeatmapFolder";
    private const string OsuFileName = "test.osu";
    private const string AudioFileName = "audio.mp3";

    public BeatmapRateGeneratorOsbFlowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_BeatmapRateGeneratorOsbFlowTests_" + Guid.NewGuid().ToString("N"));
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
    }

    private Beatmap CreateBeatmap()
    {
        // 入力ファイルを配置
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

    private (FakeAudioRateChanger Audio,
             FakeOsbFileRateConverter Osb,
             FakeOsuFileRateConverter Osu,
             FakeOsuFileAssetParser Parser,
             FakeSettingsService Settings,
             BeatmapRateGenerator Sut)
        CreateSut(OsuReferencedAssets assets)
    {
        var audio = new FakeAudioRateChanger();
        var osb = new FakeOsbFileRateConverter();
        var osu = new FakeOsuFileRateConverter();
        var parser = new FakeOsuFileAssetParser(assets);
        var settings = new FakeSettingsService(_osuFolderPath);
        var sut = new BeatmapRateGenerator(audio, osu, osb, parser, settings);
        return (audio, osb, osu, parser, settings, sut);
    }

    [Fact]
    public async Task NonAudioOsb_UsesConverterInsteadOfRawCopy()
    {
        const string osbName = "foo.osb";
        // 実ファイル（生コピー対象になり得るので元データを置く）
        File.WriteAllText(Path.Combine(_beatmapFolderPath, osbName), "ORIGINAL_OSB");

        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { osbName },
        };

        var (_, osb, _, _, _, sut) = CreateSut(assets);

        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗しました: {result.ErrorMessage}");
        Assert.Single(osb.Calls);

        // .osz 内に foo.osb が converter 由来の内容（"// converted"）で含まれる
        var osz = result.GeneratedOszPath!;
        using var archive = ZipFile.OpenRead(osz);
        var entry = archive.GetEntry(osbName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd().TrimEnd('\r', '\n');
        Assert.Equal("// converted", content);
    }

    [Fact]
    public async Task OsbConversion_ReceivesFinalSampleNameMap()
    {
        const string osbName = "story.osb";
        const string sampleName = "s.mp3";

        File.WriteAllText(Path.Combine(_beatmapFolderPath, osbName), "ORIGINAL_OSB");
        // ID3 ヘッダ風のダミー mp3
        File.WriteAllBytes(Path.Combine(_beatmapFolderPath, sampleName), [0x49, 0x44, 0x33]);

        var assets = new OsuReferencedAssets
        {
            MainAudioFilename = AudioFileName,
            SampleAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sampleName },
            NonAudioFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { osbName },
        };

        var (audio, osb, _, _, _, sut) = CreateSut(assets);

        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗しました: {result.ErrorMessage}");
        Assert.Single(osb.Calls);

        var receivedMap = osb.Calls[0].Options.SampleFilenameMap;
        Assert.NotNull(receivedMap);
        Assert.True(receivedMap!.ContainsKey(sampleName),
            $"SampleFilenameMap に '{sampleName}' が含まれていません。実際: {string.Join(",", receivedMap!.Keys)}");

        // FakeAudioRateChanger は mp3 出力要求を ogg にフォールバックする 3ch 経路を模擬する。
        // 実装側 BeatmapRateGenerator の sampleNameMap 更新ロジックが
        // ActualOutputPath をもとに map の値を書き換えていることを直接検証する。
        var sampleCall = audio.Calls.Single(c =>
            string.Equals(Path.GetFileName(c.InputPath), sampleName, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(sampleCall.ActualOutputPath);
        Assert.NotEqual(sampleCall.OutputPath, sampleCall.ActualOutputPath);

        var renamed = receivedMap[sampleName];
        Assert.Equal(Path.GetFileName(sampleCall.ActualOutputPath), renamed);
        Assert.EndsWith("_dt.ogg", renamed); // mp3 → ogg フォールバック後の拡張子
        Assert.StartsWith("s_", renamed);    // BuildAudioFileName の prefix
    }

    [Fact]
    public async Task NonOsbNonAudio_StillUsesRawCopy()
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

        var (_, osb, _, _, _, sut) = CreateSut(assets);

        var beatmap = CreateBeatmap();
        var options = new RateGenerationOptions { Rate = 1.25 };

        var result = await sut.GenerateAsync(beatmap, options);

        Assert.True(result.Success, $"GenerateAsync が失敗しました: {result.ErrorMessage}");
        Assert.Empty(osb.Calls);

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
        public List<AudioCall> Calls { get; } = [];

        public Task<AudioRateChangeResult> ChangeRateAsync(
            string inputPath,
            string outputPath,
            double rate,
            bool changePitch,
            int? mp3VbrQuality = null,
            CancellationToken cancellationToken = default)
        {
            // 3ch フォールバックの模擬: .mp3 出力要求は .ogg に切り替えて
            // ActualOutputPath として通知する。これにより
            // BeatmapRateGenerator 側の sampleNameMap 更新分岐
            // (`if (sampleResult.ActualOutputPath is not null)`) を確実に踏ませる。
            var actualOutputPath = outputPath;
            if (string.Equals(Path.GetExtension(outputPath), ".mp3", StringComparison.OrdinalIgnoreCase))
            {
                actualOutputPath = Path.ChangeExtension(outputPath, ".ogg");
            }

            var dir = Path.GetDirectoryName(actualOutputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(inputPath, actualOutputPath, overwrite: true);

            var reportedActual = actualOutputPath != outputPath ? actualOutputPath : null;
            Calls.Add(new AudioCall(inputPath, outputPath, reportedActual));
            return Task.FromResult(new AudioRateChangeResult(true, reportedActual));
        }
    }

    private sealed record AudioCall(string InputPath, string OutputPath, string? ActualOutputPath);

    private sealed class FakeOsbFileRateConverter : IOsbFileRateConverter
    {
        public List<(string Src, string Dst, OsbFileConvertOptions Options)> Calls { get; } = [];

        public void Convert(string sourceOsbPath, string destinationOsbPath, OsbFileConvertOptions options)
        {
            Calls.Add((sourceOsbPath, destinationOsbPath, options));
            File.WriteAllText(destinationOsbPath, "// converted");
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
