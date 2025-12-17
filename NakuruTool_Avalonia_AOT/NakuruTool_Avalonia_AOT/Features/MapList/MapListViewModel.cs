using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Linq;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public interface IMapListViewModel: IDisposable
{
    IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; }
    int TotalCount { get; }
    int CurrentPage { get; }
    int FilteredPages { get; }
    int FilteredCount { get; }
    void Initialize();
}

/// <summary>
/// データベース読み込み用のViewModel
/// </summary>
public partial class MapListViewModel : ViewModelBase, IMapListViewModel
{
    [ObservableProperty]
    private IAvaloniaReadOnlyList<Beatmap> _showBeatmaps = new AvaloniaList<Beatmap>();

    [ObservableProperty]
    private int _totalCount = 0;
    [ObservableProperty]
    private int _currentPage = 1;
    [ObservableProperty]
    private int _filteredPages = 0;
    [ObservableProperty]
    private int _filteredCount = 0;

    private IDatabaseService _databaseService;
    private const int SHOW_MAPS = 40;
    
    // フィルタ結果をキャッシュ
    private Beatmap[] _filteredBeatmapsArray = Array.Empty<Beatmap>();
    private AvaloniaList<Beatmap> _showBeatmapsList = new();

    public MapListViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _showBeatmaps = _showBeatmapsList;
    }

    public void Initialize()
    {
        UpdateTotalCount();
        UpdateFilteredBeatmapsArray();
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage() => CurrentPage++;
    private bool CanGoToNextPage() => CurrentPage < FilteredPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage() => CurrentPage--;
    private bool CanGoToPreviousPage() => CurrentPage > 1;
    
    private void UpdateTotalCount() => TotalCount = _databaseService.Beatmaps.Count;
    private void UpdateFilteredPages() => FilteredPages = (FilteredCount + SHOW_MAPS - 1) / SHOW_MAPS;
    
    private void UpdateFilteredBeatmapsArray()
    {
        var allBeatmaps = _databaseService.Beatmaps.Values.AsValueEnumerable();
        _filteredBeatmapsArray = allBeatmaps
            .Where(x => x.KeyCount == 7)
            .ToArray();
        FilteredCount = _filteredBeatmapsArray.Length;
    }
    
    private void UpdateShowBeatmaps()
    {
        var skip = (CurrentPage - 1) * SHOW_MAPS;
        var take = Math.Min(SHOW_MAPS, _filteredBeatmapsArray.Length - skip);

        _showBeatmapsList.Clear();
        
        if (take > 0)
        {
            var span = _filteredBeatmapsArray.AsSpan(skip, take);
            foreach (var beatmap in span)
            {
                _showBeatmapsList.Add(beatmap);
            }
        }
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdateShowBeatmaps();
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnFilteredCountChanged(int value) => UpdateFilteredPages();
}