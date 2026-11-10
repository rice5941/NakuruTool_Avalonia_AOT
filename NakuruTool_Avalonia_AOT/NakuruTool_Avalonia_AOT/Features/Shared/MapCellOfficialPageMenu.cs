using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace NakuruTool_Avalonia_AOT.Features.Shared;

/// <summary>
/// DataGrid の map セル (Classes="map-cell" を持つコントロール) を右クリックした際に
/// 「公式mapページを表示」メニューを表示し、選択されたら既定のブラウザで
/// https://osu.ppy.sh/beatmapsets/{beatmapsetid} を開く。
/// </summary>
internal sealed class MapCellOfficialPageMenu
{
    public const string MapCellClass = "map-cell";

    private readonly ContextMenu _menu;
    private readonly MenuItem? _openItem;
    private readonly Func<object?, int> _beatmapSetIdResolver;

    private Control? _pressedCell;
    private int _targetBeatmapSetId;

    /// <param name="grid">対象の DataGrid</param>
    /// <param name="menu">表示するメニュー (先頭の MenuItem を「公式mapページを表示」として扱う)</param>
    /// <param name="beatmapSetIdResolver">セルの DataContext から beatmapset ID を取得する関数</param>
    public MapCellOfficialPageMenu(DataGrid grid, ContextMenu menu, Func<object?, int> beatmapSetIdResolver)
    {
        _menu = menu;
        _beatmapSetIdResolver = beatmapSetIdResolver;

        _openItem = _menu.Items.OfType<MenuItem>().FirstOrDefault();
        if (_openItem is not null)
        {
            _openItem.Click += OnOpenOfficialPageClick;
        }

        grid.AddHandler(
            InputElement.PointerPressedEvent,
            OnGridPointerPressed,
            RoutingStrategies.Tunnel);
        grid.AddHandler(
            InputElement.PointerReleasedEvent,
            OnGridPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedCell = null;

        if (sender is not Visual visual ||
            !e.GetCurrentPoint(visual).Properties.IsRightButtonPressed)
            return;

        _pressedCell = FindMapCell(e.Source as Visual);
    }

    private void OnGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var cell = _pressedCell;
        _pressedCell = null;

        if (cell is null || e.InitialPressMouseButton != MouseButton.Right)
            return;

        // 押下したセル上で離された場合のみ (ドラッグで外れた場合は無視)
        var position = e.GetPosition(cell);
        if (!new Rect(cell.Bounds.Size).Contains(position))
            return;

        // beatmapset ID が無い譜面でもメニューは表示し、項目を無効化する
        _targetBeatmapSetId = _beatmapSetIdResolver(cell.DataContext);
        if (_openItem is not null)
            _openItem.IsEnabled = _targetBeatmapSetId > 0;

        _menu.Open(cell);
    }

    private void OnOpenOfficialPageClick(object? sender, RoutedEventArgs e)
    {
        OsuWebPageLauncher.OpenBeatmapSetPage(_targetBeatmapSetId);
    }

    private static Control? FindMapCell(Visual? source)
    {
        if (source is null)
            return null;

        // セルの余白部分をクリックした場合も対象とするため、DataGridCell 単位で判定する
        var cell = source.GetSelfAndVisualAncestors().OfType<DataGridCell>().FirstOrDefault();
        return cell?.Content is Control content && content.Classes.Contains(MapCellClass)
            ? cell
            : null;
    }
}
