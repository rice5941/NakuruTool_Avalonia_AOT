using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using Avalonia.Threading;
using R3;
using System;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// MapListPageViewのViewModel
/// MapFilterViewModelとMapListViewModelを統合する
/// </summary>
public partial class MapListPageViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// 確認ダイアログを表示する閾値
    /// </summary>
    private const int LargeCollectionThreshold = 10_000;

    [ObservableProperty]
    public partial MapFilterViewModel FilterViewModel { get; set; }

    [ObservableProperty]
    public partial MapListViewModel ListViewModel { get; set; }

    [ObservableProperty]
    public partial bool IsPresetEditorVisible { get; set; } = false;

    // PresetEditorViewModelは手動newのため[ObservableProperty]不要
    public PresetEditorViewModel PresetEditorViewModel { get; }

    [ObservableProperty]
    public partial string CollectionName { get; set; } = String.Empty;
    partial void OnCollectionNameChanged(string value) => AddToCollectionCommand.NotifyCanExecuteChanged();

    [ObservableProperty]
    public partial string GenerationStatusMessage { get; set; } = " ";

    [ObservableProperty]
    public partial int GenerationProgressValue { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsGenerating { get; set; } = false;
    partial void OnIsGeneratingChanged(bool value) => AddToCollectionCommand.NotifyCanExecuteChanged();

    [ObservableProperty]
    public partial bool IsLargeCollectionConfirmVisible { get; set; } = false;

    [ObservableProperty]
    public partial string LargeCollectionConfirmMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SingleBeatmapGenerationViewModel? SingleGenerationViewModel { get; set; }

    [ObservableProperty]
    public partial bool IsSingleGenerationOverlayVisible { get; set; } = false;

    partial void OnIsLargeCollectionConfirmVisibleChanged(bool value)
    {
        AddToCollectionCommand.NotifyCanExecuteChanged();
    }

    private readonly IGenerateCollectionService _generateCollectionService;
    private readonly IBeatmapRateGenerator _beatmapRateGenerator;
    private bool _disposed;

    public IBeatmapRateGenerator BeatmapRateGenerator => _beatmapRateGenerator;

    public MapListPageViewModel(
        IDatabaseService databaseService,
        IGenerateCollectionService generateCollectionService,
        IFilterPresetService presetService,
        AudioPlayerViewModel audioPlayerViewModel,
        AudioPlayerPanelViewModel audioPlayerPanelViewModel,
        ISettingsService settingsService,
        IBeatmapRateGenerator beatmapRateGenerator)
    {
        _generateCollectionService = generateCollectionService;
        _beatmapRateGenerator = beatmapRateGenerator;

        FilterViewModel = new MapFilterViewModel(presetService, databaseService);
        ListViewModel = new MapListViewModel(databaseService, FilterViewModel, audioPlayerViewModel, audioPlayerPanelViewModel, settingsService);
        PresetEditorViewModel = new PresetEditorViewModel(presetService, databaseService, generateCollectionService);

        // MapFilterViewModelにToggle命令を中継するコマンドを注入
        FilterViewModel.TogglePresetEditorCommand = TogglePresetEditorCommand;

        // 進捗監視
        _generateCollectionService.GenerationProgressObservable
            .Subscribe(progress =>
            {
                GenerationStatusMessage = progress.Message;
                GenerationProgressValue = progress.ProgressValue;
            })
            .AddTo(Disposables);

        // フィルタ後件数が変化したときにAddToCollectionCommandの有効/無効を更新
        ListViewModel.ObserveProperty(nameof(ListViewModel.FilteredCount))
            .Subscribe(_ => AddToCollectionCommand.NotifyCanExecuteChanged())
            .AddTo(Disposables);

        // プリセット選択時にコレクション名を反映
        FilterViewModel.ObserveProperty(nameof(FilterViewModel.SelectedPreset))
            .Subscribe(_ =>
            {
                if (FilterViewModel.SelectedPreset != null)
                {
                    CollectionName = FilterViewModel.SelectedPreset.CollectionName;
                }
            })
            .AddTo(Disposables);
    }

    public void Initialize()
    {
        // DB読み込み完了後にコレクション名リストを更新
        FilterViewModel.RefreshCollectionNames();
        ListViewModel.Initialize();
    }

    public void ShowSingleGeneration(Beatmap beatmap)
    {
        SingleGenerationViewModel?.Dispose();
        SingleGenerationViewModel = new SingleBeatmapGenerationViewModel(beatmap, _beatmapRateGenerator);
        IsSingleGenerationOverlayVisible = true;
    }

    [RelayCommand]
    private void CloseSingleGeneration()
    {
        IsSingleGenerationOverlayVisible = false;
        SingleGenerationViewModel?.Dispose();
        SingleGenerationViewModel = null;
    }

    [RelayCommand]
    private void TogglePresetEditor()
    {
        IsPresetEditorVisible = !IsPresetEditorVisible;
    }

    partial void OnIsPresetEditorVisibleChanged(bool value)
    {
        if (!value)
        {
            // 編集画面を閉じたとき、一括生成の副作用で更新されたステータスをリセット
            GenerationStatusMessage = " ";
            GenerationProgressValue = 0;
        }
    }

    private bool CanAddToCollection() =>
        !IsGenerating
        && !IsLargeCollectionConfirmVisible
        && !string.IsNullOrWhiteSpace(CollectionName)
        && ListViewModel.FilteredCount > 0;

    [RelayCommand(CanExecute = nameof(CanAddToCollection))]
    private async Task AddToCollectionAsync()
    {
        var filteredCount = ListViewModel.FilteredCount;

        if (filteredCount > LargeCollectionThreshold)
        {
            LargeCollectionConfirmMessage = string.Format(
                LanguageService.Instance.GetString("Collection.ConfirmLargeCollectionMessage"),
                filteredCount);
            IsLargeCollectionConfirmVisible = true;
            return;
        }

        await ExecuteAddToCollectionAsync();
    }

    [RelayCommand]
    private async Task ConfirmLargeCollectionAsync()
    {
        IsLargeCollectionConfirmVisible = false;
        await ExecuteAddToCollectionAsync();
    }

    [RelayCommand]
    private void CancelLargeCollection()
    {
        IsLargeCollectionConfirmVisible = false;
    }

    /// <summary>
    /// コレクション生成の実処理（確認済み前提）
    /// </summary>
    private async Task ExecuteAddToCollectionAsync()
    {
        // 確認ダイアログ表示中にフィルタが変動した場合のガード
        var filteredBeatmaps = ListViewModel.FilteredBeatmapsArray;
        if (filteredBeatmaps.Length == 0)
        {
            return;
        }

        await UpdateIsGeneratingAsync(true);

        try
        {
            var success = await _generateCollectionService.GenerateCollection(CollectionName, filteredBeatmaps);

            if (success)
            {
                // コレクション保存成功時、プリセットも保存
                SavePresetIfNeeded();

                var message = string.Format(
                    LanguageService.Instance.GetString("Collection.GenerationSuccess"),
                    CollectionName,
                    filteredBeatmaps.Length);
                await UpdateGenerationResultAsync(message, string.Empty);
            }
            else
            {
                var message = LanguageService.Instance.GetString("Collection.GenerationFailed");
                await UpdateGenerationResultAsync(message, CollectionName);
            }
        }
        catch (Exception ex)
        {
            var message = $"{LanguageService.Instance.GetString("Collection.GenerationFailed")}: {ex.Message}";
            await UpdateGenerationResultAsync(message, CollectionName);
        }
        finally
        {
            await UpdateIsGeneratingAsync(false);
        }
    }

    /// <summary>
    /// 必要に応じてプリセットを保存
    /// </summary>
    private void SavePresetIfNeeded()
    {
        // コレクション名が指定されていて、絞り込み条件がある場合のみ保存
        if (!string.IsNullOrWhiteSpace(CollectionName) && FilterViewModel.Conditions.Count > 0)
        {
            // プリセット名 = コレクション名
            var preset = FilterViewModel.CreatePreset(CollectionName, CollectionName);
            FilterViewModel.SavePreset(preset);
        }
    }

    /// <summary>
    /// IsGeneratingプロパティをUIスレッドで更新し、更新完了まで待機
    /// </summary>
    private async Task UpdateIsGeneratingAsync(bool value)
    {
        await Dispatcher.UIThread.InvokeAsync(() => IsGenerating = value);
    }

    /// <summary>
    /// 生成結果をUIスレッドで更新し、更新完了まで待機
    /// </summary>
    private async Task UpdateGenerationResultAsync(string message, string newCollectionName)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            GenerationStatusMessage = message;
            CollectionName = newCollectionName;
        });
    }

    /// <summary>
    /// 起動時の自動一括生成を実行する。
    /// </summary>
    public async Task AutoBatchGenerateFromPresetsAsync()
    {
        await PresetEditorViewModel.ExecuteBatchGenerateCoreAsync();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SingleGenerationViewModel?.Dispose();
        FilterViewModel.Dispose();
        ListViewModel.Dispose();
        PresetEditorViewModel.Dispose();
        base.Dispose();
    }
}
