using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.Licenses;

public partial class LicenseTextWindow : Window
{
    public LicenseTextWindow()
    {
        InitializeComponent();
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
