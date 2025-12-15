using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowViewModel : ViewModelBase
{
    public SettingsViewModel SettingsViewModel { get; }

    public MainWindowViewModel(SettingsViewModel settingsViewModel)
    {
        SettingsViewModel = settingsViewModel;
    }
}
