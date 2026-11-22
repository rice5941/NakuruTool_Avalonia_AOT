using Avalonia.Controls;
using NakuruTool_Avalonia_AOT.Features.Shared.Scaling;

namespace NakuruTool_Avalonia_AOT.Features.Licenses;

public partial class LicenseTextWindow : Window
{
    public LicenseTextWindow()
    {
        InitializeComponent();

        // XAML のサイズは開発時の大きさ（UI 表示倍率 125%、Scale 1.0）の値なので、中身の拡大・縮小に合わせてウィンドウの初期サイズも揃える
        var scale = UiScaleService.Instance.Scale;
        Width *= scale;
        Height *= scale;
        MinWidth *= scale;
        MinHeight *= scale;
    }

    public LicenseTextWindow(LicenseItem item) : this()
    {
        DataContext = new LicenseTextWindowViewModel(
            item.PackageName,
            item.LicenseText ?? string.Empty
        );
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
