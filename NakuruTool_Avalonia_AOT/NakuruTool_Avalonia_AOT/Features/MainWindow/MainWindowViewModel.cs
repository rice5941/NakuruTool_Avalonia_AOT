using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.Licenses;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowViewModel : ViewModelBase
{
    public ISettingsViewModel SettingsViewModel { get; }
    public IDatabaseLoadingViewModel DatabaseLoadingViewModel { get; }
    public IMapListViewModel MapListViewModel { get; }
    public MapListPageViewModel MapListPageViewModel { get; }
    public ImportExportPageViewModel ImportExportPageViewModel { get; }
    public BeatmapGenerationPageViewModel BeatmapGenerationPageViewModel { get; }
    public ILicensesViewModel LicensesViewModel { get; }

    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    public partial bool IsLoadingOverlayVisible { get; set; } = true;

    /// <summary>
    /// 現在選択されているタブのインデックス
    /// </summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; } = 1;

    // タブインデックス定数
    private const int TabIndexMapList = 1;
    private const int TabIndexSettings = 5;

    public MainWindowViewModel(
        ISettingsViewModel settingsViewModel, 
        IDatabaseLoadingViewModel databaseLoadingViewModel, 
        IMapListViewModel mapListViewModel,
        MapListPageViewModel mapListPageViewModel,
        ImportExportPageViewModel importExportPageViewModel,
        BeatmapGenerationPageViewModel beatmapGenerationPageViewModel,
        ILicensesViewModel licensesViewModel,
        ISettingsService settingsService)
    {
        SettingsViewModel = settingsViewModel;
        DatabaseLoadingViewModel = databaseLoadingViewModel;
        MapListViewModel = mapListViewModel;
        MapListPageViewModel = mapListPageViewModel;
        ImportExportPageViewModel = importExportPageViewModel;
        BeatmapGenerationPageViewModel = beatmapGenerationPageViewModel;
        LicensesViewModel = licensesViewModel;
        _settingsService = settingsService;

        // フォルダパスの変更をR3で監視し、データベースを再読み込み
        _settingsService.SettingsData
            .ObserveProperty(nameof(ISettingsData.OsuFolderPath))
            .Subscribe(OnFolderPathChanged)
            .AddTo(Disposables);
    }

    private void OnFolderPathChanged(PropertyChangedEventArgs args)
    {
        Task.Run(ReloadDatabaseAsync);
    }

    /// <summary>
    /// ウィンドウ表示時にデータベースを読み込む
    /// </summary>
    public async Task StartLoadingAsync()
    {
        IsLoadingOverlayVisible = true;

        await DatabaseLoadingViewModel.InitialLoadAsync();

        // データベース読み込み完了後にMapListPageViewModelを初期化
        MapListPageViewModel.Initialize();
        ImportExportPageViewModel.Initialize();
        BeatmapGenerationPageViewModel.Initialize();

        // 設定が有効かつDB読み込み成功時、プリセットから自動一括生成
        if (!DatabaseLoadingViewModel.HasError
            && _settingsService.SettingsData.AutoBatchGenerateOnStartup)
        {
            try
            {
                await MapListPageViewModel.AutoBatchGenerateFromPresetsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto batch generate failed: {ex.Message}");
            }
        }

        IsLoadingOverlayVisible = false;

        // 読み込み結果に応じてタブを切り替え
        if (DatabaseLoadingViewModel.HasError)
        {
            SelectedTabIndex = TabIndexSettings;
        }
        else
        {
            SelectedTabIndex = TabIndexMapList;
        }
    }

    /// <summary>
    /// フォルダパス変更時にデータベースを再読み込み
    /// </summary>
    private async Task ReloadDatabaseAsync()
    {
        IsLoadingOverlayVisible = true;

        try
        {
            await DatabaseLoadingViewModel.InitialLoadAsync();
            MapListPageViewModel.Initialize();
            ImportExportPageViewModel.Initialize();
            BeatmapGenerationPageViewModel.Initialize();
        }
        catch
        {
            // エラーは進捗通知経由で表示されるため、ここでは無視
        }
        finally
        {
            IsLoadingOverlayVisible = false;
        }
    }
}
