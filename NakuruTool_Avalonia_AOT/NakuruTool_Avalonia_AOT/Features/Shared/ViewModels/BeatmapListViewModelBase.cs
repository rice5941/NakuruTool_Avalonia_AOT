using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using R3;

namespace NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;

/// <summary>
/// BeatmapList 系 ViewModel (MapList / BeatmapGenerationPage) の共通基底。
/// ページング・ContextMenu・Mod/ScoreSystem 切替・PreferUnicode 監視を集約する。
/// </summary>
public abstract partial class BeatmapListViewModelBase : ViewModelBase, IBeatmapListViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly AvaloniaList<Beatmap> _showBeatmapsList = new();
    private Beatmap[] _sourceBeatmaps = Array.Empty<Beatmap>();
    private Func<string, Task>? _clipboardWriter;
    private Beatmap? _contextMenuBeatmap;

    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; set; } = new AvaloniaList<Beatmap>();

    [ObservableProperty]
    public partial Beatmap? SelectedBeatmap { get; set; }

    [ObservableProperty]
    public partial ModCategory SelectedModCategory { get; set; } = ModCategory.NoMod;

    [ObservableProperty]
    public partial ScoreSystemCategory SelectedScoreSystemCategory { get; set; } = ScoreSystemCategory.Default;

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int PageCount { get; set; } = 1;

    [ObservableProperty]
    public partial int PageSize { get; set; } = 20;

    [ObservableProperty]
    public partial int FilteredCount { get; set; } = 0;

    [ObservableProperty]
    public partial int TotalCount { get; set; } = 0;

    public IAvaloniaReadOnlyList<int> PageSizes { get; } = new AvaloniaList<int> { 10, 20, 50, 100 };

    protected BeatmapListViewModelBase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        ShowBeatmaps = _showBeatmapsList;

        _settingsService.SettingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.PreferUnicode),
            UpdateShowBeatmaps,
            Disposables);
    }

    /// <summary>派生から参照可能な現在のソース配列。</summary>
    protected Beatmap[] SourceBeatmapsRaw => _sourceBeatmaps;

    /// <summary>派生から参照可能な設定データ。</summary>
    protected ISettingsData SettingsData => _settingsService.SettingsData;

    /// <summary>
    /// 派生で生成したベース譜面配列を差し替える。
    /// FilteredCount を更新し、CurrentPage を 1 にリセット (既に 1 なら再投影のみ実行)。
    /// </summary>
    protected void SetSourceBeatmaps(Beatmap[] source)
    {
        _sourceBeatmaps = source ?? Array.Empty<Beatmap>();
        FilteredCount = _sourceBeatmaps.Length;
        if (CurrentPage != 1)
        {
            CurrentPage = 1;
        }
        else
        {
            UpdateShowBeatmaps();
        }
    }

    /// <summary>
    /// 現在の CurrentPage / PageSize / Mod / ScoreSystem に基づき表示用譜面リストを再投影する。
    /// AvaloniaList インスタンスは差し替えず Clear/Add でのみ更新する。
    /// </summary>
    protected void UpdateShowBeatmaps()
    {
        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var remaining = Math.Max(0, _sourceBeatmaps.Length - skip);
        var take = Math.Min(size, remaining);

        _showBeatmapsList.Clear();

        if (take > 0)
        {
            var mod = SelectedModCategory;
            var scoreSystem = SelectedScoreSystemCategory;
            var span = _sourceBeatmaps.AsSpan(skip, take);
            foreach (var beatmap in span)
            {
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

    /// <summary>
    /// PageCount を再計算する (PageSize / FilteredCount から算出のみ)。
    /// 末尾クランプは行わない。SetSourceBeatmaps / OnPageSizeChanged 双方で
    /// 必ず CurrentPage を 1 にリセットする仕様のため、クランプは冗長な
    /// 二重発火源となるので削除している。
    /// </summary>
    protected void RecalculatePageCount()
    {
        var size = Math.Max(1, PageSize);
        PageCount = Math.Max(1, (FilteredCount + size - 1) / size);
    }

    // ---- partial メソッド (基底内に閉じる) ----

    partial void OnSelectedModCategoryChanged(ModCategory value) => UpdateShowBeatmaps();

    partial void OnSelectedScoreSystemCategoryChanged(ScoreSystemCategory value) => UpdateShowBeatmaps();

    partial void OnCurrentPageChanged(int value)
    {
        UpdateShowBeatmaps();
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageCountChanged(int value)
    {
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageSizeChanged(int value)
    {
        RecalculatePageCount();
        if (CurrentPage != 1)
        {
            CurrentPage = 1;
        }
        else
        {
            UpdateShowBeatmaps();
        }
    }

    partial void OnFilteredCountChanged(int value) => RecalculatePageCount();

    // ---- ページング コマンド ----

    [RelayCommand(CanExecute = nameof(CanGoToNextPage))]
    private void NextPage() => CurrentPage++;
    private bool CanGoToNextPage() => CurrentPage < PageCount;

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))]
    private void PreviousPage() => CurrentPage--;
    private bool CanGoToPreviousPage() => CurrentPage > 1;

    // ---- ContextMenu コマンド ----

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

    private bool CanCopyDownloadUrl() =>
        _contextMenuBeatmap is { BeatmapSetId: > 0 } && _clipboardWriter is not null;

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
            // FolderName にスペース・カンマ・括弧が含まれていても 1 引数として渡るよう ArgumentList を使う。
            Process.Start(ExplorerProcessStartInfoFactory.CreateOpenFolder(folderPath));
        }
    }

    private bool CanOpenInExplorer() =>
        _contextMenuBeatmap is not null &&
        !string.IsNullOrEmpty(_contextMenuBeatmap.FolderName) &&
        !string.IsNullOrEmpty(_settingsService.SettingsData.OsuFolderPath);

    [RelayCommand(CanExecute = nameof(CanGenerateBeatmap))]
    private void GenerateBeatmap()
    {
        if (_contextMenuBeatmap is { } target)
        {
            OnGenerateBeatmap(target);
        }
    }

    private bool CanGenerateBeatmap() =>
        _contextMenuBeatmap is not null && CanGenerateBeatmapFromContextMenu;

    // ---- protected virtual フック ----

    /// <summary>譜面生成リクエストの派生差分。既定は No-op。</summary>
    protected virtual void OnGenerateBeatmap(Beatmap target)
    {
    }

    /// <summary>ContextMenu 経由で生成可能か。既定は false (生成ページ自身では生成不可)。</summary>
    protected virtual bool CanGenerateBeatmapFromContextMenu => false;

    // ---- public API (IBeatmapListViewModel) ----

    public void SetClipboardWriter(Func<string, Task>? writer)
    {
        _clipboardWriter = writer;
        CopyDownloadUrlCommand.NotifyCanExecuteChanged();
    }

    public virtual void SelectBeatmapForContextMenu(Beatmap beatmap)
    {
        SelectedBeatmap = beatmap;
    }

    public bool TryPrepareContextMenu(Beatmap beatmap)
    {
        if (string.IsNullOrEmpty(beatmap.FolderName))
        {
            _contextMenuBeatmap = null;
        }
        else if (beatmap.BeatmapSetId > 0)
        {
            _contextMenuBeatmap = beatmap;
        }
        else
        {
            // BeatmapSetId が osu!.db から取得できなかった (=0) 場合のみ、
            // 対応する .osu ファイルの [Metadata] セクションを読み、
            // BeatmapSetID をフォールバックとして取得する。
            _contextMenuBeatmap = TryFillBeatmapSetIdFromOsuFile(beatmap);
        }

        CopyDownloadUrlCommand.NotifyCanExecuteChanged();
        OpenInExplorerCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
        return _contextMenuBeatmap is not null;
    }

    private Beatmap TryFillBeatmapSetIdFromOsuFile(Beatmap beatmap)
    {
        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        if (string.IsNullOrEmpty(osuFolderPath) ||
            string.IsNullOrEmpty(beatmap.OsuFileName))
        {
            return beatmap;
        }

        var osuPath = Path.Combine(
            osuFolderPath,
            "Songs",
            beatmap.FolderName,
            beatmap.OsuFileName);

        if (!File.Exists(osuPath))
            return beatmap;

        if (OsuFileMetadataReader.TryReadBeatmapSetId(osuPath, out var setId) && setId > 0)
        {
            return beatmap with { BeatmapSetId = setId };
        }

        return beatmap;
    }

    public void ClearContextMenuBeatmap()
    {
        _contextMenuBeatmap = null;
        CopyDownloadUrlCommand.NotifyCanExecuteChanged();
        OpenInExplorerCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
    }

    // ---- IBeatmapListViewModel 明示実装ブリッジ (IAsyncRelayCommand → IRelayCommand) ----

    IRelayCommand IBeatmapListViewModel.CopyDownloadUrlCommand => CopyDownloadUrlCommand;
    IRelayCommand IBeatmapListViewModel.OpenInExplorerCommand => OpenInExplorerCommand;
    IRelayCommand IBeatmapListViewModel.GenerateBeatmapCommand => GenerateBeatmapCommand;
}
