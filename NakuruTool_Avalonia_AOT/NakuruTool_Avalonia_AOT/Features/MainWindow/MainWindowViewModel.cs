using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
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

    /// <summary>
    /// オーディオパネルの ViewModel（シングルトン）。音楽プレイヤーモード時のパネル表示とモード監視に使う。
    /// </summary>
    public AudioPlayerPanelViewModel AudioPlayerPanelViewModel { get; }

    private readonly ISettingsService _settingsService;
    private readonly IAnalyze7kService? _analyze7kService;

    [ObservableProperty]
    public partial bool IsLoadingOverlayVisible { get; set; } = true;

    /// <summary>
    /// 現在表示しているページ（サイドバーの選択項目と同期する）
    /// </summary>
    [ObservableProperty]
    public partial MainPageKind SelectedPage { get; set; } = MainPageKind.MapList;

    public MainWindowViewModel(
        ISettingsViewModel settingsViewModel, 
        IDatabaseLoadingViewModel databaseLoadingViewModel, 
        IMapListViewModel mapListViewModel,
        MapListPageViewModel mapListPageViewModel,
        ImportExportPageViewModel importExportPageViewModel,
        BeatmapGenerationPageViewModel beatmapGenerationPageViewModel,
        ILicensesViewModel licensesViewModel,
        ISettingsService settingsService,
        IAnalyze7kService? analyze7kService = null)
    {
        SettingsViewModel = settingsViewModel;
        DatabaseLoadingViewModel = databaseLoadingViewModel;
        MapListViewModel = mapListViewModel;
        MapListPageViewModel = mapListPageViewModel;
        ImportExportPageViewModel = importExportPageViewModel;
        BeatmapGenerationPageViewModel = beatmapGenerationPageViewModel;
        LicensesViewModel = licensesViewModel;
        _analyze7kService = analyze7kService;
        AudioPlayerPanelViewModel = mapListPageViewModel.ListViewModel.AudioPlayerPanel;
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
        StartAnalyze7k();

        // 設定が有効かつDB読み込み成功時、プリセットから自動一括生成
        if (!DatabaseLoadingViewModel.HasError
            && _settingsService.SettingsData.AutoBatchGenerateOnStartup)
        {
            try
            {
                // 7K 解析の条件を使うプリセットのため、解析済みの値の反映を待つ（最大30秒）
                if (_analyze7kService is not null)
                    await Task.WhenAny(_analyze7kService.WhenCachedResultsAppliedAsync(), Task.Delay(TimeSpan.FromSeconds(30)));
                await MapListPageViewModel.AutoBatchGenerateFromPresetsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto batch generate failed: {ex.Message}");
            }
        }

        IsLoadingOverlayVisible = false;

        // 読み込み結果に応じて表示ページを切り替え
        SelectedPage = DatabaseLoadingViewModel.HasError
            ? MainPageKind.Settings
            : MainPageKind.MapList;
    }

    /// <summary>
    /// 7K 譜面のバックグラウンド解析を始める（DB 読み込み成功時のみ）
    /// </summary>
    private void StartAnalyze7k()
    {
        if (DatabaseLoadingViewModel.HasError || _analyze7kService is null)
            return;
        try
        {
            _analyze7kService.OnDatabaseLoaded();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Analyze7k start failed: {ex.Message}");
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
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(StartAnalyze7k);
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
