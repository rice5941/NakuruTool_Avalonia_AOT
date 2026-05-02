using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public partial class MapListView : UserControl
{
    public static readonly StyledProperty<bool> ShowAudioPlayerProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowAudioPlayer), defaultValue: true);

    public static readonly StyledProperty<bool> ShowTotalCountProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowTotalCount), defaultValue: true);

    public static readonly StyledProperty<bool> ShowFilteredCountProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowFilteredCount), defaultValue: true);

    public static readonly StyledProperty<bool> ShowResolvedCountProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowResolvedCount), defaultValue: false);

    public static readonly StyledProperty<bool> ShowHistoryColumnProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowHistoryColumn), defaultValue: true);

    public static readonly StyledProperty<bool> ShowIsPlayedColumnProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowIsPlayedColumn), defaultValue: true);

    public static readonly StyledProperty<string?> ScoreSystemGroupNameProperty =
        AvaloniaProperty.Register<MapListView, string?>(nameof(ScoreSystemGroupName), defaultValue: null);

    public static readonly StyledProperty<string?> ModGroupNameProperty =
        AvaloniaProperty.Register<MapListView, string?>(nameof(ModGroupName), defaultValue: null);

    public static readonly StyledProperty<double> DataGridRowHeightProperty =
        AvaloniaProperty.Register<MapListView, double>(nameof(DataGridRowHeight), defaultValue: 100d);

    public static readonly StyledProperty<bool> ShowSortButtonProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowSortButton), defaultValue: false);

    public static readonly StyledProperty<ICommand?> SortCommandProperty =
        AvaloniaProperty.Register<MapListView, ICommand?>(nameof(SortCommand), defaultValue: null);

    public bool ShowAudioPlayer
    {
        get => GetValue(ShowAudioPlayerProperty);
        set => SetValue(ShowAudioPlayerProperty, value);
    }

    public bool ShowTotalCount
    {
        get => GetValue(ShowTotalCountProperty);
        set => SetValue(ShowTotalCountProperty, value);
    }

    public bool ShowFilteredCount
    {
        get => GetValue(ShowFilteredCountProperty);
        set => SetValue(ShowFilteredCountProperty, value);
    }

    public bool ShowResolvedCount
    {
        get => GetValue(ShowResolvedCountProperty);
        set => SetValue(ShowResolvedCountProperty, value);
    }

    public bool ShowHistoryColumn
    {
        get => GetValue(ShowHistoryColumnProperty);
        set => SetValue(ShowHistoryColumnProperty, value);
    }

    public bool ShowIsPlayedColumn
    {
        get => GetValue(ShowIsPlayedColumnProperty);
        set => SetValue(ShowIsPlayedColumnProperty, value);
    }

    public string? ScoreSystemGroupName
    {
        get => GetValue(ScoreSystemGroupNameProperty);
        set => SetValue(ScoreSystemGroupNameProperty, value);
    }

    public string? ModGroupName
    {
        get => GetValue(ModGroupNameProperty);
        set => SetValue(ModGroupNameProperty, value);
    }

    public double DataGridRowHeight
    {
        get => GetValue(DataGridRowHeightProperty);
        set => SetValue(DataGridRowHeightProperty, value);
    }

    public bool ShowSortButton
    {
        get => GetValue(ShowSortButtonProperty);
        set => SetValue(ShowSortButtonProperty, value);
    }

    public ICommand? SortCommand
    {
        get => GetValue(SortCommandProperty);
        set => SetValue(SortCommandProperty, value);
    }

    private Beatmap? _rightClickTarget;
    private IBeatmapListViewModel? _boundViewModel;

    public MapListView()
    {
        InitializeComponent();

        MapListDataGrid.AddHandler(
            InputElement.PointerPressedEvent,
            OnDataGridPointerPressed,
            RoutingStrategies.Tunnel);

        ApplyHistoryColumnVisibility(ShowHistoryColumn);
        ApplyIsPlayedColumnVisibility(ShowIsPlayedColumn);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowHistoryColumnProperty)
        {
            ApplyHistoryColumnVisibility(change.GetNewValue<bool>());
        }
        else if (change.Property == ShowIsPlayedColumnProperty)
        {
            ApplyIsPlayedColumnVisibility(change.GetNewValue<bool>());
        }
    }

    private void ApplyHistoryColumnVisibility(bool visible)
    {
        var column = FindColumnByTag("HistoryColumn");
        if (column is not null)
        {
            column.IsVisible = visible;
        }
    }

    private void ApplyIsPlayedColumnVisibility(bool visible)
    {
        var column = FindColumnByTag("IsPlayedColumn");
        if (column is not null)
        {
            column.IsVisible = visible;
        }
    }

    private DataGridColumn? FindColumnByTag(string tag)
    {
        if (MapListDataGrid is null)
            return null;

        foreach (var column in MapListDataGrid.Columns)
        {
            if (column.Tag is string s && s == tag)
                return column;
        }

        return null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_boundViewModel is not null)
        {
            _boundViewModel.SetClipboardWriter(null);
            _boundViewModel.ClearContextMenuBeatmap();
        }

        ClearViewModelBindings();

        _boundViewModel = DataContext as IBeatmapListViewModel;
        if (_boundViewModel is not null)
        {
            _boundViewModel.SetClipboardWriter(CopyToClipboardAsync);

            if (CopyMenuItem is not null)
                CopyMenuItem.Command = _boundViewModel.CopyDownloadUrlCommand;
            if (OpenMenuItem is not null)
                OpenMenuItem.Command = _boundViewModel.OpenInExplorerCommand;
            if (GenerateMenuItem is not null)
                GenerateMenuItem.Command = _boundViewModel.GenerateBeatmapCommand;
        }

        // AudioPlayerHost は MapListViewModel 固有 (AudioPlayerPanel) のためここで個別分岐
        if (DataContext is MapListViewModel mapListVm && AudioPlayerHost is not null)
        {
            AudioPlayerHost.DataContext = mapListVm.AudioPlayerPanel;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _boundViewModel?.SetClipboardWriter(null);
        _boundViewModel?.ClearContextMenuBeatmap();

        ClearViewModelBindings();

        _boundViewModel = null;
        _rightClickTarget = null;
    }

    private void ClearViewModelBindings()
    {
        if (AudioPlayerHost is not null)
            AudioPlayerHost.DataContext = null;
        if (CopyMenuItem is not null)
            CopyMenuItem.Command = null;
        if (OpenMenuItem is not null)
            OpenMenuItem.Command = null;
        if (GenerateMenuItem is not null)
            GenerateMenuItem.Command = null;
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(MapListDataGrid).Properties.IsRightButtonPressed)
            return;

        if (_boundViewModel is null)
            return;

        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>();
        _rightClickTarget = row?.DataContext as Beatmap;

        if (_rightClickTarget is not null)
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
