using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public partial class MapListView : UserControl
{
    public static readonly StyledProperty<bool> ShowAudioPlayerProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowAudioPlayer), defaultValue: true);

    public static readonly StyledProperty<bool> ShowTotalCountProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowTotalCount), defaultValue: true);

    public static readonly StyledProperty<bool> ShowFilteredCountProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowFilteredCount), defaultValue: true);

    public static readonly StyledProperty<bool> ShowResolvedCountProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowResolvedCount), defaultValue: false);

    public static readonly StyledProperty<bool> ShowHistoryColumnProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowHistoryColumn), defaultValue: true);

    public static readonly StyledProperty<bool> ShowIsPlayedColumnProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowIsPlayedColumn), defaultValue: true);

    public static readonly StyledProperty<string?> ScoreSystemGroupNameProperty =
        AvaloniaProperty.Register<MapListView, string?>(nameof(ScoreSystemGroupName), defaultValue: null);

    public static readonly StyledProperty<string?> ModGroupNameProperty =
        AvaloniaProperty.Register<MapListView, string?>(nameof(ModGroupName), defaultValue: null);

    public static readonly StyledProperty<double> DataGridRowHeightProperty =
        AvaloniaProperty.Register<MapListView, double>(nameof(DataGridRowHeight), defaultValue: 100d);

    public static readonly StyledProperty<bool> ShowSortButtonProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowSortButton), defaultValue: false);

    public static readonly StyledProperty<bool> ShowLocalRankingButtonProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowLocalRankingButton), defaultValue: true);

    public static readonly StyledProperty<bool> ShowAnalyze7kColumnsProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowAnalyze7kColumns), defaultValue: true);

    /// <summary>Prism 列を基準スキル値で描くか（ViewModel の値を写す。セルからの参照用）</summary>
    public static readonly StyledProperty<bool> ShowGenreSkillValuesProperty =
        AvaloniaProperty.Register<MapListView, bool>(nameof(ShowGenreSkillValues));

    public static readonly StyledProperty<ICommand?> SortCommandProperty =
        AvaloniaProperty.Register<MapListView, ICommand?>(nameof(SortCommand), defaultValue: null);

    public bool ShowAudioPlayer
    {
        get => GetValue(ShowAudioPlayerProperty);
        set => SetValue(ShowAudioPlayerProperty, value);
    }

    public bool ShowTotalCount
    {
        get => GetValue(ShowTotalCountProperty);
        set => SetValue(ShowTotalCountProperty, value);
    }

    public bool ShowFilteredCount
    {
        get => GetValue(ShowFilteredCountProperty);
        set => SetValue(ShowFilteredCountProperty, value);
    }

    public bool ShowResolvedCount
    {
        get => GetValue(ShowResolvedCountProperty);
        set => SetValue(ShowResolvedCountProperty, value);
    }

    public bool ShowHistoryColumn
    {
        get => GetValue(ShowHistoryColumnProperty);
        set => SetValue(ShowHistoryColumnProperty, value);
    }

    public bool ShowIsPlayedColumn
    {
        get => GetValue(ShowIsPlayedColumnProperty);
        set => SetValue(ShowIsPlayedColumnProperty, value);
    }

    public string? ScoreSystemGroupName
    {
        get => GetValue(ScoreSystemGroupNameProperty);
        set => SetValue(ScoreSystemGroupNameProperty, value);
    }

    public string? ModGroupName
    {
        get => GetValue(ModGroupNameProperty);
        set => SetValue(ModGroupNameProperty, value);
    }

    public double DataGridRowHeight
    {
        get => GetValue(DataGridRowHeightProperty);
        set => SetValue(DataGridRowHeightProperty, value);
    }

    public bool ShowSortButton
    {
        get => GetValue(ShowSortButtonProperty);
        set => SetValue(ShowSortButtonProperty, value);
    }

    /// <summary>ローカルランキング切替ボタンを表示するか（beatmap 生成ページでは非表示）。</summary>
    public bool ShowLocalRankingButton
    {
        get => GetValue(ShowLocalRankingButtonProperty);
        set => SetValue(ShowLocalRankingButtonProperty, value);
    }

    /// <summary>7K の解析結果ボタン（Prism 列・段位推定列）と表示切替を出すか（beatmap 生成ページでは非表示）。</summary>
    public bool ShowAnalyze7kColumns
    {
        get => GetValue(ShowAnalyze7kColumnsProperty);
        set => SetValue(ShowAnalyze7kColumnsProperty, value);
    }

    public bool ShowGenreSkillValues
    {
        get => GetValue(ShowGenreSkillValuesProperty);
        private set => SetValue(ShowGenreSkillValuesProperty, value);
    }

    public ICommand? SortCommand
    {
        get => GetValue(SortCommandProperty);
        set => SetValue(SortCommandProperty, value);
    }

    private Beatmap? _rightClickTarget;
    private IBeatmapListViewModel? _boundViewModel;
    private IDisposable? _scrollRequestSubscription;
    private IDisposable? _localRankingModeSubscription;
    private IDisposable? _genreSkillModeSubscription;
    private IDisposable? _analysisModeSubscription;
    private bool _isAnalysisMode;
    private bool _isLocalRankingMode;

    public MapListView()
    {
        InitializeComponent();

        MapListDataGrid.AddHandler(
            InputElement.PointerPressedEvent,
            OnDataGridPointerPressed,
            RoutingStrategies.Tunnel);

        ApplyColumnVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowHistoryColumnProperty ||
            change.Property == ShowIsPlayedColumnProperty ||
            change.Property == ShowAnalyze7kColumnsProperty)
        {
            ApplyColumnVisibility();
        }
    }

    /// <summary>
    /// DataGrid 列はビジュアルツリー外でバインドできないため、Tag で引いて IsVisible を直接設定する。
    /// <list type="bullet">
    /// <item>ローカルランキングモード: BPM / LN率 / 履歴 / プレイ済み を隠し、空いた幅にランキング用 DataGrid を出す。</item>
    /// <item>解析結果モード: 履歴 / プレイ済み を隠し、Prism 列・段位推定列を出す（ランキングとは排他）。</item>
    /// </list>
    /// </summary>
    private void ApplyColumnVisibility()
    {
        var ranking = _isLocalRankingMode;
        var analysis = ShowAnalyze7kColumns && _isAnalysisMode && !ranking;
        SetColumnVisible("BpmColumn", !ranking);
        SetColumnVisible("LongNoteRateColumn", !ranking);
        SetColumnVisible("PrismColumn", analysis);
        SetColumnVisible("DanEstimateColumn", analysis);
        SetColumnVisible("HistoryColumn", ShowHistoryColumn && !ranking && !analysis);
        SetColumnVisible("IsPlayedColumn", ShowIsPlayedColumn && !ranking && !analysis);
        if (Analyze7kHeaderPanel is not null)
            Analyze7kHeaderPanel.IsVisible = ShowAnalyze7kColumns;
        if (GenreDisplayModePanel is not null)
            GenreDisplayModePanel.IsVisible = analysis;
    }

    private void SetColumnVisible(string tag, bool visible)
    {
        var column = FindColumnByTag(tag);
        if (column is not null)
        {
            column.IsVisible = visible;
        }
    }

    private DataGridColumn? FindColumnByTag(string tag)
    {
        if (MapListDataGrid is null)
            return null;

        foreach (var column in MapListDataGrid.Columns)
        {
            if (column.Tag is string s && s == tag)
                return column;
        }

        return null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        BindToDataContext();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // NavigationPage のページ切り替えで Detach → 再 Attach された場合は DataContext が変わらないため、
        // OnDetachedFromVisualTree で解除したバインドをここで張り直す
        if (_boundViewModel is null && DataContext is not null)
        {
            BindToDataContext();
        }
    }

    private void BindToDataContext()
    {
        _boundViewModel?.ClearContextMenuBeatmap();

        ClearViewModelBindings();

        _boundViewModel = DataContext as IBeatmapListViewModel;
        if (_boundViewModel is not null)
        {
            if (OpenOfficialPageMenuItem is not null)
                OpenOfficialPageMenuItem.Command = _boundViewModel.OpenOfficialPageCommand;
            if (OpenMenuItem is not null)
                OpenMenuItem.Command = _boundViewModel.OpenInExplorerCommand;
            if (GenerateMenuItem is not null)
                GenerateMenuItem.Command = _boundViewModel.GenerateBeatmapCommand;

            var vm = _boundViewModel;
            _localRankingModeSubscription = vm
                .ObserveProperty(nameof(IBeatmapListViewModel.IsLocalRankingMode))
                .Subscribe(_ => UpdateLocalRankingMode(vm.IsLocalRankingMode));
            _genreSkillModeSubscription = vm
                .ObserveProperty(nameof(IBeatmapListViewModel.ShowGenreSkillValues))
                .Subscribe(_ => ShowGenreSkillValues = vm.ShowGenreSkillValues);
            _analysisModeSubscription = vm
                .ObserveProperty(nameof(IBeatmapListViewModel.IsAnalysisMode))
                .Subscribe(_ => UpdateAnalysisMode(vm.IsAnalysisMode));
        }
        ShowGenreSkillValues = _boundViewModel?.ShowGenreSkillValues ?? false;
        UpdateAnalysisMode(_boundViewModel?.IsAnalysisMode ?? false);

        UpdateLocalRankingMode(_boundViewModel?.IsLocalRankingMode ?? false);

        // AudioPlayerHost は MapListViewModel 固有 (AudioPlayerPanel) のためここで個別分岐
        if (DataContext is MapListViewModel mapListVm)
        {
            if (AudioPlayerHost is not null)
                AudioPlayerHost.DataContext = mapListVm.AudioPlayerPanel;

            // オーディオプレイヤーの次曲/前曲で選択された譜面をリストの表示範囲までスクロールする
            _scrollRequestSubscription = mapListVm.ScrollToBeatmapRequested
                .Subscribe(ScheduleScrollIntoView);
        }
    }

    private void UpdateAnalysisMode(bool isAnalysisMode)
    {
        if (_isAnalysisMode == isAnalysisMode)
            return;

        _isAnalysisMode = isAnalysisMode;
        ApplyColumnVisibility();
    }

    private void UpdateLocalRankingMode(bool isLocalRankingMode)
    {
        if (_isLocalRankingMode == isLocalRankingMode)
            return;

        _isLocalRankingMode = isLocalRankingMode;
        ApplyColumnVisibility();
    }

    /// <summary>
    /// ページ切り替え直後は DataGrid の行がまだ生成されていないため、
    /// レイアウト更新後に ScrollIntoView を実行する。
    /// </summary>
    private void ScheduleScrollIntoView(Beatmap beatmap)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (MapListDataGrid is null) return;
            // 遅延実行までに選択が変わっていたらスクロールしない
            if (!ReferenceEquals(MapListDataGrid.SelectedItem, beatmap)) return;

            MapListDataGrid.ScrollIntoView(beatmap, null);
        }, DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _boundViewModel?.ClearContextMenuBeatmap();

        ClearViewModelBindings();

        _boundViewModel = null;
        _rightClickTarget = null;
    }

    private void ClearViewModelBindings()
    {
        _scrollRequestSubscription?.Dispose();
        _scrollRequestSubscription = null;
        _localRankingModeSubscription?.Dispose();
        _localRankingModeSubscription = null;
        _genreSkillModeSubscription?.Dispose();
        _genreSkillModeSubscription = null;
        _analysisModeSubscription?.Dispose();
        _analysisModeSubscription = null;

        if (AudioPlayerHost is not null)
            AudioPlayerHost.DataContext = null;
        if (OpenOfficialPageMenuItem is not null)
            OpenOfficialPageMenuItem.Command = null;
        if (OpenMenuItem is not null)
            OpenMenuItem.Command = null;
        if (GenerateMenuItem is not null)
            GenerateMenuItem.Command = null;
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(MapListDataGrid).Properties.IsRightButtonPressed)
            return;

        if (_boundViewModel is null)
            return;

        var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>();
        _rightClickTarget = row?.DataContext as Beatmap;

        if (_rightClickTarget is not null)
        {
            _boundViewModel.SelectBeatmapForContextMenu(_rightClickTarget);
        }
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (_rightClickTarget is null ||
            _boundViewModel is null ||
            !_boundViewModel.TryPrepareContextMenu(_rightClickTarget))
        {
            e.Cancel = true;
        }
    }

    private void OnContextMenuClosing(object? sender, CancelEventArgs e)
    {
        _boundViewModel?.ClearContextMenuBeatmap();
    }
}
