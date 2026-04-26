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

    public BeatmapGenerationPageViewModel(
        IDatabaseService databaseService,
        IBeatmapRateGenerator beatmapRateGenerator,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _databaseService = databaseService;
        _beatmapRateGenerator = beatmapRateGenerator;

        CollectionSelector = new CollectionSelectorViewModel(databaseService);
        RateGeneration = new RateGenerationViewModel();

        CollectionSelector.ObserveProperty(nameof(CollectionSelectorViewModel.SelectedCollection))
            .Subscribe(_ =>
            {
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
        CollectionSelector.SelectedCollection is not null &&
        !RateGeneration.HasValidationErrors &&
        FilteredCount > 0;

    [RelayCommand(CanExecute = nameof(CanBatchGenerate))]
    private async Task BatchGenerateAsync()
    {
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
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        CollectionSelector.Dispose();
        RateGeneration.Dispose();
        base.Dispose();
    }
}
