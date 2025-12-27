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
    int PageSize { get; }
    IAvaloniaReadOnlyList<int> PageSizes { get; }
    void Initialize();
    void ApplyFilter();
    Beatmap[] FilteredBeatmapsArray { get; }
}

/// <summary>
/// 譜面一覧のViewModel
/// </summary>
public partial class MapListViewModel : ViewModelBase, IMapListViewModel
{
    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; set; } = new AvaloniaList<Beatmap>();

    [ObservableProperty]
    public partial int TotalCount { get; set; } = 0;
    
    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;
    
    [ObservableProperty]
    public partial int FilteredPages { get; set; } = 0;
    
    [ObservableProperty]
    public partial int FilteredCount { get; set; } = 0;
    
    [ObservableProperty]
    public partial int PageSize { get; set; } = DefaultPageSize;

    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

    private IDatabaseService _databaseService;
    private MapFilterViewModel _filterViewModel;

    private const int DefaultPageSize = 20;

    [ObservableProperty]
    public partial Beatmap[] FilteredBeatmapsArray { get; set; } = Array.Empty<Beatmap>();
    
    private AvaloniaList<Beatmap> _showBeatmapsList = new();

    public MapListViewModel(IDatabaseService databaseService, MapFilterViewModel filterViewModel)
    {
        _databaseService = databaseService;
        _filterViewModel = filterViewModel;

        ShowBeatmaps = _showBeatmapsList;

        _filterViewModel.FilterChanged
            .Subscribe(_ => ApplyFilter())
            .AddTo(Disposables);
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

    private void UpdateTotalCount() => TotalCount = _databaseService.Beatmaps.Length;

    private void UpdateFilteredPages()
    {
        var size = Math.Max(1, PageSize);
        FilteredPages = Math.Max(1, (FilteredCount + size - 1) / size);
    }
    
    private void UpdateFilteredBeatmapsArray()
    {
        var allBeatmaps = _databaseService.Beatmaps.AsValueEnumerable();
        
        FilteredBeatmapsArray = allBeatmaps
            .Where(x => _filterViewModel.Matches(x))
            .ToArray();

        FilteredCount = FilteredBeatmapsArray.Length;
    }

    public void ApplyFilter()
    {
        UpdateFilteredBeatmapsArray();
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }
    
    private void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var remaining = Math.Max(0, FilteredBeatmapsArray.Length - skip);
        var take = Math.Min(size, remaining);

        _showBeatmapsList.Clear();
        
        if (take > 0)
        {
            var span = FilteredBeatmapsArray.AsSpan(skip, take);
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
    partial void OnFilteredPagesChanged(int value) => CurrentPage = 1;

    partial void OnPageSizeChanged(int value)
    {
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }
}