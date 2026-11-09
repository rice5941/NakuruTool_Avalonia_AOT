using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// ミラーから .osz を 1 件ダウンロードし、指定フォルダに保存する。
/// 各 HTTP リクエストの開始前に <see cref="DownloadIntervalGate"/> で間隔を空ける。
/// </summary>
internal sealed class OszDownloader
{
    /// <summary>429 / 503 を受けた場合の最大試行回数 (初回を含む)。</summary>
    public const int MaxAttempts = 3;

    /// <summary>Retry-After が無い場合の待機時間。</summary>
    private static readonly TimeSpan s_defaultRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Task.Delay に渡せる範囲に収めるための上限。</summary>
    private static readonly TimeSpan s_maxRetryDelay = TimeSpan.FromHours(1);

    private const string TempExtension = ".download";
    private const int CopyBufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly DownloadIntervalGate _gate;

    public OszDownloader(HttpClient httpClient, DownloadIntervalGate gate)
    {
        _httpClient = httpClient;
        _gate = gate;
    }

    /// <summary>ダウンロードして保存し、保存先のフルパスを返す。</summary>
    public async Task<string> DownloadAsync(string url, string destinationFolder, int beatmapSetId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await _gate.WaitForTurnAsync(ct).ConfigureAwait(false);

            using var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (IsRetryable(response.StatusCode) && attempt < MaxAttempts)
            {
                await Task.Delay(GetRetryDelay(response), ct).ConfigureAwait(false);
                continue;
            }

            EnsureOszResponse(response);

            var fileName = GetFileName(response, beatmapSetId);
            var destinationPath = Path.Combine(destinationFolder, fileName);
            await SaveAsync(response.Content, destinationPath, ct).ConfigureAwait(false);
            return destinationPath;
        }
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : s_defaultRetryDelay);

        if (delay < TimeSpan.Zero) return TimeSpan.Zero;
        if (delay > s_maxRetryDelay) return s_maxRetryDelay;
        return delay;
    }

    private static void EnsureOszResponse(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                throw new HttpRequestException("ミラー上にビートマップが見つかりませんでした (404)。", null, response.StatusCode);
            case HttpStatusCode.TooManyRequests:
            case HttpStatusCode.ServiceUnavailable:
                throw new HttpRequestException($"ミラーが混雑しています ({code})。時間をおいて再試行してください。", null, response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"ダウンロードに失敗しました (HTTP {code})。", null, response.StatusCode);

        // エラーページや JSON を .osz として保存しないようにする
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null
            && (mediaType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            throw new HttpRequestException($"ミラーが .osz ではない応答を返しました ({mediaType})。");
        }
    }

    /// <summary>Content-Disposition のファイル名を使い、無ければ "{beatmapSetId}.osz" にする。</summary>
    internal static string GetFileName(HttpResponseMessage response, int beatmapSetId)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');

        if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".osz", StringComparison.OrdinalIgnoreCase))
            return $"{beatmapSetId}.osz";

        var chars = name.ToCharArray();
        var invalidChars = Path.GetInvalidFileNameChars();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }

    /// <summary>一時ファイルに書き込んでから置き換えるため、途中で失敗しても壊れた .osz は残らない。</summary>
    private static async Task SaveAsync(HttpContent content, string destinationPath, CancellationToken ct)
    {
        var tempPath = destinationPath + TempExtension;
        try
        {
            await using (var source = await content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
            {
                await source.CopyToAsync(file, CopyBufferSize, ct).ConfigureAwait(false);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
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
