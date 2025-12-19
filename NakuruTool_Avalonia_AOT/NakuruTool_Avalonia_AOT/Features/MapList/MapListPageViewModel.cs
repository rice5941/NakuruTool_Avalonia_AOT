using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using System;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// MapListPageViewのViewModel
/// MapFilterViewModelとMapListViewModelを統合する
/// </summary>
public partial class MapListPageViewModel : ViewModelBase, IDisposable
{
    [ObservableProperty]
    private MapFilterViewModel _filterViewModel;

    [ObservableProperty]
    private MapListViewModel _listViewModel;

    private bool _disposed;

    public MapListPageViewModel(IDatabaseService databaseService)
    {
        _filterViewModel = new MapFilterViewModel();
        _listViewModel = new MapListViewModel(databaseService, _filterViewModel);
    }

    public void Initialize()
    {
        ListViewModel.Initialize();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        FilterViewModel.Dispose();
        ListViewModel.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
