using System.Net;
using System.Net.Http.Headers;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>代替経路 (osu.direct) のダウンロード予算に余裕を持って打ち切ることを確認する。</summary>
public sealed class FallbackBudgetTests : IDisposable
{
    private const long GiB = 1024L * 1024 * 1024;
    private const string ProxyPrefix = "https://mirror.hinamizawa.ai/api/v1/hinai/d/";
    private const string ResolvePrefix = "https://mirror.hinamizawa.ai/d/";
    private const string BudgetUrl = "https://osu.direct/api/d/budget";
    private const string DirectPrefix = "https://osu.direct/api/d/";

    private readonly string _folder = Directory.CreateTempSubdirectory("NakuruTool-budget-tests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary><c>/api/d/budget</c> の応答 (リセットは 1 時間後 / 1 日後)。</summary>
    internal static string BudgetJson(int remaining, long remainingBytes, bool exempt = false)
    {
        var hourReset = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();
        var dayReset = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        var exemptText = exempt ? "true" : "false";
        return "{\"code\":200,\"exempt\":" + exemptText
            + ",\"hourly\":{\"limit\":200,\"used\":" + (200 - remaining) + ",\"remaining\":" + remaining + ",\"resetAt\":" + hourReset + "}"
            + ",\"daily\":{\"limitBytes\":8589934592,\"remainingBytes\":" + remainingBytes + ",\"resetAt\":" + dayReset + "}}";
    }

    [Theory]
    [InlineData(10, 7 * GiB)]          // 残り 10 件
    [InlineData(3, 7 * GiB)]
    [InlineData(150, 1 * GiB)]         // 残り 1 GiB
    [InlineData(150, 1 * GiB - 1)]
    public async Task FailsWithFallbackLimit_WhenBudgetIsLow(int remaining, long remainingBytes)
    {
        var handler = new Handler(remaining, remainingBytes);
        var downloader = CreateDownloader(handler);

        var ex = await Assert.ThrowsAsync<OszDownloadException>(() => DownloadAsync(downloader, 1));

        Assert.Equal(OszDownloadFailure.FallbackLimitReached, ex.Reason);
        Assert.Equal("ImportExport.DownloadError.FallbackLimit", DownloadError.FromException(ex).ResourceKey);
        Assert.DoesNotContain(handler.Requests, uri => uri.StartsWith(DirectPrefix + "1"));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task DownloadsViaFallback_WhileBudgetHasRoom_AndCountsLocally()
    {
        // 残り 11 件: 1 件目は通り、手元で 10 件に減るので 2 件目は予算を再取得せずに打ち切る
        var handler = new Handler(remaining: 11, remainingBytes: 7 * GiB);
        var downloader = CreateDownloader(handler);

        await DownloadAsync(downloader, 1);
        var ex = await Assert.ThrowsAsync<OszDownloadException>(() => DownloadAsync(downloader, 2));

        Assert.Equal(OszDownloadFailure.FallbackLimitReached, ex.Reason);
        Assert.Single(handler.Requests, uri => uri == BudgetUrl);
        Assert.Contains(DirectPrefix + "1", handler.Requests);
        Assert.DoesNotContain(DirectPrefix + "2", handler.Requests);
    }

    [Fact]
    public async Task CountsDownloadedBytes_AgainstDailyBudget()
    {
        // 残り 1 GiB + 100 バイト: 200 バイトの譜面を 1 件落とすと 1 GiB を下回る
        var handler = new Handler(remaining: 150, remainingBytes: GiB + 100, oszSize: 200);
        var downloader = CreateDownloader(handler);

        await DownloadAsync(downloader, 1);
        var ex = await Assert.ThrowsAsync<OszDownloadException>(() => DownloadAsync(downloader, 2));

        Assert.Equal(OszDownloadFailure.FallbackLimitReached, ex.Reason);
    }

    [Fact]
    public async Task LongRateLimitOnFallback_FailsImmediately_AndBlocksFollowingFallbacks()
    {
        // 予算 API は余裕ありと言うが、ブラウザ等の利用で実際には超過していた場合
        var handler = new Handler(remaining: 150, remainingBytes: 7 * GiB, directRetryAfter: TimeSpan.FromMinutes(30));
        var downloader = CreateDownloader(handler);

        var first = await Assert.ThrowsAsync<OszDownloadException>(() => DownloadAsync(downloader, 1));
        var second = await Assert.ThrowsAsync<OszDownloadException>(() => DownloadAsync(downloader, 2));

        Assert.Equal(OszDownloadFailure.FallbackLimitReached, first.Reason);
        Assert.Equal(OszDownloadFailure.FallbackLimitReached, second.Reason);
        Assert.Single(handler.Requests, uri => uri.StartsWith(DirectPrefix) && uri != BudgetUrl); // 待たず・再試行せず
        Assert.DoesNotContain(DirectPrefix + "2", handler.Requests);
    }

    [Fact]
    public async Task ExemptCaller_IsNeverLimited()
    {
        var handler = new Handler(remaining: 0, remainingBytes: 0, exempt: true);
        var downloader = CreateDownloader(handler);

        await DownloadAsync(downloader, 1);
        await DownloadAsync(downloader, 2);

        Assert.Contains(DirectPrefix + "2", handler.Requests);
    }

    [Fact]
    public async Task BudgetUnavailable_DoesNotBlockFallback()
    {
        var handler = new Handler(remaining: 150, remainingBytes: 7 * GiB, budgetStatus: HttpStatusCode.InternalServerError);
        var downloader = CreateDownloader(handler);

        await DownloadAsync(downloader, 1);

        Assert.Contains(DirectPrefix + "1", handler.Requests);
    }

    [Fact]
    public async Task NonOsuDirectUrl_IsNotBudgeted()
    {
        var handler = new Handler(remaining: 0, remainingBytes: 0);
        var guard = new FallbackBudgetGuard(new HttpClient(handler));

        Assert.True(await guard.HasRoomAsync("https://other-mirror.example/d/1", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    private static OszDownloader CreateDownloader(Handler handler)
    {
        var client = new HttpClient(handler);
        return new OszDownloader(client, new DownloadIntervalGate(TimeSpan.Zero), new FallbackBudgetGuard(client));
    }

    private Task<string> DownloadAsync(OszDownloader downloader, int setId)
        => downloader.DownloadAsync(
            BeatmapMirrors.GetDownloadUrl(BeatmapMirrors.Hinai, setId),
            BeatmapMirrors.GetFallbackResolveUrl(BeatmapMirrors.Hinai, setId),
            _folder, setId, null, CancellationToken.None);

    /// <summary>hinai プロキシは常に 503、代替経路は osu.direct を返すスタブ。</summary>
    private sealed class Handler(
        int remaining, long remainingBytes, bool exempt = false, int oszSize = 4,
        TimeSpan? directRetryAfter = null, HttpStatusCode budgetStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);
            return Task.FromResult(Respond(url));
        }

        private HttpResponseMessage Respond(string url)
        {
            if (url.StartsWith(ProxyPrefix))
            {
                var unavailable = Json("""{"error":"mirrors temporarily unavailable for this beatmapset, retry","success":false}""", HttpStatusCode.ServiceUnavailable);
                unavailable.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return unavailable;
            }
            if (url.StartsWith(ResolvePrefix))
            {
                var id = url[ResolvePrefix.Length..];
                return Json($$"""{"success":true,"download_url":"{{DirectPrefix}}{{id}}","beatmapset_id":{{id}}}""");
            }
            if (url == BudgetUrl)
                return Json(BudgetJson(remaining, remainingBytes, exempt), budgetStatus);
            if (url.StartsWith(DirectPrefix))
            {
                if (directRetryAfter is { } retryAfter)
                {
                    var limited = Json("""{"code":429,"message":"download budget exceeded"}""", HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
                    return limited;
                }
                var osz = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[oszSize]) };
                osz.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                return osz;
            }
            throw new InvalidOperationException(url);
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}
