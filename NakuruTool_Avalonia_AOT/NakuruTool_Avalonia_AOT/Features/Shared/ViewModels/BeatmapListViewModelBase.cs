using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.MapList.Sorting;
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
    private readonly IDatabaseService? _databaseService;
    private readonly AvaloniaList<Beatmap> _showBeatmapsList = new();
    private readonly AvaloniaList<LocalRankingEntry> _localRankingList = new();
    /// <summary>
    /// ローカルランキング対象譜面の MD5。ページ再投影で SelectedBeatmap が一時的に null になっても
    /// Mod / ScoreSystem 切替に追従できるよう、最後に選択された譜面を保持する。
    /// </summary>
    private string? _localRankingTargetMd5;
    private Beatmap[] _sourceBeatmaps = Array.Empty<Beatmap>();
    /// <summary>SetSourceBeatmaps で受け取った未ソートの基準配列。ApplyCurrentSort はここからコピーして再ソートする。</summary>
    private Beatmap[] _unsortedBeatmaps = Array.Empty<Beatmap>();
    private Beatmap? _contextMenuBeatmap;

    [ObservableProperty]
    public partial IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; set; } = new AvaloniaList<Beatmap>();

    [ObservableProperty]
    public partial Beatmap? SelectedBeatmap { get; set; }

    [ObservableProperty]
    public partial ModCategory SelectedModCategory { get; set; } = ModCategory.NoMod;

    [ObservableProperty]
    public partial ScoreSystemCategory SelectedScoreSystemCategory { get; set; } = ScoreSystemCategory.Default;

    /// <summary>
    /// ローカルランキング表示モード。ON の間だけ選択譜面のランキングを生成する（永続化しない）。
    /// </summary>
    [ObservableProperty]
    public partial bool IsLocalRankingMode { get; set; }

    /// <summary>
    /// 7K 解析結果の表示モード（Prism 列・段位推定列を出し、履歴・プレイ済み列を隠す）。
    /// ローカルランキング表示とは排他（永続化しない）。
    /// </summary>
    [ObservableProperty]
    public partial bool IsAnalysisMode { get; set; }

    /// <summary>選択譜面のローカルランキング（選択中 ScoreSystem / Mod で絞り込み、スコア降順）。</summary>
    public IAvaloniaReadOnlyList<LocalRankingEntry> LocalRanking => _localRankingList;

    /// <summary><see cref="LocalRanking"/> が1件以上あるか。</summary>
    [ObservableProperty]
    public partial bool HasLocalRanking { get; set; }

    /// <summary>Prism 列を基準スキル値の横棒で表示する（false はジャンル濃度の六角形）。設定に保存する。</summary>
    [ObservableProperty]
    public partial bool ShowGenreSkillValues { get; set; }

    /// <summary>7K 解析の進み具合（解析中だけ値がある）</summary>
    [ObservableProperty]
    public partial Analyze7kProgressDisplay? Analyze7kStatus { get; set; }

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

    /// <summary>ソート設定 ViewModel。Dispose は基底の <c>Disposables</c> 経由で連鎖する。</summary>
    internal MapListSortViewModel SortViewModel { get; }

    /// <param name="settingsService">設定サービス</param>
    /// <param name="databaseService">ローカルスコア参照用。null の場合は BestPlay / ローカルランキングを生成しない。</param>
    protected BeatmapListViewModelBase(ISettingsService settingsService, IDatabaseService? databaseService = null)
    {
        SortViewModel = new MapListSortViewModel();
        Disposables.Add(SortViewModel);

        _settingsService = settingsService;
        _databaseService = databaseService;
        ShowBeatmaps = _showBeatmapsList;
        // 既定は基準スキル値（設定はジャンル濃度を選んだときだけ true）
        ShowGenreSkillValues = !_settingsService.SettingsData.Analyze7kShowConcentration;

        _settingsService.SettingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.PreferUnicode),
            ApplyCurrentSort,
            Disposables);

        SortViewModel.SortChanged
            .Subscribe(_ => ApplyCurrentSort())
            .AddTo(Disposables);
    }

    partial void OnShowGenreSkillValuesChanged(bool value)
    {
        if (_settingsService.SettingsData is SettingsData settings && settings.Analyze7kShowConcentration == value)
        {
            settings.Analyze7kShowConcentration = !value;
            _settingsService.SaveSettings(settings);
        }
    }

    /// <summary>派生から参照可能な現在のソース配列。</summary>
    protected Beatmap[] SourceBeatmapsRaw => _sourceBeatmaps;

    /// <summary>派生から参照可能な設定データ。</summary>
    protected ISettingsData SettingsData => _settingsService.SettingsData;

    /// <summary>
    /// 派生で生成したベース譜面配列を差し替える。
    /// FilteredCount を更新し、CurrentPage を 1 にリセット (既に 1 なら再投影のみ実行)。
    /// 配列長が変わるタイミングでのみ <c>_sourceBeatmaps</c> を再確保する。
    /// </summary>
    protected void SetSourceBeatmaps(Beatmap[] source)
    {
        _unsortedBeatmaps = source ?? Array.Empty<Beatmap>();
        _sourceBeatmaps = new Beatmap[_unsortedBeatmaps.Length];
        RefillAndSortInPlace();

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
    /// 並び・件数を変えずに、指定 MD5 の譜面レコードを最新のものへ差し替える（7K 解析結果の反映用）。
    /// 配列インスタンスは維持する。表示中のページに差し替えがあれば再投影して true を返す。
    /// </summary>
    protected bool ReplaceRecordsInPlace(IReadOnlySet<string> changedMd5, Func<string, Beatmap?> lookup)
    {
        static void Replace(Beatmap[] array, IReadOnlySet<string> changed, Func<string, Beatmap?> lookup)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (changed.Contains(array[i].MD5Hash) && lookup(array[i].MD5Hash) is { } latest)
                    array[i] = latest;
            }
        }

        Replace(_unsortedBeatmaps, changedMd5, lookup);
        Replace(_sourceBeatmaps, changedMd5, lookup);

        var size = Math.Max(1, PageSize);
        var skip = (CurrentPage - 1) * size;
        var end = Math.Min(_sourceBeatmaps.Length, skip + size);
        for (int i = Math.Max(0, skip); i < end; i++)
        {
            if (changedMd5.Contains(_sourceBeatmaps[i].MD5Hash))
            {
                UpdateShowBeatmaps();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ベース譜面配列を差し替えるが、表示中のページはできるだけ維持する（7K 解析結果の反映用）。
    /// ページ数が減った場合だけ最終ページに寄せる。
    /// </summary>
    protected void ReplaceSourceBeatmapsPreservingPage(Beatmap[] source)
    {
        _unsortedBeatmaps = source ?? Array.Empty<Beatmap>();
        if (_sourceBeatmaps.Length != _unsortedBeatmaps.Length)
        {
            _sourceBeatmaps = new Beatmap[_unsortedBeatmaps.Length];
        }
        RefillAndSortInPlace();

        FilteredCount = _sourceBeatmaps.Length;
        RecalculatePageCount();
        if (CurrentPage > PageCount)
        {
            CurrentPage = PageCount;
        }
        else
        {
            UpdateShowBeatmaps();
        }
    }

    /// <summary>
    /// 現在のソート条件で _sourceBeatmaps の中身を再構築し、表示を更新する。
    /// 配列インスタンスは維持し、外部 (AudioPlayer 等) が保持している参照の整合性を保つ。
    /// </summary>
    protected void ApplyCurrentSort()
    {
        // 万一長さがずれている場合のフォールバック (理論上 _unsortedBeatmaps と _sourceBeatmaps は同長)
        if (_sourceBeatmaps.Length != _unsortedBeatmaps.Length)
        {
            _sourceBeatmaps = new Beatmap[_unsortedBeatmaps.Length];
        }
        RefillAndSortInPlace();
        UpdateShowBeatmaps();
    }

    /// <summary>
    /// _sourceBeatmaps の中身を _unsortedBeatmaps からコピーし直し、active rule があれば in-place ソートする。
    /// 配列インスタンス自体は維持するため、外部 (AudioPlayer 等) が保持している参照の整合性を保つ。
    /// 長さが一致しない場合のみ呼び出し側で再確保すること。
    /// </summary>
    private void RefillAndSortInPlace()
    {
        Array.Copy(_unsortedBeatmaps, _sourceBeatmaps, _unsortedBeatmaps.Length);

        var activeRules = SortViewModel.GetActiveRules();
        if (activeRules.Length == 0)
        {
            return;
        }

        var comparer = new BeatmapMultiComparer(
            activeRules,
            SettingsData.PreferUnicode,
            SelectedScoreSystemCategory,
            SelectedModCategory);
        Array.Sort(_sourceBeatmaps, comparer);
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
                    Grade = beatmap.GetGrade(scoreSystem, mod),
                    BestPlay = _databaseService is null
                        ? null
                        : FindBestPlay(_databaseService.GetLocalScores(beatmap.MD5Hash), scoreSystem, mod)
                };
                _showBeatmapsList.Add(displayed);
            }
        }
    }

    /// <summary>
    /// ReplayScore 降順のローカルスコアから、指定 ScoreSystem / Mod に一致する最初の1件（=最高スコア）を返す。
    /// </summary>
    internal static LocalRankingEntry? FindBestPlay(
        ReadOnlySpan<LocalScore> scores,
        ScoreSystemCategory scoreSystem,
        ModCategory mod)
    {
        foreach (ref readonly var score in scores)
        {
            if (IsMatch(score, scoreSystem, mod))
                return new LocalRankingEntry(score);
        }
        return null;
    }

    /// <summary>
    /// ReplayScore 降順のローカルスコアから、指定 ScoreSystem / Mod に一致するものを順序を保って <paramref name="destination"/> に追加する。
    /// </summary>
    internal static void FillLocalRanking(
        ReadOnlySpan<LocalScore> scores,
        ScoreSystemCategory scoreSystem,
        ModCategory mod,
        IList<LocalRankingEntry> destination)
    {
        foreach (ref readonly var score in scores)
        {
            if (IsMatch(score, scoreSystem, mod))
                destination.Add(new LocalRankingEntry(score));
        }
    }

    /// <summary>
    /// 選択譜面のローカルランキングを再構築する。モード OFF の間は空にして生成しない。
    /// AvaloniaList インスタンスは差し替えず Clear/AddRange でのみ更新する。
    /// </summary>
    private void UpdateLocalRanking()
    {
        _localRankingList.Clear();

        if (IsLocalRankingMode &&
            _databaseService is not null &&
            !string.IsNullOrEmpty(_localRankingTargetMd5))
        {
            var buffer = new List<LocalRankingEntry>();
            FillLocalRanking(
                _databaseService.GetLocalScores(_localRankingTargetMd5),
                SelectedScoreSystemCategory,
                SelectedModCategory,
                buffer);
            _localRankingList.AddRange(buffer);
        }

        HasLocalRanking = _localRankingList.Count > 0;
    }

    private static bool IsMatch(in LocalScore score, ScoreSystemCategory scoreSystem, ModCategory mod)
        => OsuMods.CategorizeScoreSystem(score.Mods) == scoreSystem
           && OsuMods.Categorize(score.Mods) == mod;

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

    partial void OnSelectedModCategoryChanged(ModCategory value)
    {
        ApplyCurrentSort();
        UpdateLocalRanking();
    }

    partial void OnSelectedScoreSystemCategoryChanged(ScoreSystemCategory value)
    {
        ApplyCurrentSort();
        UpdateLocalRanking();
    }

    partial void OnIsLocalRankingModeChanged(bool value)
    {
        if (value)
            IsAnalysisMode = false;
        UpdateLocalRanking();
    }

    partial void OnIsAnalysisModeChanged(bool value)
    {
        if (value)
            IsLocalRankingMode = false;
    }

    partial void OnSelectedBeatmapChanged(Beatmap? value)
    {
        // ページ再投影による一時的な null では対象を維持する
        if (value is null || value.MD5Hash == _localRankingTargetMd5)
            return;

        _localRankingTargetMd5 = value.MD5Hash;
        UpdateLocalRanking();
    }

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

    [RelayCommand(CanExecute = nameof(CanOpenOfficialPage))]
    private void OpenOfficialPage()
    {
        if (_contextMenuBeatmap is { BeatmapSetId: > 0 } target)
        {
            OnOpenOfficialPage(target.BeatmapSetId);
        }
    }

    private bool CanOpenOfficialPage() =>
        _contextMenuBeatmap is { BeatmapSetId: > 0 };

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

    /// <summary>公式 beatmapset ページを開く。既定は既定のブラウザで開く (テストで差し替え可能)。</summary>
    protected virtual void OnOpenOfficialPage(int beatmapSetId)
    {
        OsuWebPageLauncher.OpenBeatmapSetPage(beatmapSetId);
    }

    /// <summary>譜面生成リクエストの派生差分。既定は No-op。</summary>
    protected virtual void OnGenerateBeatmap(Beatmap target)
    {
    }

    /// <summary>ContextMenu 経由で生成可能か。既定は false (生成ページ自身では生成不可)。</summary>
    protected virtual bool CanGenerateBeatmapFromContextMenu => false;

    // ---- public API (IBeatmapListViewModel) ----

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

        OpenOfficialPageCommand.NotifyCanExecuteChanged();
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
        OpenOfficialPageCommand.NotifyCanExecuteChanged();
        OpenInExplorerCommand.NotifyCanExecuteChanged();
        GenerateBeatmapCommand.NotifyCanExecuteChanged();
    }

    // ---- IBeatmapListViewModel 明示実装ブリッジ ----

    IRelayCommand IBeatmapListViewModel.OpenOfficialPageCommand => OpenOfficialPageCommand;
    IRelayCommand IBeatmapListViewModel.OpenInExplorerCommand => OpenInExplorerCommand;
    IRelayCommand IBeatmapListViewModel.GenerateBeatmapCommand => GenerateBeatmapCommand;
}
