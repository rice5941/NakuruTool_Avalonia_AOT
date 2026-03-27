using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public sealed class BeatmapDownloadService : IBeatmapDownloadService
{
    private const int BaseIntervalMs = 2000;
    private const string TempExtension = ".download";

    private static readonly HttpClient s_httpClient;

    static BeatmapDownloadService()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
        };
        s_httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        s_httpClient.DefaultRequestHeaders.Add("User-Agent", "NakuruTool/1.0");
    }

    private readonly ISettingsService _settingsService;
    private readonly ConcurrentDictionary<int, byte> _inFlightSetIds = new();

    private IReadOnlyList<ImportExportBeatmapItem>? _lastAllItems;
    private bool _disposed;

    private CancellationTokenSource _workerCts;
    private Channel<DownloadRequest> _downloadChannel;
    private Task _processingTask;

    private readonly record struct DownloadRequest(ImportExportBeatmapItem Item, IReadOnlyList<ImportExportBeatmapItem> AllItems);

    public BeatmapDownloadService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _workerCts = new CancellationTokenSource();
        _downloadChannel = Channel.CreateUnbounded<DownloadRequest>(new UnboundedChannelOptions { SingleReader = true });
        _processingTask = ProcessDownloadQueueAsync(_workerCts.Token);
    }

    public void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems)
    {
        if (!item.CanDownload) return;
        if (!_inFlightSetIds.TryAdd(item.BeatmapSetId, 0)) return;

        item.DownloadState = BeatmapDownloadState.Queued;

        foreach (var other in allItems)
        {
            if (other != item && other.BeatmapSetId == item.BeatmapSetId && other.CanDownload)
                other.DownloadState = BeatmapDownloadState.Queued;
        }

        _lastAllItems = allItems;

        if (!_downloadChannel.Writer.TryWrite(new DownloadRequest(item, allItems)))
        {
            // Channel が閉じられている場合、状態を元に戻す
            foreach (var other in allItems)
            {
                if (other.BeatmapSetId == item.BeatmapSetId
                    && other.DownloadState == BeatmapDownloadState.Queued)
                {
                    other.DownloadState = BeatmapDownloadState.NotExists;
                }
            }
            _inFlightSetIds.TryRemove(item.BeatmapSetId, out _);
        }
    }

    public async Task CancelAllAsync()
    {
        _workerCts.Cancel();
        _downloadChannel.Writer.TryComplete();

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        if (_lastAllItems is { } items)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var item in items)
                {
                    if (item.DownloadState is BeatmapDownloadState.Queued or BeatmapDownloadState.Downloading)
                    {
                        item.DownloadState = BeatmapDownloadState.NotExists;
                        item.ErrorMessage = null;
                    }
                }
            });
        }
        _inFlightSetIds.Clear();
        _workerCts.Dispose();
        _workerCts = new CancellationTokenSource();
        _downloadChannel = Channel.CreateUnbounded<DownloadRequest>(new UnboundedChannelOptions { SingleReader = true });
        _processingTask = ProcessDownloadQueueAsync(_workerCts.Token);
    }

    private async Task ProcessDownloadQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var request in _downloadChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var item = request.Item;
                var allItems = request.AllItems;

                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        item.DownloadState = BeatmapDownloadState.Downloading;
                        foreach (var other in allItems)
                        {
                            if (other != item && other.BeatmapSetId == item.BeatmapSetId)
                                other.DownloadState = BeatmapDownloadState.Downloading;
                        }
                    });

                    await DownloadBeatmapAsync(item, ct).ConfigureAwait(false);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var other in allItems)
                        {
                            if (other.BeatmapSetId == item.BeatmapSetId)
                                other.DownloadState = BeatmapDownloadState.Downloaded;
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        item.DownloadState = BeatmapDownloadState.NotExists;
                    });
                    break;
                }
                catch (Exception ex)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var other in allItems)
                        {
                            if (other.BeatmapSetId == item.BeatmapSetId)
                            {
                                other.DownloadState = BeatmapDownloadState.Error;
                                other.ErrorMessage = ex.Message;
                            }
                        }
                    });
                }
                finally
                {
                    _inFlightSetIds.TryRemove(item.BeatmapSetId, out _);
                }

                if (!ct.IsCancellationRequested)
                    await ApplyRateLimitDelayAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task DownloadBeatmapAsync(ImportExportBeatmapItem item, CancellationToken ct)
    {
        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        if (string.IsNullOrEmpty(osuFolderPath))
            throw new InvalidOperationException("osu! フォルダパスが設定されていません。");

        var songsFolder = Path.Combine(osuFolderPath, "Songs");
        Directory.CreateDirectory(songsFolder);

        var mirrorUrl = _settingsService.SettingsData.BeatmapMirrorUrl;
        if (string.IsNullOrEmpty(mirrorUrl))
            mirrorUrl = "https://catboy.best/d/";
        var url = $"{mirrorUrl}{item.BeatmapSetId}";
        var finalPath = Path.Combine(songsFolder, $"{item.BeatmapSetId}.osz");
        var tempPath = finalPath + TempExtension;

        try
        {
            using var response = await s_httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // RetryAfterの解析: Delta（秒数指定）またはDate（日時指定）に対応
                double retryDelayMs;
                if (response.Headers.RetryAfter?.Delta is { } delta)
                {
                    retryDelayMs = delta.TotalMilliseconds;
                }
                else if (response.Headers.RetryAfter?.Date is { } date)
                {
                    retryDelayMs = Math.Max((date - DateTimeOffset.UtcNow).TotalMilliseconds, 0);
                }
                else
                {
                    retryDelayMs = 60_000; // ヘッダーなしのフォールバック
                }
                await Task.Delay((int)Math.Min(retryDelayMs, 120_000), ct).ConfigureAwait(false); // 最大2分に制限
                throw new HttpRequestException($"レート制限に達しました。{retryDelayMs}ms 待機後に再試行してください。");
            }

            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await responseStream.CopyToAsync(fileStream, 81920, ct).ConfigureAwait(false);
            } // ← ここで fileStream は確実にディスポーズ済み

            File.Move(tempPath, finalPath, overwrite: true); // 安全に移動可能
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }

    private static async Task ApplyRateLimitDelayAsync(CancellationToken ct)
    {
        await Task.Delay(BaseIntervalMs, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _workerCts.Cancel();
        _downloadChannel.Writer.TryComplete();
        // ワーカータスクの完了を待たない（UIスレッドからのデッドロック防止）
        _workerCts.Dispose();
    }
}
