using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public partial class CollectionSelectorViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;

    public AvaloniaList<OsuCollection> Collections { get; } = new();

    [ObservableProperty]
    public partial OsuCollection? SelectedCollection { get; set; }

    [ObservableProperty]
    public partial int SelectedCollectionBeatmapCount { get; set; }

    public CollectionSelectorViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public void RefreshCollections()
    {
        SelectedCollection = null;
        Collections.Clear();
        foreach (var col in _databaseService.OsuCollections)
            Collections.Add(col);
    }

    partial void OnSelectedCollectionChanged(OsuCollection? value)
    {
        SelectedCollectionBeatmapCount = value?.BeatmapMd5s?.Length ?? 0;
    }
}
