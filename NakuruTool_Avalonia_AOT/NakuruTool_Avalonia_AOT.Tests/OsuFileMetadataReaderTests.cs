using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="OsuFileMetadataReader.TryReadBeatmapSetId"/> の回帰テスト。
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
