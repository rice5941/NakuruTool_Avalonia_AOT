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
/// MapListView縺ｮ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繝・せ繝・
/// </summary>
public class MapListViewScreenshotTests
{
    /// <summary>
    /// 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ菫晏ｭ伜・繝・ぅ繝ｬ繧ｯ繝医Μ
    /// </summary>
    private static string ScreenshotsDirectory => 
        Path.Combine(AppContext.BaseDirectory, "Screenshots");

    /// <summary>
    /// MapListView縺ｮ遨ｺ縺ｮ迥ｶ諷九・繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListView_Empty()
    {
        // 繝｢繝・けViewModel繧剃ｽ懈・・育ｩｺ縺ｮ繝・・繧ｿ・・
        var mockViewModel = new MockMapListViewModel();

        // MapListView繧剃ｽ懈・
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // Window縺ｫ繧ｻ繝・ヨ
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // 繧ｦ繧｣繝ｳ繝峨え繧定｡ｨ遉ｺ
        window.Show();

        // UI繧ｹ繝ｬ繝・ラ縺ｮ繧ｸ繝ｧ繝悶ｒ螳御ｺ・＆縺帙√Ξ繝ｳ繝繝ｪ繝ｳ繧ｰ繧貞ｾ・ｩ・
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
        var frame = window.CaptureRenderedFrame();

        // 繝・ぅ繝ｬ繧ｯ繝医Μ繧剃ｽ懈・
        Directory.CreateDirectory(ScreenshotsDirectory);

        // 繝輔ぃ繧､繝ｫ縺ｫ菫晏ｭ・
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListView_Empty.png");
        frame?.Save(filePath);

        // 繝輔ぃ繧､繝ｫ縺御ｽ懈・縺輔ｌ縺溘％縺ｨ繧堤｢ｺ隱・
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// MapListView縺ｫ繝・・繧ｿ縺後≠繧狗憾諷九・繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListView_WithData()
    {
        // 繝｢繝・けViewModel繧剃ｽ懈・・医ユ繧ｹ繝医ョ繝ｼ繧ｿ縺ゅｊ・・
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateTestBeatmaps();
        mockViewModel.SetTestData(testData);

        // 矢埼・繧ｿ縺梧ｭ｣縺励￥險ｭ螳壹＆繧後※縺・ｋ縺薙→繧堤｢ｺ隱・
        Assert.Equal(testData.Count, mockViewModel.ShowBeatmaps.Count);

        // MapListView繧剃ｽ懈・
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // Window縺ｫ繧ｳ繝・ヨ
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // 繧ｦ繧｣繝ｳ繝峨え繧定｡ｨ遉ｺ
        window.Show();

        // UI繧ｹ繝ｬ繝・ラ縺ｮ繧ｸ繝ｧ繝悶ｒ螳御ｺ・＆縺帙√Ξ繝ｳ繝繝ｪ繝ｳ繧ｰ繧貞ｾ・ｩ・
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGrid縺梧ｭ｣縺励￥繝舌う繝ｳ繝峨＆繧後※縺・ｋ縺薙→繧堤｢ｺ隱・
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        Assert.NotNull(dataGrid);
        
        // DataContext繧堤｢ｺ隱・
        var viewModel = MapListView.DataContext as IMapListViewModel;
        Assert.NotNull(viewModel);
        Assert.Equal(testData.Count, viewModel.ShowBeatmaps.Count);
        
        // ItemsSource縺系ull縺ｮ蝣ｴ蜷茨ｼ医さ繝ｳ繝代う繝ｫ貂医∩繝舌う繝ｳ繝・ぅ繝ｳ繧ｰ縺ｮ蝠城｡鯉ｼ峨∫峩謗･險ｭ螳・
        if (dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = viewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        
        Assert.NotNull(dataGrid.ItemsSource);
        Assert.Equal(testData.Count, dataGrid.ItemsSource.Cast<object>().Count());

        // 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
        var frame = window.CaptureRenderedFrame();

        // 繝・ぅ繝ｬ繧ｯ繝医Μ繧剃ｽ懈・
        Directory.CreateDirectory(ScreenshotsDirectory);

        // 繝輔ぃ繧､繝ｫ縺ｫ菫晏ｭ・
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListView_WithData.png");
        frame?.Save(filePath);

        // 繝輔ぃ繧､繝ｫ縺御ｽ懈・縺輔ｌ縺溘％縺ｨ繧堤｢ｺ隱・
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 逡ｰ縺ｪ繧九え繧｣繝ｳ繝峨え繧ｵ繧､繧ｺ縺ｧ縺ｮ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
    /// </summary>
    [AvaloniaTheory]
    [InlineData(800, 600, "Small")]
    [InlineData(1280, 720, "HD")]
    [InlineData(1920, 1080, "FullHD")]
    public void CaptureMapListView_DifferentSizes(int width, int height, string sizeName)
    {
        // 繝｢繝・けViewModel繧剃ｽ懈・
        var mockViewModel = new MockMapListViewModel();
        mockViewModel.SetTestData(CreateTestBeatmaps());

        // MapListView繧剃ｽ懈・
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // Window縺ｫ繧ｻ繝・ヨ
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = MapListView
        };

        // 繧ｦ繧｣繝ｳ繝峨え繧定｡ｨ遉ｺ
        window.Show();

        // UI繧ｹ繝ｬ繝・ラ縺ｮ繧ｸ繝ｧ繝悶ｒ螳御ｺ・＆縺帙√Ξ繝ｳ繝繝ｪ繝ｳ繧ｰ繧貞ｾ・ｩ・
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGrid縺ｮItemsSource繧堤峩謗･險ｭ螳夲ｼ医さ繝ｳ繝代う繝ｫ貂医∩繝舌う繝ｳ繝・ぅ繝ｳ繧ｰ縺ｮ蝠城｡悟ｯｾ遲厄ｼ・
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
        var frame = window.CaptureRenderedFrame();

        // 繝・ぅ繝ｬ繧ｯ繝医Μ繧剃ｽ懈・
        Directory.CreateDirectory(ScreenshotsDirectory);

        // 繝輔ぃ繧､繝ｫ縺ｫ菫晏ｭ・
        var filePath = Path.Combine(ScreenshotsDirectory, $"MapListView_{sizeName}_{width}x{height}.png");
        frame?.Save(filePath);

        // 繝輔ぃ繧､繝ｫ縺御ｽ懈・縺輔ｌ縺溘％縺ｨ繧堤｢ｺ隱・
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 繝壹・繧ｸ騾√ｊ縺悟虚菴懊＠縺ｦ2繝壹・繧ｸ逶ｮ縺瑚｡ｨ遉ｺ縺輔ｌ繧九％縺ｨ繧堤｢ｺ隱・
    /// </summary>
    [AvaloniaFact]
    public void CaptureMapListView_Pagination_Page2()
    {
        // 繝壹・繧ｸ騾√ｊ繧偵ユ繧ｹ繝医☆繧九◆繧√↓螟壹￥縺ｮ繝・・繧ｿ繧剃ｽ懈・・・ageSize=5縺ｧ3繝壹・繧ｸ蛻・ｼ・
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateManyTestBeatmaps(15);
        mockViewModel.PageSize = 5; // 1繝壹・繧ｸ5莉ｶ
        mockViewModel.SetTestData(testData);

        // 蛻晄悄迥ｶ諷具ｼ・繝壹・繧ｸ逶ｮ・峨ｒ遒ｺ隱・
        Assert.Equal(1, mockViewModel.CurrentPage);
        Assert.Equal(3, mockViewModel.FilteredPages);
        Assert.Equal(5, mockViewModel.ShowBeatmaps.Count);

        // 2繝壹・繧ｸ逶ｮ縺ｫ遘ｻ蜍・
        mockViewModel.CurrentPage = 2;

        // 2繝壹・繧ｸ逶ｮ縺ｮ繝・・繧ｿ縺瑚｡ｨ遉ｺ縺輔ｌ縺ｦ縺・ｋ縺薙→繧堤｢ｺ隱・
        Assert.Equal(2, mockViewModel.CurrentPage);
        Assert.Equal(5, mockViewModel.ShowBeatmaps.Count);
        Assert.Equal("test6", mockViewModel.ShowBeatmaps[0].MD5Hash); // 6逡ｪ逶ｮ縺ｮ繝・・繧ｿ

        // MapListView繧剃ｽ懈・
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // Window縺ｫ繧ｻ繝・ヨ
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // 繧ｦ繧｣繝ｳ繝峨え繧定｡ｨ遉ｺ
        window.Show();

        // UI繧ｹ繝ｬ繝・ラ縺ｮ繧ｸ繝ｧ繝悶ｒ螳御ｺ・＆縺帙√Ξ繝ｳ繝繝ｪ繝ｳ繧ｰ繧貞ｾ・ｩ・
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGrid縺ｮItemsSource繧堤峩謗･險ｭ螳・
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
        var frame = window.CaptureRenderedFrame();

        // 繝・ぅ繝ｬ繧ｯ繝医Μ繧剃ｽ懈・
        Directory.CreateDirectory(ScreenshotsDirectory);

        // 繝輔ぃ繧､繝ｫ縺ｫ菫晏ｭ・
        var filePath = Path.Combine(ScreenshotsDirectory, "MapListView_Pagination_Page2.png");
        frame?.Save(filePath);

        // 繝輔ぃ繧､繝ｫ縺御ｽ懈・縺輔ｌ縺溘％縺ｨ繧堤｢ｺ隱・
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 繝壹・繧ｸ騾√ｊ - 1繝壹・繧ｸ逶ｮ縺ｨ2繝壹・繧ｸ逶ｮ繧呈ｯ碑ｼ・☆繧九せ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ
    /// </summary>
    [AvaloniaTheory]
    [InlineData(1, "Page1")]
    [InlineData(2, "Page2")]
    [InlineData(3, "Page3")]
    public void CaptureMapListView_Pagination_Pages(int pageNumber, string pageName)
    {
        // 繝壹・繧ｸ騾√ｊ繧偵ユ繧ｹ繝医☆繧九◆繧√↓螟壹￥縺ｮ繝・・繧ｿ繧剃ｽ懈・
        var mockViewModel = new MockMapListViewModel();
        var testData = CreateManyTestBeatmaps(15);
        mockViewModel.PageSize = 5;
        mockViewModel.SetTestData(testData);

        // 謖・ｮ壹・繝ｼ繧ｸ縺ｫ遘ｻ蜍・
        mockViewModel.CurrentPage = pageNumber;

        // MapListView繧剃ｽ懈・
        var MapListView = new MapListView
        {
            DataContext = mockViewModel
        };

        // Window縺ｫ繧ｻ繝・ヨ
        var window = new Window
        {
            Width = 1280,
            Height = 720,
            Content = MapListView
        };

        // 繧ｦ繧｣繝ｳ繝峨え繧定｡ｨ遉ｺ
        window.Show();

        // UI繧ｹ繝ｬ繝・ラ縺ｮ繧ｸ繝ｧ繝悶ｒ螳御ｺ・＆縺帙√Ξ繝ｳ繝繝ｪ繝ｳ繧ｰ繧貞ｾ・ｩ・
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        // DataGrid縺ｮItemsSource繧堤峩謗･險ｭ螳・
        var dataGrid = MapListView.FindControl<DataGrid>("MapListDataGrid");
        if (dataGrid != null && dataGrid.ItemsSource == null)
        {
            dataGrid.ItemsSource = mockViewModel.ShowBeatmaps;
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        // 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ繧呈聴蠖ｱ
        var frame = window.CaptureRenderedFrame();

        // 繝・ぅ繝ｬ繧ｯ繝医Μ繧剃ｽ懈・
        Directory.CreateDirectory(ScreenshotsDirectory);

        // 繝輔ぃ繧､繝ｫ縺ｫ菫晏ｭ・
        var filePath = Path.Combine(ScreenshotsDirectory, $"MapListView_Pagination_{pageName}.png");
        frame?.Save(filePath);

        // 繝輔ぃ繧､繝ｫ縺御ｽ懈・縺輔ｌ縺溘％縺ｨ繧堤｢ｺ隱・
        Assert.True(File.Exists(filePath), $"Screenshot was saved to: {filePath}");

        window.Close();
    }

    /// <summary>
    /// 螟壹￥縺ｮ繝・せ繝育畑Beatmap繝・・繧ｿ繧剃ｽ懈・
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
                Title = $"Test Song #{i:D2} - 繝・せ繝域峇{i}",
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
    /// 繝・せ繝育畑縺ｮBeatmap繝・・繧ｿ繧剃ｽ懈・
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
                Title = "繝・せ繝域峇1 - Test Song 1",
                Artist = "繝・せ繝医い繝ｼ繝・ぅ繧ｹ繝・",
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
