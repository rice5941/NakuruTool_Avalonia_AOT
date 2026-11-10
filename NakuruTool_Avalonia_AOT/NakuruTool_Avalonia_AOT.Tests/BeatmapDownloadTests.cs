using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

public sealed class BeatmapDownloadTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("NakuruTool-osz-tests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Theory]
    [InlineData("https://catboy.best/d/", BeatmapMirrors.Hinai)]
    [InlineData(null, BeatmapMirrors.Hinai)]
    [InlineData("", BeatmapMirrors.Hinai)]
    [InlineData(BeatmapMirrors.Nerinyan, BeatmapMirrors.Nerinyan)]
    [InlineData(BeatmapMirrors.Hinai, BeatmapMirrors.Hinai)]
    public void Normalize_ReplacesUnknownMirrorWithHinai(string? input, string expected)
        => Assert.Equal(expected, BeatmapMirrors.Normalize(input));

    [Fact]
    public void GetDownloadUrl_UsesHinaiBinaryEndpoint()
        => Assert.Equal("https://mirror.hinamizawa.ai/api/v1/hinai/d/320118",
            BeatmapMirrors.GetDownloadUrl("https://catboy.best/d/", 320118));

    [Fact]
    public async Task Gate_WaitsIntervalMeasuredFromPreviousStart()
    {
        var interval = TimeSpan.FromMilliseconds(400);
        var gate = new DownloadIntervalGate(interval);

        await gate.WaitForTurnAsync(null, CancellationToken.None);
        var first = Stopwatch.StartNew();

        // 前回開始から 300ms 処理していた想定 → 残り約 100ms だけ待つ
        await Task.Delay(300);
        var beforeSecond = first.Elapsed;
        await gate.WaitForTurnAsync(null, CancellationToken.None);
        var secondStart = first.Elapsed;

        Assert.True(secondStart >= interval - TimeSpan.FromMilliseconds(20), $"started at {secondStart}");
        Assert.True(secondStart - beforeSecond < TimeSpan.FromMilliseconds(300), $"waited {secondStart - beforeSecond}");
    }

    [Fact]
    public async Task Gate_DoesNotWaitWhenIntervalAlreadyElapsed()
    {
        var gate = new DownloadIntervalGate(TimeSpan.FromMilliseconds(100));
        await gate.WaitForTurnAsync(null, CancellationToken.None);
        await Task.Delay(150);

        var sw = Stopwatch.StartNew();
        await gate.WaitForTurnAsync(null, CancellationToken.None);
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(50), $"waited {sw.Elapsed}");
    }

    [Fact]
    public async Task Download_SavesOszUsingContentDispositionName()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("PK\u0003\u0004"u8.ToArray()) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-osu-beatmap-archive");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "\"320118 Artist - Title.osz\"" };
            return response;
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var path = await downloader.DownloadAsync(BeatmapMirrors.GetDownloadUrl(null, 320118), null, _folder, 320118, null, CancellationToken.None);

        Assert.Equal(Path.Combine(_folder, "320118 Artist - Title.osz"), path);
        Assert.Equal("PK\u0003\u0004"u8.ToArray(), await File.ReadAllBytesAsync(path));
        Assert.Equal("https://mirror.hinamizawa.ai/api/v1/hinai/d/320118", handler.Requests.Single().ToString());
        Assert.Empty(Directory.GetFiles(_folder, "*.download"));
    }

    [Fact]
    public async Task Download_RetriesSameUrlAfterRateLimit()
    {
        var handler = new StubHandler(n =>
        {
            if (n == 1)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return limited;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var path = await downloader.DownloadAsync("https://mirror.example/d/5", null, _folder, 5, null, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, uri => Assert.Equal("https://mirror.example/d/5", uri.ToString()));
        Assert.Equal(Path.Combine(_folder, "5.osz"), path);
    }

    [Fact]
    public async Task Download_On503_ResolvesDirectUrlAndDownloadsFromIt()
    {
        var handler = StubHandler.ForRequest(request => request.RequestUri!.ToString() switch
        {
            "https://mirror.hinamizawa.ai/api/v1/hinai/d/2285358" => Unavailable(),
            "https://mirror.hinamizawa.ai/d/2285358" => Json("""{"success":true,"download_url":"https://osu.direct/api/d/2285358","beatmapset_id":2285358}"""),
            "https://osu.direct/api/d/budget" => Json(FallbackBudgetTests.BudgetJson(remaining: 137, remainingBytes: 7_995_105_267)),
            // osu.direct はストレージへリダイレクトし、最終応答には Content-Disposition が無い
            "https://osu.direct/api/d/2285358" => Osz(request, finalUri: "https://storage.osu.direct/osz/2285358%20kessoku%20band%20-%20Ano%20Band.osz?X-Amz-Expires=15"),
            var other => throw new InvalidOperationException(other),
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));
        var progress = new List<OszDownloadProgress>();

        var path = await downloader.DownloadAsync(
            BeatmapMirrors.GetDownloadUrl(null, 2285358),
            BeatmapMirrors.GetFallbackResolveUrl(null, 2285358),
            _folder, 2285358, progress.Add, CancellationToken.None);

        Assert.Equal(Path.Combine(_folder, "2285358 kessoku band - Ano Band.osz"), path);
        Assert.Equal(
            ["https://mirror.hinamizawa.ai/api/v1/hinai/d/2285358", "https://mirror.hinamizawa.ai/d/2285358", "https://osu.direct/api/d/budget", "https://osu.direct/api/d/2285358"],
            handler.Requests.Select(uri => uri.ToString()));
        // 503 では同じ URL を再試行しない。代替経路の問い合わせ先も hinai なので Retry-After だけ待つ
        Assert.DoesNotContain(progress, p => p.Phase == DownloadPhase.WaitingRetry);
        Assert.Contains(progress, p => p.Phase == DownloadPhase.WaitingRetryAfter && p.StatusCode == 503);
        Assert.Contains(progress, p => p.Phase == DownloadPhase.Downloading && p.FallbackUrl == "https://osu.direct/api/d/2285358");
    }

    [Theory]
    [InlineData("""{"success":false,"error":"All mirrors returned errors"}""", HttpStatusCode.ServiceUnavailable)]
    [InlineData("""{"success":true,"download_url":"http://insecure.example/d/1"}""", HttpStatusCode.OK)]
    [InlineData("""not json""", HttpStatusCode.OK)]
    public async Task Download_On503_FailsAsUnavailableWhenNoDirectUrl(string resolveBody, HttpStatusCode resolveStatus)
    {
        var handler = StubHandler.ForRequest(request => request.RequestUri!.AbsolutePath.StartsWith("/api/v1/hinai/d/")
            ? Unavailable()
            : Json(resolveBody, resolveStatus));
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var ex = await Assert.ThrowsAsync<OszDownloadException>(() => downloader.DownloadAsync(
            BeatmapMirrors.GetDownloadUrl(null, 1), BeatmapMirrors.GetFallbackResolveUrl(null, 1),
            _folder, 1, null, CancellationToken.None));

        Assert.Equal(OszDownloadFailure.Unavailable, ex.Reason);
        Assert.Equal(2, handler.Requests.Count); // プロキシ 1 回 + 問い合わせ 1 回のみ (再試行しない)
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void FallbackResolveUrl_IsOnlyForHinai()
    {
        Assert.Equal("https://mirror.hinamizawa.ai/d/42", BeatmapMirrors.GetFallbackResolveUrl(BeatmapMirrors.Hinai, 42));
        Assert.Null(BeatmapMirrors.GetFallbackResolveUrl(BeatmapMirrors.Nerinyan, 42));
    }

    private static HttpResponseMessage Unavailable()
    {
        var response = Json("""{"error":"mirrors temporarily unavailable for this beatmapset, retry","success":false}""", HttpStatusCode.ServiceUnavailable);
        // 実際の hinai は 30 秒を返すが、テスト時間短縮のため 1 秒にする
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
        return response;
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Osz(HttpRequestMessage request, string finalUri)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("PK"u8.ToArray()) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.RequestUri = new Uri(finalUri); // 自動リダイレクト後と同じく、最終 URL を持たせる
        response.RequestMessage = request;
        return response;
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, OszDownloadFailure.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, OszDownloadFailure.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, OszDownloadFailure.Unavailable)]
    [InlineData(HttpStatusCode.InternalServerError, OszDownloadFailure.HttpError)]
    public async Task Download_FailsWithoutLeavingFiles(HttpStatusCode status, OszDownloadFailure expectedReason)
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var ex = await Assert.ThrowsAsync<OszDownloadException>(
            () => downloader.DownloadAsync("https://mirror.example/d/7", null, _folder, 7, null, CancellationToken.None));

        Assert.Equal(expectedReason, ex.Reason);
        Assert.Equal((int)status, ex.StatusCode);
        Assert.Empty(Directory.GetFiles(_folder));
        var expectedAttempts = expectedReason == OszDownloadFailure.RateLimited ? OszDownloader.MaxAttempts : 1;
        Assert.Equal(expectedAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task Download_RejectsHtmlResponse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>login</html>", System.Text.Encoding.UTF8, "text/html")
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var ex = await Assert.ThrowsAsync<OszDownloadException>(
            () => downloader.DownloadAsync("https://mirror.example/d/8", null, _folder, 8, null, CancellationToken.None));
        Assert.Equal(OszDownloadFailure.NotOsz, ex.Reason);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void GetFileName_SanitizesAndFallsBack()
    {
        using var bad = new HttpResponseMessage { Content = new ByteArrayContent([]) };
        bad.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "\"a/b:c.osz\"" };
        Assert.Equal("a_b_c.osz", OszDownloader.GetFileName(bad, 1));

        using var notOsz = new HttpResponseMessage { Content = new ByteArrayContent([]) };
        notOsz.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "x.zip" };
        Assert.Equal("1.osz", OszDownloader.GetFileName(notOsz, 1));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;

        public StubHandler(Func<int, HttpResponseMessage> respond) => _respond = (_, n) => respond(n);
        private StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) => _respond = respond;

        /// <summary>リクエスト内容 (URL) に応じて応答を返す。</summary>
        public static StubHandler ForRequest(Func<HttpRequestMessage, HttpResponseMessage> respond) => new((request, _) => respond(request));

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_respond(request, Requests.Count));
        }
    }
}
