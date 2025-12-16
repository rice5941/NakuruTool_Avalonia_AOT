using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowViewModel : ViewModelBase
{
    public ISettingsViewModel SettingsViewModel { get; }
    public IDatabaseLoadingViewModel DatabaseLoadingViewModel { get; }

    [ObservableProperty]
    private bool _isLoadingOverlayVisible = true;

    public MainWindowViewModel(ISettingsViewModel settingsViewModel, IDatabaseLoadingViewModel databaseLoadingViewModel)
    {
        SettingsViewModel = settingsViewModel;
        DatabaseLoadingViewModel = databaseLoadingViewModel;

        Task.Run(() => StartLoadingAsync());
    }

    /// <summary>
    /// ウィンドウ表示時にデータベースを読み込む
    /// </summary>
    public async Task StartLoadingAsync()
    {
        IsLoadingOverlayVisible = true;

        await DatabaseLoadingViewModel.InitialLoadAsync();

        //await Task.Delay(350);

        IsLoadingOverlayVisible = false;
    }
}
