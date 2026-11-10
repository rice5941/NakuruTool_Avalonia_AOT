using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Globalization;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// Beatmap プレビュー表示 + ページング の ViewModel
/// </summary>
public partial class ImportExportBeatmapListViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<ImportExportBeatmapItem> ShowBeatmaps { get; set; }
        = new AvaloniaList<ImportExportBeatmapItem>();

    [ObservableProperty]
    public partial int TotalPreviewCount { get; set; } = 0;

    [ObservableProperty]
    public partial int FilteredCount { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsImportPreview { get; set; } = false;

    [ObservableProperty]
    public partial bool HasDownloadedItems { get; set; } = false;

    /// <summary>キュー待ち or DL中の譜面が1つ以上存在するか</summary>
    [ObservableProperty]
    public partial bool IsDownloadInProgress { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowOnlyMissing { get; set; } = false;

    // ---- ダウンロード状況 (何を待っているか) ----

    /// <summary>ダウンロード状況の説明文。Idle のときは空。</summary>
    [ObservableProperty]
    public partial string DownloadStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDownloadStatus { get; set; }

    [ObservableProperty]
    public partial bool IsStatusDownloading { get; set; }

    [ObservableProperty]
    public partial bool IsStatusWaitingInterval { get; set; }

    [ObservableProperty]
    public partial bool IsStatusWaitingRetry { get; set; }

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int FilteredPages { get; set; } = 1;

    [ObservableProperty]
    public partial int PageSize { get; set; } = 20;

    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

    private ImportExportBeatmapItem[] _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
    private ImportExportBeatmapItem[] _filteredRows = Array.Empty<ImportExportBeatmapItem>();
    private readonly AvaloniaList<ImportExportBeatmapItem> _showBeatmapsList = new();
    private readonly IBeatmapDownloadService _downloadService;
    private readonly ISettingsService _settingsService;
    private readonly SerialDisposable _itemSubscriptions = new();

    /// <summary>待機中の残り秒数を更新するためのタイマー。</summary>
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private DownloadActivity _activity = DownloadActivity.Idle;

    private bool _disposed;

    public ImportExportBeatmapListViewModel(ISettingsService settingsService, IBeatmapDownloadService downloadService)
    {
        _downloadService = downloadService;
        _settingsService = settingsService;
        ShowBeatmaps = _showBeatmapsList;
        _itemSubscriptions.AddTo(Disposables);

        _countdownTimer.Tick += OnCountdownTick;
        _downloadService.ActivityChanged += OnDownloadActivityChanged;
        LangServiceInstance.LanguageChanged += OnLanguageChanged;
        OnDownloadActivityChanged(this, _downloadService.CurrentActivity);

        // Unicode表示設定の変更時にリスト表示を更新
        settingsService.SettingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.PreferUnicode),
            () => UpdateShowBeatmaps(),
            Disposables);
    }

    [RelayCommand]
    private void DownloadBeatmap(ImportExportBeatmapItem? item)
    {
        if (item is null || !item.CanDownload) return;
        _downloadService.EnqueueDownload(item, _allPreviewRows);
    }

    [RelayCommand(CanExecute = nameof(CanDownloadAllMissing))]
    private void DownloadAllMissing()
    {
        foreach (var item in _allPreviewRows)
        {
            if (item.CanDownload)
            {
                _downloadService.EnqueueDownload(item, _allPreviewRows);
            }
        }
    }

    private bool CanDownloadAllMissing() => !IsDownloadInProgress;

    [RelayCommand(CanExecute = nameof(CanCancelDownloads))]
    private async Task CancelDownloadsAsync()
    {
        await _downloadService.CancelAllAsync();
    }

    private bool CanCancelDownloads() => IsDownloadInProgress;

    /// <summary>DL中またはキュー待ちの譜面が1つでもあるか</summary>
    private bool ComputeIsAnyDownloading()
    {
        foreach (var item in _allPreviewRows)
        {
            if (item.DownloadState is BeatmapDownloadState.Queued or BeatmapDownloadState.Waiting or BeatmapDownloadState.Downloading)
                return true;
        }
        return false;
    }

    /// <summary>DL状態を再評価して関連プロパティ/コマンドを更新する</summary>
    private void RefreshDownloadState()
    {
        IsDownloadInProgress = ComputeIsAnyDownloading();
        DownloadAllMissingCommand.NotifyCanExecuteChanged();
        CancelDownloadsCommand.NotifyCanExecuteChanged();
    }

    private void OnDownloadActivityChanged(object? sender, DownloadActivity activity)
    {
        ClearRowWaitInfo(_activity.BeatmapSetId);
        _activity = activity;

        if (activity.ResumeAt is null)
            _countdownTimer.Stop();
        else
            _countdownTimer.Start();

        UpdateDownloadStatus();
    }

    private void OnCountdownTick(object? sender, EventArgs e) => UpdateDownloadStatus();

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateDownloadStatus();
        foreach (var row in _allPreviewRows)
            row.RefreshLocalizedText();
    }

    /// <summary>現在の <see cref="DownloadActivity"/> から、ステータス欄と対象行の表示を作り直す。</summary>
    private void UpdateDownloadStatus()
    {
        var activity = _activity;
        var phase = activity.Phase;

        IsStatusDownloading = phase == DownloadPhase.Downloading;
        IsStatusWaitingInterval = phase == DownloadPhase.WaitingInterval;
        IsStatusWaitingRetry = phase is DownloadPhase.WaitingRetry or DownloadPhase.WaitingRetryAfter;
        HasDownloadStatus = phase != DownloadPhase.Idle;

        if (phase == DownloadPhase.Idle)
        {
            _countdownTimer.Stop();
            DownloadStatusText = string.Empty;
            return;
        }

        var title = GetDisplayTitle(activity.BeatmapSetId);
        var seconds = GetRemainingSeconds(activity);

        DownloadStatusText = phase switch
        {
            DownloadPhase.Downloading when activity.Attempt > 1 => Format("ImportExport.DownloadStatus.DownloadingRetry",
                title, activity.Attempt, activity.MaxAttempts),
            DownloadPhase.Downloading when activity.FallbackUrl is { } fallbackUrl => Format("ImportExport.DownloadStatus.DownloadingFallback", title, fallbackUrl),
            DownloadPhase.Downloading => Format("ImportExport.DownloadStatus.Downloading", title),
            DownloadPhase.WaitingInterval => Format("ImportExport.DownloadStatus.WaitingInterval", seconds, title),
            DownloadPhase.WaitingRetry => Format("ImportExport.DownloadStatus.WaitingRetry",
                activity.StatusCode ?? 0, seconds, title, activity.Attempt, activity.MaxAttempts),
            DownloadPhase.WaitingRetryAfter => Format("ImportExport.DownloadStatus.WaitingRetryAfter",
                activity.StatusCode ?? 0, seconds, title),
            _ => string.Empty,
        };

        var rowMessage = phase switch
        {
            DownloadPhase.WaitingInterval => Format("ImportExport.DownloadStatus.WaitingIntervalTip", seconds),
            DownloadPhase.WaitingRetry => Format("ImportExport.DownloadStatus.WaitingRetryTip",
                activity.StatusCode ?? 0, seconds, activity.Attempt, activity.MaxAttempts),
            DownloadPhase.WaitingRetryAfter => Format("ImportExport.DownloadStatus.WaitingRetryAfterTip",
                activity.StatusCode ?? 0, seconds),
            _ => null,
        };
        var countdown = rowMessage is null ? null : $"{seconds}s";

        foreach (var row in _allPreviewRows)
        {
            if (row.BeatmapSetId != activity.BeatmapSetId) continue;
            row.WaitCountdown = countdown;
            row.WaitMessage = rowMessage;
        }
    }

    private void ClearRowWaitInfo(int beatmapSetId)
    {
        if (beatmapSetId <= 0) return;
        foreach (var row in _allPreviewRows)
        {
            if (row.BeatmapSetId != beatmapSetId) continue;
            row.WaitCountdown = null;
            row.WaitMessage = null;
        }
    }

    private static int GetRemainingSeconds(DownloadActivity activity)
    {
        if (activity.ResumeAt is not { } resumeAt) return 0;
        var remaining = resumeAt - DateTimeOffset.Now;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private string GetDisplayTitle(int beatmapSetId)
    {
        foreach (var row in _allPreviewRows)
        {
            if (row.BeatmapSetId != beatmapSetId) continue;
            return _settingsService.SettingsData.PreferUnicode && !string.IsNullOrEmpty(row.TitleUnicode)
                ? row.TitleUnicode
                : row.Title;
        }
        return beatmapSetId.ToString(CultureInfo.InvariantCulture);
    }

    private string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, LangServiceInstance.GetString(key), args);

    /// <summary>
    /// 外部からプレビュー行を設定する
    /// </summary>
    /// <param name="rows">表示するすべての行データ</param>
    /// <param name="isImport">Import プレビュー時は true（Exists 列の表示制御に使用）</param>
    public void SetPreviewRows(ImportExportBeatmapItem[] rows, bool isImport)
    {
        _allPreviewRows = rows;
        if (!isImport) ShowOnlyMissing = false;
        SubscribeItemEvents();
        TotalPreviewCount = rows.Length;
        IsImportPreview = isImport;
        ApplyMissingFilter();
        UpdateFilteredPages();
        UpdateShowBeatmaps();
        RefreshDownloadState();
    }

    /// <summary>
    /// プレビューを初期状態にリセットする
    /// </summary>
    public void Reset()
    {
        _itemSubscriptions.Disposable = null;
        _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
        _filteredRows = Array.Empty<ImportExportBeatmapItem>();
        TotalPreviewCount = 0;
        FilteredCount = 0;
        IsImportPreview = false;
        HasDownloadedItems = false;
        ShowOnlyMissing = false;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
        RefreshDownloadState();
    }

    private void SubscribeItemEvents()
    {
        var disposables = new CompositeDisposable();
        foreach (var item in _allPreviewRows)
        {
            item.ObserveProperty(nameof(ImportExportBeatmapItem.DownloadState))
                .Subscribe(_ =>
                {
                    RefreshDownloadState();
                    if (!HasDownloadedItems)
                        HasDownloadedItems = CheckHasDownloadedItems();
                    if (ShowOnlyMissing)
                    {
                        ApplyMissingFilter();
                        UpdateFilteredPages();
                        UpdateShowBeatmaps();
                    }
                })
                .AddTo(disposables);
        }
        _itemSubscriptions.Disposable = disposables;
    }

    private bool CheckHasDownloadedItems()
    {
        foreach (var item in _allPreviewRows)
        {
            if (item.DownloadState == BeatmapDownloadState.Downloaded)
                return true;
        }
        return false;
    }

    private void UpdateFilteredPages()
    {
        var size = Math.Max(1, PageSize);
        FilteredPages = Math.Max(1, (_filteredRows.Length + size - 1) / size);
    }

    private void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var remaining = Math.Max(0, _filteredRows.Length - skip);
        var take = Math.Min(size, remaining);

        _showBeatmapsList.Clear();

        if (take > 0)
        {
            var span = _filteredRows.AsSpan(skip, take);
            foreach (var item in span)
            {
                _showBeatmapsList.Add(item);
            }
        }
    }

    private void ApplyMissingFilter()
    {
        if (ShowOnlyMissing)
        {
            _filteredRows = _allPreviewRows
                .AsValueEnumerable()
                .Where(item => item.DownloadState != BeatmapDownloadState.Exists
                            && item.DownloadState != BeatmapDownloadState.Downloaded)
                .ToArray();
        }
        else
        {
            _filteredRows = _allPreviewRows;
        }
        FilteredCount = _filteredRows.Length;
    }

    partial void OnShowOnlyMissingChanged(bool value)
    {
        CurrentPage = 1;
        ApplyMissingFilter();
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdateShowBeatmaps();
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnFilteredPagesChanged(int value)
    {
        CurrentPage = 1;
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageSizeChanged(int value)
    {
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage() => CurrentPage++;
    private bool CanGoToNextPage() => CurrentPage < FilteredPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage() => CurrentPage--;
    private bool CanGoToPreviousPage() => CurrentPage > 1;

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        _downloadService.ActivityChanged -= OnDownloadActivityChanged;
        LangServiceInstance.LanguageChanged -= OnLanguageChanged;
        base.Dispose();
    }
}
