using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowViewModel : ViewModelBase
{
    public ISettingsViewModel SettingsViewModel { get; }
    public IDatabaseLoadingViewModel DatabaseLoadingViewModel { get; }
    public IMapListViewModel MapListViewModel { get; }

    [ObservableProperty]
    private bool _isLoadingOverlayVisible = true;

    public MainWindowViewModel(
        ISettingsViewModel settingsViewModel, 
        IDatabaseLoadingViewModel databaseLoadingViewModel, 
        IMapListViewModel mapListViewModel)
    {
        SettingsViewModel = settingsViewModel;
        DatabaseLoadingViewModel = databaseLoadingViewModel;
        MapListViewModel = mapListViewModel;
    }

    /// <summary>
    /// ウィンドウ表示時にデータベースを読み込む
    /// </summary>
    public async Task StartLoadingAsync()
    {
        IsLoadingOverlayVisible = true;

        await DatabaseLoadingViewModel.InitialLoadAsync();

        // データベース読み込み完了後にMapListViewModelを初期化
        MapListViewModel.Initialize();

        //await Task.Delay(350);

        IsLoadingOverlayVisible = false;
    }
}
