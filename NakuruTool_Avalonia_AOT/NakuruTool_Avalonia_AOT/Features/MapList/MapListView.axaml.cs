using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public partial class MapListView : UserControl
{
    private Beatmap? _rightClickTarget;
    private MapListViewModel? _boundViewModel;

    public MapListView()
    {
        InitializeComponent();

        MapListDataGrid.AddHandler(
            InputElement.PointerPressedEvent,
            OnDataGridPointerPressed,
            RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
        {
            _boundViewModel.SetClipboardWriter(null);
            _boundViewModel.ClearContextMenuBeatmap();
        }

        _boundViewModel = DataContext as MapListViewModel;
        _boundViewModel?.SetClipboardWriter(CopyToClipboardAsync);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _boundViewModel?.SetClipboardWriter(null);
        _boundViewModel?.ClearContextMenuBeatmap();
        _boundViewModel = null;
        _rightClickTarget = null;
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(MapListDataGrid).Properties.IsRightButtonPressed)
            return;

        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>();
        _rightClickTarget = row?.DataContext as Beatmap;

        if (_rightClickTarget is not null && _boundViewModel is not null)
        {
            _boundViewModel.SelectBeatmapForContextMenu(_rightClickTarget);
        }
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (_rightClickTarget is null ||
            _boundViewModel is null ||
            !_boundViewModel.TryPrepareContextMenu(_rightClickTarget))
        {
            e.Cancel = true;
        }
    }

    private void OnContextMenuClosing(object? sender, CancelEventArgs e)
    {
        _boundViewModel?.ClearContextMenuBeatmap();
    }

    private async Task CopyToClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
