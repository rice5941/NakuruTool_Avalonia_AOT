using Avalonia;
using Avalonia.Controls;
using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public partial class MapListPageView : ContentPage
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
        Resubscribe();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // NavigationPage でのページ切り替えにより Detach → 再 Attach されるため、
        // DataContext が変わらなくても購読を張り直す
        if (_viewModel is null)
        {
            Resubscribe();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _subscriptions.Dispose();
        _subscriptions = [];
        _viewModel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Resubscribe()
    {
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
}
