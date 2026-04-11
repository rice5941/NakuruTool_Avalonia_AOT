using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public interface IMapListViewModel: IDisposable
{
    IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; }
    int TotalCount { get; }
    int CurrentPage { get; }
    int FilteredPages { get; }
    int FilteredCount { get; }
    int PageSize { get; }
    IAvaloniaReadOnlyList<int> PageSizes { get; }
    Beatmap? SelectedBeatmap { get; set; }
    ModCategory SelectedModCategory { get; set; }
    ScoreSystemCategory SelectedScoreSystemCategory { get; set; }
    void Initialize();
    void ApplyFilter();
    Beatmap[] FilteredBeatmapsArray { get; }
}

/// <summary>
/// 譜面一覧のViewModel
/// </summary>
public partial class MapListViewModel : ViewModelBase, IMapListViewModel
{
    /// <summary>オーディオ再生パネルのViewModel。</summary>
    public AudioPlayerPanelViewModel AudioPlayerPanel { get; }

    private bool _isNavigating;
    private Func<string, Task>? _clipboardWriter;
    private Beatmap? _contextMenuBeatmap;
    private readonly Subject<Beatmap> _generateBeatmapRequested = new();

    public Observable<Beatmap> GenerateBeatmapRequested => _generateBeatmapRequested;

    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; set; } = new AvaloniaList<Beatmap>();

    [ObservableProperty]
    public partial int TotalCount { get; set; } = 0;
    
    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;
    
    [ObservableProperty]
    public partial int FilteredPages { get; set; } = 0;
    
    [ObservableProperty]
    public partial int FilteredCount { get; set; } = 0;
    
    [ObservableProperty]
    public partial int PageSize { get; set; } = DefaultPageSize;

    [ObservableProperty]
    public partial Beatmap? SelectedBeatmap { get; set; }

    [ObservableProperty]
    public partial ModCategory SelectedModCategory { get; set; } = ModCategory.NoMod;

    [ObservableProperty]
    public partial ScoreSystemCategory SelectedScoreSystemCategory { get; set; } = ScoreSystemCategory.Default;

    private readonly AudioPlayerViewModel _audioPlayer;

    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

    private IDatabaseService _databaseService;
    private MapFilterViewModel _filterViewModel;
    private readonly ISettingsService _settingsService;

    private const int DefaultPageSize = 20;

    [ObservableProperty]
    public partial Beatmap[] FilteredBeatmapsArray { get; set; } = Array.Empty<Beatmap>();

    private AvaloniaList<Beatmap> _showBeatmapsList = new();

    public MapListViewModel(
        IDatabaseService databaseService,
        MapFilterViewModel filterViewModel,
        AudioPlayerViewModel audioPlayerViewModel,
        AudioPlayerPanelViewModel audioPlayerPanelViewModel,
        ISettingsService settingsService)
    {
        _databaseService = databaseService;
        _filterViewModel = filterViewModel;
        _audioPlayer = audioPlayerViewModel;
        AudioPlayerPanel = audioPlayerPanelViewModel;
        _settingsService = settingsService;

        ShowBeatmaps = _showBeatmapsList;

        AudioPlayerPanel.SetNavigateCallback(NavigateToFilteredIndex);

        _filterViewModel.FilterChanged
            .Subscribe(_ => ApplyFilter())
            .AddTo(Disposables);

        // 譜面選択時にオーディオを再生
        this.ObserveProperty(nameof(SelectedBeatmap))
            .Subscribe(_ =>
            {
                if (SelectedBeatmap == null) return;
                if (_isNavigating) return;

                var index = FindBeatmapIndex(SelectedBeatmap.MD5Hash);
                AudioPlayerPanel.PlayBeatmap(SelectedBeatmap, index, _settingsService.SettingsData.AutoPlayOnSelect);
            })
            .AddTo(Disposables);

        // Unicode表示設定の変更時にリスト表示を更新（Converter再評価のため）
        _settingsService.SettingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.PreferUnicode),
            () => UpdateShowBeatmaps(),
            Disposables);
    }

    public void Initialize()
    {
        UpdateTotalCount();
        UpdateFilteredBeatmapsArray();
        UpdateFilteredPages();
        UpdateShowBeatmaps();
        AudioPlayerPanel.SetNavigationContext(FilteredBeatmapsArray, -1);
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage() => CurrentPage++;
    private bool CanGoToNextPage() => CurrentPage < FilteredPages;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage() => CurrentPage--;
    private bool CanGoToPreviousPage() => CurrentPage > 1;

    private bool CanCopyDownloadUrl() =>
        _contextMenuBeatmap is { BeatmapSetId: > 0 } && _clipboardWriter is not null;

    [RelayCommand(CanExecute = nameof(CanCopyDownloadUrl))]
    private async Task CopyDownloadUrlAsync()
    {
        if (_contextMenuBeatmap is null || _clipboardWriter is null)
            return;

        var mirrorUrl = _settingsService.SettingsData.BeatmapMirrorUrl;
        if (string.IsNullOrEmpty(mirrorUrl))
            mirrorUrl = "https://catboy.best/d/";

        var url = $"{mirrorUrl}{_contextMenuBeatmap.BeatmapSetId}";
        await _clipboardWriter(url);
    }

    public void SetClipboardWriter(Func<string, Task>? writer)
    {
        _clipboardWriter = writer;
        CopyDownloadUrlCommand.NotifyCanExecuteChanged();
    }

    public void SelectBeatmapForContextMenu(Beatmap beatmap)
    {
        _isNavigating = true;
        try
        {
            SelectedBeatmap = beatmap;
        }
        finally
        {
            _isNavigating = false;
        }
    }

    public bool TryPrepareContextMenu(Beatmap beatmap)
    {
        // FolderName が存在すればメニューを表示（エクスプローラーで開く機能は BeatmapSetId 不要）
        _contextMenuBeatmap = !string.IsNullOrEmpty(beatmap.FolderName) ? beatmap : null;
        CopyDownloadUrlCommand.NotifyCanExecuteChanged();
        OpenInExplorerCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
        return _contextMenuBeatmap is not null;
    }

    public void ClearContextMenuBeatmap()
    {
        _contextMenuBeatmap = null;
        CopyDownloadUrlCommand.NotifyCanExecuteChanged();
        OpenInExplorerCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpenInExplorer() =>
        _contextMenuBeatmap is not null &&
        !string.IsNullOrEmpty(_contextMenuBeatmap.FolderName) &&
        !string.IsNullOrEmpty(_settingsService.SettingsData.OsuFolderPath);

    [RelayCommand(CanExecute = nameof(CanOpenInExplorer))]
    private void OpenInExplorer()
    {
        if (_contextMenuBeatmap is null)
            return;

        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        if (string.IsNullOrEmpty(osuFolderPath))
            return;

        var folderPath = Path.Combine(osuFolderPath, "Songs", _contextMenuBeatmap.FolderName);
        if (Directory.Exists(folderPath))
        {
            Process.Start("explorer.exe", folderPath);
        }
    }

    private bool CanGenerateBeatmap() => _contextMenuBeatmap is not null;

    [RelayCommand(CanExecute = nameof(CanGenerateBeatmap))]
    private void GenerateBeatmap()
    {
        if (_contextMenuBeatmap is not null)
        {
            _generateBeatmapRequested.OnNext(_contextMenuBeatmap);
        }
    }

    private void UpdateTotalCount() => TotalCount = _databaseService.Beatmaps.Length;

    private void UpdateFilteredPages()
    {
        var size = Math.Max(1, PageSize);
        FilteredPages = Math.Max(1, (FilteredCount + size - 1) / size);
    }
    
    private void UpdateFilteredBeatmapsArray()
    {
        var allBeatmaps = _databaseService.Beatmaps.AsValueEnumerable();
        
        FilteredBeatmapsArray = allBeatmaps
            .Where(x => _filterViewModel.Matches(x))
            .ToArray();

        FilteredCount = FilteredBeatmapsArray.Length;
    }

    public void ApplyFilter()
    {
        UpdateFilteredBeatmapsArray();
        UpdateFilteredPages();
        UpdateShowBeatmaps();
        AudioPlayerPanel.SetNavigationContext(FilteredBeatmapsArray, -1);
    }

    /// <summary>
    /// FilteredBeatmapsArray 内の指定インデックスにナビゲートする。
    /// AudioPlayerPanelViewModelの _navigateCallback から呼び出される。
    /// </summary>
    public void NavigateToFilteredIndex(int filteredIndex)
    {
        if (filteredIndex < 0 || filteredIndex >= FilteredBeatmapsArray.Length) return;

        var targetPage = (filteredIndex / Math.Max(1, PageSize)) + 1;
        _isNavigating = true;
        try
        {
            if (CurrentPage != targetPage)
                CurrentPage = targetPage;
            else
                UpdateShowBeatmaps();

            var indexInPage = filteredIndex % Math.Max(1, PageSize);
            if (indexInPage < ShowBeatmaps.Count)
                SelectedBeatmap = ShowBeatmaps[indexInPage];
        }
        finally
        {
            _isNavigating = false;
        }

        // ナビゲーション完了後に再生を呼び出す
        if (SelectedBeatmap != null)
        {
            AudioPlayerPanel.PlayBeatmap(SelectedBeatmap, filteredIndex);
        }
    }

    /// <summary>
    /// FilteredBeatmapsArray 内で指定の MD5Hash に一致する譜面のインデックスを返す。見つからなければ -1 を返す。
    /// </summary>
    private int FindBeatmapIndex(string md5Hash)
    {
        var arr = FilteredBeatmapsArray;
        for (var i = 0; i < arr.Length; i++)
        {
            if (arr[i].MD5Hash == md5Hash)
                return i;
        }
        return -1;
    }

    private void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var remaining = Math.Max(0, FilteredBeatmapsArray.Length - skip);
        var take = Math.Min(size, remaining);

        _showBeatmapsList.Clear();
        
        if (take > 0)
        {
            var mod = SelectedModCategory;
            var scoreSystem = SelectedScoreSystemCategory;
            var span = FilteredBeatmapsArray.AsSpan(skip, take);
            foreach (var beatmap in span)
            {
                // 選択されたスコアシステム・mod区分に応じてBestScore/BestAccuracy/Gradeを差し替え
                var displayed = beatmap with
                {
                    BestScore = beatmap.GetBestScore(scoreSystem, mod),
                    BestAccuracy = beatmap.GetBestAccuracy(scoreSystem, mod),
                    Grade = beatmap.GetGrade(scoreSystem, mod)
                };
                _showBeatmapsList.Add(displayed);
            }
        }
    }

    partial void OnCurrentPageChanged(int value)
    {
        UpdateShowBeatmaps();
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnFilteredCountChanged(int value) => UpdateFilteredPages();
    partial void OnFilteredPagesChanged(int value)
    {
        CurrentPage = 1;
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageSizeChanged(int value)
    {
        UpdateFilteredPages();
        UpdateShowBeatmaps();
    }

    partial void OnSelectedModCategoryChanged(ModCategory value)
    {
        UpdateShowBeatmaps();
    }

    partial void OnSelectedScoreSystemCategoryChanged(ScoreSystemCategory value)
    {
        UpdateShowBeatmaps();
    }

    public override void Dispose()
    {
        _generateBeatmapRequested.OnCompleted();
        _generateBeatmapRequested.Dispose();
        _audioPlayer.Dispose();
        AudioPlayerPanel.Dispose();
        base.Dispose();
    }
}