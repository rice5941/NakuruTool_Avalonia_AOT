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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
    public partial bool IsImportListEmpty { get; set; } = true;

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
        ReloadImportFiles();
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
                        BeatmapSetId = beatmap.BeatmapSetId,
                        KeyCount = beatmap.KeyCount,
                        Title = beatmap.Title,
                        TitleUnicode = beatmap.TitleUnicode,
                        Artist = beatmap.Artist,
                        ArtistUnicode = beatmap.ArtistUnicode,
                        Version = beatmap.Version,
                        Creator = beatmap.Creator,
                        DownloadState = BeatmapDownloadState.Exists
                    };
                }
                return new ImportExportBeatmapItem
                {
                    BeatmapSetId = bm.BeatmapsetId,
                    Title = bm.Title,
                    Artist = bm.Artist,
                    Version = bm.Version,
                    Creator = bm.Creator,
                    KeyCount = (int)bm.Cs,
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
        ReloadImportFiles();
        _previewRequestedSubject.OnNext(Array.Empty<ImportExportBeatmapItem>());
    }

    public async Task HandleDroppedPathsAsync(IReadOnlyList<string> localPaths)
    {
        if (localPaths.Count == 0) return;

        var copiedCount = await Task.Run(() =>
        {
            var count = 0;
            foreach (var path in localPaths)
            {
                if (File.Exists(path))
                {
                    var ext = Path.GetExtension(path);
                    if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_importExportService.CopyToImportsFolder(path))
                            count++;
                    }
                    else if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        count += ExtractJsonFromZip(path);
                    }
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
                    {
                        var fileExt = Path.GetExtension(file);
                        if (fileExt.Equals(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_importExportService.CopyToImportsFolder(file))
                                count++;
                        }
                        else if (fileExt.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            count += ExtractJsonFromZip(file);
                        }
                    }
                }
            }
            return count;
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SelectedImportFile = null;
            _previewRequestedSubject.OnNext(Array.Empty<ImportExportBeatmapItem>());
            ReloadImportFiles();
            if (copiedCount > 0)
            {
                var message = string.Format(
                    LanguageService.Instance.GetString("ImportExport.Status.DropSuccess"),
                    copiedCount);
                _statusMessageSubject.OnNext(message);
            }
            else
            {
                _statusMessageSubject.OnNext(
                    LanguageService.Instance.GetString("ImportExport.Status.DropNoFiles"));
            }
        });
    }

    private void ReloadImportFiles()
    {
        ImportFiles.Clear();
        foreach (var item in _importExportService.GetImportFiles())
        {
            ImportFiles.Add(item);
        }
        IsImportListEmpty = ImportFiles.Count == 0;
    }

    private int ExtractJsonFromZip(string zipPath)
    {
        var count = 0;
        var tempDir = Path.Combine(Path.GetTempPath(), "NakuruTool_zip_" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            foreach (var jsonFile in Directory.GetFiles(tempDir, "*.json", SearchOption.AllDirectories))
            {
                if (_importExportService.CopyToImportsFolder(jsonFile))
                    count++;
            }
        }
        catch
        {
            // zip解凍エラーはスキップ
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
        return count;
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
