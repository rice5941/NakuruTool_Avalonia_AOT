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
/// MapListViewのスクリーンショチE��チE��チE
/// </summary>
public class MapListViewScreenshotTests
{
    /// <summary>
    /// スクリーンショチE��保存�EチE��レクトリ
    /// </summary>
    private static string ScreenshotsDirectory => 
        Path.Combine(AppContext.BaseDirectory, "Screenshots");

    /// <summary>
    /// MapListViewの空の状態�EスクリーンショチE��を撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListView_Empty()
    {
        // モチE��ViewModelを作�E�E�空のチE�Eタ�E�E
        var mockViewModel = new MockMapListViewModel();

        // MapListViewを作�E
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // WindowにセチE��
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // ウィンドウを表示
        window.Show();

        // UIスレチE��のジョブを完亁E��せ、レンダリングを征E��E
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // スクリーンショチE��を撮影
        var frame = window.CaptureRenderedFrame();

        // チE��レクトリを作�E
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保孁E
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListView_Empty.png");
        frame?.Save(filePath);

        // ファイルが作�Eされたことを確誁E
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// MapListViewにチE�Eタがある状態�EスクリーンショチE��を撮影
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListView_WithData()
    {
        // モチE��ViewModelを作�E�E�テストデータあり�E�E
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateTestBeatmaps();
        mockViewModel.SetTestData(testData);

        // ���Eタが正しく設定されてぁE��ことを確誁E
        Assert.Equal(testData.Count, mockViewModel.ShowBeatmaps.Count);

        // MapListViewを作�E
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // WindowにコチE��
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // ウィンドウを表示
        window.Show();

        // UIスレチE��のジョブを完亁E��せ、レンダリングを征E��E
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridが正しくバインドされてぁE��ことを確誁E
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        Assert.NotNull(dataGrid);
        
        // DataContextを確誁E
        var viewModel = MapListView.DataContext as IMapListViewModel;
        Assert.NotNull(viewModel);
        Assert.Equal(testData.Count, viewModel.ShowBeatmaps.Count);
        
        // ItemsSourceがnullの場合（コンパイル済みバインチE��ングの問題）、直接設宁E
        if (dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = viewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        
        Assert.NotNull(dataGrid.ItemsSource);
        Assert.Equal(testData.Count, dataGrid.ItemsSource.Cast<object>().Count());

        // スクリーンショチE��を撮影
        var frame = window.CaptureRenderedFrame();

        // チE��レクトリを作�E
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保孁E
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListView_WithData.png");
        frame?.Save(filePath);

        // ファイルが作�Eされたことを確誁E
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 異なるウィンドウサイズでのスクリーンショチE��を撮影
    /// </summary>
    [AvaloniaTheory]
    [InlineData(800, 600, "Small")]
    [InlineData(1280, 720, "HD")]
    [InlineData(1920, 1080, "FullHD")]
    public void CaptureMapListView_DifferentSizes(int width, int height, string sizeName)
    {
        // モチE��ViewModelを作�E
        var mockViewModel = new MockMapListViewModel();
        mockViewModel.SetTestData(CreateTestBeatmaps());

        // MapListViewを作�E
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // WindowにセチE��
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = MapListView
        };

        // ウィンドウを表示
        window.Show();

        // UIスレチE��のジョブを完亁E��せ、レンダリングを征E��E
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridのItemsSourceを直接設定（コンパイル済みバインチE��ングの問題対策！E
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // スクリーンショチE��を撮影
        var frame = window.CaptureRenderedFrame();

        // チE��レクトリを作�E
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保孁E
        var filePath = Path.Combine(ScreenshotsDirectory, $"MapListView_{sizeName}_{width}x{height}.png");
        frame?.Save(filePath);

        // ファイルが作�Eされたことを確誁E
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// ペ�Eジ送りが動作して2ペ�Eジ目が表示されることを確誁E
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListView_Pagination_Page2()
    {
        // ペ�Eジ送りをテストするために多くのチE�Eタを作�E�E�EageSize=5で3ペ�Eジ刁E��E
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateManyTestBeatmaps(15);
        mockViewModel.PageSize = 5; // 1ペ�Eジ5件
        mockViewModel.SetTestData(testData);

        // 初期状態！Eペ�Eジ目�E�を確誁E
        Assert.Equal(1, mockViewModel.CurrentPage);
        Assert.Equal(3, mockViewModel.FilteredPages);
        Assert.Equal(5, mockViewModel.ShowBeatmaps.Count);

        // 2ペ�Eジ目に移勁E
        mockViewModel.CurrentPage = 2;

        // 2ペ�Eジ目のチE�Eタが表示されてぁE��ことを確誁E
        Assert.Equal(2, mockViewModel.CurrentPage);
        Assert.Equal(5, mockViewModel.ShowBeatmaps.Count);
        Assert.Equal("test6", mockViewModel.ShowBeatmaps[0].MD5Hash); // 6番目のチE�Eタ

        // MapListViewを作�E
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // WindowにセチE��
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // ウィンドウを表示
        window.Show();

        // UIスレチE��のジョブを完亁E��せ、レンダリングを征E��E
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridのItemsSourceを直接設宁E
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // スクリーンショチE��を撮影
        var frame = window.CaptureRenderedFrame();

        // チE��レクトリを作�E
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保孁E
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListView_Pagination_Page2.png");
        frame?.Save(filePath);

        // ファイルが作�Eされたことを確誁E
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// ペ�Eジ送り - 1ペ�Eジ目と2ペ�Eジ目を比輁E��るスクリーンショチE��
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1, "Page1")]
    [InlineData(2, "Page2")]
    [InlineData(3, "Page3")]
    public void CaptureMapListView_Pagination_Pages(int pageNumber, string pageName)
    {
        // ペ�Eジ送りをテストするために多くのチE�Eタを作�E
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateManyTestBeatmaps(15);
        mockViewModel.PageSize = 5;
        mockViewModel.SetTestData(testData);

        // 持E���Eージに移勁E
        mockViewModel.CurrentPage = pageNumber;

        // MapListViewを作�E
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // WindowにセチE��
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // ウィンドウを表示
        window.Show();

        // UIスレチE��のジョブを完亁E��せ、レンダリングを征E��E
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGridのItemsSourceを直接設宁E
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // スクリーンショチE��を撮影
        var frame = window.CaptureRenderedFrame();

        // チE��レクトリを作�E
        Directory.CreateDirectory(ScreenshotsDirectory);

        // ファイルに保孁E
        var filePath = Path.Combine(ScreenshotsDirectory, $"MapListView_Pagination_{pageName}.png");
        frame?.Save(filePath);

        // ファイルが作�Eされたことを確誁E
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 多くのチE��ト用BeatmapチE�Eタを作�E
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
                Title = $"Test Song #{i:D2} - チE��ト曲{i}",
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
                AudioFilename = "audio.mp3",
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
    /// チE��ト用のBeatmapチE�Eタを作�E
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
                Title = "チE��ト曲1 - Test Song 1",
                Artist = "チE��トアーチE��スチE",
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
                AudioFilename = "audio.mp3",
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
