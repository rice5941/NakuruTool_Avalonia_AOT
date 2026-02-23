using Avalonia.Controls;
using Avalonia.Input;
using System.Diagnostics;

namespace NakuruTool_Avalonia_AOT.Features.Licenses;

public partial class LicensesPage : UserControl
{
    public LicensesPage()
    {
        InitializeComponent();
    }

    private void OnUrlClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Text is string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // URL を開けない場合は何もしない
            }
        }
    }

    private async void OnViewFullTextClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: LicenseItem item })
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            var dialog = new LicenseTextWindow(item);
            if (window is not null)
            {
                await dialog.ShowDialog(window);
            }
            else
            {
                dialog.Show();
            }
        }
    }
}
