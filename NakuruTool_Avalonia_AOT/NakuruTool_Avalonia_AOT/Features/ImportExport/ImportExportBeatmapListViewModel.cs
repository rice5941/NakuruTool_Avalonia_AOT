using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using System;

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
    public partial bool IsImportPreview { get; set; } = false;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int FilteredPages { get; set; } = 1;

    [ObservableProperty]
    public partial int PageSize { get; set; } = 20;

    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

    private ImportExportBeatmapItem[] _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
    private readonly AvaloniaList<ImportExportBeatmapItem> _showBeatmapsList = new();

    private bool _disposed;

    public ImportExportBeatmapListViewModel()
    {
        ShowBeatmaps = _showBeatmapsList;
    }

    /// <summary>
    /// 外部からプレビュー行を設定する
    /// </summary>
    /// <param name="rows">表示するすべての行データ</param>
    /// <param name="isImport">Import プレビュー時は true（Exists 列の表示制御に使用）</param>
    public void SetPreviewRows(ImportExportBeatmapItem[] rows, bool isImport)
    {
        _allPreviewRows = rows;
        TotalPreviewCount = rows.Length;
        IsImportPreview = isImport;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    /// <summary>
    /// プレビューを初期状態にリセットする
    /// </summary>
    public void Reset()
    {
        _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
        TotalPreviewCount = 0;
        IsImportPreview = false;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    private void UpdateFilteredPages()
    {
        var size = Math.Max(1, PageSize);
        FilteredPages = Math.Max(1, (_allPreviewRows.Length + size - 1) / size);
    }

    private void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var remaining = Math.Max(0, _allPreviewRows.Length - skip);
        var take = Math.Min(size, remaining);

        _showBeatmapsList.Clear();

        if (take > 0)
        {
            var span = _allPreviewRows.AsSpan(skip, take);
            foreach (var item in span)
            {
                _showBeatmapsList.Add(item);
            }
        }
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
