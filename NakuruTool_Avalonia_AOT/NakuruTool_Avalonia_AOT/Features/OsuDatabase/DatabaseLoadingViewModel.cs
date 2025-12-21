using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

public interface IDatabaseLoadingViewModel: IDisposable
{
    bool IsLoading { get; }
    bool HasError { get; }
    string ErrorMessage { get; }
    int CollectionDbProgress { get; }
    string CollectionDbMessage { get; }
    int OsuDbProgress { get; }
    string OsuDbMessage { get; }
    int ScoresDbProgress { get; }
    string ScoresDbMessage { get; }
    Task InitialLoadAsync();
}

/// <summary>
/// データベース読み込み用のViewModel
/// </summary>
public partial class DatabaseLoadingViewModel : ViewModelBase, IDatabaseLoadingViewModel
{
    private readonly IDatabaseService _databaseService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;
    
    [ObservableProperty]
    public partial bool HasError { get; set; } = false;
    
    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    // 各DBファイル個別の進捗管理
    [ObservableProperty]
    public partial int CollectionDbProgress { get; set; } = 0;
    
    [ObservableProperty]
    public partial string CollectionDbMessage { get; set; } = "";
    
    [ObservableProperty]
    public partial int OsuDbProgress { get; set; } = 0;
    
    [ObservableProperty]
    public partial string OsuDbMessage { get; set; } = "";
    
    [ObservableProperty]
    public partial int ScoresDbProgress { get; set; } = 0;
    
    [ObservableProperty]
    public partial string ScoresDbMessage { get; set; } = "";

    public DatabaseLoadingViewModel(IDatabaseService databaseService, ISettingsService settingsService)
    {
        _databaseService = databaseService;
        _settingsService = settingsService;

        // R3のObservableを使った進捗監視
        _databaseService.CollectionDbProgress
            .Subscribe(progress =>
            {
                CollectionDbMessage = progress.Message;
                CollectionDbProgress = progress.Progress;
            })
            .AddTo(Disposables);

        _databaseService.OsuDbProgress
            .Subscribe(progress =>
            {
                OsuDbMessage = progress.Message;
                OsuDbProgress = progress.Progress;
            })
            .AddTo(Disposables);

        _databaseService.ScoresDbProgress
            .Subscribe(progress =>
            {
                ScoresDbMessage = progress.Message;
                ScoresDbProgress = progress.Progress;
            })
            .AddTo(Disposables);
    }

    /// <summary>
    /// データベースを読み込む
    /// </summary>
    private async Task LoadDatabasesAsync()
    {
        if (IsLoading) 
            return;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            // osu!フォルダパスの確認
            var settings = _settingsService.SettingsData;
            var osuPath = settings.OsuFolderPath;
            if (string.IsNullOrWhiteSpace(osuPath))
            {
                throw new InvalidOperationException(LangServiceInstance.GetString("Loading.OsuPathNotSet"));
            }

            // データベースを読み込む
            await _databaseService.LoadDatabasesAsync();

            // 統計情報を表示
            var collectionCount = _databaseService.OsuCollections?.Count ?? 0;
            var beatmapCount = _databaseService.Beatmaps.Length;
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = string.Format(LangServiceInstance.GetString("Loading.ErrorMessage"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 初回の自動読み込み
    /// </summary>
    public async Task InitialLoadAsync()
    {
        // データベースを読み込む
        await LoadDatabasesAsync();
    }
    public override void Dispose()
    {
        base.Dispose();
    }
}