using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// エクスポートリスト管理・実行の ViewModel
/// </summary>
public partial class ExportViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    public partial AvaloniaList<ExportCollectionItem> ExportCollections { get; set; } = new();

    [ObservableProperty]
    public partial ExportCollectionItem? SelectedExportCollection { get; set; }

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    [ObservableProperty]
    public partial bool IsAnyProcessing { get; set; } = false;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = " ";

    [ObservableProperty]
    public partial int ProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsProgressVisible { get; set; } = false;

    private readonly Subject<ImportExportBeatmapItem[]> _previewRequestedSubject;
    public Observable<ImportExportBeatmapItem[]> PreviewRequested => _previewRequestedSubject;

    private readonly Subject<string> _statusMessageSubject;
    public Observable<string> StatusMessageRequested => _statusMessageSubject;

    private readonly IDatabaseService _databaseService;
    private readonly IImportExportService _importExportService;
    private bool _disposed;

    public ExportViewModel(
        IDatabaseService databaseService,
        IImportExportService importExportService)
    {
        _databaseService = databaseService;
        _importExportService = importExportService;

        _previewRequestedSubject = new Subject<ImportExportBeatmapItem[]>();
        _previewRequestedSubject.AddTo(Disposables);

        _statusMessageSubject = new Subject<string>();
        _statusMessageSubject.AddTo(Disposables);
    }

    /// <summary>
    /// DB コレクション一覧を読み込む（プレビュー Subject は発行しない）
    /// </summary>
    public void Initialize()
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
        // プレビューSubjectは発行しない（親VMのBeatmapListViewModel.Reset()に一本化）
    }

    partial void OnSelectedExportCollectionChanged(ExportCollectionItem? value)
    {
        if (value is null)
        {
            // null化は排他選択またはReloadで発生する。
            // Reloadは明示的に空配列をSubject発行するため、ここでは何もしない。
            // 排他選択時にプレビューを発行すると、反対側のプレビューを上書きしてしまう。
            return;
        }
        var rows = BuildPreviewRows(value);
        _previewRequestedSubject.OnNext(rows);
    }

    partial void OnIsAnyProcessingChanged(bool value)
    {
        ExportCommand.NotifyCanExecuteChanged();
        ReloadExportCommand.NotifyCanExecuteChanged();
    }

    private ImportExportBeatmapItem[] BuildPreviewRows(ExportCollectionItem item)
    {
        return item.BeatmapMd5s
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
    }

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
            await NotifyStatusAsync(
                string.Format(LanguageService.Instance.GetString("ImportExport.Status.ExportComplete"), count));
        }
        catch (Exception ex)
        {
            await NotifyStatusAsync(
                string.Format(LanguageService.Instance.GetString("ImportExport.Status.ExportError"), ex.Message));
        }
        finally
        {
            await UpdateIsProcessingAsync(false);
        }
    }
    private bool CanExport() => !IsAnyProcessing;

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

    [RelayCommand(CanExecute = nameof(CanReloadExport))]
    private async Task ReloadExportAsync()
    {
        await UpdateIsProcessingAsync(true);
        try
        {
            await _databaseService.ReloadCollectionDbAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Initialize();
                _previewRequestedSubject.OnNext(Array.Empty<ImportExportBeatmapItem>());
            });
        }
        catch (Exception ex)
        {
            await NotifyStatusAsync(
                string.Format(LanguageService.Instance.GetString("ImportExport.Status.ReloadError"), ex.Message));
        }
        finally
        {
            await UpdateIsProcessingAsync(false);
        }
    }
    private bool CanReloadExport() => !IsAnyProcessing;

    private async Task UpdateIsProcessingAsync(bool value)
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsProcessing = value);
    }

    private async Task NotifyStatusAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => _statusMessageSubject.OnNext(message));
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(); // Subject群はAddTo(Disposables)で管理済み
    }
}
