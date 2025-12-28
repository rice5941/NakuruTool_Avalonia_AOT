using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
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
    [ObservableProperty]
    public partial MapFilterViewModel FilterViewModel { get; set; }

    [ObservableProperty]
    public partial MapListViewModel ListViewModel { get; set; }

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

    private readonly IGenerateCollectionService _generateCollectionService;
    private bool _disposed;

    public MapListPageViewModel(
        IDatabaseService databaseService,
        IGenerateCollectionService generateCollectionService,
        IFilterPresetService presetService)
    {
        _generateCollectionService = generateCollectionService;

        FilterViewModel = new MapFilterViewModel(presetService);
        ListViewModel = new MapListViewModel(databaseService, FilterViewModel);

        // 進捗監視
        _generateCollectionService.GenerationProgressObservable
            .Subscribe(progress =>
            {
                GenerationStatusMessage = progress.Message;
                GenerationProgressValue = progress.ProgressValue;
            })
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
        ListViewModel.Initialize();
    }

    private bool CanAddToCollection() => !IsGenerating && !string.IsNullOrWhiteSpace(CollectionName);

    [RelayCommand(CanExecute = nameof(CanAddToCollection))]
    private async Task AddToCollectionAsync()
    {
        await UpdateIsGeneratingAsync(true);
        var filteredBeatmaps = ListViewModel.FilteredBeatmapsArray;

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

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        FilterViewModel.Dispose();
        ListViewModel.Dispose();
        base.Dispose();
    }
}
