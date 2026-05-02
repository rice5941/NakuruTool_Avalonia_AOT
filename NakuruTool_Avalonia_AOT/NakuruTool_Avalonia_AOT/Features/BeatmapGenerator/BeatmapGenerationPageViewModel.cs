using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public partial class BeatmapGenerationPageViewModel : BeatmapListViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IBeatmapRateGenerator _beatmapRateGenerator;
    private readonly ISettingsService _settingsService;
    private readonly IRateGenerationCollectionJsonWriter _collectionJsonWriter;
    private CancellationTokenSource? _cts;

    public IBeatmapRateGenerator BeatmapRateGenerator => _beatmapRateGenerator;

    public CollectionSelectorViewModel CollectionSelector { get; }
    public RateGenerationViewModel RateGeneration { get; }

    [ObservableProperty]
    public partial int SelectedGenerationTabIndex { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsGenerating { get; set; } = false;

    [ObservableProperty]
    public partial int GenerationProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial string GenerationStatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial SingleBeatmapGenerationViewModel? SingleGenerationViewModel { get; set; }

    [ObservableProperty]
    public partial bool IsSingleGenerationOverlayVisible { get; set; } = false;

    [ObservableProperty]
    public partial bool IsSortOverlayVisible { get; set; } = false;

    [RelayCommand]
    private void ToggleSortOverlay() => IsSortOverlayVisible = !IsSortOverlayVisible;

    [RelayCommand]
    private void CloseSortOverlay() => IsSortOverlayVisible = false;

    public BeatmapGenerationPageViewModel(
        IDatabaseService databaseService,
        IBeatmapRateGenerator beatmapRateGenerator,
        ISettingsService settingsService,
        IRateGenerationCollectionJsonWriter collectionJsonWriter)
        : base(settingsService)
    {
        _databaseService = databaseService;
        _beatmapRateGenerator = beatmapRateGenerator;
        _settingsService = settingsService;
        _collectionJsonWriter = collectionJsonWriter;

        CollectionSelector = new CollectionSelectorViewModel(databaseService);
        RateGeneration = new RateGenerationViewModel();

        CollectionSelector.ObserveProperty(nameof(CollectionSelectorViewModel.SelectedCollection))
            .Subscribe(_ =>
            {
                // コレクション切替時は単体生成オーバーレイを閉じる (stale な VM を残さない)
                CloseSingleGeneration();
                BatchGenerateCommand.NotifyCanExecuteChanged();
                RefreshCollectionBeatmaps();
            })
            .AddTo(Disposables);

        RateGeneration.ObserveProperty(nameof(RateGenerationViewModel.HasValidationErrors))
            .Subscribe(_ => BatchGenerateCommand.NotifyCanExecuteChanged())
            .AddTo(Disposables);

        this.ObservePropertyAndSubscribe(
            nameof(FilteredCount),
            () => BatchGenerateCommand.NotifyCanExecuteChanged(),
            Disposables);
    }

    public void Initialize()
    {
        CollectionSelector.RefreshCollections();
    }

    private bool CanBatchGenerate() =>
        !IsGenerating &&
        !IsSingleGenerationOverlayVisible &&
        CollectionSelector.SelectedCollection is not null &&
        !RateGeneration.HasValidationErrors &&
        FilteredCount > 0;

    [RelayCommand(CanExecute = nameof(CanBatchGenerate))]
    private async Task BatchGenerateAsync()
    {
        IsSortOverlayVisible = false;

        var collection = CollectionSelector.SelectedCollection;
        if (collection is null) return;

        var beatmaps = SourceBeatmapsRaw;
        if (beatmaps.Length == 0) return;

        _cts = new CancellationTokenSource();
        IsGenerating = true;
        GenerationProgressValue = 0;
        GenerationStatusMessage = "";

        try
        {
            var lang = LanguageService.Instance;
            var generationDone = false;
            var progress = new Progress<RateGenerationProgress>(p =>
            {
                if (generationDone) return;
                GenerationProgressValue = p.ProgressPercent;
                GenerationStatusMessage = p.Message;
            });

            // 内部に同期 I/O・ZIP 圧縮・.osu パース等が多数含まれるため、
            // UI スレッドをブロックしないようスレッドプール上で実行する。
            // Progress<T> は呼び出し元の SynchronizationContext をキャプチャするため、
            // UI スレッドへの進捗反映は引き続き安全に行われる。
            var options = RateGeneration.ToOptions();
            var token = _cts.Token;
            var memory = beatmaps.AsMemory();
            var result = await Task.Run(
                () => _beatmapRateGenerator.GenerateBatchAsync(memory, options, progress, token),
                token);
            generationDone = true;

            if (result.WasCancelled)
            {
                GenerationStatusMessage = lang.GetString("BeatmapGen.Cancelled");
            }
            else
            {
                // ThreadPool 上で遅延実行された進捗コールバックが
                // 100% 報告の後に到着して値を巻き戻すケースに備え、明示的に 100 をセットする。
                // (generationDone=true により以降のコールバックはゲートで弾かれる)
                GenerationProgressValue = 100;

                var message = string.Format(
                    lang.GetString("BeatmapGen.BatchComplete"),
                    beatmaps.Length,
                    result.SuccessCount);

                message += "\n" + lang.GetString("BeatmapGen.RefreshHint");

                // コレクションインポート用 JSON 出力 (チェックボックス ON かつ 1 件以上成功した場合のみ)
                if (RateGeneration.EmitCollectionJson && result.SuccessCount > 0)
                {
                    try
                    {
                        // JSON 書き出しは MD5 計算 / ファイル I/O を伴うため、
                        // バッチ生成本体と同様に UI スレッドをブロックしないよう Task.Run で逃がす。
                        var collectionName = collection.Name;
                        var batchResults = result.Results;
                        var writeResult = await Task.Run(
                            () => _collectionJsonWriter.WriteBatchAsync(
                                collectionName,
                                options,
                                batchResults,
                                token),
                            token).ConfigureAwait(true);

                        if (writeResult.FileWritten && writeResult.OutputFilePath is not null)
                        {
                            var fileName = System.IO.Path.GetFileName(writeResult.OutputFilePath);
                            message += "\n" + string.Format(
                                lang.GetString("BeatmapGen.CollectionJsonEmitted"),
                                fileName,
                                writeResult.WrittenBeatmapCount);
                            message += "\n" + lang.GetString("BeatmapGen.CollectionJsonImportNote");
                        }

                        if (writeResult.SkippedBeatmapCount > 0)
                        {
                            message += "\n" + string.Format(
                                lang.GetString("BeatmapGen.CollectionJsonSkipped"),
                                writeResult.SkippedBeatmapCount);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // .osz の生成自体は完了済みなので、専用キーで JSON 出力のみキャンセルされた旨を示す
                        message += "\n" + lang.GetString("BeatmapGen.CollectionJsonCancelled");
                    }
                    catch (Exception ex)
                    {
                        message += "\n" + string.Format(
                            lang.GetString("BeatmapGen.CollectionJsonFailed"),
                            ex.Message);
                    }
                }

                GenerationStatusMessage = message;
            }
        }
        catch (OperationCanceledException)
        {
            GenerationStatusMessage = LanguageService.Instance.GetString("BeatmapGen.Cancelled");
        }
        finally
        {
            IsGenerating = false;
            _cts?.Dispose();
            _cts = null;
        }

        BatchGenerateCommand.NotifyCanExecuteChanged();
    }

    private bool CanCancelGeneration() => IsGenerating;

    [RelayCommand(CanExecute = nameof(CanCancelGeneration))]
    private void CancelGeneration()
    {
        _cts?.Cancel();
    }

    public void ShowSingleGeneration(Beatmap beatmap)
    {
        IsSortOverlayVisible = false;

        // 旧 VM を Dispose してから新規生成 (stale 参照防止)
        SingleGenerationViewModel?.Dispose();
        SingleGenerationViewModel = new SingleBeatmapGenerationViewModel(beatmap, _beatmapRateGenerator, _settingsService);
        IsSingleGenerationOverlayVisible = true;
    }

    [RelayCommand]
    private void CloseSingleGeneration()
    {
        IsSingleGenerationOverlayVisible = false;
        SingleGenerationViewModel?.Dispose();
        SingleGenerationViewModel = null;
    }

    /// <summary>ContextMenu 経由の単体生成リクエストをオーバーレイで処理する。</summary>
    protected override void OnGenerateBeatmap(Beatmap target)
    {
        ShowSingleGeneration(target);
    }

    /// <summary>バッチ生成中／単体オーバーレイ表示中は ContextMenu Generate を無効化する。</summary>
    protected override bool CanGenerateBeatmapFromContextMenu =>
        !IsGenerating && !IsSingleGenerationOverlayVisible;

    private void RefreshCollectionBeatmaps()
    {
        var collection = CollectionSelector.SelectedCollection;
        if (collection is null)
        {
            SetSourceBeatmaps(Array.Empty<Beatmap>());
            BatchGenerateCommand.NotifyCanExecuteChanged();
            return;
        }

        var results = new List<Beatmap>(collection.BeatmapMd5s.Length);
        foreach (var md5 in collection.BeatmapMd5s)
        {
            if (_databaseService.TryGetBeatmapByMd5(md5, out var beatmap) && beatmap is not null)
                results.Add(beatmap);
        }

        SetSourceBeatmaps(results.ToArray());
        BatchGenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        BatchGenerateCommand.NotifyCanExecuteChanged();
        CancelGenerationCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSingleGenerationOverlayVisibleChanged(bool value)
    {
        BatchGenerateCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        SingleGenerationViewModel?.Dispose();
        SingleGenerationViewModel = null;
        CollectionSelector.Dispose();
        RateGeneration.Dispose();
        base.Dispose();
    }
}
