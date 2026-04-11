using Avalonia.Collections;
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

public partial class BeatmapGenerationPageViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private readonly IBeatmapRateGenerator _beatmapRateGenerator;
    private readonly ISettingsService _settingsService;
    private readonly AvaloniaList<Beatmap> _showBeatmapsList;
    private Beatmap[] _resolvedBeatmaps = Array.Empty<Beatmap>();

    public IBeatmapRateGenerator BeatmapRateGenerator => _beatmapRateGenerator;

    public CollectionSelectorViewModel CollectionSelector { get; }
    public RateGenerationViewModel RateGeneration { get; }

    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; set; }

    [ObservableProperty]
    public partial int ResolvedBeatmapCount { get; set; }

    [ObservableProperty]
    public partial ModCategory SelectedModCategory { get; set; } = ModCategory.NoMod;

    [ObservableProperty]
    public partial ScoreSystemCategory SelectedScoreSystemCategory { get; set; } = ScoreSystemCategory.Default;

    [ObservableProperty]
    public partial int CurrentBeatmapPage { get; set; } = 1;

    [ObservableProperty]
    public partial int BeatmapPageCount { get; set; } = 1;

    [ObservableProperty]
    public partial int BeatmapPageSize { get; set; } = 20;

    public IAvaloniaReadOnlyList<int> BeatmapPageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

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
        IBeatmapRateGenerator beatmapRateGenerator,
        ISettingsService settingsService)
    {
        _databaseService = databaseService;
        _beatmapRateGenerator = beatmapRateGenerator;
        _settingsService = settingsService;

        _showBeatmapsList = new AvaloniaList<Beatmap>();
        ShowBeatmaps = _showBeatmapsList;

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

        _settingsService.SettingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.PreferUnicode),
            () => UpdateShowBeatmaps(),
            Disposables);
    }

    public void Initialize()
    {
        CollectionSelector.RefreshCollections();
    }

    private bool CanBatchGenerate() => !IsGenerating && CollectionSelector.SelectedCollection is not null && !RateGeneration.HasValidationErrors && ResolvedBeatmapCount > 0;

    [RelayCommand(CanExecute = nameof(CanBatchGenerate))]
    private async Task BatchGenerateAsync()
    {
        var collection = CollectionSelector.SelectedCollection;
        if (collection is null) return;

        var beatmaps = _resolvedBeatmaps;
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
                beatmaps.AsMemory(), RateGeneration.ToOptions(), progress, _cts.Token);

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

    [RelayCommand(CanExecute = nameof(CanGoToNextBeatmapPage))]
    private void NextBeatmapPage() => CurrentBeatmapPage++;
    private bool CanGoToNextBeatmapPage() => CurrentBeatmapPage < BeatmapPageCount;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousBeatmapPage))]
    private void PreviousBeatmapPage() => CurrentBeatmapPage--;
    private bool CanGoToPreviousBeatmapPage() => CurrentBeatmapPage > 1;

    private void RefreshCollectionBeatmaps()
    {
        var collection = CollectionSelector.SelectedCollection;
        if (collection is null)
        {
            _resolvedBeatmaps = Array.Empty<Beatmap>();
            ResolvedBeatmapCount = 0;
            UpdateBeatmapPageCount();
            CurrentBeatmapPage = 1;
            UpdateShowBeatmaps();
            BatchGenerateCommand.NotifyCanExecuteChanged();
            return;
        }

        var results = new List<Beatmap>(collection.BeatmapMd5s.Length);
        foreach (var md5 in collection.BeatmapMd5s)
        {
            if (_databaseService.TryGetBeatmapByMd5(md5, out var beatmap) && beatmap is not null)
                results.Add(beatmap);
        }

        _resolvedBeatmaps = results.ToArray();
        ResolvedBeatmapCount = _resolvedBeatmaps.Length;
        UpdateBeatmapPageCount();
        CurrentBeatmapPage = 1;
        UpdateShowBeatmaps();
        BatchGenerateCommand.NotifyCanExecuteChanged();
    }

    private void UpdateBeatmapPageCount()
    {
        var size = Math.Max(1, BeatmapPageSize);
        BeatmapPageCount = Math.Max(1, (ResolvedBeatmapCount + size - 1) / size);
    }

    private void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, BeatmapPageSize);
        var skip = (CurrentBeatmapPage - 1) * size;
        var remaining = Math.Max(0, _resolvedBeatmaps.Length - skip);
        var take = Math.Min(size, remaining);

        _showBeatmapsList.Clear();

        if (take > 0)
        {
            var mod = SelectedModCategory;
            var scoreSystem = SelectedScoreSystemCategory;
            var span = _resolvedBeatmaps.AsSpan(skip, take);
            foreach (var beatmap in span)
            {
                var displayed = beatmap with
                {
                    BestScore = beatmap.GetBestScore(scoreSystem, mod),
                    BestAccuracy = beatmap.GetBestAccuracy(scoreSystem, mod),
                    Grade = beatmap.GetGrade(scoreSystem, mod)
                };
                _showBeatmapsList.Add(displayed);
            }
        }
    }

    partial void OnCurrentBeatmapPageChanged(int value)
    {
        UpdateShowBeatmaps();
        NextBeatmapPageCommand.NotifyCanExecuteChanged();
        PreviousBeatmapPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnBeatmapPageCountChanged(int value)
    {
        NextBeatmapPageCommand.NotifyCanExecuteChanged();
        PreviousBeatmapPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnBeatmapPageSizeChanged(int value)
    {
        UpdateBeatmapPageCount();
        UpdateShowBeatmaps();
    }

    partial void OnSelectedModCategoryChanged(ModCategory value)
    {
        UpdateShowBeatmaps();
    }

    partial void OnSelectedScoreSystemCategoryChanged(ScoreSystemCategory value)
    {
        UpdateShowBeatmaps();
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
