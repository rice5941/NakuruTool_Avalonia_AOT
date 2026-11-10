using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>代替経路を使う場合も含め、Retry-After で指定された時刻までそのサーバーへ送らないことを確認する。</summary>
public sealed class RetryAfterTests : IDisposable
{
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(50);

    private readonly string _folder = Directory.CreateTempSubdirectory("NakuruTool-retry-after-tests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task On503_WaitsRetryAfterBeforeQueryingFallbackOnSameServer()
    {
        var handler = new Handler(request => request.RequestUri!.ToString() switch
        {
            "https://mirror.hinamizawa.ai/api/v1/hinai/d/1" => Unavailable(RetryAfter),
            "https://mirror.hinamizawa.ai/d/1" => Json("""{"success":true,"download_url":"https://osu.direct/api/d/1"}"""),
            "https://osu.direct/api/d/budget" => Json(FallbackBudgetTests.BudgetJson(remaining: 150, remainingBytes: 7L << 30)),
            "https://osu.direct/api/d/1" => Osz(),
            var other => throw new InvalidOperationException(other),
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));
        var progress = new List<OszDownloadProgress>();

        await downloader.DownloadAsync(
            BeatmapMirrors.GetDownloadUrl(BeatmapMirrors.Hinai, 1), BeatmapMirrors.GetFallbackResolveUrl(BeatmapMirrors.Hinai, 1),
            _folder, 1, progress.Add, CancellationToken.None);

        var proxy = handler.TimeOf("https://mirror.hinamizawa.ai/api/v1/hinai/d/1");
        var resolve = handler.TimeOf("https://mirror.hinamizawa.ai/d/1");
        Assert.True(resolve - proxy >= RetryAfter - Tolerance, $"hinai への問い合わせが {resolve - proxy} で送られた");
        Assert.Contains(progress, p => p.Phase == DownloadPhase.WaitingRetryAfter && p.StatusCode == 503);
    }

    [Fact]
    public async Task On503WithoutFallback_NextDownloadToSameServerWaitsRetryAfter()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/1")
            ? Unavailable(RetryAfter)
            : Osz());
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        await Assert.ThrowsAsync<OszDownloadException>(
            () => downloader.DownloadAsync("https://api.nerinyan.moe/d/1", null, _folder, 1, null, CancellationToken.None));
        await downloader.DownloadAsync("https://api.nerinyan.moe/d/2", null, _folder, 2, null, CancellationToken.None);

        var first = handler.TimeOf("https://api.nerinyan.moe/d/1");
        var second = handler.TimeOf("https://api.nerinyan.moe/d/2");
        Assert.True(second - first >= RetryAfter - Tolerance, $"次の譜面が {second - first} で送られた");
    }

    [Fact]
    public async Task RetryAfter_IsPerServer()
    {
        var tracker = new RetryAfterTracker();
        tracker.Record(new Uri("https://mirror.hinamizawa.ai/api/v1/hinai/d/1"), TimeSpan.FromMinutes(5), 503);

        var sw = Stopwatch.StartNew();
        await tracker.WaitAsync("https://osu.direct/api/d/1", null, CancellationToken.None);

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task WaitAsync_ReportsRemainingTimeAndStatus()
    {
        var tracker = new RetryAfterTracker();
        tracker.Record(new Uri("https://mirror.hinamizawa.ai/d/1"), TimeSpan.FromMilliseconds(300), 503);
        (TimeSpan Delay, int Status)? reported = null;

        await tracker.WaitAsync("https://mirror.hinamizawa.ai/api/v1/hinai/d/2", (d, s) => reported = (d, s), CancellationToken.None);

        Assert.NotNull(reported);
        Assert.Equal(503, reported.Value.Status);
        Assert.True(reported.Value.Delay > TimeSpan.Zero);
    }

    [Fact]
    public void GetRetryAfter_SupportsSecondsAndDate()
    {
        var now = DateTimeOffset.UtcNow;
        using var seconds = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        seconds.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
        using var date = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        date.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(45));
        using var none = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterTracker.GetRetryAfter(seconds, now));
        Assert.InRange(RetryAfterTracker.GetRetryAfter(date, now)!.Value.TotalSeconds, 44, 46);
        Assert.Null(RetryAfterTracker.GetRetryAfter(none, now));
    }

    private static HttpResponseMessage Unavailable(TimeSpan retryAfter)
    {
        var response = Json("""{"error":"mirrors temporarily unavailable for this beatmapset, retry","success":false}""", HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
        return response;
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Osz()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return response;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<(string Url, TimeSpan At)> _requests = [];

        public TimeSpan TimeOf(string url) => _requests.Single(r => r.Url == url).At;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add((request.RequestUri!.ToString(), _clock.Elapsed));
            return Task.FromResult(respond(request));
        }
    }
}
