using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
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

    [ObservableProperty]
    public partial bool ShowOnlyMissing { get; set; } = false;

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
    private readonly SerialDisposable _itemSubscriptions = new();

    private bool _disposed;

    public ImportExportBeatmapListViewModel(ISettingsService settingsService, IBeatmapDownloadService downloadService)
    {
        _downloadService = downloadService;
        ShowBeatmaps = _showBeatmapsList;
        _itemSubscriptions.AddTo(Disposables);

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

    private bool CanDownloadAllMissing() => !IsAnyDownloading;

    [RelayCommand(CanExecute = nameof(CanCancelDownloads))]
    private async Task CancelDownloadsAsync()
    {
        await _downloadService.CancelAllAsync();
    }

    private bool CanCancelDownloads() => IsAnyDownloading;

    /// <summary>DL中またはキュー待ちの譜面が1つでもあるか</summary>
    private bool IsAnyDownloading
    {
        get
        {
            foreach (var item in _allPreviewRows)
            {
                if (item.DownloadState is BeatmapDownloadState.Queued or BeatmapDownloadState.Downloading)
                    return true;
            }
            return false;
        }
    }

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
    }

    private void SubscribeItemEvents()
    {
        var disposables = new CompositeDisposable();
        foreach (var item in _allPreviewRows)
        {
            item.ObserveProperty(nameof(ImportExportBeatmapItem.DownloadState))
                .Subscribe(_ =>
                {
                    DownloadAllMissingCommand.NotifyCanExecuteChanged();
                    CancelDownloadsCommand.NotifyCanExecuteChanged();
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
        base.Dispose();
    }
}
