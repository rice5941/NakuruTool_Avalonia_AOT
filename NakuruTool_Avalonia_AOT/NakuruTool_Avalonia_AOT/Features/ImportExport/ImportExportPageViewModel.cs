using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// ImportExportPageView の ViewModel
/// </summary>
public partial class ImportExportPageViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    public partial AvaloniaList<ExportCollectionItem> ExportCollections { get; set; } = new();

    [ObservableProperty]
    public partial ExportCollectionItem? SelectedExportCollection { get; set; }

    partial void OnSelectedExportCollectionChanged(ExportCollectionItem? value)
    {
        if (value is null) return;
        UpdateBeatmapPreview(value, isImport: false);
    }

    [ObservableProperty]
    public partial AvaloniaList<ImportFileItem> ImportFiles { get; set; } = new();

    [ObservableProperty]
    public partial ImportFileItem? SelectedImportFile { get; set; }

    partial void OnSelectedImportFileChanged(ImportFileItem? value)
    {
        if (value is null) return;
        UpdateBeatmapPreview(value, isImport: true);
    }

    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<ImportExportBeatmapItem> ShowBeatmaps { get; set; }
        = new AvaloniaList<ImportExportBeatmapItem>();

    [ObservableProperty]
    public partial int TotalPreviewCount { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsImportPreview { get; set; } = false;

    private ImportExportBeatmapItem[] _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
    private readonly AvaloniaList<ImportExportBeatmapItem> _showBeatmapsList = new();

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int FilteredPages { get; set; } = 1;

    [ObservableProperty]
    public partial int PageSize { get; set; } = 20;

    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = " ";

    [ObservableProperty]
    public partial int ProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    private readonly IDatabaseService _databaseService;
    private readonly IImportExportService _importExportService;
    private bool _disposed;

    public ImportExportPageViewModel(
        IDatabaseService databaseService,
        IImportExportService importExportService)
    {
        _databaseService = databaseService;
        _importExportService = importExportService;

        ShowBeatmaps = _showBeatmapsList;

        // 進捗監視
        _importExportService.ProgressObservable
            .Subscribe(progress =>
            {
                StatusMessage = progress.Message;
                ProgressValue = progress.ProgressValue;
            })
            .AddTo(Disposables);
    }

    public void Initialize()
    {
        // エクスポートリスト: DB のコレクション一覧を反映
        ExportCollections.Clear();
        foreach (var col in _databaseService.OsuCollections)
        {
            ExportCollections.Add(new ExportCollectionItem
            {
                Name = col.Name,
                BeatmapMd5s = col.BeatmapMd5s
            });
        }

        // インポートリスト: imports/ フォルダのファイル一覧を反映
        ImportFiles.Clear();
        foreach (var item in _importExportService.GetImportFiles())
        {
            ImportFiles.Add(item);
        }

        // プレビューリセット
        _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
        TotalPreviewCount = 0;
        IsImportPreview = false;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    private void UpdateBeatmapPreview(ExportCollectionItem exportItem, bool isImport)
    {
        var rows = exportItem.BeatmapMd5s
            .AsValueEnumerable()
            .Select(md5 =>
            {
                if (_databaseService.TryGetBeatmapByMd5(md5, out var beatmap) && beatmap is not null)
                {
                    return new ImportExportBeatmapItem
                    {
                        KeyCount = beatmap.KeyCount,
                        Title = beatmap.Title,
                        Artist = beatmap.Artist,
                        Version = beatmap.Version,
                        Creator = beatmap.Creator,
                        Exists = true
                    };
                }
                return new ImportExportBeatmapItem { Exists = false };
            })
            .ToArray();

        SetPreviewRows(rows, isImport);
    }

    private void UpdateBeatmapPreview(ImportFileItem importItem, bool isImport)
    {
        if (importItem.ParsedData is null)
        {
            SetPreviewRows(Array.Empty<ImportExportBeatmapItem>(), isImport);
            return;
        }

        var rows = importItem.ParsedData.Beatmaps
            .AsValueEnumerable()
            .Select(bm =>
            {
                if (!string.IsNullOrEmpty(bm.Md5) &&
                    _databaseService.TryGetBeatmapByMd5(bm.Md5, out var beatmap) && beatmap is not null)
                {
                    return new ImportExportBeatmapItem
                    {
                        KeyCount = beatmap.KeyCount,
                        Title = beatmap.Title,
                        Artist = beatmap.Artist,
                        Version = beatmap.Version,
                        Creator = beatmap.Creator,
                        Exists = true
                    };
                }
                return new ImportExportBeatmapItem
                {
                    Title = bm.Title,
                    Artist = bm.Artist,
                    Version = bm.Version,
                    Creator = bm.Creator,
                    KeyCount = (int)bm.Cs,
                    Exists = false
                };
            })
            .ToArray();

        SetPreviewRows(rows, isImport);
    }

    private void SetPreviewRows(ImportExportBeatmapItem[] rows, bool isImport)
    {
        _allPreviewRows = rows;
        TotalPreviewCount = rows.Length;
        IsImportPreview = isImport;
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

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        await UpdateIsProcessingAsync(true);
        try
        {
            var targets = ExportCollections
                .AsValueEnumerable()
                .Where(c => c.IsChecked)
                .Select(c => c.Name)
                .ToList();

            var count = await _importExportService.ExportAsync(targets);
            await UpdateStatusAsync(string.Format(LanguageService.Instance.GetString("ImportExport.Status.ExportComplete"), count));
        }
        catch (Exception ex)
        {
            await UpdateStatusAsync(string.Format(LanguageService.Instance.GetString("ImportExport.Status.ExportError"), ex.Message));
        }
        finally
        {
            await UpdateIsProcessingAsync(false);
        }
    }
    private bool CanExport() => !IsProcessing;

    [RelayCommand]
    private void SelectAllExport()
    {
        foreach (var item in ExportCollections)
            item.IsChecked = true;
    }

    [RelayCommand]
    private void ClearAllExport()
    {
        foreach (var item in ExportCollections)
            item.IsChecked = false;
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        await UpdateIsProcessingAsync(true);
        try
        {
            var targets = ImportFiles
                .AsValueEnumerable()
                .Where(f => f.IsChecked)
                .Select(f => f.FilePath)
                .ToList();

            var success = await _importExportService.ImportAsync(targets);
            await UpdateStatusAsync(success
                ? LanguageService.Instance.GetString("ImportExport.Status.ImportComplete")
                : LanguageService.Instance.GetString("ImportExport.Status.ImportError"));

            if (success)
            {
                // インポート後にエクスポートリストも更新
                Initialize();
            }
        }
        catch (Exception ex)
        {
            await UpdateStatusAsync($"インポートエラー: {ex.Message}");
        }
        finally
        {
            await UpdateIsProcessingAsync(false);
        }
    }
    private bool CanImport() => !IsProcessing;

    [RelayCommand]
    private void SelectAllImport()
    {
        foreach (var item in ImportFiles)
            item.IsChecked = true;
    }

    [RelayCommand]
    private void ClearAllImport()
    {
        foreach (var item in ImportFiles)
            item.IsChecked = false;
    }

    [RelayCommand]
    private void ReloadExport()
    {
        ExportCollections.Clear();
        foreach (var col in _databaseService.OsuCollections)
        {
            ExportCollections.Add(new ExportCollectionItem
            {
                Name = col.Name,
                BeatmapMd5s = col.BeatmapMd5s
            });
        }
        _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
        TotalPreviewCount = 0;
        IsImportPreview = false;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    [RelayCommand]
    private void ReloadImport()
    {
        ImportFiles.Clear();
        foreach (var item in _importExportService.GetImportFiles())
        {
            ImportFiles.Add(item);
        }
        _allPreviewRows = Array.Empty<ImportExportBeatmapItem>();
        TotalPreviewCount = 0;
        IsImportPreview = false;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    private async Task UpdateIsProcessingAsync(bool value)
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsProcessing = value);
    }

    private async Task UpdateStatusAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = message);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose();
    }
}
