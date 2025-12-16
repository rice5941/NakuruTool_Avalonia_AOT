using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();
    }
    
    public MainWindowView(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
        
        // ウィンドウが開いたときにデータベース読み込みを開始
        /*
        Opened += async (_, _) =>
        {
            await viewModel.StartLoadingAsync();
        };
        */
    }
}