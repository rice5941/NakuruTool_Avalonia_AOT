using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
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

        await gate.WaitForTurnAsync(CancellationToken.None);
        var first = Stopwatch.StartNew();

        // 前回開始から 300ms 処理していた想定 → 残り約 100ms だけ待つ
        await Task.Delay(300);
        var beforeSecond = first.Elapsed;
        await gate.WaitForTurnAsync(CancellationToken.None);
        var secondStart = first.Elapsed;

        Assert.True(secondStart >= interval - TimeSpan.FromMilliseconds(20), $"started at {secondStart}");
        Assert.True(secondStart - beforeSecond < TimeSpan.FromMilliseconds(300), $"waited {secondStart - beforeSecond}");
    }

    [Fact]
    public async Task Gate_DoesNotWaitWhenIntervalAlreadyElapsed()
    {
        var gate = new DownloadIntervalGate(TimeSpan.FromMilliseconds(100));
        await gate.WaitForTurnAsync(CancellationToken.None);
        await Task.Delay(150);

        var sw = Stopwatch.StartNew();
        await gate.WaitForTurnAsync(CancellationToken.None);
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

        var path = await downloader.DownloadAsync(BeatmapMirrors.GetDownloadUrl(null, 320118), _folder, 320118, CancellationToken.None);

        Assert.Equal(Path.Combine(_folder, "320118 Artist - Title.osz"), path);
        Assert.Equal("PK\u0003\u0004"u8.ToArray(), await File.ReadAllBytesAsync(path));
        Assert.Equal("https://mirror.hinamizawa.ai/api/v1/hinai/d/320118", handler.Requests.Single().ToString());
        Assert.Empty(Directory.GetFiles(_folder, "*.download"));
    }

    [Fact]
    public async Task Download_RetriesAfterServiceUnavailable()
    {
        var handler = new StubHandler(n =>
        {
            if (n == 1)
            {
                var busy = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                busy.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return busy;
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var path = await downloader.DownloadAsync("https://mirror.example/d/5", _folder, 5, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(Path.Combine(_folder, "5.osz"), path);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "404")]
    [InlineData(HttpStatusCode.TooManyRequests, "429")]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    public async Task Download_FailsWithoutLeavingFiles(HttpStatusCode status, string expectedInMessage)
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return response;
        });
        var downloader = new OszDownloader(new HttpClient(handler), new DownloadIntervalGate(TimeSpan.Zero));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAsync("https://mirror.example/d/7", _folder, 7, CancellationToken.None));

        Assert.Contains(expectedInMessage, ex.Message);
        Assert.Empty(Directory.GetFiles(_folder));
        var expectedAttempts = status == HttpStatusCode.TooManyRequests ? OszDownloader.MaxAttempts : 1;
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

        await Assert.ThrowsAsync<HttpRequestException>(
            () => downloader.DownloadAsync("https://mirror.example/d/8", _folder, 8, CancellationToken.None));
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

    private sealed class StubHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(Requests.Count));
        }
    }
}
