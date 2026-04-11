using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public partial class BeatmapGenerationPageViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IBeatmapRateGenerator _beatmapRateGenerator;

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

    private CancellationTokenSource? _cts;

    public BeatmapGenerationPageViewModel(
        IDatabaseService databaseService,
        IBeatmapRateGenerator beatmapRateGenerator)
    {
        _databaseService = databaseService;
        _beatmapRateGenerator = beatmapRateGenerator;

        CollectionSelector = new CollectionSelectorViewModel(databaseService);
        RateGeneration = new RateGenerationViewModel();

        CollectionSelector.ObserveProperty(nameof(CollectionSelectorViewModel.SelectedCollection))
            .Subscribe(_ => BatchGenerateCommand.NotifyCanExecuteChanged())
            .AddTo(Disposables);

        RateGeneration.ObserveProperty(nameof(RateGenerationViewModel.HasValidationErrors))
            .Subscribe(_ => BatchGenerateCommand.NotifyCanExecuteChanged())
            .AddTo(Disposables);
    }

    public void Initialize()
    {
        CollectionSelector.RefreshCollections();
    }

    private bool CanBatchGenerate() => !IsGenerating && CollectionSelector.SelectedCollection is not null && !RateGeneration.HasValidationErrors;

    [RelayCommand(CanExecute = nameof(CanBatchGenerate))]
    private async Task BatchGenerateAsync()
    {
        var collection = CollectionSelector.SelectedCollection;
        if (collection is null) return;

        var beatmaps = ResolveBeatmaps(collection.BeatmapMd5s);
        if (beatmaps.Length == 0) return;

        _cts = new CancellationTokenSource();
        IsGenerating = true;
        GenerationProgressValue = 0;
        GenerationStatusMessage = "";

        try
        {
            var lang = LanguageService.Instance;
            var progress = new Progress<RateGenerationProgress>(p =>
            {
                GenerationProgressValue = p.ProgressPercent;
                GenerationStatusMessage = p.Message;
            });

            var result = await _beatmapRateGenerator.GenerateBatchAsync(
                beatmaps, RateGeneration.ToOptions(), progress, _cts.Token);

            if (result.WasCancelled)
            {
                GenerationStatusMessage = lang.GetString("BeatmapGen.Cancelled");
            }
            else
            {
                GenerationStatusMessage = string.Format(
                    lang.GetString("BeatmapGen.BatchComplete"),
                    beatmaps.Length,
                    result.SuccessCount);
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

    private ReadOnlyMemory<Beatmap> ResolveBeatmaps(string[] md5Hashes)
    {
        var results = new List<Beatmap>(md5Hashes.Length);
        foreach (var md5 in md5Hashes)
        {
            if (_databaseService.TryGetBeatmapByMd5(md5, out var beatmap) && beatmap is not null)
                results.Add(beatmap);
        }
        return results.ToArray();
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
