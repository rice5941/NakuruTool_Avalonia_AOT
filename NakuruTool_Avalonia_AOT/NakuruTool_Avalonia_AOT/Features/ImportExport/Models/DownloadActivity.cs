using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>ダウンロードワーカーが今何をしているか。</summary>
public enum DownloadPhase
{
    /// <summary>キューが空で何もしていない。</summary>
    Idle,

    /// <summary>ミラーと通信中。</summary>
    Downloading,

    /// <summary>前回の開始から一定間隔をあけるために待機中。</summary>
    WaitingInterval,

    /// <summary>ミラーがレート制限 (429) を返したため、同じ URL の再試行まで待機中。</summary>
    WaitingRetry,

    /// <summary>
    /// サーバーが Retry-After で指定した時刻まで、そのサーバーへの次のリクエスト
    /// (代替経路の問い合わせや次の譜面) を待機中。
    /// </summary>
    WaitingRetryAfter,
}

/// <summary>
/// ダウンロードワーカーの現在の状況。UI で「何を待っているか」を表示するために使う。
/// </summary>
/// <param name="BeatmapSetId">対象の beatmapset ID (Idle のときは 0)。</param>
/// <param name="ResumeAt">待機中の場合、次の処理を開始する予定時刻。</param>
/// <param name="StatusCode">WaitingRetry の原因となった HTTP ステータスコード。</param>
/// <param name="Attempt">現在 (または次) の試行回数 (1 始まり)。</param>
/// <param name="MaxAttempts">最大試行回数。</param>
/// <param name="FallbackUrl">ミラーが配信できなかったため代替経路を使っている場合、その直接ダウンロード URL。</param>
public sealed record DownloadActivity(
    DownloadPhase Phase,
    int BeatmapSetId = 0,
    DateTimeOffset? ResumeAt = null,
    int? StatusCode = null,
    int Attempt = 0,
    int MaxAttempts = 0,
    string? FallbackUrl = null)
{
    /// <summary>代替経路を使っているか。</summary>
    public bool ViaFallback => FallbackUrl is not null;

    public static DownloadActivity Idle { get; } = new(DownloadPhase.Idle);
}

/// <summary><see cref="OszDownloader"/> から報告される 1 件分の進行状況。</summary>
internal readonly record struct OszDownloadProgress(
    DownloadPhase Phase,
    TimeSpan Delay,
    int? StatusCode,
    int Attempt,
    string? FallbackUrl = null);
