using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public interface IMapListViewModel : IBeatmapListViewModel, IDisposable
{
    int FilteredPages { get; }
    Beatmap[] FilteredBeatmapsArray { get; }
    void Initialize();
    void ApplyFilter();
}

/// <summary>
/// 譜面一覧のViewModel
/// </summary>
public partial class MapListViewModel : BeatmapListViewModelBase, IMapListViewModel
{
    /// <summary>オーディオ再生パネルのViewModel。</summary>
    public AudioPlayerPanelViewModel AudioPlayerPanel { get; }

    private bool _isNavigating;
    private readonly Subject<Beatmap> _generateBeatmapRequested = new();

    public Observable<Beatmap> GenerateBeatmapRequested => _generateBeatmapRequested;

    private readonly AudioPlayerViewModel _audioPlayer;
    private readonly IDatabaseService _databaseService;
    private readonly MapFilterViewModel _filterViewModel;

    /// <summary>基底の SourceBeatmapsRaw を MapList 公開 API として再エクスポートする alias。</summary>
    public Beatmap[] FilteredBeatmapsArray => SourceBeatmapsRaw;

    /// <summary>基底の PageCount を MapList 互換 API として再エクスポートする alias。</summary>
    public int FilteredPages => PageCount;

    public MapListViewModel(
        IDatabaseService databaseService,
        MapFilterViewModel filterViewModel,
        AudioPlayerViewModel audioPlayerViewModel,
        AudioPlayerPanelViewModel audioPlayerPanelViewModel,
        ISettingsService settingsService)
        : base(settingsService)
    {
        _databaseService = databaseService;
        _filterViewModel = filterViewModel;
        _audioPlayer = audioPlayerViewModel;
        AudioPlayerPanel = audioPlayerPanelViewModel;

        AudioPlayerPanel.SetNavigateCallback(NavigateToFilteredIndex);

        _filterViewModel.FilterChanged
            .Subscribe(_ => ApplyFilter())
            .AddTo(Disposables);

        // 譜面選択時にオーディオを再生
        this.ObserveProperty(nameof(SelectedBeatmap))
            .Subscribe(_ =>
            {
                if (SelectedBeatmap is null) return;
                if (_isNavigating) return;

                var index = FindBeatmapIndex(SelectedBeatmap.MD5Hash);
                AudioPlayerPanel.PlayBeatmap(SelectedBeatmap, index, SettingsData.AutoPlayOnSelect);
            })
            .AddTo(Disposables);

        // 基底 PageCount 変化を FilteredPages alias 名義でも再通知
        this.ObservePropertyAndSubscribe(
            nameof(PageCount),
            () => OnPropertyChanged(nameof(FilteredPages)),
            Disposables);

        // ソート/Mod/ScoreSystem/PreferUnicode 変更で SourceBeatmapsRaw が並び替わるため、
        // AudioPlayer の現在曲 (MD5) を保持したまま index を再同期する。
        // Skip(1) で起動時の即時発火を抑止 (AudioPlayer 未初期化状態での実行を防ぐ)。
        Observable.Merge(
                SortViewModel.SortChanged,
                this.ObserveProperty(nameof(SelectedModCategory)).Skip(1).Select(_ => Unit.Default),
                this.ObserveProperty(nameof(SelectedScoreSystemCategory)).Skip(1).Select(_ => Unit.Default),
                SettingsData.ObserveProperty(nameof(ISettingsData.PreferUnicode)).Skip(1).Select(_ => Unit.Default))
            .Subscribe(_ => AudioPlayerPanel.RefreshNavigationContextPreservingCurrent(SourceBeatmapsRaw))
            .AddTo(Disposables);
    }

    public void Initialize()
    {
        UpdateTotalCount();
        UpdateFilteredBeatmapsArray();
        AudioPlayerPanel.SetNavigationContext(SourceBeatmapsRaw, -1);
    }

    public void ApplyFilter()
    {
        UpdateFilteredBeatmapsArray();
        AudioPlayerPanel.SetNavigationContext(SourceBeatmapsRaw, -1);
    }

    private void UpdateTotalCount() => TotalCount = _databaseService.Beatmaps.Length;

    private void UpdateFilteredBeatmapsArray()
    {
        var allBeatmaps = _databaseService.Beatmaps.AsValueEnumerable();
        var filtered = allBeatmaps
            .Where(x => _filterViewModel.Matches(x))
            .ToArray();

        SetSourceBeatmaps(filtered);
    }

    /// <summary>
    /// FilteredBeatmapsArray 内の指定インデックスにナビゲートする。
    /// AudioPlayerPanelViewModelの _navigateCallback から呼び出される。
    /// </summary>
    public void NavigateToFilteredIndex(int filteredIndex)
    {
        var arr = SourceBeatmapsRaw;
        if (filteredIndex < 0 || filteredIndex >= arr.Length) return;

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
        var arr = SourceBeatmapsRaw;
        for (var i = 0; i < arr.Length; i++)
        {
            if (arr[i].MD5Hash == md5Hash)
                return i;
        }
        return -1;
    }

    public override void SelectBeatmapForContextMenu(Beatmap beatmap)
    {
        _isNavigating = true;
        try
        {
            base.SelectBeatmapForContextMenu(beatmap);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    protected override void OnGenerateBeatmap(Beatmap target)
    {
        _generateBeatmapRequested.OnNext(target);
    }

    protected override bool CanGenerateBeatmapFromContextMenu => true;

    public override void Dispose()
    {
        _generateBeatmapRequested.OnCompleted();
        _generateBeatmapRequested.Dispose();
        _audioPlayer.Dispose();
        AudioPlayerPanel.Dispose();
        base.Dispose();
    }
}
