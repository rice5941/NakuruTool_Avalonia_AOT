using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Translate;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>一括ダウンロード中に「何を待っているか」が UI に表示されることを確認する。</summary>
public sealed class DownloadStatusDisplayTests
{
    [AvaloniaFact]
    public void StatusBarAndRow_ShowWhatTheDownloadIsWaitingFor()
    {
        var service = new FakeDownloadService();
        using var settings = new StubSettingsService(new SettingsData());
        using var viewModel = new ImportExportBeatmapListViewModel(settings, service);

        var waiting = new ImportExportBeatmapItem { BeatmapSetId = 10, Title = "Waiting Song", KeyCount = 7 };
        var queued = new ImportExportBeatmapItem { BeatmapSetId = 20, Title = "Queued Song", KeyCount = 4 };
        viewModel.SetPreviewRows([waiting, queued], isImport: true);

        var view = new ImportExportBeatmapListView { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 420, Content = view };
        window.Show();
        try
        {
            Assert.False(viewModel.HasDownloadStatus);

            // 1) ダウンロード間隔待ち
            waiting.DownloadState = BeatmapDownloadState.Waiting;
            queued.DownloadState = BeatmapDownloadState.Queued;
            service.Raise(new DownloadActivity(DownloadPhase.WaitingInterval, 10,
                DateTimeOffset.Now.AddSeconds(3.5), Attempt: 1, MaxAttempts: 3));

            Assert.True(viewModel.HasDownloadStatus);
            Assert.True(viewModel.IsStatusWaitingInterval);
            Assert.Contains("Waiting Song", viewModel.DownloadStatusText);
            Assert.Contains("4", viewModel.DownloadStatusText); // 残り秒数 (切り上げ)
            Assert.Equal("4s", waiting.WaitCountdown);
            Assert.False(string.IsNullOrEmpty(waiting.WaitMessage));
            Assert.Null(queued.WaitCountdown);
            Save(window, "DownloadStatus_WaitingInterval");

            // 2) ミラー混雑による再試行待ち
            service.Raise(new DownloadActivity(DownloadPhase.WaitingRetry, 10,
                DateTimeOffset.Now.AddSeconds(29.5), StatusCode: 429, Attempt: 2, MaxAttempts: 3));

            Assert.True(viewModel.IsStatusWaitingRetry);
            Assert.False(viewModel.IsStatusWaitingInterval);
            Assert.Contains("429", viewModel.DownloadStatusText);
            Assert.Contains("2/3", viewModel.DownloadStatusText);
            Assert.Equal("30s", waiting.WaitCountdown);
            Assert.Contains("429", waiting.WaitMessage);
            Save(window, "DownloadStatus_WaitingRetry");

            // 3) 代替経路で通信中 → 行の待機表示は消え、代替経路であることを表示
            waiting.DownloadState = BeatmapDownloadState.Downloading;
            service.Raise(new DownloadActivity(DownloadPhase.Downloading, 10, Attempt: 1, MaxAttempts: 3,
                FallbackUrl: "https://osu.direct/api/d/10"));
            Assert.Equal(
                string.Format(LanguageService.Instance.GetString("ImportExport.DownloadStatus.DownloadingFallback"), "Waiting Song", "https://osu.direct/api/d/10"),
                viewModel.DownloadStatusText);
            Assert.Contains("https://osu.direct/api/d/10", viewModel.DownloadStatusText);
            Assert.Null(waiting.WaitCountdown);
            Save(window, "DownloadStatus_Fallback");

            // 通信中 → 行の待機表示は消える
            waiting.DownloadState = BeatmapDownloadState.Downloading;
            service.Raise(new DownloadActivity(DownloadPhase.Downloading, 10, Attempt: 3, MaxAttempts: 3));
            Assert.True(viewModel.IsStatusDownloading);
            Assert.Null(waiting.WaitCountdown);
            Assert.Null(waiting.WaitMessage);

            // 4) 完了 → ステータス欄は消える
            service.Raise(DownloadActivity.Idle);
            Assert.False(viewModel.HasDownloadStatus);
            Assert.Equal(string.Empty, viewModel.DownloadStatusText);
            var bar = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "DownloadStatusBar");
            Assert.False(bar.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ErrorTooltip_IsLocalizedFromFailureReason()
    {
        var service = new FakeDownloadService();
        using var settings = new StubSettingsService(new SettingsData());
        using var viewModel = new ImportExportBeatmapListViewModel(settings, service);
        var failed = new ImportExportBeatmapItem { BeatmapSetId = 30, Title = "Failed Song", KeyCount = 7 };
        viewModel.SetPreviewRows([failed], isImport: true);

        var view = new ImportExportBeatmapListView { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 300, Content = view };
        window.Show();
        try
        {
            failed.DownloadState = BeatmapDownloadState.Error;
            failed.Error = DownloadError.FromException(new OszDownloadException(OszDownloadFailure.Unavailable, 503));

            var expected = string.Format(LanguageService.Instance.GetString("ImportExport.DownloadError.Unavailable"), 503);
            Assert.Equal(expected, failed.ErrorMessage);
            Assert.NotEqual("ImportExport.DownloadError.Unavailable", LanguageService.Instance.GetString("ImportExport.DownloadError.Unavailable"));

            Dispatcher.UIThread.RunJobs();
            // 行のリトライボタンのツールチップに翻訳済みの理由が出ている
            Assert.Single(view.GetVisualDescendants().OfType<Button>(),
                b => b.IsEffectivelyVisible && Equals(ToolTip.GetTip(b), expected));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DownloadedIcon_ShowsFallbackUrlOnlyWhenFallbackWasUsed()
    {
        var viaMirror = new ImportExportBeatmapItem { BeatmapSetId = 1, DownloadState = BeatmapDownloadState.Downloaded };
        var viaFallback = new ImportExportBeatmapItem
        {
            BeatmapSetId = 2,
            DownloadState = BeatmapDownloadState.Downloaded,
            FallbackUrl = "https://osu.direct/api/d/2",
        };

        Assert.Null(viaMirror.DownloadedTip);
        Assert.Equal(
            string.Format(LanguageService.Instance.GetString("ImportExport.DownloadStatus.DownloadedFallbackTip"), "https://osu.direct/api/d/2"),
            viaFallback.DownloadedTip);
        Assert.Contains("https://osu.direct/api/d/2", viaFallback.DownloadedTip);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public void FromException_MapsToTranslatedKeys(Exception exception, string expectedKey)
    {
        var error = DownloadError.FromException(exception);
        Assert.Equal(expectedKey, error.ResourceKey);
    }

    public static TheoryData<Exception, string> FailureCases() => new()
    {
        { new OszDownloadException(OszDownloadFailure.NotFound, 404), "ImportExport.DownloadError.NotFound" },
        { new OszDownloadException(OszDownloadFailure.RateLimited, 429), "ImportExport.DownloadError.RateLimited" },
        { new OszDownloadException(OszDownloadFailure.Unavailable, 503), "ImportExport.DownloadError.Unavailable" },
        { new OszDownloadException(OszDownloadFailure.HttpError, 500), "ImportExport.DownloadError.Http" },
        { new OszDownloadException(OszDownloadFailure.NotOsz, 200), "ImportExport.DownloadError.NotOsz" },
        { new TaskCanceledException(), "ImportExport.DownloadError.Timeout" },
        { new HttpRequestException(), "ImportExport.DownloadError.Network" },
        { new IOException(), "ImportExport.DownloadError.SaveFailed" },
        { new InvalidOperationException(), "ImportExport.DownloadError.Unknown" },
    };

    /// <summary>ダウンロード関連の文言が全言語に揃っていること。</summary>
    [Fact]
    public void AllLanguages_DefineDownloadStatusAndErrorKeys()
    {
        var languagesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "NakuruTool_Avalonia_AOT", "Features", "Translate", "Resources", "Languages"));
        var jaKeys = ReadDownloadKeys(Path.Combine(languagesDir, "ja-JP.json"));
        Assert.Equal(23, jaKeys.Count);

        foreach (var file in Directory.GetFiles(languagesDir, "*.json"))
            Assert.Equal(jaKeys, ReadDownloadKeys(file));
    }

    private static SortedSet<string> ReadDownloadKeys(string path)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var keys = new SortedSet<string>();
        foreach (var property in doc.RootElement.GetProperty("ImportExport").EnumerateObject())
        {
            if (property.Name.StartsWith("DownloadStatus.") || property.Name.StartsWith("DownloadError."))
                keys.Add(property.Name);
        }
        return keys;
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var directory = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"));
    }

    private sealed class FakeDownloadService : IBeatmapDownloadService
    {
        public DownloadActivity CurrentActivity { get; private set; } = DownloadActivity.Idle;
        public event EventHandler<DownloadActivity>? ActivityChanged;

        public void Raise(DownloadActivity activity)
        {
            CurrentActivity = activity;
            ActivityChanged?.Invoke(this, activity);
        }

        public void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems) { }
        public Task CancelAllAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class StubSettingsService(ISettingsData settingsData) : ISettingsService
    {
        public ISettingsData SettingsData { get; } = settingsData;
        public bool SaveSettings(SettingsData settings) => true;
        public bool CheckSettingsPath() => true;
        public string GetSettingsPath() => string.Empty;
        public void Dispose() { }
    }
}

public sealed class OszDownloaderProgressTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("NakuruTool-osz-progress-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task ReportsIntervalWaitRetryWaitAndDownloading()
    {
        var calls = 0;
        var handler = new LambdaHandler(() =>
        {
            calls++;
            if (calls == 1)
            {
                var busy = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero);
                return busy;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
        });
        var gate = new DownloadIntervalGate(TimeSpan.FromMilliseconds(200));
        await gate.WaitForTurnAsync(null, CancellationToken.None); // 直前に別のダウンロードが開始された想定
        var downloader = new OszDownloader(new HttpClient(handler), gate);
        var progress = new List<OszDownloadProgress>();

        await downloader.DownloadAsync("https://mirror.example/d/1", null, _folder, 1, progress.Add, CancellationToken.None);

        Assert.Collection(progress,
            p => { Assert.Equal(DownloadPhase.WaitingInterval, p.Phase); Assert.True(p.Delay > TimeSpan.Zero); },
            p => { Assert.Equal(DownloadPhase.Downloading, p.Phase); Assert.Equal(1, p.Attempt); },
            p => { Assert.Equal(DownloadPhase.WaitingRetry, p.Phase); Assert.Equal(429, p.StatusCode); Assert.Equal(2, p.Attempt); },
            p => Assert.Equal(DownloadPhase.WaitingInterval, p.Phase),
            p => { Assert.Equal(DownloadPhase.Downloading, p.Phase); Assert.Equal(2, p.Attempt); });
    }

    private sealed class LambdaHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond());
    }
}
