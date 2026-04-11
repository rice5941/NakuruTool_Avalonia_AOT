using Avalonia;
using Avalonia.Controls;
using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public partial class MapListPageView : UserControl
{
    private CompositeDisposable _subscriptions = [];
    private MapListPageViewModel? _viewModel;

    public MapListPageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _subscriptions.Dispose();
        _subscriptions = [];
        _viewModel = null;

        if (DataContext is MapListPageViewModel vm)
        {
            _viewModel = vm;
            vm.ListViewModel.GenerateBeatmapRequested
                .Subscribe(beatmap =>
                {
                    vm.ShowSingleGeneration(beatmap);
                })
                .AddTo(_subscriptions);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _subscriptions.Dispose();
        _subscriptions = [];
        _viewModel = null;
        base.OnDetachedFromVisualTree(e);
    }
}
