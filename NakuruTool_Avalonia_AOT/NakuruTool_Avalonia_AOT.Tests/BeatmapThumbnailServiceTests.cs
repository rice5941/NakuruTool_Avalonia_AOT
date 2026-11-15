using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Thumbnails;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="BeatmapThumbnailService"/> の読み込み・共有・優先度・追い出しの挙動を固定する。
/// UI スレッドへの Post はテスト内のキューに溜め、テストスレッドで実行する。
/// </summary>
public sealed class BeatmapThumbnailServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SettingsData _settings;
    private readonly ConcurrentQueue<Action> _uiQueue = new();
    private readonly ConcurrentQueue<string> _decoded = new();

    public BeatmapThumbnailServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "NakuruThumbTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Songs"));
        _settings = new SettingsData { OsuFolderPath = _root };
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [AvaloniaFact]
    public void SetPage_LoadsImage_AndSharesDecodeForSameBackground()
    {
        using var service = CreateService();
        var a = CreateBeatmap("set1", "a.osu", "bg.jpg");
        var b = CreateBeatmap("set1", "b.osu", "bg.jpg");
        using var session = service.CreateSession();
        var thumbs = new BeatmapThumbnail?[2];

        session.SetPage(new[] { a, b }, thumbs);

        Assert.All(thumbs, t => Assert.NotNull(t));
        Assert.All(thumbs, t => Assert.Null(t!.Image)); // 読み込みは非同期
        Pump(() => thumbs[0]!.Image is not null && thumbs[1]!.Image is not null);

        Assert.Same(thumbs[0]!.Image, thumbs[1]!.Image);
        Assert.Single(_decoded);
    }

    [AvaloniaFact]
    public void SetPage_SameBeatmap_ReturnsCachedThumbnail_WithoutReload()
    {
        using var service = CreateService();
        var a = CreateBeatmap("set1", "a.osu", "bg.jpg");
        using var session = service.CreateSession();
        var first = new BeatmapThumbnail?[1];
        var second = new BeatmapThumbnail?[1];

        session.SetPage(new[] { a }, first);
        Pump(() => first[0]!.Image is not null);
        session.SetPage(new[] { a with { Title = "copy" } }, second);

        Assert.Same(first[0], second[0]);
        Assert.NotNull(second[0]!.Image); // キャッシュから即表示
        Assert.Single(_decoded);
    }

    [AvaloniaFact]
    public void MissingBackground_IsFailed_AndNotRetried()
    {
        using var service = CreateService();
        var noBackground = CreateBeatmap("set1", "a.osu", background: null);
        var missingOsu = CreateBeatmap("set2", "missing.osu", background: null, writeOsu: false);
        var missingImage = CreateBeatmap("set3", "c.osu", "nothing.jpg", writeImage: false);
        var page = new[] { noBackground, missingOsu, missingImage };
        using var session = service.CreateSession();
        var thumbs = new BeatmapThumbnail?[3];

        session.SetPage(page, thumbs);
        Pump(() => thumbs.All(t => t!.State == BeatmapThumbnailState.Failed));
        session.SetPage(page, thumbs);
        Pump(() => false, timeoutMs: 100);

        Assert.All(thumbs, t =>
        {
            Assert.Equal(BeatmapThumbnailState.Failed, t!.State);
            Assert.Null(t.Image);
        });
        Assert.Empty(_decoded);
    }

    [AvaloniaFact]
    public void SetPage_WithoutOsuFolder_ReturnsNull()
    {
        _settings.OsuFolderPath = string.Empty;
        using var service = CreateService();
        using var session = service.CreateSession();
        var thumbs = new BeatmapThumbnail?[1];

        session.SetPage(new[] { CreateBeatmap("set1", "a.osu", "bg.jpg") }, thumbs);

        Assert.Null(thumbs[0]);
    }

    [AvaloniaFact]
    public void PageChange_SkipsQueuedItemsNoLongerDisplayed()
    {
        using var gate = new ManualResetEventSlim(false);
        using var firstDecodeStarted = new ManualResetEventSlim(false);
        using var service = CreateService(maxWorkers: 1, decoder: path =>
        {
            firstDecodeStarted.Set();
            gate.Wait(TimeSpan.FromSeconds(5));
            return DefaultTestDecoder(path);
        });
        var a = CreateBeatmap("a", "a.osu", "a.jpg");
        var b = CreateBeatmap("b", "b.osu", "b.jpg");
        var c = CreateBeatmap("c", "c.osu", "c.jpg");
        var d = CreateBeatmap("d", "d.osu", "d.jpg");
        using var session = service.CreateSession();
        var page1 = new BeatmapThumbnail?[3];
        var page2 = new BeatmapThumbnail?[1];

        session.SetPage(new[] { a, b, c }, page1);
        Assert.True(firstDecodeStarted.Wait(TimeSpan.FromSeconds(5)));
        session.SetPage(new[] { d }, page2);
        gate.Set();
        Pump(() => page2[0]!.Image is not null
                   && page1[0]!.State == BeatmapThumbnailState.Loaded
                   && page1[1]!.State == BeatmapThumbnailState.NotLoaded
                   && page1[2]!.State == BeatmapThumbnailState.NotLoaded);

        Assert.Equal(new[] { "a.jpg", "d.jpg" }, _decoded.Select(Path.GetFileName).ToArray());
        Assert.Equal(BeatmapThumbnailState.NotLoaded, page1[1]!.State);
        Assert.Equal(BeatmapThumbnailState.NotLoaded, page1[2]!.State);
    }

    [AvaloniaFact]
    public void Paused_SetPageDoesNotLoad_AndResumeLoadsOnlyCurrentPage()
    {
        using var service = CreateService();
        var a = CreateBeatmap("a", "a.osu", "a.jpg");
        var b = CreateBeatmap("b", "b.osu", "b.jpg");
        var c = CreateBeatmap("c", "c.osu", "c.jpg");
        using var session = service.CreateSession();
        var page = new BeatmapThumbnail?[2];

        session.SetPaused(true);
        session.SetPage(new[] { a, b }, page);
        var thumbA = page[0]!;
        Assert.NotNull(thumbA); // 停止中もサムネイル自体は返す
        session.SetPage(new[] { c }, page); // 停止中にページが進む（音楽プレイヤーモードの曲送り）
        var thumbC = page[0]!;
        Pump(() => false, timeoutMs: 100);
        Assert.Empty(_decoded);
        Assert.Equal(BeatmapThumbnailState.NotLoaded, thumbA.State);

        session.SetPaused(false);
        Pump(() => thumbC.Image is not null);

        Assert.Equal(new[] { "c.jpg" }, _decoded.Select(Path.GetFileName).ToArray());
        Assert.Null(thumbA.Image);
    }

    [AvaloniaFact]
    public void Pause_DropsQueuedItems()
    {
        using var gate = new ManualResetEventSlim(false);
        using var firstDecodeStarted = new ManualResetEventSlim(false);
        using var service = CreateService(maxWorkers: 1, decoder: path =>
        {
            firstDecodeStarted.Set();
            gate.Wait(TimeSpan.FromSeconds(5));
            return DefaultTestDecoder(path);
        });
        using var session = service.CreateSession();
        var page = new BeatmapThumbnail?[3];

        session.SetPage(new[]
        {
            CreateBeatmap("a", "a.osu", "a.jpg"),
            CreateBeatmap("b", "b.osu", "b.jpg"),
            CreateBeatmap("c", "c.osu", "c.jpg"),
        }, page);
        Assert.True(firstDecodeStarted.Wait(TimeSpan.FromSeconds(5)));
        session.SetPaused(true);
        gate.Set();
        Pump(() => page[0]!.State == BeatmapThumbnailState.Loaded
                   && page[1]!.State == BeatmapThumbnailState.NotLoaded
                   && page[2]!.State == BeatmapThumbnailState.NotLoaded);

        // 読み込み中だった 1 件だけ。待ちの 2 件は読まずに捨てる
        Assert.Equal(new[] { "a.jpg" }, _decoded.Select(Path.GetFileName).ToArray());
    }

    [AvaloniaFact]
    public void Paused_KeepsLoadedThumbnailsDisplayed_AndNotEvicted()
    {
        using var service = CreateService(capacity: 1);
        var a = CreateBeatmap("a", "a.osu", "a.jpg");
        var b = CreateBeatmap("b", "b.osu", "b.jpg");
        using var session = service.CreateSession();
        var page = new BeatmapThumbnail?[2];
        session.SetPage(new[] { a }, page);
        Pump(() => page[0]!.Image is not null);
        var thumbA = page[0]!;

        session.SetPaused(true);
        session.SetPage(new[] { a, b }, page);

        Assert.Same(thumbA, page[0]);
        Assert.NotNull(thumbA.Image); // 読み込み済みはキャッシュから表示
        Assert.False(thumbA.Evicted);  // 表示中なので上限を超えても残す
        Assert.Equal(BeatmapThumbnailState.NotLoaded, page[1]!.State);
    }

    [AvaloniaFact]
    public void Paused_OtherActiveSessionStillLoadsSharedThumbnail()
    {
        using var service = CreateService();
        var a = CreateBeatmap("a", "a.osu", "a.jpg");
        using var paused = service.CreateSession();
        using var active = service.CreateSession();
        var pausedPage = new BeatmapThumbnail?[1];
        var activePage = new BeatmapThumbnail?[1];

        paused.SetPaused(true);
        paused.SetPage(new[] { a }, pausedPage);
        active.SetPage(new[] { a }, activePage);
        Pump(() => activePage[0]!.Image is not null);

        Assert.Same(pausedPage[0], activePage[0]);
        Assert.Single(_decoded);
    }

    [AvaloniaFact]
    public void Eviction_RemovesOnlyThumbnailsNotDisplayed()
    {
        using var service = CreateService(capacity: 1);
        var a = CreateBeatmap("a", "a.osu", "a.jpg");
        var b = CreateBeatmap("b", "b.osu", "b.jpg");
        var c = CreateBeatmap("c", "c.osu", "c.jpg");
        using var session = service.CreateSession();
        var page = new BeatmapThumbnail?[2];

        session.SetPage(new[] { a }, page);
        Pump(() => page[0]!.Image is not null);
        var thumbA = page[0]!;

        session.SetPage(new[] { b }, page);
        Pump(() => page[0]!.Image is not null);
        var thumbB = page[0]!;

        // a は表示から外れたので追い出され、表示が消える
        Assert.True(thumbA.Evicted);
        Assert.Null(thumbA.Image);
        Assert.Equal(1, service.CachedCount);

        // 表示中のものは上限を超えても残る
        session.SetPage(new[] { b, c }, page);
        Pump(() => page[1]!.Image is not null);
        Assert.False(thumbB.Evicted);
        Assert.NotNull(thumbB.Image);
        Assert.Equal(2, service.CachedCount);
    }

    [AvaloniaFact]
    public void OsuFolderChange_ClearsCache()
    {
        using var service = CreateService();
        using var session = service.CreateSession();
        var thumbs = new BeatmapThumbnail?[1];
        session.SetPage(new[] { CreateBeatmap("set1", "a.osu", "bg.jpg") }, thumbs);
        Pump(() => thumbs[0]!.Image is not null);

        _settings.OsuFolderPath = Path.Combine(_root, "other");

        Assert.Null(thumbs[0]!.Image);
        Assert.Equal(0, service.CachedCount);
    }

    [AvaloniaFact]
    public void DefaultDecoder_DownscalesToDecodeWidth()
    {
        var beatmap = CreateBeatmap("set1", "a.osu", "bg.png", writeImage: false);
        using (var source = new WriteableBitmap(new PixelSize(800, 450), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul))
        {
            source.Save(Path.Combine(_root, "Songs", "set1", "bg.png"), PngBitmapEncoderOptions.Default);
        }
        using var service = new BeatmapThumbnailService(new StubSettingsService(_settings))
        {
            PostToUi = _uiQueue.Enqueue,
        };
        using var session = service.CreateSession();
        var thumbs = new BeatmapThumbnail?[1];

        session.SetPage(new[] { beatmap }, thumbs);
        Pump(() => thumbs[0]!.Image is not null);

        Assert.Equal(BeatmapThumbnailService.DecodeWidth, thumbs[0]!.Image!.PixelSize.Width);
    }

    [AvaloniaFact]
    public void BeatmapList_ShowBeatmaps_CarryThumbnails()
    {
        using var service = CreateService();
        using var viewModel = new TestBeatmapListViewModel(new StubSettingsService(_settings), service);

        viewModel.SetTestSource(new[]
        {
            CreateBeatmap("set1", "a.osu", "bg.jpg"),
            CreateBeatmap("set2", "b.osu", "bg.jpg"),
        });

        Assert.All(viewModel.ShowBeatmaps, row => Assert.NotNull(row.Thumbnail));
        Pump(() => viewModel.ShowBeatmaps.All(row => row.Thumbnail!.Image is not null));
    }

    // ---- helpers ----

    private BeatmapThumbnailService CreateService(int capacity = 300, int maxWorkers = 2, Func<string, Bitmap?>? decoder = null)
        => new(new StubSettingsService(_settings))
        {
            Capacity = capacity,
            MaxWorkers = maxWorkers,
            Decoder = decoder ?? DefaultTestDecoder,
            PostToUi = _uiQueue.Enqueue,
        };

    private Bitmap DefaultTestDecoder(string path)
    {
        _decoded.Enqueue(path);
        return new WriteableBitmap(new PixelSize(4, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    /// <summary>UI スレッド宛ての処理を実行しながら、条件が満たされるまで待つ。</summary>
    private void Pump(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (true)
        {
            while (_uiQueue.TryDequeue(out var action))
                action();
            if (condition()) return;
            if (Environment.TickCount64 > deadline)
            {
                Assert.True(timeoutMs < 1000, "Timed out waiting for thumbnails.");
                return;
            }
            Thread.Sleep(5);
        }
    }

    private Beatmap CreateBeatmap(string folder, string osuFile, string? background, bool writeOsu = true, bool writeImage = true)
    {
        var dir = Path.Combine(_root, "Songs", folder);
        Directory.CreateDirectory(dir);
        if (writeOsu)
        {
            var events = background is null ? string.Empty : $"0,0,\"{background}\",0,0\n";
            File.WriteAllText(Path.Combine(dir, osuFile),
                $"osu file format v14\n\n[General]\nMode: 3\n\n[Events]\n//Background and Video events\n{events}\n[TimingPoints]\n");
        }
        if (writeImage && background is not null)
            File.WriteAllBytes(Path.Combine(dir, background), new byte[] { 1, 2, 3 });

        return new Beatmap
        {
            MD5Hash = folder + "/" + osuFile,
            KeyCount = 7,
            Title = "title",
            Artist = "artist",
            Version = "version",
            Creator = "creator",
            FolderName = folder,
            AudioFilename = "audio.mp3",
            OsuFileName = osuFile,
            Grade = string.Empty,
        };
    }

    private sealed class TestBeatmapListViewModel(ISettingsService settingsService, IBeatmapThumbnailService thumbnailService)
        : BeatmapListViewModelBase(settingsService, null, thumbnailService)
    {
        public void SetTestSource(Beatmap[] source) => SetSourceBeatmaps(source);
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
