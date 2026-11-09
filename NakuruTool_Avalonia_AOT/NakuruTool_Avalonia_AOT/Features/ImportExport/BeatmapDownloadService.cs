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

/// <summary>
/// 譜面ダウンロードのキュー管理と、行 (<see cref="ImportExportBeatmapItem"/>) の状態更新を行う。
/// ダウンロードは 1 件ずつ順番に実行し、各ダウンロードの開始は前回の開始から
/// <see cref="DownloadInterval"/> 以上空ける。
/// </summary>
public sealed class BeatmapDownloadService : IBeatmapDownloadService
{
    public static readonly TimeSpan DownloadInterval = TimeSpan.FromSeconds(5);

    private const string ProjectUrl = "https://github.com/rice5941/NakuruTool_Avalonia_AOT";

    private static readonly HttpClient s_httpClient = CreateHttpClient();

    private readonly ISettingsService _settingsService;
    private readonly OszDownloader _downloader;

    /// <summary>キュー投入済み・ダウンロード中の beatmapset ID (同一セットの二重投入防止)。</summary>
    private readonly ConcurrentDictionary<int, byte> _pendingSetIds = new();

    private IReadOnlyList<ImportExportBeatmapItem>? _lastAllItems;
    private QueueWorker _worker;
    private bool _disposed;

    private readonly record struct DownloadRequest(int BeatmapSetId, IReadOnlyList<ImportExportBeatmapItem> AllItems);

    public BeatmapDownloadService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        // ゲートはキャンセル後も使い回し、キャンセル直後の再ダウンロードでも間隔を守る
        _downloader = new OszDownloader(s_httpClient, new DownloadIntervalGate(DownloadInterval));
        _worker = new QueueWorker(ProcessAsync);
    }

    /// <summary>UI スレッドから呼び出す。</summary>
    public void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems)
    {
        if (!item.CanDownload) return;

        var setId = item.BeatmapSetId;
        if (!_pendingSetIds.TryAdd(setId, 0)) return;

        _lastAllItems = allItems;

        if (_worker.TryEnqueue(new DownloadRequest(setId, allItems)))
        {
            foreach (var row in RowsOf(allItems, setId))
            {
                if (row.CanDownload)
                    row.DownloadState = BeatmapDownloadState.Queued;
            }
        }
        else
        {
            _pendingSetIds.TryRemove(setId, out _);
        }
    }

    public async Task CancelAllAsync()
    {
        await _worker.StopAsync().ConfigureAwait(false);
        _pendingSetIds.Clear();

        if (_lastAllItems is { } items)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var row in items)
                {
                    if (row.DownloadState is BeatmapDownloadState.Queued or BeatmapDownloadState.Downloading)
                    {
                        row.DownloadState = BeatmapDownloadState.NotExists;
                        row.ErrorMessage = null;
                    }
                }
            });
        }

        if (!_disposed)
            _worker = new QueueWorker(ProcessAsync);
    }

    /// <summary>キューから取り出した 1 件を処理する (ワーカースレッド)。</summary>
    private async Task ProcessAsync(DownloadRequest request, CancellationToken ct)
    {
        var (setId, allItems) = request;
        try
        {
            await SetStateAsync(allItems, setId, BeatmapDownloadState.Downloading).ConfigureAwait(false);

            var songsFolder = GetSongsFolder();
            var url = BeatmapMirrors.GetDownloadUrl(_settingsService.SettingsData.BeatmapMirrorUrl, setId);
            await _downloader.DownloadAsync(url, songsFolder, setId, ct).ConfigureAwait(false);

            await SetStateAsync(allItems, setId, BeatmapDownloadState.Downloaded).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 行の状態は CancelAllAsync で元に戻す
            throw;
        }
        catch (Exception ex)
        {
            await SetStateAsync(allItems, setId, BeatmapDownloadState.Error, ex.Message).ConfigureAwait(false);
        }
        finally
        {
            _pendingSetIds.TryRemove(setId, out _);
        }
    }

    private string GetSongsFolder()
    {
        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        if (string.IsNullOrEmpty(osuFolderPath))
            throw new InvalidOperationException("osu! フォルダパスが設定されていません。");

        var songsFolder = Path.Combine(osuFolderPath, "Songs");
        Directory.CreateDirectory(songsFolder);
        return songsFolder;
    }

    /// <summary>同じ beatmapset に属する全行 (難易度違い) の状態を UI スレッドで更新する。</summary>
    private static Task SetStateAsync(
        IReadOnlyList<ImportExportBeatmapItem> allItems, int setId, BeatmapDownloadState state, string? errorMessage = null)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var row in RowsOf(allItems, setId))
            {
                row.DownloadState = state;
                row.ErrorMessage = errorMessage;
            }
        }).GetTask();
    }

    private static IEnumerable<ImportExportBeatmapItem> RowsOf(IReadOnlyList<ImportExportBeatmapItem> allItems, int setId)
    {
        foreach (var row in allItems)
        {
            if (row.BeatmapSetId == setId)
                yield return row;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };

        // hinai は "tool-name/version (+repo-or-contact-url)" 形式の User-Agent を求めている
        var version = typeof(BeatmapDownloadService).Assembly.GetName().Version;
        var versionText = version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"NakuruTool/{versionText} (+{ProjectUrl})");
        return client;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // ワーカーの完了は待たない (UI スレッドからのデッドロック防止)
        _worker.Stop();
    }

    /// <summary>
    /// 単一リーダーのキューと、それを順番に処理するバックグラウンドタスク。
    /// キャンセル時は丸ごと破棄して作り直す。
    /// </summary>
    private sealed class QueueWorker
    {
        private readonly Channel<DownloadRequest> _channel =
            Channel.CreateUnbounded<DownloadRequest>(new UnboundedChannelOptions { SingleReader = true });
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _task;

        public QueueWorker(Func<DownloadRequest, CancellationToken, Task> process)
        {
            _task = Task.Run(() => RunAsync(process, _cts.Token));
        }

        public bool TryEnqueue(DownloadRequest request) => _channel.Writer.TryWrite(request);

        private async Task RunAsync(Func<DownloadRequest, CancellationToken, Task> process, CancellationToken ct)
        {
            try
            {
                await foreach (var request in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                    await process(request, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        }

        public void Stop()
        {
            _channel.Writer.TryComplete();
            _cts.Cancel();
        }

        public async Task StopAsync()
        {
            Stop();
            await _task.ConfigureAwait(false);
            _cts.Dispose();
        }
    }
}
