using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// サーバー (ホスト) ごとに Retry-After を記録し、指定された時刻までそのホストへの
/// リクエストを待たせる。ダウンロードだけでなく、代替経路の問い合わせや予算確認など
/// 同じホストへの全リクエストに適用する。
/// </summary>
internal sealed class RetryAfterTracker
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (DateTimeOffset Until, int StatusCode)> _blockedHosts =
        new(StringComparer.OrdinalIgnoreCase);

    public RetryAfterTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 応答に Retry-After があれば、リクエスト先 <paramref name="url"/> のホストへのリクエストを指定時刻まで止める。
    /// </summary>
    /// <param name="maxDelay">これより長い指定は <paramref name="maxDelay"/> に丸める (Task.Delay の上限対策)。</param>
    /// <returns>記録した待機時間。Retry-After が無ければ null。</returns>
    public TimeSpan? Record(string url, HttpResponseMessage response, TimeSpan maxDelay)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (GetRetryAfter(response, _timeProvider.GetUtcNow()) is not { } delay) return null;

        if (delay > maxDelay) delay = maxDelay;
        Record(uri, delay, (int)response.StatusCode);
        return delay;
    }

    public void Record(Uri uri, TimeSpan delay, int statusCode)
    {
        var until = _timeProvider.GetUtcNow() + delay;
        lock (_lock)
        {
            if (!_blockedHosts.TryGetValue(uri.Host, out var current) || current.Until < until)
                _blockedHosts[uri.Host] = (until, statusCode);
        }
    }

    /// <summary>
    /// <paramref name="url"/> のホストに Retry-After が記録されていれば、その時刻まで待つ。
    /// </summary>
    /// <param name="onWaiting">実際に待つ場合に (待機時間, 原因の HTTP ステータス) で呼ばれる。</param>
    public async Task WaitAsync(string url, Action<TimeSpan, int>? onWaiting, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;

        (DateTimeOffset Until, int StatusCode) blocked;
        lock (_lock)
        {
            if (!_blockedHosts.TryGetValue(uri.Host, out blocked)) return;
        }

        var remaining = blocked.Until - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return;

        onWaiting?.Invoke(remaining, blocked.StatusCode);
        await Task.Delay(remaining, _timeProvider, ct).ConfigureAwait(false);
    }

    /// <summary>Retry-After ヘッダ (秒数または日時) を待機時間に変換する。無ければ null。</summary>
    internal static TimeSpan? GetRetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return null;

        var delay = retryAfter.Delta ?? (retryAfter.Date is { } date ? date - now : TimeSpan.Zero);
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }
}
