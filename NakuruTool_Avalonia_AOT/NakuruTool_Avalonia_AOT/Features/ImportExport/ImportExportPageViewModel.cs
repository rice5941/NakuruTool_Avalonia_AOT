using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>
/// ImportExportPageView の親 ViewModel（子VM統合・進捗・排他選択を担当）
/// </summary>
public partial class ImportExportPageViewModel : ViewModelBase, IDisposable
{
    // ── 子VM（get-only, DI不要・手動生成）────────────────────────────
    public ExportViewModel ExportViewModel { get; }
    public ImportViewModel ImportViewModel { get; }
    public ImportExportBeatmapListViewModel BeatmapListViewModel { get; }

    // ── 親VMが管理するプロパティ ──────────────────────────────────────
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = " ";

    [ObservableProperty]
    public partial int ProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsProcessing { get; set; } = false;

    private readonly IImportExportService _importExportService;
    private bool _disposed;

    public ImportExportPageViewModel(
        IDatabaseService databaseService,
        IImportExportService importExportService)
    {
        _importExportService = importExportService;

        // 子VMを手動生成（MapListパターン準拠）
        ExportViewModel = new ExportViewModel(databaseService, importExportService);
        ImportViewModel = new ImportViewModel(databaseService, importExportService);
        BeatmapListViewModel = new ImportExportBeatmapListViewModel();

        // (1) Service進捗監視
        _importExportService.ProgressObservable
            .Subscribe(progress =>
            {
                StatusMessage = progress.Message;
                ProgressValue = progress.ProgressValue;
            })
            .AddTo(Disposables);

        // (2) Export: プレビューリクエスト
        ExportViewModel.PreviewRequested
            .Subscribe(rows => BeatmapListViewModel.SetPreviewRows(rows, isImport: false))
            .AddTo(Disposables);

        // (3) Import: プレビューリクエスト
        ImportViewModel.PreviewRequested
            .Subscribe(rows => BeatmapListViewModel.SetPreviewRows(rows, isImport: true))
            .AddTo(Disposables);

        // (4) Export: 結果メッセージ
        ExportViewModel.StatusMessageRequested
            .Subscribe(msg => StatusMessage = msg)
            .AddTo(Disposables);

        // (5) Import: 結果メッセージ
        ImportViewModel.StatusMessageRequested
            .Subscribe(msg => StatusMessage = msg)
            .AddTo(Disposables);

        // (6) Import成功後の再初期化
        ImportViewModel.ImportCompleted
            .Subscribe(_ => Initialize())
            .AddTo(Disposables);

        // (7) IsProcessing統合＋子VMへの逆流（Merge方式）
        ExportViewModel.ObserveProperty(nameof(ExportViewModel.IsProcessing))
            .Merge(ImportViewModel.ObserveProperty(nameof(ImportViewModel.IsProcessing)))
            .Subscribe(_ =>
            {
                IsProcessing = ExportViewModel.IsProcessing || ImportViewModel.IsProcessing;
                ExportViewModel.IsAnyProcessing = IsProcessing;
                ImportViewModel.IsAnyProcessing = IsProcessing;
            })
            .AddTo(Disposables);

        // (8) 排他選択: Export選択時にImport選択をクリア
        ExportViewModel.ObserveProperty(nameof(ExportViewModel.SelectedExportCollection))
            .Subscribe(_ =>
            {
                if (ExportViewModel.SelectedExportCollection != null)
                    ImportViewModel.SelectedImportFile = null;
            })
            .AddTo(Disposables);

        // (9) 排他選択: Import選択時にExport選択をクリア
        ImportViewModel.ObserveProperty(nameof(ImportViewModel.SelectedImportFile))
            .Subscribe(_ =>
            {
                if (ImportViewModel.SelectedImportFile != null)
                    ExportViewModel.SelectedExportCollection = null;
            })
            .AddTo(Disposables);
    }

    public void Initialize()
    {
        ExportViewModel.Initialize();
        ImportViewModel.Initialize();
        BeatmapListViewModel.Reset();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ExportViewModel.Dispose();
        ImportViewModel.Dispose();
        BeatmapListViewModel.Dispose();
        base.Dispose();
    }
}
