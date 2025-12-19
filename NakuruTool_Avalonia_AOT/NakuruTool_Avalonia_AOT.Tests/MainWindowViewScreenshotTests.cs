using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.MainWindow;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// MainWindowViewのスクリーンショットテスト
/// </summary>
public class MainWindowViewScreenshotTests
{
    /// <summary>
    /// スクリーンショット保存先ディレクトリ
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

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel);

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

        // UIスレッドのジョブを完了させ、レンダリングを待機
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

        // MapListViewPageのDataGridを設定
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

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel);

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

        // UIスレッドのジョブを完了させ、レンダリングを待機
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

        // TabControlを取得してMapListタブを選択
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

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel);

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

        // UIスレッドのジョブを完了させ、レンダリングを待機
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

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel);

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

        // UIスレッドのジョブを完了させ、レンダリングを待機
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
    /// MapListViewPageのDataGridにデータを設定するヘルパーメソッド
    /// </summary>
    private static void SetupMapListDataGrid(Window window, MockMapListViewModel mockViewModel)
    {
        // MapListViewPageを探す
        var mapListPage = window.FindDescendantOfType<MapListViewPage>();
        if (mapListPage != null)
        {
            var dataGrid = mapListPage.FindControl<DataGrid>("MapListDataGrid");
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
                Artist = "テストアーティスト1",
                Version = "Hard",
                Creator = "TestMapper1",
                BPM = 180.0,
                Difficulty = 4.5,
                LongNoteRate = 0.3,
                IsPlayed = true,
                LastPlayed = DateTime.Now.AddDays(-1),
                LastModifiedTime = DateTime.Now.AddMonths(-1),
                FolderName = "test_folder_1",
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
