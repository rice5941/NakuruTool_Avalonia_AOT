using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// 代替経路 (osu.direct からの直接ダウンロード) のダウンロード予算を管理する。
/// </summary>
/// <remarks>
/// osu.direct は IP ごとに「200 件 / UTC 1 時間」「8 GiB / UTC 1 日」の予算を持つ
/// (https://osu.direct/api/docs)。上限ぎりぎりまで使うと、ブラウザや osu! クライアントからの
/// 利用分と合わせて超過し、次の時間帯まで 429 になるため、余裕を持って
/// 残り <see cref="MinRemainingCount"/> 件または <see cref="MinRemainingBytes"/> バイトで打ち切る。
/// <para>
/// 予算は <c>/api/d/budget</c> で取得する。取得は初回とリセット時刻を過ぎたときだけ行い、
/// その間はダウンロードした件数・バイト数を手元で差し引く。
/// </para>
/// </remarks>
internal sealed class FallbackBudgetGuard
{
    public const int MinRemainingCount = 10;
    public const long MinRemainingBytes = 1L * 1024 * 1024 * 1024;

    private const string OsuDirectHost = "osu.direct";
    private const string BudgetUrl = "https://osu.direct/api/d/budget";

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly RetryAfterTracker _retryAfter;
    private readonly Lock _lock = new();
    private Budget? _budget;

    /// <param name="RemainingCount">今の UTC 1 時間で残っている件数。</param>
    /// <param name="CountResetAt">件数がリセットされる時刻。</param>
    /// <param name="RemainingBytes">今日 (UTC) 残っているバイト数。</param>
    /// <param name="BytesResetAt">バイト数がリセットされる時刻。</param>
    private sealed record Budget(int RemainingCount, DateTimeOffset CountResetAt, long RemainingBytes, DateTimeOffset BytesResetAt);

    public FallbackBudgetGuard(HttpClient httpClient, TimeProvider? timeProvider = null, RetryAfterTracker? retryAfter = null)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _retryAfter = retryAfter ?? new RetryAfterTracker(_timeProvider);
    }

    /// <summary>予算の対象となる URL (osu.direct) か。</summary>
    public static bool AppliesTo(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Host.Equals(OsuDirectHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="url"/> から余裕を持ってダウンロードできるか。
    /// 予算の対象外の URL や、予算を取得できない場合は true (429 は別途処理する)。
    /// </summary>
    public async Task<bool> HasRoomAsync(string url, CancellationToken ct)
    {
        if (!AppliesTo(url)) return true;

        var budget = GetCurrentBudget() ?? await RefreshAsync(ct).ConfigureAwait(false);
        return budget is null
            || (budget.RemainingCount > MinRemainingCount && budget.RemainingBytes > MinRemainingBytes);
    }

    /// <summary>代替経路で 1 件ダウンロードしたことを予算に反映する。</summary>
    public void RecordDownload(string url, long bytes)
    {
        if (!AppliesTo(url)) return;
        lock (_lock)
        {
            if (_budget is { } b)
                _budget = b with { RemainingCount = b.RemainingCount - 1, RemainingBytes = b.RemainingBytes - bytes };
        }
    }

    /// <summary>予算超過 (長い Retry-After 付きの 429) を受けたので、<paramref name="retryAfter"/> の間は使わない。</summary>
    public void MarkExhausted(string url, TimeSpan retryAfter)
    {
        if (!AppliesTo(url)) return;
        var until = _timeProvider.GetUtcNow() + retryAfter;
        lock (_lock)
        {
            _budget = new Budget(0, until, _budget?.RemainingBytes ?? 0, _budget?.BytesResetAt ?? until);
        }
    }

    /// <summary>手元の予算。未取得か、どちらかのリセット時刻を過ぎていれば null。</summary>
    private Budget? GetCurrentBudget()
    {
        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            return _budget is { } b && now < b.CountResetAt && now < b.BytesResetAt ? b : null;
        }
    }

    /// <summary>
    /// <c>/api/d/budget</c> から予算を取得する。例:
    /// <c>{"exempt":false,"hourly":{"remaining":137,"resetAt":1794236400000},"daily":{"remainingBytes":7995105267,"resetAt":1794268800000}}</c>
    /// </summary>
    private async Task<Budget?> RefreshAsync(CancellationToken ct)
    {
        Budget? budget;
        try
        {
            await _retryAfter.WaitAsync(BudgetUrl, onWaiting: null, ct).ConfigureAwait(false);
            using var response = await _httpClient.GetAsync(BudgetUrl, ct).ConfigureAwait(false);
            _retryAfter.Record(BudgetUrl, response, TimeSpan.FromMinutes(1));
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            budget = Parse(json.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }

        lock (_lock)
        {
            _budget = budget;
        }
        return budget;
    }

    private static Budget? Parse(JsonElement root)
    {
        // API トークン保持者などは予算の対象外
        if (root.TryGetProperty("exempt", out var exempt) && exempt.ValueKind == JsonValueKind.True)
            return new Budget(int.MaxValue, DateTimeOffset.MaxValue, long.MaxValue, DateTimeOffset.MaxValue);

        var hourly = root.GetProperty("hourly");
        var daily = root.GetProperty("daily");
        return new Budget(
            hourly.GetProperty("remaining").GetInt32(),
            DateTimeOffset.FromUnixTimeMilliseconds(hourly.GetProperty("resetAt").GetInt64()),
            daily.GetProperty("remainingBytes").GetInt64(),
            DateTimeOffset.FromUnixTimeMilliseconds(daily.GetProperty("resetAt").GetInt64()));
    }
}
