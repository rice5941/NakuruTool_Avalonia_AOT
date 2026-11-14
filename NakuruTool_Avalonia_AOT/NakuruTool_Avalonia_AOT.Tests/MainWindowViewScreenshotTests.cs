using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using NakuruTool_Avalonia_AOT.Features.Licenses;
using System.Collections.Generic;
using NakuruTool_Avalonia_AOT.Features.MainWindow;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
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
        var mockBeatmapGenerationPageViewModelA = new BeatmapGenerationPageViewModel(mockDatabaseServiceA, new MockBeatmapRateGenerator(), mockSettingsService, new MockRateGenerationCollectionJsonWriter());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModelA,
            mockBeatmapGenerationPageViewModelA,
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

        // UIスレッドのジョブを完了させ、レンダリングを完了（NavigationPage への初回 Push を含む）
        PumpUi();

        // サイドバーで MapList ページを選択（NavigationPage の遷移は非同期なので UI ジョブを回す）
        mainWindowViewModel.SelectedPage = MainPageKind.MapList;
        PumpUi();

        // MapListViewPageのDataGridを設宁E
        SetupMapListDataGrid(mainWindowView, mockMapListViewModel);

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_MapList.png");
        frame?.Save(filePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);

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
        var mockBeatmapGenerationPageViewModelB = new BeatmapGenerationPageViewModel(mockDatabaseServiceB, new MockBeatmapRateGenerator(), mockSettingsService, new MockRateGenerationCollectionJsonWriter());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModelB,
            mockBeatmapGenerationPageViewModelB,
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

        // UIスレッドのジョブを完了させ、レンダリングを完了（NavigationPage への初回 Push を含む）
        PumpUi();

        // サイドメニュー(DrawerPage)を閉じる（ExpandButtonをチェック状態にする）
        var expandButton = mainWindowView.FindControl<ToggleButton>("ExpandButton");
        if (expandButton != null)
        {
            expandButton.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // サイドバーで MapList ページを選択（NavigationPage の遷移は非同期なので UI ジョブを回す）
        mainWindowViewModel.SelectedPage = MainPageKind.MapList;
        PumpUi();

        // MapListViewPageのDataGridを設宁E
        SetupMapListDataGrid(mainWindowView, mockMapListViewModel);

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_MapList_MenuCollapsed.png");
        frame?.Save(filePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);

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
        var mockBeatmapGenerationPageViewModelC = new BeatmapGenerationPageViewModel(mockDatabaseServiceC, new MockBeatmapRateGenerator(), mockSettingsService, new MockRateGenerationCollectionJsonWriter());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModelC,
            mockBeatmapGenerationPageViewModelC,
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

        // UIスレッドのジョブを完了させ、レンダリングを完了（NavigationPage への初回 Push を含む）
        PumpUi();

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_LoadingOverlay.png");
        frame?.Save(filePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);

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
        var mockBeatmapGenerationPageViewModel3 = new BeatmapGenerationPageViewModel(mockDatabaseService3, new MockBeatmapRateGenerator(), mockSettingsService, new MockRateGenerationCollectionJsonWriter());

        // MainWindowViewModelを作成
        var mainWindowViewModel = new MainWindowViewModel(
            mockSettingsViewModel,
            mockDatabaseLoadingViewModel,
            mockMapListViewModel,
            mockMapListPageViewModel,
            mockImportExportPageViewModel3,
            mockBeatmapGenerationPageViewModel3,
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

        // UIスレッドのジョブを完了させ、レンダリングを完了（NavigationPage への初回 Push を含む）
        PumpUi();

        // サイドバーで MapList ページを選択（NavigationPage の遷移は非同期なので UI ジョブを回す）
        mainWindowViewModel.SelectedPage = MainPageKind.MapList;
        PumpUi();

        // MapListViewPageのDataGridを設定
        SetupMapListDataGrid(mainWindowView, mockMapListViewModel);

        // スクリーンショットを撮影
        var frame = mainWindowView.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, $"MainWindowView_{sizeName}_{width}x{height}.png");
        frame?.Save(filePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        mainWindowView.Close();
    }

    /// <summary>
    /// サイドバー(DrawerPage)からのページ切り替えで、ページインスタンスが再利用されメニュー選択と同期することを確認
    /// </summary>
    [AvaloniaFact]
    public void SidebarNavigation_ReusesPagesAndSyncsMenuSelection()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        mainWindowViewModel.IsLoadingOverlayVisible = false;

        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };
        mainWindowView.Show();
        PumpUi();

        var nav = mainWindowView.FindControl<NavigationPage>("MainNav")!;
        var mainMenu = mainWindowView.FindControl<ListBox>("MainMenu")!;
        var systemsMenu = mainWindowView.FindControl<ListBox>("SystemsMenu")!;

        // 初期表示は MapList
        var mapListPage = Assert.IsType<MapListPageView>(mainWindowView.CurrentContentPage);
        Assert.Equal(MainPageKind.MapList, Assert.IsType<ListBoxItem>(mainMenu.SelectedItem).Tag);
        Assert.Null(systemsMenu.SelectedItem);

        // 開いていないページの項目は旧 TabControl と同じ淡いテキスト色、開いているページは通常のテキスト色
        Assert.Equal(GetResourceColor(mainWindowView, "TabItemLineHeaderForeground"),
            GetMenuItemForegroundColor((ListBoxItem)systemsMenu.Items[0]!));
        Assert.Equal(GetResourceColor(mainWindowView, "TabItemLineHeaderSelectedForeground"),
            GetMenuItemForegroundColor((ListBoxItem)mainMenu.SelectedItem!));

        // サイドバー(システム)の「設定」をクリック相当で選択
        systemsMenu.SelectedIndex = 0;
        PumpUi();
        Assert.Equal(MainPageKind.Settings, mainWindowViewModel.SelectedPage);
        Assert.IsType<SettingsPage>(mainWindowView.CurrentContentPage);
        Assert.Null(mainMenu.SelectedItem);
        Assert.Equal(1, nav.StackDepth);

        // 非表示になった MapList ページは Visual Tree に接続されたまま（DataGrid の作り直しを起こさない）
        Assert.False(mapListPage.IsVisible);
        Assert.NotNull(mapListPage.GetVisualParent());

        // ViewModel 側からの切り替え（DB 読み込み成功時の MapList 遷移など）でも同じページインスタンスに戻る
        mainWindowViewModel.SelectedPage = MainPageKind.MapList;
        PumpUi();
        Assert.Same(mapListPage, mainWindowView.CurrentContentPage);
        Assert.Equal(MainPageKind.MapList, Assert.IsType<ListBoxItem>(mainMenu.SelectedItem).Tag);
        Assert.Null(systemsMenu.SelectedItem);
        Assert.Equal(1, nav.StackDepth);

        // 再表示後も MapListView のバインド（右クリックメニューのコマンド）が復元されている
        var mapListView = mainWindowView.FindDescendantOfType<MapListView>()!;
        var generateMenuItem = mapListView.FindControl<MenuItem>("GenerateMenuItem");
        Assert.Same(mainWindowViewModel.MapListPageViewModel.ListViewModel.GenerateBeatmapCommand, generateMenuItem?.Command);

        // メニュー開閉トグルで DrawerPage が閉じる
        var drawer = mainWindowView.FindControl<DrawerPage>("MainDrawer")!;
        mainWindowView.FindControl<ToggleButton>("ExpandButton")!.IsChecked = true;
        PumpUi();
        Assert.False(drawer.IsOpen);

        mainWindowView.Close();
    }

    /// <summary>
    /// 設定ページを表示した状態のスクリーンショットを撮影（サイズ未指定だったアイコンの確認用）
    /// </summary>
    [AvaloniaFact]
    public void CaptureMainWindowView_Settings()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        mainWindowViewModel.IsLoadingOverlayVisible = false;

        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };
        mainWindowView.Show();
        PumpUi();

        mainWindowViewModel.SelectedPage = MainPageKind.Settings;
        PumpUi();

        var frame = mainWindowView.CaptureRenderedFrame();
        Directory.CreateDirectory(ScreenshotsDirectory);
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_Settings.png");
        frame?.Save(filePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        mainWindowView.Close();
    }

    /// <summary>
    /// 音楽プレイヤーモード: オーディオパネルのみ表示し、高さをパネルに合わせて固定する。
    /// 解除すると元のレイアウト・高さに戻ることを確認する。
    /// </summary>
    [AvaloniaFact]
    public void MusicPlayerMode_ShowsOnlyAudioPanelAndRestoresWindow()
    {
        var mainWindowViewModel = CreateMainWindowViewModel();
        mainWindowViewModel.IsLoadingOverlayVisible = false;

        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };
        mainWindowView.Show();
        PumpUi();

        var drawer = mainWindowView.FindControl<DrawerPage>("MainDrawer")!;
        var playerHost = mainWindowView.FindControl<Border>("MusicPlayerHost")!;
        Assert.True(drawer.IsEffectivelyVisible);
        Assert.False(playerHost.IsVisible);

        // パネルの切替コマンドでプレイヤーモードへ
        mainWindowViewModel.AudioPlayerPanelViewModel.ToggleMusicPlayerModeCommand.Execute(null);
        PumpUi();

        Assert.True(mainWindowViewModel.AudioPlayerPanelViewModel.IsMusicPlayerMode);
        Assert.False(drawer.IsEffectivelyVisible);
        Assert.True(playerHost.IsVisible);
        Assert.Same(mainWindowViewModel.AudioPlayerPanelViewModel,
            playerHost.FindDescendantOfType<AudioPlayerPanelView>()!.DataContext);
        Assert.Equal(MainWindowView.MusicPlayerModeWidth, mainWindowView.ClientSize.Width);
        Assert.True(mainWindowView.ClientSize.Height < 720, $"Height was not reduced: {mainWindowView.ClientSize.Height}");
        Assert.Equal(mainWindowView.ClientSize.Height, mainWindowView.MinHeight);
        Assert.Equal(mainWindowView.ClientSize.Height, mainWindowView.MaxHeight);

        // プレイヤーモード中は最大化できない
        Assert.False(mainWindowView.CanMaximize);
        mainWindowView.WindowState = WindowState.Maximized;
        PumpUi();
        Assert.Equal(WindowState.Normal, mainWindowView.WindowState);

        var frame = mainWindowView.CaptureRenderedFrame();
        Directory.CreateDirectory(ScreenshotsDirectory);
        var filePath = Path.Combine(ScreenshotsDirectory, "MainWindowView_MusicPlayerMode.png");
        frame?.Save(filePath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        // プレイヤーモード中に幅を変更しても、解除時は通常モードのサイズ (1280x720) に戻る
        mainWindowView.Width = 900;
        PumpUi();
        Assert.Equal(900, mainWindowView.ClientSize.Width);

        mainWindowViewModel.AudioPlayerPanelViewModel.ToggleMusicPlayerModeCommand.Execute(null);
        PumpUi();

        Assert.True(drawer.IsEffectivelyVisible);
        Assert.False(playerHost.IsVisible);
        Assert.Equal(SizeToContent.Manual, mainWindowView.SizeToContent);
        Assert.Equal(1280, mainWindowView.ClientSize.Width);
        Assert.Equal(720, mainWindowView.ClientSize.Height);
        Assert.Equal(500, mainWindowView.MinHeight);
        Assert.True(mainWindowView.CanMaximize);

        // 通常モードでリサイズした後に再度往復した場合は、その新しいサイズに戻る
        mainWindowView.Width = 1100;
        mainWindowView.Height = 650;
        PumpUi();
        mainWindowViewModel.AudioPlayerPanelViewModel.ToggleMusicPlayerModeCommand.Execute(null);
        PumpUi();
        mainWindowView.Width = 900;
        PumpUi();
        mainWindowViewModel.AudioPlayerPanelViewModel.ToggleMusicPlayerModeCommand.Execute(null);
        PumpUi();
        Assert.Equal(1100, mainWindowView.ClientSize.Width);
        Assert.Equal(650, mainWindowView.ClientSize.Height);

        mainWindowView.Close();
    }

    /// <summary>
    /// beatmap一覧 → 他ページ → beatmap一覧 と遷移しても、選択中の譜面が再選択されない
    /// （再選択されると AudioPlayerPanel.PlayBeatmap が呼ばれ、再生中の曲が先頭から始まってしまう）ことを確認
    /// </summary>
    [AvaloniaFact]
    public void SidebarNavigation_ReturningToMapList_DoesNotReselectPlayingBeatmap()
    {
        var beatmaps = CreateTestBeatmaps().ToArray();
        var mapListPageViewModel = new MockMapListPageViewModel(new MockDatabaseService(beatmaps));
        mapListPageViewModel.Initialize();

        var mockSettingsService = new MockSettingsService();
        var mockDatabaseService = new MockDatabaseService();
        var mainWindowViewModel = new MainWindowViewModel(
            new MockSettingsViewModel(),
            new MockDatabaseLoadingViewModel(),
            mapListPageViewModel.ListViewModel,
            mapListPageViewModel,
            new ImportExportPageViewModel(mockDatabaseService, new MockImportExportService(), mockSettingsService, new NullBeatmapDownloadService()),
            new BeatmapGenerationPageViewModel(mockDatabaseService, new MockBeatmapRateGenerator(), mockSettingsService, new MockRateGenerationCollectionJsonWriter()),
            new MockLicensesViewModel(),
            mockSettingsService)
        {
            IsLoadingOverlayVisible = false
        };

        var mainWindowView = new MainWindowView
        {
            Width = 1280,
            Height = 720,
            DataContext = mainWindowViewModel
        };
        mainWindowView.Show();
        PumpUi();

        // 一覧で譜面を選択（= 再生開始）
        var listViewModel = mapListPageViewModel.ListViewModel;
        var selected = listViewModel.ShowBeatmaps[1];
        listViewModel.SelectedBeatmap = selected;
        PumpUi();

        var selectionChanges = new List<Beatmap?>();
        listViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(listViewModel.SelectedBeatmap))
            {
                selectionChanges.Add(listViewModel.SelectedBeatmap);
            }
        };

        // 他ページへ移動して戻る
        mainWindowViewModel.SelectedPage = MainPageKind.Settings;
        PumpUi();
        mainWindowViewModel.SelectedPage = MainPageKind.MapList;
        PumpUi();

        Assert.Same(selected, listViewModel.SelectedBeatmap);
        Assert.Empty(selectionChanges);

        mainWindowView.Close();
    }

    private static Avalonia.Media.Color GetMenuItemForegroundColor(ListBoxItem item)
    {
        var presenter = item.GetVisualDescendants()
            .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
            .First(p => p.Name == "PART_ContentPresenter");
        return Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(presenter.Foreground).Color;
    }

    private static Avalonia.Media.Color GetResourceColor(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var resource), $"Resource not found: {key}");
        return Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(resource).Color;
    }

    private static MainWindowViewModel CreateMainWindowViewModel()
    {
        var mockMapListViewModel = new MockMapListViewModel();
        mockMapListViewModel.SetTestData(CreateTestBeatmaps());
        var mockSettingsService = new MockSettingsService();
        var mockDatabaseService = new MockDatabaseService();

        return new MainWindowViewModel(
            new MockSettingsViewModel(),
            new MockDatabaseLoadingViewModel(),
            mockMapListViewModel,
            new MockMapListPageViewModel(),
            new ImportExportPageViewModel(mockDatabaseService, new MockImportExportService(), mockSettingsService, new NullBeatmapDownloadService()),
            new BeatmapGenerationPageViewModel(mockDatabaseService, new MockBeatmapRateGenerator(), mockSettingsService, new MockRateGenerationCollectionJsonWriter()),
            new MockLicensesViewModel(),
            mockSettingsService);
    }

    /// <summary>
    /// UI スレッドのジョブとレンダリングを数回進める。
    /// NavigationPage の Push/Replace は非同期で完了するため、1 回では反映されないことがある。
    /// </summary>
    private static void PumpUi()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
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
    public IAvaloniaReadOnlyList<string> MirrorUrls { get; } = new AvaloniaList<string>(["https://mirror.hinamizawa.ai/api/v1/hinai/d/", "https://api.nerinyan.moe/d/"]);
    public string SelectedMirrorUrl { get; set; } = "https://mirror.hinamizawa.ai/api/v1/hinai/d/";
    public bool AutoBatchGenerateOnStartup { get; set; } = false;
    public bool Analyze7kEnabled { get; set; } = true;
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

    public MockMapListPageViewModel() : this(new MockDatabaseService())
    {
    }

    public MockMapListPageViewModel(IDatabaseService databaseService) : base(
        databaseService,
        new MockGenerateCollectionService(),
        new MockFilterPresetService(),
        new MockAudioPlayerViewModel(),
        CreatePanelViewModel(),
        new MockSettingsService(),
        new MockBeatmapRateGenerator())
    {
    }
}
        
/// <summary>
/// テスト用のモックMapListViewModel
/// </summary>
public class MockMapListViewModel : ObservableObject, IMapListViewModel
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
    /// <summary>共通 BeatmapList View 向け alias。<see cref="FilteredPages"/> と同値。</summary>
    public int PageCount => FilteredPages;
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
    public bool IsLocalRankingMode { get; set; }
    public bool IsAnalysisMode { get; set; }
    public IAvaloniaReadOnlyList<LocalRankingEntry> LocalRanking { get; } = new AvaloniaList<LocalRankingEntry>();
    public bool HasLocalRanking => false;
    public bool ShowGenreSkillValues { get; set; }
    public Analyze7kProgressDisplay? Analyze7kStatus { get; set; }

    public IRelayCommand PreviousPageCommand { get; }
    public IRelayCommand NextPageCommand { get; }

    // ---- ContextMenu 共通契約 (No-op 実装) ----
    public IRelayCommand OpenOfficialPageCommand { get; } = new RelayCommand(() => { }, () => false);
    public IRelayCommand OpenInExplorerCommand { get; } = new RelayCommand(() => { }, () => false);
    public IRelayCommand GenerateBeatmapCommand { get; } = new RelayCommand(() => { }, () => false);

    public void SelectBeatmapForContextMenu(Beatmap beatmap) { }
    public bool TryPrepareContextMenu(Beatmap beatmap) => false;
    public void ClearContextMenuBeatmap() { }

    public MockMapListViewModel()
    {
        PreviousPageCommand = new RelayCommand(() =>
        {
            if (_currentPage > 1)
            {
                CurrentPage = _currentPage - 1;
            }
        });
        NextPageCommand = new RelayCommand(() =>
        {
            if (_currentPage < FilteredPages)
            {
                CurrentPage = _currentPage + 1;
            }
        });
    }

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
    public bool CopyToImportsFolder(string sourceFilePath) => false;
    public void Dispose() { }
}

/// <summary>
/// テスト用のモックDatabaseService
/// </summary>
public class MockDatabaseService : IDatabaseService
{
    public MockDatabaseService() { }

    public MockDatabaseService(Beatmap[] beatmaps) => Beatmaps = beatmaps;

    public Beatmap[] Beatmaps { get; } = Array.Empty<Beatmap>();
    public List<OsuCollection> OsuCollections { get; } = new List<OsuCollection>();
    public Observable<DatabaseLoadProgress> CollectionDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
    public Observable<DatabaseLoadProgress> OsuDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
    public Observable<DatabaseLoadProgress> ScoresDbProgress { get; } = Observable.Empty<DatabaseLoadProgress>();
    public Task LoadDatabasesAsync() => Task.CompletedTask;
    public Task ReloadDatabasesAsync() => Task.CompletedTask;
    public Task ReloadCollectionDbAsync() => Task.CompletedTask;
    public bool TryGetBeatmapByMd5(string md5Hash, out Beatmap? beatmap) { beatmap = null; return false; }
    public ReadOnlySpan<LocalScore> GetLocalScores(string md5Hash) => ReadOnlySpan<LocalScore>.Empty;
    public IReadOnlySet<string> ApplyChartAnalyses(Func<Beatmap, ManiaChartAnalysis?> resolver)
        => new HashSet<string>();
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
    public string BeatmapMirrorUrl { get; set; } = "https://mirror.hinamizawa.ai/api/v1/hinai/d/";
    public bool AutoBatchGenerateOnStartup { get; set; } = false;
    public bool Analyze7kEnabled { get; set; } = true;
    public int Analyze7kParallelism { get; set; }
    public bool Analyze7kShowConcentration { get; set; }
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
    public DownloadActivity CurrentActivity => DownloadActivity.Idle;
    public event EventHandler<DownloadActivity>? ActivityChanged { add { } remove { } }
    public void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems) { }
    public Task CancelAllAsync() => Task.CompletedTask;
    public void Dispose() { }
}

/// <summary>
/// テスト用のモックBeatmapRateGenerator
/// </summary>
public class MockBeatmapRateGenerator : IBeatmapRateGenerator
{
    public Task<RateGenerationResult> GenerateAsync(
        Beatmap beatmap,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new RateGenerationResult { Success = false, SourceBeatmap = beatmap });

    public Task<BatchGenerationResult> GenerateBatchAsync(
        ReadOnlyMemory<Beatmap> beatmaps,
        RateGenerationOptions options,
        IProgress<RateGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BatchGenerationResult { Results = [] });
}

/// <summary>
/// テスト用のモック RateGenerationCollectionJsonWriter (常に未書き出し結果を返す)
/// </summary>
public class MockRateGenerationCollectionJsonWriter : IRateGenerationCollectionJsonWriter
{
    public Task<RateGenerationCollectionJsonWriteResult> WriteBatchAsync(
        string sourceCollectionName,
        RateGenerationOptions options,
        IReadOnlyList<RateGenerationResult> results,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new RateGenerationCollectionJsonWriteResult { FileWritten = false });
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
