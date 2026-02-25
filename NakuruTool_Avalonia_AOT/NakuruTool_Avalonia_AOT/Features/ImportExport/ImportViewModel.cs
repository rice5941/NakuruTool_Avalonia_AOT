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
/// インポートリスト管理・実行の ViewModel
/// </summary>
public partial class ImportViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    public partial AvaloniaList<ImportFileItem> ImportFiles { get; set; } = new();

    [ObservableProperty]
    public partial ImportFileItem? SelectedImportFile { get; set; }

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

    private readonly Subject<Unit> _importCompletedSubject;
    public Observable<Unit> ImportCompleted => _importCompletedSubject;

    private readonly IDatabaseService _databaseService;
    private readonly IImportExportService _importExportService;
    private bool _disposed;

    public ImportViewModel(
        IDatabaseService databaseService,
        IImportExportService importExportService)
    {
        _databaseService = databaseService;
        _importExportService = importExportService;

        _previewRequestedSubject = new Subject<ImportExportBeatmapItem[]>();
        _previewRequestedSubject.AddTo(Disposables);

        _statusMessageSubject = new Subject<string>();
        _statusMessageSubject.AddTo(Disposables);

        _importCompletedSubject = new Subject<Unit>();
        _importCompletedSubject.AddTo(Disposables);
    }

    /// <summary>
    /// imports/ フォルダのファイル一覧を読み込む（プレビュー Subject は発行しない）
    /// </summary>
    public void Initialize()
    {
        ImportFiles.Clear();
        foreach (var item in _importExportService.GetImportFiles())
        {
            ImportFiles.Add(item);
        }
        // プレビューSubjectは発行しない（親VMのBeatmapListViewModel.Reset()に一本化）
    }

    partial void OnSelectedImportFileChanged(ImportFileItem? value)
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
        ImportCommand.NotifyCanExecuteChanged();
    }

    private ImportExportBeatmapItem[] BuildPreviewRows(ImportFileItem item)
    {
        if (item.ParsedData is null)
            return Array.Empty<ImportExportBeatmapItem>();

        return item.ParsedData.Beatmaps
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
                        TitleUnicode = beatmap.TitleUnicode,
                        Artist = beatmap.Artist,
                        ArtistUnicode = beatmap.ArtistUnicode,
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
            await NotifyStatusAsync(success
                ? LanguageService.Instance.GetString("ImportExport.Status.ImportComplete")
                : LanguageService.Instance.GetString("ImportExport.Status.ImportError"));
        }
        catch (Exception ex)
        {
            await NotifyStatusAsync($"インポートエラー: {ex.Message}");
        }
        finally
        {
            await UpdateIsProcessingAsync(false);
            // 成功・失敗に関わらず常に発行（部分成功でもDB状態が変わっている可能性があるため）
            await Dispatcher.UIThread.InvokeAsync(() =>
                _importCompletedSubject.OnNext(Unit.Default));
        }
    }
    private bool CanImport() => !IsAnyProcessing;

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
    private void ReloadImport()
    {
        Initialize();
        _previewRequestedSubject.OnNext(Array.Empty<ImportExportBeatmapItem>());
    }

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
