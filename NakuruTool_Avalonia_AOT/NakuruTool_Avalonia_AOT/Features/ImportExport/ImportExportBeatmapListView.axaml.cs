using Avalonia;
using Avalonia.Controls;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public partial class ImportExportBeatmapListView : UserControl
{
    private IDisposable? _propertySubscription;
    private DataGridColumn? _existsColumn;

    public ImportExportBeatmapListView()
    {
        InitializeComponent();
        _existsColumn = this.FindControl<DataGrid>("BeatmapGrid")?.Columns[^1];
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _propertySubscription?.Dispose();
        _propertySubscription = null;

        if (DataContext is ImportExportBeatmapListViewModel vm)
        {
            // IsImportPreview変更を監視してExists列の表示を制御
            _propertySubscription = vm.ObserveProperty(
                    nameof(ImportExportBeatmapListViewModel.IsImportPreview))
                .Subscribe(_ =>
                {
                    if (_existsColumn != null)
                        _existsColumn.IsVisible = vm.IsImportPreview;
                });

            // 初期値を即時適用（ObservePropertyは初期値をPushしないため）
            if (_existsColumn != null)
                _existsColumn.IsVisible = vm.IsImportPreview;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _propertySubscription?.Dispose();
        _propertySubscription = null;
    }
}
