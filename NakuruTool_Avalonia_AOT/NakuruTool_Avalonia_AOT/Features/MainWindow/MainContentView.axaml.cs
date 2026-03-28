using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainContentView : UserControl
{
    public MainContentView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public MainContentView(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.StartLoadingAsync();
        }
    }
}
