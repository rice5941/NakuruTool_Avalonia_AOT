using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// ミラーから .osz を 1 件ダウンロードし、指定フォルダに保存する。
/// 各ダウンロードリクエストの開始前に <see cref="DownloadIntervalGate"/> で間隔を空ける。
/// </summary>
/// <remarks>
/// hinai のプロキシは、その譜面を配信できるミラーが無いと 503 (Retry-After: 30) を返す。
/// これは混雑ではなく同じ URL を再試行しても解消しないため、503 のときは
/// 代替経路 (<c>fallbackResolveUrl</c> で直接ダウンロード URL を問い合わせ、そこから取得) を試す。
/// 429 (レート制限) のときだけ Retry-After に従って待ち、同じ URL を再試行する。
/// <para>
/// Retry-After はサーバー (ホスト) ごとに <see cref="RetryAfterTracker"/> へ記録し、
/// 代替経路を使う場合も含めて、指定時刻までそのホストへはリクエストを送らない
/// (例: hinai が 503 + Retry-After: 30 を返したら、hinai への代替経路の問い合わせも 30 秒待つ)。
/// </para>
/// <para>
/// 代替経路 (osu.direct) にはダウンロード予算があるため、<see cref="FallbackBudgetGuard"/> で
/// 残りが少ないときは代替経路を使わずに <see cref="OszDownloadFailure.FallbackLimitReached"/> で失敗させる。
/// </para>
/// </remarks>
internal sealed class OszDownloader
{
    /// <summary>429 を受けた場合の最大試行回数 (初回を含む)。</summary>
    public const int MaxAttempts = 3;

    /// <summary>Retry-After が無い場合の待機時間。</summary>
    private static readonly TimeSpan s_defaultRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Task.Delay に渡せる範囲に収めるための上限。</summary>
    private static readonly TimeSpan s_maxRetryDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// 代替経路の 429 でこれより長く待たされる場合は、短時間の集中制限 (最大 60 秒) ではなく
    /// ダウンロード予算の超過とみなし、待たずに失敗させる。
    /// </summary>
    private static readonly TimeSpan s_budgetExceededRetryDelay = TimeSpan.FromSeconds(60);

    private const string TempExtension = ".download";
    private const int CopyBufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly DownloadIntervalGate _gate;
    private readonly FallbackBudgetGuard _fallbackBudget;
    private readonly RetryAfterTracker _retryAfter;

    public OszDownloader(
        HttpClient httpClient, DownloadIntervalGate gate,
        FallbackBudgetGuard? fallbackBudget = null, RetryAfterTracker? retryAfter = null)
    {
        _httpClient = httpClient;
        _gate = gate;
        _retryAfter = retryAfter ?? new RetryAfterTracker();
        _fallbackBudget = fallbackBudget ?? new FallbackBudgetGuard(httpClient, retryAfter: _retryAfter);
    }

    /// <summary>ダウンロードして保存し、保存先のフルパスを返す。</summary>
    /// <param name="url">.osz を返すダウンロード URL。</param>
    /// <param name="fallbackResolveUrl">
    /// <paramref name="url"/> が 503 を返したときに、直接ダウンロード URL を問い合わせる URL
    /// (hinai の JSON エンドポイント)。null なら代替経路は使わない。
    /// </param>
    /// <param name="onProgress">待機の開始・通信の開始時に呼ばれる (呼び出し元スレッドは不定)。</param>
    public async Task<string> DownloadAsync(
        string url, string? fallbackResolveUrl, string destinationFolder, int beatmapSetId,
        Action<OszDownloadProgress>? onProgress, CancellationToken ct)
    {
        var response = await SendAsync(url, fallbackUrl: null, onProgress, ct).ConfigureAwait(false);
        string? fallbackUrl = null;
        try
        {
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable && fallbackResolveUrl is not null)
            {
                response.Dispose();
                (response, fallbackUrl) = await SendViaFallbackAsync(fallbackResolveUrl, onProgress, ct).ConfigureAwait(false);
            }

            EnsureOszResponse(response);

            var destinationPath = Path.Combine(destinationFolder, GetFileName(response, beatmapSetId));
            var bytes = await SaveAsync(response.Content, destinationPath, ct).ConfigureAwait(false);

            if (fallbackUrl is not null)
                _fallbackBudget.RecordDownload(fallbackUrl, bytes);
            return destinationPath;
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// 間隔を守ってダウンロードリクエストを送る。429 のときだけ Retry-After に従って再試行する。
    /// </summary>
    /// <param name="fallbackUrl">代替経路でのリクエストなら、その URL (進行状況の表示用)。</param>
    private async Task<HttpResponseMessage> SendAsync(
        string url, string? fallbackUrl, Action<OszDownloadProgress>? onProgress, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var currentAttempt = attempt;
            await WaitForHostAsync(url, fallbackUrl, attempt, onProgress, ct).ConfigureAwait(false);
            await _gate.WaitForTurnAsync(
                delay => onProgress?.Invoke(new(DownloadPhase.WaitingInterval, delay, null, currentAttempt, fallbackUrl)),
                ct).ConfigureAwait(false);

            onProgress?.Invoke(new(DownloadPhase.Downloading, TimeSpan.Zero, null, attempt, fallbackUrl));

            var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                // 503 などに付いた Retry-After も記録し、このホストへの次のリクエストを待たせる
                _retryAfter.Record(url, response, s_maxRetryDelay);
                return response;
            }

            var retryDelay = GetRetryDelay(response);
            if (fallbackUrl is not null && retryDelay > s_budgetExceededRetryDelay)
            {
                // 代替経路のダウンロード予算超過。次の時間帯まで待つとキュー全体が止まるため、待たずに失敗させる。
                // 予算がリセットされるまで代替経路へは送らない (FallbackBudgetGuard が止める)
                response.Dispose();
                _fallbackBudget.MarkExhausted(fallbackUrl, retryDelay);
                throw new OszDownloadException(OszDownloadFailure.FallbackLimitReached, (int)HttpStatusCode.TooManyRequests);
            }

            _retryAfter.Record(new Uri(url), retryDelay, (int)HttpStatusCode.TooManyRequests);
            if (attempt >= MaxAttempts)
                return response;

            response.Dispose();
            onProgress?.Invoke(new(DownloadPhase.WaitingRetry, retryDelay, (int)HttpStatusCode.TooManyRequests, attempt + 1, fallbackUrl));
            await _retryAfter.WaitAsync(url, onWaiting: null, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <paramref name="url"/> のホストが Retry-After を指定していれば、その時刻まで待つ。
    /// </summary>
    private Task WaitForHostAsync(
        string url, string? fallbackUrl, int attempt, Action<OszDownloadProgress>? onProgress, CancellationToken ct)
    {
        return _retryAfter.WaitAsync(
            url,
            (delay, statusCode) => onProgress?.Invoke(new(DownloadPhase.WaitingRetryAfter, delay, statusCode, attempt, fallbackUrl)),
            ct);
    }

    /// <summary>直接ダウンロード URL を問い合わせ、予算に余裕があればそこからダウンロードする。</summary>
    private async Task<(HttpResponseMessage Response, string DirectUrl)> SendViaFallbackAsync(
        string resolveUrl, Action<OszDownloadProgress>? onProgress, CancellationToken ct)
    {
        // 直前の 503 に Retry-After が付いていれば、同じサーバーへの問い合わせもそれまで待つ
        await WaitForHostAsync(resolveUrl, null, 1, onProgress, ct).ConfigureAwait(false);
        var directUrl = await ResolveDirectUrlAsync(resolveUrl, ct).ConfigureAwait(false)
            ?? throw new OszDownloadException(OszDownloadFailure.Unavailable, (int)HttpStatusCode.ServiceUnavailable);

        // 予算確認も代替経路のサーバーへのリクエストなので、Retry-After を守る
        await WaitForHostAsync(directUrl, directUrl, 1, onProgress, ct).ConfigureAwait(false);
        if (!await _fallbackBudget.HasRoomAsync(directUrl, ct).ConfigureAwait(false))
            throw new OszDownloadException(OszDownloadFailure.FallbackLimitReached, (int)HttpStatusCode.ServiceUnavailable);

        var response = await SendAsync(directUrl, fallbackUrl: directUrl, onProgress, ct).ConfigureAwait(false);
        return (response, directUrl);
    }

    /// <summary>
    /// hinai の JSON エンドポイント (<c>{"success":true,"download_url":"..."}</c>) から
    /// 直接ダウンロード URL を取得する。取得できなければ null。
    /// </summary>
    /// <remarks>このエンドポイントはファイルを転送しないため、ダウンロード間隔の対象外。</remarks>
    private async Task<string?> ResolveDirectUrlAsync(string resolveUrl, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(resolveUrl, ct).ConfigureAwait(false);
        _retryAfter.Record(resolveUrl, response, s_maxRetryDelay);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = json.RootElement;

            if (!root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
                return null;
            if (!root.TryGetProperty("download_url", out var downloadUrl) || downloadUrl.ValueKind != JsonValueKind.String)
                return null;

            return Uri.TryCreate(downloadUrl.GetString(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                ? uri.AbsoluteUri
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var delay = RetryAfterTracker.GetRetryAfter(response, DateTimeOffset.UtcNow) ?? s_defaultRetryDelay;
        return delay > s_maxRetryDelay ? s_maxRetryDelay : delay;
    }

    private static void EnsureOszResponse(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new OszDownloadException(OszDownloadFailure.NotFound, code);
            case HttpStatusCode.TooManyRequests:
                throw new OszDownloadException(OszDownloadFailure.RateLimited, code);
            case HttpStatusCode.ServiceUnavailable:
                throw new OszDownloadException(OszDownloadFailure.Unavailable, code);
        }

        if (!response.IsSuccessStatusCode)
            throw new OszDownloadException(OszDownloadFailure.HttpError, code);

        // エラーページや JSON を .osz として保存しないようにする
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null
            && (mediaType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            throw new OszDownloadException(OszDownloadFailure.NotOsz, code);
        }
    }

    /// <summary>
    /// 保存ファイル名を決める。Content-Disposition → 最終的な URL のファイル名 (リダイレクト先のストレージなど)
    /// → "{beatmapSetId}.osz" の順に使う。
    /// </summary>
    internal static string GetFileName(HttpResponseMessage response, int beatmapSetId)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');

        if (!IsOszName(name) && response.RequestMessage?.RequestUri is { } finalUri)
            name = Uri.UnescapeDataString(Path.GetFileName(finalUri.AbsolutePath));

        return IsOszName(name) ? Sanitize(name!) : $"{beatmapSetId}.osz";
    }

    private static bool IsOszName(string? name)
        => !string.IsNullOrWhiteSpace(name) && name.EndsWith(".osz", StringComparison.OrdinalIgnoreCase);

    private static string Sanitize(string name)
    {
        var chars = name.ToCharArray();
        var invalidChars = Path.GetInvalidFileNameChars();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }

    /// <summary>
    /// 一時ファイルに書き込んでから置き換えるため、途中で失敗しても壊れた .osz は残らない。
    /// 保存したバイト数を返す。
    /// </summary>
    private static async Task<long> SaveAsync(HttpContent content, string destinationPath, CancellationToken ct)
    {
        var tempPath = destinationPath + TempExtension;
        try
        {
            long bytes;
            await using (var source = await content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
            {
                await source.CopyToAsync(file, CopyBufferSize, ct).ConfigureAwait(false);
                bytes = file.Length;
            }

            File.Move(tempPath, destinationPath, overwrite: true);
            return bytes;
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* 削除できなくても元の例外を優先する */ }
    }
}
