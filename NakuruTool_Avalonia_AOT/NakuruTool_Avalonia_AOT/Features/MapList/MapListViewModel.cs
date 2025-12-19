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
    [ObservableProperty]
    private int _pageSize = DefaultPageSize;

    private IDatabaseService _databaseService;
    private const int DefaultPageSize = 20;
    
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

    [RelayCommand]
    private void LoadPage(int pageNumber)
    {
        var maxPage = Math.Max(1, FilteredPages);
        var next = Math.Clamp(pageNumber, 1, maxPage);
        CurrentPage = next;
    }
    
    private void UpdateTotalCount() => TotalCount = _databaseService.Beatmaps.Length;
    private void UpdateFilteredPages()
    {
        var size = Math.Max(1, PageSize);
        FilteredPages = Math.Max(1, (FilteredCount + size - 1) / size);
        if (CurrentPage > FilteredPages)
        {
            CurrentPage = FilteredPages;
        }
        else if (CurrentPage < 1)
        {
            CurrentPage = 1;
        }
    }
    
    private void UpdateFilteredBeatmapsArray()
    {
        var allBeatmaps = _databaseService.Beatmaps.AsValueEnumerable();
        _filteredBeatmapsArray = allBeatmaps
            .Where(x => x.KeyCount == 7)
            .ToArray();
        FilteredCount = _filteredBeatmapsArray.Length;
    }
    
    private void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var remaining = Math.Max(0, _filteredBeatmapsArray.Length - skip);
        var take = Math.Min(size, remaining);

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

    partial void OnPageSizeChanged(int value)
    {
        if (value <= 0)
        {
            PageSize = DefaultPageSize;
            return;
        }

        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }
}