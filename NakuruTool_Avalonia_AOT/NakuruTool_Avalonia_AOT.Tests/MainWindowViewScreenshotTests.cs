using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Licenses;
using System.Collections.Generic;
using NakuruTool_Avalonia_AOT.Features.MainWindow;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using R3;
using System.ComponentModel;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// MainWindowViewのスクリーンショットテスト
/// </summary>
public class MainWindowViewScreenshotTests
{
    /// <summary>
    /// スクリーンショットの保存ディレクトリ
    /// </summary>
    private static string ScreenshotsDirectory => 
        Path.Combine(AppContext.BaseDirectory, "Screenshots");

    /// <summary>
    /// MainWindowViewでMapListViewPageを表示した状態のスクリーンショットを撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMainWindowView_WithMapList()
    {
        // モックViewModelを作成
        var mockSettingsViewModel = new MockSettingsViewModel();
        var mockDatabaseLoadingViewModel = new MockDatabaseLoadingViewModel();
        var mockMapListViewModel = new MockMapListViewModel();
        mockMapListViewModel.SetTestData(CreateTestBeatmaps());
        var mockMapListPageViewModel = new MockMapListPageViewModel();
        var mockLicensesViewModel = new MockLicensesViewModel();
        var mockSettingsService = new MockSettingsService();
        var mockDatabaseServiceA = new MockDatabaseService();
        var mockImportExportServiceA = new MockImportExportService();
        var mockImportExportPageViewModelA = new ImportExportPageViewModel(mockDatabaseServiceA, mockImportExportServiceA, mockSettingsService, new NullBeatmapDownloadService());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModelA,
            mockLicensesViewModel,
            mockSettingsService);

        // 読み込みオーバーレイを非表示にする
        mainWindowViewModel.IsLoadingOverlayVisible = false;

        // MainWindowViewを作成
        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };

        // ウィンドウを表示
        mainWindowView.Show();

        // UIスレッドのジョブを完了させ、レンダリングを完了
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // TabControlを取得してMapListタブを選択（インデックス1）
        var tabControl = mainWindowView.FindControl<TabControl>("MainTab");
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1; // MapListViewPageのタブ
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // MapListViewPageのDataGridを設宁E
        SetupMapListDataGrid(mainWindowView, mockMapListViewModel);

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_MapList.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        mainWindowView.Close();
    }

    /// <summary>
    /// MainWindowViewでサイドメニューを閉じた状態のスクリーンショットを撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMainWindowView_WithMapList_MenuCollapsed()
    {
        // モックViewModelを作成
        var mockSettingsViewModel = new MockSettingsViewModel();
        var mockDatabaseLoadingViewModel = new MockDatabaseLoadingViewModel();
        var mockMapListViewModel = new MockMapListViewModel();
        mockMapListViewModel.SetTestData(CreateTestBeatmaps());
        var mockMapListPageViewModel = new MockMapListPageViewModel();
        var mockLicensesViewModel = new MockLicensesViewModel();
        var mockSettingsService = new MockSettingsService();
        var mockDatabaseServiceB = new MockDatabaseService();
        var mockImportExportServiceB = new MockImportExportService();
        var mockImportExportPageViewModelB = new ImportExportPageViewModel(mockDatabaseServiceB, mockImportExportServiceB, mockSettingsService, new NullBeatmapDownloadService());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModelB,
            mockLicensesViewModel,
            mockSettingsService);

        // 読み込みオーバーレイを非表示にする
        mainWindowViewModel.IsLoadingOverlayVisible = false;

        // MainWindowViewを作成
        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };

        // ウィンドウを表示
        mainWindowView.Show();

        // UIスレッドのジョブを完了させ、レンダリングを完了
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // サイドメニューを閉じる（ExpandButtonをチェック状態にする）
        var expandButton = mainWindowView.FindControl<ToggleButton>("ExpandButton");
        if (expandButton != null)
        {
            expandButton.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // TabControlを取得してMapListタブを選抁E
        var tabControl = mainWindowView.FindControl<TabControl>("MainTab");
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // MapListViewPageのDataGridを設宁E
        SetupMapListDataGrid(mainWindowView, mockMapListViewModel);

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_MapList_MenuCollapsed.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        mainWindowView.Close();
    }

    /// <summary>
    /// MainWindowViewで読み込みオーバーレイを表示した状態のスクリーンショットを撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMainWindowView_LoadingOverlay()
    {
        // モックViewModelを作成
        var mockSettingsViewModel = new MockSettingsViewModel();
        var mockDatabaseLoadingViewModel = new MockDatabaseLoadingViewModel
        {
            IsLoading = true,
            CollectionDbProgress = 50,
            CollectionDbMessage = "collection.db を読み込み中...",
            OsuDbProgress = 30,
            OsuDbMessage = "osu!.db を読み込み中...",
            ScoresDbProgress = 10,
            ScoresDbMessage = "scores.db を読み込み中..."
        };
        var mockMapListViewModel = new MockMapListViewModel();
        var mockMapListPageViewModel = new MockMapListPageViewModel();
        var mockLicensesViewModel = new MockLicensesViewModel();
        var mockSettingsService = new MockSettingsService();
        var mockDatabaseServiceC = new MockDatabaseService();
        var mockImportExportServiceC = new MockImportExportService();
        var mockImportExportPageViewModelC = new ImportExportPageViewModel(mockDatabaseServiceC, mockImportExportServiceC, mockSettingsService, new NullBeatmapDownloadService());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModelC,
            mockLicensesViewModel,
            mockSettingsService);

        // 読み込みオーバーレイを表示する
        mainWindowViewModel.IsLoadingOverlayVisible = true;

        // MainWindowViewを作成
        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };

        // ウィンドウを表示
        mainWindowView.Show();

        // UIスレッドのジョブを完了させ、レンダリングを完了
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_LoadingOverlay.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        mainWindowView.Close();
    }

    /// <summary>
    /// 異なるウィンドウサイズでMainWindowViewのスクリーンショットを撮影
    /// </summary>
    [AvaloniaTheory]
    [InlineData(800, 600, "Small")]
    [InlineData(1280, 720, "HD")]
    [InlineData(1920, 1080, "FullHD")]
    public void CaptureMainWindowView_DifferentSizes(int width, int height, string sizeName)
    {
        // モックViewModelを作成
        var mockSettingsViewModel = new MockSettingsViewModel();
        var mockDatabaseLoadingViewModel = new MockDatabaseLoadingViewModel();
        var mockMapListViewModel = new MockMapListViewModel();
        mockMapListViewModel.SetTestData(CreateTestBeatmaps());
        var mockMapListPageViewModel = new MockMapListPageViewModel();
        var mockLicensesViewModel = new MockLicensesViewModel();
        var mockSettingsService = new MockSettingsService();
        var mockDatabaseService3 = new MockDatabaseService();
        var mockImportExportService3 = new MockImportExportService();
        var mockImportExportPageViewModel3 = new ImportExportPageViewModel(mockDatabaseService3, mockImportExportService3, mockSettingsService, new NullBeatmapDownloadService());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModel3,
            mockLicensesViewModel,
            mockSettingsService);

        mainWindowViewModel.IsLoadingOverlayVisible = false;

        // MainWindowViewを作成
        var mainWindowView = new MainWindowView
        {
            Width = width,
            Height = height,
            DataContext = mainWindowViewModel
        };

        // ウィンドウを表示
        mainWindowView.Show();

        // UIスレッドのジョブを完了させ、レンダリングを完了
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // MapListタブを選択
        var tabControl = mainWindowView.FindControl<TabControl>("MainTab");
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // MapListViewPageのDataGridを設定
        SetupMapListDataGrid(mainWindowView, mockMapListViewModel);

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, $"MainWindowView_{sizeName}_{width}x{height}.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        mainWindowView.Close();
    }

    /// <summary>
    /// MapListViewのDataGridにデータを設定するヘルパーメソッド
    /// </summary>
    private static void SetupMapListDataGrid(Window window, MockMapListViewModel mockViewModel)
    {
        // MapListViewを探す
        var mapListView = window.FindDescendantOfType<MapListView>();
        if (mapListView != null)
        {
            var dataGrid = mapListView.FindControl<DataGrid>("MapListDataGrid");
            if (dataGrid != null && dataGrid.ItemsSource == null)
            {
                dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }
    }

    /// <summary>
    /// テスト用のBeatmapデータを作成
    /// </summary>
    private static List<Beatmap> CreateTestBeatmaps()
    {
        return
        [
            new Beatmap
            {
                MD5Hash = "test1",
                KeyCount = 7,
                Status = BeatmapStatus.Ranked,
                Title = "テスト曲1 - Test Song 1",
                Artist = "テストアーティスト",
                Version = "Hard",
                Creator = "TestMapper1",
                BPM = 180.0,
                Difficulty = 4.5,
                LongNoteRate = 0.3,
                IsPlayed = true,
                LastPlayed = DateTime.Now.AddDays(-1),
                LastModifiedTime = DateTime.Now.AddMonths(-1),
                FolderName = "test_folder_1",
                AudioFilename = "audio.mp3",
                OsuFileName = "test.osu",
                BeatmapSetId = 1001,
                BeatmapId = 10001,
                BestScore = 985000,
                BestAccuracy = 98.5,
                PlayCount = 15,
                Grade = "S"
            },
            new Beatmap
            {
                MD5Hash = "test2",
                KeyCount = 7,
                Status = BeatmapStatus.Ranked,
                Title = "Another Test Song",
                Artist = "Another Artist",
                Version = "Insane",
                Creator = "TestMapper2",
                BPM = 200.0,
                Difficulty = 5.8,
                LongNoteRate = 0.5,
                IsPlayed = true,
                LastPlayed = DateTime.Now.AddHours(-5),
                LastModifiedTime = DateTime.Now.AddDays(-14),
                FolderName = "test_folder_2",
                AudioFilename = "audio.mp3",
                OsuFileName = "test.osu",
                BeatmapSetId = 1002,
                BeatmapId = 10002,
                BestScore = 970000,
                BestAccuracy = 97.0,
                PlayCount = 8,
                Grade = "A"
            },
            new Beatmap
            {
                MD5Hash = "test3",
                KeyCount = 4,
                Status = BeatmapStatus.Loved,
                Title = "4K Map Example",
                Artist = "4K Artist",
                Version = "Expert",
                Creator = "4KMapper",
                BPM = 160.0,
                Difficulty = 6.2,
                LongNoteRate = 0.7,
                IsPlayed = false,
                LastPlayed = null,
                LastModifiedTime = DateTime.Now.AddDays(-10),
                FolderName = "test_folder_3",
                AudioFilename = "audio.mp3",
                OsuFileName = "test.osu",
                BeatmapSetId = 1003,
                BeatmapId = 10003,
                BestScore = 0,
                BestAccuracy = 0,
                PlayCount = 0,
                Grade = ""
            },
            new Beatmap
            {
                MD5Hash = "test4",
                KeyCount = 7,
                Status = BeatmapStatus.Pending,
                Title = "Unranked Test",
                Artist = "Pending Artist",
                Version = "Normal",
                Creator = "NewMapper",
                BPM = 140.0,
                Difficulty = 3.2,
                LongNoteRate = 0.2,
                IsPlayed = true,
                LastPlayed = DateTime.Now.AddMinutes(-30),
                LastModifiedTime = DateTime.Now,
                FolderName = "test_folder_4",
                AudioFilename = "audio.mp3",
                OsuFileName = "test.osu",
                BeatmapSetId = 1004,
                BeatmapId = 10004,
                BestScore = 920000,
                BestAccuracy = 92.0,
                PlayCount = 3,
                Grade = "B"
            },
            new Beatmap
            {
                MD5Hash = "test5",
                KeyCount = 7,
                Status = BeatmapStatus.Ranked,
                Title = "Long Title Example - This is a very long title",
                Artist = "Long Artist Name Example",
                Version = "Another Very Long Difficulty Name",
                Creator = "LongMapperName",
                BPM = 220.0,
                Difficulty = 7.5,
                LongNoteRate = 0.8,
                IsPlayed = true,
                LastPlayed = DateTime.Now,
                LastModifiedTime = DateTime.Now.AddHours(-2),
                FolderName = "test_folder_5",
                AudioFilename = "audio.mp3",
                OsuFileName = "test.osu",
                BeatmapSetId = 1005,
                BeatmapId = 10005,
                BestScore = 999000,
                BestAccuracy = 99.9,
                PlayCount = 100,
                Grade = "SS"
            }
        ];
    }
}

/// <summary>
/// テスト用のモックSettingsViewModel
/// </summary>
public class MockSettingsViewModel : ISettingsViewModel
{
    public IAvaloniaReadOnlyList<string> LanguageKeys { get; } = new AvaloniaList<string>(["ja-JP", "en-US"]);
    public string SelectedLanguageKey { get; set; } = "ja-JP";
    public string SelectedFolderPath { get; set; } = @"C:\osu!";
    public string OsuPathErrorMessage { get; set; } = string.Empty;
    public bool HasOsuPathError { get; set; } = false;
    public bool AutoPlayOnSelect { get; set; } = true;
    public bool PreferUnicode { get; set; } = false;
    public IAvaloniaReadOnlyList<string> MirrorUrls { get; } = new AvaloniaList<string>(["https://catboy.best/d/", "https://api.nerinyan.moe/d/"]);
    public string SelectedMirrorUrl { get; set; } = "https://catboy.best/d/";
    public string AppVersion { get; } = "1.0.0";

    public void Dispose() { }
}

/// <summary>
/// テスト用のモックDatabaseLoadingViewModel
/// </summary>
public class MockDatabaseLoadingViewModel : IDatabaseLoadingViewModel
{
    public bool IsLoading { get; set; } = false;
    public bool HasError { get; set; } = false;
    public string ErrorMessage { get; set; } = string.Empty;
    public int CollectionDbProgress { get; set; } = 0;
    public string CollectionDbMessage { get; set; } = string.Empty;
    public int OsuDbProgress { get; set; } = 0;
    public string OsuDbMessage { get; set; } = string.Empty;
    public int ScoresDbProgress { get; set; } = 0;
    public string ScoresDbMessage { get; set; } = string.Empty;

    public Task InitialLoadAsync() => Task.CompletedTask;

    public void Dispose() { }
}

/// <summary>
/// テスト用のモックFilterPresetService
/// </summary>
public class MockFilterPresetService : IFilterPresetService
{
    public AvaloniaList<FilterPreset> Presets { get; } = new();

    public bool SavePreset(FilterPreset preset)
    {
        return true;
    }

    public void LoadPresets()
    {
    }

    public bool DeletePreset(string presetName)
    {
        return true;
    }

    public bool RenamePreset(string oldName, FilterPreset updatedPreset) => true;
}

/// <summary>
/// テスト用のモックMapListPageViewModel
/// </summary>
public class MockMapListPageViewModel : MapListPageViewModel
{
    private static AudioPlayerPanelViewModel CreatePanelViewModel()
    {
        var service = new MockAudioPlayerService();
        var settingsService = new MockSettingsService();
        var audioVm = new MockAudioPlayerViewModel();
        return new AudioPlayerPanelViewModel(service, audioVm, settingsService);
    }

    public MockMapListPageViewModel() : base(
        new MockDatabaseService(),
        new MockGenerateCollectionService(),
        new MockFilterPresetService(),
        new MockAudioPlayerViewModel(),
        CreatePanelViewModel(),
        new MockSettingsService())
    {
    }
}
        
/// <summary>
/// テスト用のモックMapListViewModel
/// </summary>
public class MockMapListViewModel : IMapListViewModel
{
    private readonly AvaloniaList<Beatmap> _showBeatmapsList = [];
    private List<Beatmap> _allBeatmaps = [];
    private int _pageSize = 20;
    private int _currentPage = 1;

    public IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps => _showBeatmapsList;
    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };
    public int TotalCount { get; private set; }
    public int CurrentPage 
    { 
        get => _currentPage;
        set
        {
            _currentPage = value;
            UpdateShowBeatmaps();
        }
    }
    public int FilteredPages { get; private set; } = 1;
    public int FilteredCount { get; private set; }
    public int PageSize
    {
        get => _pageSize;
        set
        {
            _pageSize = value;
            UpdateFilteredPages();
            UpdateShowBeatmaps();
        }
    }
    public Beatmap[] FilteredBeatmapsArray { get; private set; } = Array.Empty<Beatmap>();
    public Beatmap? SelectedBeatmap { get; set; }
    public ModCategory SelectedModCategory { get; set; } = ModCategory.NoMod;
    public ScoreSystemCategory SelectedScoreSystemCategory { get; set; } = ScoreSystemCategory.Default;

    public void Initialize()
    {
        // モックでは何もしない
    }

    public void ApplyFilter()
    {
        // モックでは何もしない
    }

    public void SetTestData(List<Beatmap> beatmaps)
    {
        _allBeatmaps = beatmaps;
        TotalCount = beatmaps.Count;
        FilteredCount = beatmaps.Count;
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    private void UpdateFilteredPages()
    {
        var size = Math.Max(1, _pageSize);
        FilteredPages = Math.Max(1, (FilteredCount + size - 1) / size);
    }

    private void UpdateShowBeatmaps()
    {
        _showBeatmapsList.Clear();
        var size = Math.Max(1, _pageSize);
        var skip = (_currentPage - 1) * size;
        var pageBeatmaps = _allBeatmaps.Skip(skip).Take(size);
        foreach (var beatmap in pageBeatmaps)
        {
            _showBeatmapsList.Add(beatmap);
        }
    }

    public void Dispose()
    {
        // モックでは何もしない
    }
}

/// <summary>
/// テスト用のモックImportExportService
/// </summary>
public class MockImportExportService : IImportExportService
{
    public Observable<ImportExportProgress> ProgressObservable { get; } = Observable.Empty<ImportExportProgress>();
    public List<ImportFileItem> GetImportFiles() => [];
    public Task<int> ExportAsync(IReadOnlyList<string> collectionNames) => Task.FromResult(0);
    public Task<bool> ImportAsync(IReadOnlyList<string> filePaths) => Task.FromResult(true);
    public void Dispose() { }
}

/// <summary>
/// テスト用のモックDatabaseService
/// </summary>
public class MockDatabaseService : IDatabaseService
{
    public Beatmap[] Beatmaps { get; } = Array.Empty<Beatmap>();
    public List<OsuCollection> OsuCollections { get; } = new List<OsuCollection>();
    public Observable<DatabaseLoadProgress> CollectionDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
    public Observable<DatabaseLoadProgress> OsuDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
    public Observable<DatabaseLoadProgress> ScoresDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
    public Task LoadDatabasesAsync() => Task.CompletedTask;
    public Task ReloadDatabasesAsync() => Task.CompletedTask;
    public Task ReloadCollectionDbAsync() => Task.CompletedTask;
    public bool TryGetBeatmapByMd5(string md5Hash, out Beatmap? beatmap) { beatmap = null; return false; }
    public void Dispose() { }
}

/// <summary>
/// テスト用のモックGenerateCollectionService
/// </summary>
public class MockGenerateCollectionService : IGenerateCollectionService
{
    public Observable<GenerationProgress> GenerationProgressObservable { get; } = Observable.Empty<GenerationProgress>();
    public Task<bool> GenerateCollection(string collectionName, Beatmap[] beatmaps) => Task.FromResult(true);
    public void Dispose() { }
}

/// <summary>
/// テスト用のモックLicensesViewModel
/// </summary>
public class MockLicensesViewModel : ILicensesViewModel
{
    public IAvaloniaReadOnlyList<LicenseItem> Licenses { get; } = new AvaloniaList<LicenseItem>();

    public void Dispose() { }
}

/// <summary>
/// テスト用のモックSettingsService
/// </summary>
public class MockSettingsService : ISettingsService
{
    public ISettingsData SettingsData { get; } = new MockSettingsData();

    public bool SaveSettings(SettingsData settings) => true;

    public bool CheckSettingsPath() => true;

    public string GetSettingsPath() => "mock/settings.json";

    public void Dispose() { }
}

/// <summary>
/// テスト用のモックSettingsData
/// </summary>
public class MockSettingsData : ObservableObject, ISettingsData
{
    public string OsuFolderPath { get; set; } = @"C:\osu!";
    public string LanguageKey { get; set; } = "ja-JP";
    public int AudioVolume { get; set; } = 10;
    public bool AutoPlayOnSelect { get; set; } = true;
    public bool PreferUnicode { get; set; } = false;
    public bool IsDarkTheme { get; set; } = true;
    public string BeatmapMirrorUrl { get; set; } = "https://catboy.best/d/";
}

/// <summary>
/// テスト用のモックAudioPlayerViewModel
/// </summary>
public class MockAudioPlayerViewModel : AudioPlayerViewModel
{
    public MockAudioPlayerViewModel() : base(new MockAudioPlayerService(), new MockSettingsService())
    {
    }
}

/// <summary>
/// テスト用のモックAudioPlayerService
/// </summary>
public class MockAudioPlayerService : IAudioPlayerService
{
    private readonly ReactiveProperty<AudioPlayerState> _stateChanged = new(AudioPlayerState.Stopped);
    public Observable<AudioPlayerState> StateChanged => _stateChanged;
    public AudioPlayerState CurrentState => _stateChanged.CurrentValue;
    public int Volume { get; set; } = 50;
    public void Play(string filePath) { _stateChanged.Value = AudioPlayerState.Playing; }
    public void Pause() { _stateChanged.Value = AudioPlayerState.Paused; }
    public void Resume() { _stateChanged.Value = AudioPlayerState.Playing; }
    public void Stop() { _stateChanged.Value = AudioPlayerState.Stopped; }
    public void TogglePlayPause()
    {
        if (CurrentState == AudioPlayerState.Playing)
            Pause();
        else
            Resume();
    }
    public Observable<Unit> PlaybackCompleted { get; } = Observable.Empty<Unit>();
    public double GetPosition() => 0;
    public double GetDuration() => 0;
    public void Seek(double positionSeconds) { }
    public void Dispose() { _stateChanged.Dispose(); }
}

/// <summary>
/// テスト用の何もしない IBeatmapDownloadService 実装
/// </summary>
public class NullBeatmapDownloadService : IBeatmapDownloadService
{
    public void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems) { }
    public Task CancelAllAsync() => Task.CompletedTask;
    public void Dispose() { }
}

/// <summary>
/// ビジュアルツリーの子孫を検索する拡張メソッド
/// </summary>
public static class VisualTreeExtensions
{
    public static T? FindDescendantOfType<T>(this Visual visual) where T : Visual
    {
        // Avalonia.VisualTreeのGetVisualDescendants拡張メソッドを使用
        foreach (var descendant in visual.GetVisualDescendants())
        {
            if (descendant is T result)
                return result;
        }
        return null;
    }
}
