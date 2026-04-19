using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public partial class ImportView : UserControl
{
    private IBrush? _originalBorderBrush;

    public ImportView()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
        DragDrop.AddDragEnterHandler(this, OnDragEnter);
        DragDrop.AddDragLeaveHandler(this, OnDragLeave);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        var border = this.FindControl<Border>("RootBorder");
        if (border is null) return;
        _originalBorderBrush = border.BorderBrush;
        if (this.TryFindResource("SemiBluePrimary", out var res) && res is IBrush accentBrush)
        {
            border.BorderBrush = accentBrush;
        }
        var overlay = this.FindControl<Border>("DragOverlay");
        if (overlay is not null) overlay.IsVisible = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        HideDragFeedback();
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        HideDragFeedback();

        var storageItems = e.DataTransfer.TryGetFiles();
        if (storageItems is null) return;

        var paths = new List<string>();
        foreach (var item in storageItems)
        {
            var localPath = item.Path?.LocalPath;
            if (!string.IsNullOrEmpty(localPath))
                paths.Add(localPath);
        }

        if (paths.Count > 0 && DataContext is ImportViewModel vm)
        {
            await vm.HandleDroppedPathsAsync(paths);
        }
    }

    private void HideDragFeedback()
    {
        var border = this.FindControl<Border>("RootBorder");
        if (border is not null && _originalBorderBrush is not null)
        {
            border.BorderBrush = _originalBorderBrush;
            _originalBorderBrush = null;
        }
        var overlay = this.FindControl<Border>("DragOverlay");
        if (overlay is not null) overlay.IsVisible = false;
    }
}
