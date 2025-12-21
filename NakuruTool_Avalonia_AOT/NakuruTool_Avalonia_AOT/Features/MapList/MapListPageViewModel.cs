using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
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
    public partial MapFilterViewModel FilterViewModel { get; set; } = new MapFilterViewModel();

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

    public MapListPageViewModel(IDatabaseService databaseService, IGenerateCollectionService generateCollectionService)
    {
        _generateCollectionService = generateCollectionService;

        ListViewModel = new MapListViewModel(databaseService, FilterViewModel);

        // 進捗監視
        _generateCollectionService.GenerationProgressObservable
            .Subscribe(progress =>
            {
                GenerationStatusMessage = progress.Message;
                GenerationProgressValue = progress.ProgressValue;
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
