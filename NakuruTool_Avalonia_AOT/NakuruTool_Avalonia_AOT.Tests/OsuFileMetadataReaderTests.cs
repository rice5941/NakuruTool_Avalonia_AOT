using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="OsuFileMetadataReader.TryReadBeatmapSetId"/> および
/// <see cref="OsuFileMetadataReader.TryReadBasicMetadata(string, out OsuFileBasicMetadata)"/>
/// の回帰テスト。
/// </summary>
public class OsuFileMetadataReaderTests : IDisposable
{
    private readonly string _tempDir;

    public OsuFileMetadataReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "NakuruTool_OsuFileMetadataReaderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
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

    private string WriteFile(IEnumerable<string> lines)
    {
        var path = Path.Combine(_tempDir, "test_" + Guid.NewGuid().ToString("N") + ".osu");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void TryReadBeatmapSetId_ReturnsTrue_WhenMetadataContainsValidId()
    {
        var path = WriteFile(new[]
        {
            "osu file format v14",
            "",
            "[General]",
            "AudioFilename: audio.mp3",
            "",
            "[Metadata]",
            "Title:Namae no Nai Kaibutsu",
            "Artist:EGOIST",
            "BeatmapID:2226278",
            "BeatmapSetID:1063231",
            "",
            "[Difficulty]",
            "HPDrainRate:7",
        });

        var ok = OsuFileMetadataReader.TryReadBeatmapSetId(path, out var id);

        Assert.True(ok);
        Assert.Equal(1063231, id);
    }

    [Fact]
    public void TryReadBeatmapSetId_AcceptsWhitespaceAroundColon()
    {
        var path = WriteFile(new[]
        {
            "[Metadata]",
            "BeatmapSetID :  42 ",
            "[Difficulty]",
        });

        var ok = OsuFileMetadataReader.TryReadBeatmapSetId(path, out var id);

        Assert.True(ok);
        Assert.Equal(42, id);
    }

    [Fact]
    public void TryReadBeatmapSetId_ReturnsFalse_WhenKeyMissing()
    {
        var path = WriteFile(new[]
        {
            "[Metadata]",
            "Title:t",
            "Artist:a",
            "[Difficulty]",
        });

        var ok = OsuFileMetadataReader.TryReadBeatmapSetId(path, out var id);

        Assert.False(ok);
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryReadBeatmapSetId_StopsAtNextSection_AndIgnoresOutsideKeys()
    {
        // [Metadata] 外に同名キーがあっても拾わない
        var path = WriteFile(new[]
        {
            "[General]",
            "BeatmapSetID:9999",
            "[Metadata]",
            "Title:t",
            "[Difficulty]",
            "BeatmapSetID:7777",
        });

        var ok = OsuFileMetadataReader.TryReadBeatmapSetId(path, out var id);

        Assert.False(ok);
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryReadBeatmapSetId_ReturnsFalse_WhenValueIsNotInteger()
    {
        var path = WriteFile(new[]
        {
            "[Metadata]",
            "BeatmapSetID:abc",
            "[Difficulty]",
        });

        var ok = OsuFileMetadataReader.TryReadBeatmapSetId(path, out var id);

        Assert.False(ok);
    }

    [Fact]
    public void TryReadBeatmapSetId_ReturnsFalse_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_tempDir, "missing.osu");

        var ok = OsuFileMetadataReader.TryReadBeatmapSetId(path, out var id);

        Assert.False(ok);
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryReadBeatmapSetId_ReturnsFalse_WhenPathIsNullOrEmpty()
    {
        Assert.False(OsuFileMetadataReader.TryReadBeatmapSetId("", out _));
        Assert.False(OsuFileMetadataReader.TryReadBeatmapSetId(null!, out _));
    }

    [Fact]
    public void TryReadBasicMetadata_FromPath_ReturnsAllFields()
    {
        var path = WriteFile(new[]
        {
            "osu file format v14",
            "[General]",
            "AudioFilename: audio.mp3",
            "[Metadata]",
            "Title:Namae no Nai Kaibutsu",
            "Artist:EGOIST",
            "Version:Fallacy",
            "Creator:ruka",
            "BeatmapSetID:1063231",
            "[Difficulty]",
            "CircleSize:7",
            "HPDrainRate:7",
        });

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(path, out var metadata);

        Assert.True(ok);
        Assert.Equal("Namae no Nai Kaibutsu", metadata.Title);
        Assert.Equal("EGOIST", metadata.Artist);
        Assert.Equal("Fallacy", metadata.Version);
        Assert.Equal("ruka", metadata.Creator);
        Assert.Equal(7.0, metadata.CircleSize);
        Assert.Equal(1063231, metadata.BeatmapSetId);
    }

    [Fact]
    public void TryReadBasicMetadata_FromStream_ReadsAllFields_AndDoesNotCloseStream()
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', new[]
        {
            "[Metadata]",
            "Title:t",
            "Artist:a",
            "Version:v",
            "Creator:c",
            "BeatmapSetID:42",
            "[Difficulty]",
            "CircleSize:4.2",
            "",
        }));
        using var ms = new MemoryStream(bytes);

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(ms, out var metadata);

        Assert.True(ok);
        Assert.Equal("t", metadata.Title);
        Assert.Equal("a", metadata.Artist);
        Assert.Equal("v", metadata.Version);
        Assert.Equal("c", metadata.Creator);
        Assert.Equal(4.2, metadata.CircleSize);
        Assert.Equal(42, metadata.BeatmapSetId);

        // stream が閉じられていないことを確認
        Assert.True(ms.CanRead);
        ms.Position = 0;
        Assert.Equal(bytes[0], (byte)ms.ReadByte());
    }

    [Fact]
    public void TryReadBasicMetadata_AcceptsWhitespaceAroundColon()
    {
        var path = WriteFile(new[]
        {
            "[Metadata]",
            "Title :  spaced title  ",
            "Artist  :a",
            "Version:  v",
            "Creator :c  ",
            "BeatmapSetID :  123 ",
            "[Difficulty]",
            "CircleSize :  5 ",
        });

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(path, out var metadata);

        Assert.True(ok);
        Assert.Equal("spaced title", metadata.Title);
        Assert.Equal("a", metadata.Artist);
        Assert.Equal("v", metadata.Version);
        Assert.Equal("c", metadata.Creator);
        Assert.Equal(5.0, metadata.CircleSize);
        Assert.Equal(123, metadata.BeatmapSetId);
    }

    [Fact]
    public void TryReadBasicMetadata_IgnoresKeysOutsideTargetSections()
    {
        var path = WriteFile(new[]
        {
            "[General]",
            "Title:wrong",
            "BeatmapSetID:9999",
            "CircleSize:9",
            "[Metadata]",
            "Title:right",
            "Artist:a",
            "Version:v",
            "Creator:c",
            "[Difficulty]",
            "CircleSize:3",
            "[Events]",
            "BeatmapSetID:8888",
            "CircleSize:8",
        });

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(path, out var metadata);

        Assert.True(ok);
        Assert.Equal("right", metadata.Title);
        Assert.Equal(3.0, metadata.CircleSize);
        Assert.Equal(-1, metadata.BeatmapSetId); // [Metadata] に無いので欠落扱い
    }

    [Fact]
    public void TryReadBasicMetadata_DefaultsWhenOptionalFieldsMissing()
    {
        var path = WriteFile(new[]
        {
            "[Metadata]",
            "Title:t",
            "Artist:a",
            "Version:v",
            "Creator:c",
            "[Difficulty]",
            "HPDrainRate:5",
        });

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(path, out var metadata);

        Assert.True(ok);
        Assert.Equal(-1, metadata.BeatmapSetId);
        Assert.Equal(0.0, metadata.CircleSize);
    }

    [Fact]
    public void TryReadBasicMetadata_ReturnsFalse_WhenRequiredFieldsMissing()
    {
        var path = WriteFile(new[]
        {
            "[Metadata]",
            "Title:t",
            "Artist:a",
            // Version / Creator なし
            "[Difficulty]",
            "CircleSize:4",
        });

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(path, out var metadata);

        Assert.False(ok);
        Assert.Equal(default, metadata);
    }

    [Fact]
    public void TryReadBasicMetadata_ReturnsFalse_WhenFileMissing()
    {
        var path = Path.Combine(_tempDir, "missing.osu");

        var ok = OsuFileMetadataReader.TryReadBasicMetadata(path, out var metadata);

        Assert.False(ok);
        Assert.Equal(default, metadata);
    }

    [Fact]
    public void TryReadBasicMetadata_ReturnsFalse_WhenPathIsNullOrEmpty()
    {
        Assert.False(OsuFileMetadataReader.TryReadBasicMetadata("", out _));
        Assert.False(OsuFileMetadataReader.TryReadBasicMetadata((string)null!, out _));
    }

    [Fact]
    public async Task TryPrepareContextMenu_ResolvesMissingBeatmapSetId_FromOsuFileAndCopiesUrl()
    {
        var folderName = "1063231 EGOIST - Namae no Nai Kaibutsu";
        var osuFileName = "EGOIST - Namae no Nai Kaibutsu (ruka) [Fallacy].osu";
        var beatmapFolder = Path.Combine(_tempDir, "Songs", folderName);
        Directory.CreateDirectory(beatmapFolder);
        File.WriteAllLines(Path.Combine(beatmapFolder, osuFileName), new[]
        {
            "osu file format v14",
            "[Metadata]",
            "BeatmapID:2226278",
            "BeatmapSetID:1063231",
            "[Difficulty]",
        });

        var settings = new SettingsData
        {
            OsuFolderPath = _tempDir,
            BeatmapMirrorUrl = "https://example.invalid/d/"
        };
        var viewModel = new TestBeatmapListViewModel(new StubSettingsService(settings));

        string? copied = null;
        viewModel.SetClipboardWriter(value =>
        {
            copied = value;
            return Task.CompletedTask;
        });

        var beatmap = CreateBeatmap(folderName, osuFileName) with { BeatmapSetId = 0 };

        Assert.True(viewModel.TryPrepareContextMenu(beatmap));
        Assert.True(viewModel.CopyDownloadUrlCommand.CanExecute(null));

        await viewModel.CopyDownloadUrlCommand.ExecuteAsync(null);

        Assert.Equal("https://example.invalid/d/1063231", copied);
    }

    private static Beatmap CreateBeatmap(string folderName, string osuFileName) => new()
    {
        MD5Hash = "md5",
        KeyCount = 7,
        Title = "Namae no Nai Kaibutsu",
        Artist = "EGOIST",
        Version = "Fallacy",
        Creator = "ruka",
        FolderName = folderName,
        AudioFilename = "audio.mp3",
        OsuFileName = osuFileName,
        Grade = string.Empty
    };

    private sealed class TestBeatmapListViewModel(ISettingsService settingsService)
        : BeatmapListViewModelBase(settingsService)
    {
    }

    private sealed class StubSettingsService(ISettingsData settingsData) : ISettingsService
    {
        public ISettingsData SettingsData { get; } = settingsData;
        public bool SaveSettings(SettingsData settings) => true;
        public bool CheckSettingsPath() => true;
        public string GetSettingsPath() => string.Empty;
        public void Dispose()
        {
        }
    }
}
