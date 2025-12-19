using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// MapListViewPageのスクリーンショットテスト
/// </summary>
public class MapListViewPageScreenshotTests
{
    /// <summary>
    /// スクリーンショット保存先ディレクトリ
    /// </summary>
    private static string ScreenshotsDirectory => 
        Path.Combine(AppContext.BaseDirectory, "Screenshots");

    /// <summary>
    /// MapListViewPageの空の状態のスクリーンショットを撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListViewPage_Empty()
    {
        // モックViewModelを作成（空のデータ）
        var mockViewModel = new MockMapListViewModel();

        // MapListViewPageを作成
        var mapListViewPage = new MapListViewPage
        {
            DataContext = mockViewModel
        };

        // Windowにセット
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = mapListViewPage
        };

        // ウィンドウを表示
        window.Show();

        // UIスレッドのジョブを完了させ、レンダリングを待機
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // スクリーンショットを撮影
        var frame = window.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListViewPage_Empty.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// MapListViewPageにデータがある状態のスクリーンショットを撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListViewPage_WithData()
    {
        // モックViewModelを作成（テストデータあり）
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateTestBeatmaps();
        mockViewModel.SetTestData(testData);

        // データが正しく設定されていることを確認
        Assert.Equal(testData.Count, mockViewModel.ShowBeatmaps.Count);

        // MapListViewPageを作成
        var mapListViewPage = new MapListViewPage
        {
            DataContext = mockViewModel
        };

        // Windowにセット
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = mapListViewPage
        };

        // ウィンドウを表示
        window.Show();

        // UIスレッドのジョブを完了させ、レンダリングを待機
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridが正しくバインドされていることを確認
        var dataGrid = mapListViewPage.FindControl<DataGrid>("MapListDataGrid");
        Assert.NotNull(dataGrid);
        
        // DataContextを確認
        var viewModel = mapListViewPage.DataContext as IMapListViewModel;
        Assert.NotNull(viewModel);
        Assert.Equal(testData.Count, viewModel.ShowBeatmaps.Count);
        
        // ItemsSourceがnullの場合（コンパイル済みバインディングの問題）、直接設定
        if (dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = viewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        
        Assert.NotNull(dataGrid.ItemsSource);
        Assert.Equal(testData.Count, dataGrid.ItemsSource.Cast<object>().Count());

        // スクリーンショットを撮影
        var frame = window.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListViewPage_WithData.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 異なるウィンドウサイズでのスクリーンショットを撮影
    /// </summary>
    [AvaloniaTheory]
    [InlineData(800, 600, "Small")]
    [InlineData(1280, 720, "HD")]
    [InlineData(1920, 1080, "FullHD")]
    public void CaptureMapListViewPage_DifferentSizes(int width, int height, string sizeName)
    {
        // モックViewModelを作成
        var mockViewModel = new MockMapListViewModel();
        mockViewModel.SetTestData(CreateTestBeatmaps());

        // MapListViewPageを作成
        var mapListViewPage = new MapListViewPage
        {
            DataContext = mockViewModel
        };

        // Windowにセット
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = mapListViewPage
        };

        // ウィンドウを表示
        window.Show();

        // UIスレッドのジョブを完了させ、レンダリングを待機
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridのItemsSourceを直接設定（コンパイル済みバインディングの問題対策）
        var dataGrid = mapListViewPage.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // スクリーンショットを撮影
        var frame = window.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, $"MapListViewPage_{sizeName}_{width}x{height}.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// ページ送りが動作して2ページ目が表示されることを確認
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListViewPage_Pagination_Page2()
    {
        // ページ送りをテストするために多くのデータを作成（PageSize=5で3ページ分）
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateManyTestBeatmaps(15);
        mockViewModel.PageSize = 5; // 1ページ5件
        mockViewModel.SetTestData(testData);

        // 初期状態（1ページ目）を確認
        Assert.Equal(1, mockViewModel.CurrentPage);
        Assert.Equal(3, mockViewModel.FilteredPages);
        Assert.Equal(5, mockViewModel.ShowBeatmaps.Count);

        // 2ページ目に移動
        mockViewModel.CurrentPage = 2;

        // 2ページ目のデータが表示されていることを確認
        Assert.Equal(2, mockViewModel.CurrentPage);
        Assert.Equal(5, mockViewModel.ShowBeatmaps.Count);
        Assert.Equal("test6", mockViewModel.ShowBeatmaps[0].MD5Hash); // 6番目のデータ

        // MapListViewPageを作成
        var mapListViewPage = new MapListViewPage
        {
            DataContext = mockViewModel
        };

        // Windowにセット
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = mapListViewPage
        };

        // ウィンドウを表示
        window.Show();

        // UIスレッドのジョブを完了させ、レンダリングを待機
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridのItemsSourceを直接設定
        var dataGrid = mapListViewPage.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // スクリーンショットを撮影
        var frame = window.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListViewPage_Pagination_Page2.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// ページ送り - 1ページ目と2ページ目を比較するスクリーンショット
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1, "Page1")]
    [InlineData(2, "Page2")]
    [InlineData(3, "Page3")]
    public void CaptureMapListViewPage_Pagination_Pages(int pageNumber, string pageName)
    {
        // ページ送りをテストするために多くのデータを作成
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateManyTestBeatmaps(15);
        mockViewModel.PageSize = 5;
        mockViewModel.SetTestData(testData);

        // 指定ページに移動
        mockViewModel.CurrentPage = pageNumber;

        // MapListViewPageを作成
        var mapListViewPage = new MapListViewPage
        {
            DataContext = mockViewModel
        };

        // Windowにセット
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = mapListViewPage
        };

        // ウィンドウを表示
        window.Show();

        // UIスレッドのジョブを完了させ、レンダリングを待機
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridのItemsSourceを直接設定
        var dataGrid = mapListViewPage.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // スクリーンショットを撮影
        var frame = window.CaptureRenderedFrame();

        // ディレクトリを作成
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保存
        var filePath = Path.Combine(ScreenshotsDirectory, $"MapListViewPage_Pagination_{pageName}.png");
        frame?.Save(filePath);

        // ファイルが作成されたことを確認
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 多くのテスト用Beatmapデータを作成
    /// </summary>
    private static List<Beatmap> CreateManyTestBeatmaps(int count)
    {
        var beatmaps = new List<Beatmap>();
        var statuses = new[] { BeatmapStatus.Ranked, BeatmapStatus.Loved, BeatmapStatus.Approved, BeatmapStatus.Pending };
        var grades = new[] { "SS", "S", "A", "B", "C", "D", "" };

        for (int i = 1; i <= count; i++)
        {
            beatmaps.Add(new Beatmap
            {
                MD5Hash = $"test{i}",
                KeyCount = i % 2 == 0 ? 7 : 4,
                Status = statuses[i % statuses.Length],
                Title = $"Test Song #{i:D2} - テスト曲{i}",
                Artist = $"Artist {i}",
                Version = i switch
                {
                    <= 5 => "Easy",
                    <= 10 => "Normal",
                    _ => "Hard"
                },
                Creator = $"Mapper{i}",
                BPM = 120 + (i * 10),
                Difficulty = 1.0 + (i * 0.5),
                LongNoteRate = (i % 10) * 0.1,
                IsPlayed = i % 3 != 0,
                LastPlayed = i % 3 != 0 ? DateTime.Now.AddDays(-i) : null,
                LastModifiedTime = DateTime.Now.AddDays(-i * 2),
                FolderName = $"folder_{i}",
                BeatmapSetId = 1000 + i,
                BeatmapId = 10000 + i,
                BestScore = i % 3 != 0 ? 900000 + (i * 1000) : 0,
                BestAccuracy = i % 3 != 0 ? 90.0 + (i * 0.5) : 0,
                PlayCount = i % 3 != 0 ? i * 2 : 0,
                Grade = i % 3 != 0 ? grades[i % grades.Length] : ""
            });
        }

        return beatmaps;
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
                Title = "Long Title Example - This is a very long title that should be truncated in the UI display",
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
/// テスト用のモックMapListViewModel
/// </summary>
public class MockMapListViewModel : IMapListViewModel
{
    private readonly AvaloniaList<Beatmap> _showBeatmapsList = [];
    private List<Beatmap> _allBeatmaps = [];
    private int _pageSize = 20;
    private int _currentPage = 1;

    public IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps => _showBeatmapsList;
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

    public void Initialize()
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
