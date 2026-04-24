using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using R3;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public partial class SingleBeatmapGenerationViewModel : ViewModelBase
{
    private readonly IBeatmapRateGenerator _beatmapRateGenerator;
    private CancellationTokenSource? _cts;

    public RateGenerationViewModel RateGeneration { get; }

    [ObservableProperty]
    public partial Beatmap TargetBeatmap { get; set; }

    [ObservableProperty]
    public partial bool IsGenerating { get; set; } = false;

    [ObservableProperty]
    public partial int GenerationProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial string GenerationStatusMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool IsCompleted { get; set; } = false;

    [ObservableProperty]
    public partial string DisplayTitle { get; set; } = "";

    [ObservableProperty]
    public partial string DisplayArtist { get; set; } = "";

    public SingleBeatmapGenerationViewModel(
        Beatmap targetBeatmap,
        IBeatmapRateGenerator beatmapRateGenerator,
        ISettingsService settingsService)
    {
        TargetBeatmap = targetBeatmap;
        _beatmapRateGenerator = beatmapRateGenerator;
        RateGeneration = new RateGenerationViewModel();
        RateGeneration.SourceBpm = targetBeatmap.BPM;

        // Unicode優先設定に基づいて表示名を決定
        var preferUnicode = settingsService.SettingsData.PreferUnicode;
        DisplayTitle = (preferUnicode && !string.IsNullOrEmpty(targetBeatmap.TitleUnicode))
            ? targetBeatmap.TitleUnicode
            : targetBeatmap.Title;
        DisplayArtist = (preferUnicode && !string.IsNullOrEmpty(targetBeatmap.ArtistUnicode))
            ? targetBeatmap.ArtistUnicode
            : targetBeatmap.Artist;

        RateGeneration.ObserveProperty(nameof(RateGenerationViewModel.HasValidationErrors))
            .Subscribe(_ => GenerateCommand.NotifyCanExecuteChanged())
            .AddTo(Disposables);

        // 設定値が変更されたら IsCompleted をリセットして再生成を許可
        RateGeneration.ObservePropertyChanged()
            .Subscribe(_ =>
            {
                if (IsCompleted)
                {
                    IsCompleted = false;
                }
            })
            .AddTo(Disposables);
    }

    private bool CanGenerate() => !IsGenerating && !IsCompleted && !RateGeneration.HasValidationErrors;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
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

            var result = await _beatmapRateGenerator.GenerateAsync(
                TargetBeatmap, RateGeneration.ToOptions(), progress, _cts.Token);
            generationDone = true;
            GenerationProgressValue = 100;

            if (result.Success)
            {
                IsCompleted = true;
                var message = string.Format(lang.GetString("BeatmapGen.GenerationComplete"));
                if (result.SkippedFileCount > 0)
                {
                    message += "\n" + string.Format(
                        lang.GetString("BeatmapGen.SkippedFiles"),
                        result.SkippedFileCount);
                }
                message += "\n" + lang.GetString("BeatmapGen.RefreshHint");
                GenerationStatusMessage = message;
            }
            else
            {
                GenerationStatusMessage = result.ErrorMessage ?? lang.GetString("BeatmapGen.UnknownError");
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

        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCompletedChanged(bool value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        RateGeneration.Dispose();
        base.Dispose();
    }
}
