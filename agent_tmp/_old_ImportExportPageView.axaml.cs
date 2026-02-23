using Avalonia;
using Avalonia.Controls;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public partial class ImportExportPageView : UserControl
{
    private IDisposable? _propertySubscription;
    private DataGridColumn? _existsColumn;

    public ImportExportPageView()
    {
        InitializeComponent();
        _existsColumn = this.FindControl<DataGrid>("BeatmapGrid")?.Columns[^1];
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _propertySubscription?.Dispose();
        if (DataContext is ImportExportPageViewModel vm)
        {
            _propertySubscription = vm.ObserveProperty(nameof(ImportExportPageViewModel.IsImportPreview))
                .Subscribe(_ =>
                {
                    if (_existsColumn != null)
                        _existsColumn.IsVisible = vm.IsImportPreview;
                });
            if (_existsColumn != null)
                _existsColumn.IsVisible = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _propertySubscription?.Dispose();
        _propertySubscription = null;
    }
}
