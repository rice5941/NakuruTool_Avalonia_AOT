using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.Licenses;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.Scaling;
using R3;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowView : Window
{
    /// <summary>
    /// 生成済みページのキャッシュ。ページ切り替えで状態（フィルタ・スクロール位置など）を失わないよう再利用する。
    /// </summary>
    private readonly Dictionary<MainPageKind, ContentPage> _pages = [];

    /// <summary>
    /// 各ページを重ねて配置するパネル。NavigationPage のルートページ（<see cref="_hostPage"/>）の中身。
    /// ページの差し替え（Replace）ではなく IsVisible の切り替えで表示ページを変えるため、
    /// 非表示中もページは Visual Tree に接続されたままになる。
    /// （差し替えると DataGrid が再接続時に行を作り直して選択を一度解除し、
    ///   選択中の譜面が「再選択」扱いになって再生中の曲が先頭から始まる・スクロール位置が失われるため）
    /// </summary>
    private readonly Panel _pageHost = new();

    private readonly ContentPage _hostPage;
    private bool _isHostPagePushed;

    private CompositeDisposable _subscriptions = [];
    private MainWindowViewModel? _viewModel;

    /// <summary>ListBox の選択をコードから同期している間は SelectionChanged を無視する</summary>
    private bool _isSyncingMenu;

    /// <summary>現在表示しているページ（テスト用）</summary>
    internal ContentPage? CurrentContentPage { get; private set; }

    /// <summary>
    /// 音楽プレイヤーモードへ切り替えたときのウィンドウ幅（開発時の大きさ = UI 表示倍率 125%、Scale 1.0 のとき）。
    /// オーディオパネルの音量スライダーまで切れずに収まる幅（約1030）を切りのいい値に丸めたもの。
    /// 実際の幅はこれに <see cref="UiScaleService.Scale"/> を掛けた値になる。
    /// </summary>
    public const double MusicPlayerModeWidth = 1050;

    /// <summary>ウィンドウに音楽プレイヤーモードのサイズ設定を適用済みか</summary>
    private bool _isMusicPlayerModeApplied;

    /// <summary>音楽プレイヤーモードのウィンドウサイズを決めたときの UI 表示倍率</summary>
    private double _musicPlayerModeScale = 1.0;

    // XAML で指定した最小サイズ（開発時の大きさ = Scale 1.0 のときの値）。実際の最小サイズはこれに Scale を掛けた値になる
    private readonly double _baseMinWidth;
    private readonly double _baseMinHeight;

    // 音楽プレイヤーモードへ切り替える直前のウィンドウ状態（通常モードへ戻すときに復元する）
    private WindowState _savedWindowState = WindowState.Normal;
    private double _savedMinHeight;
    private double _savedMaxHeight = double.PositiveInfinity;
    private bool _savedCanMaximize = true;

    /// <summary>
    /// 通常モードかつ通常状態（最大化・最小化していない）のときの最新のウィンドウサイズ。
    /// 音楽プレイヤーモード中のリサイズや最大化時のサイズでは更新しないため、
    /// 通常モードへ戻すときにこのサイズへ復元できる。
    /// </summary>
    private Size? _normalModeSize;

    public MainWindowView()
    {
        _hostPage = new ContentPage { Content = _pageHost };
        NavigationPage.SetHasNavigationBar(_hostPage, false);
        NavigationPage.SetHasBackButton(_hostPage, false);

        InitializeComponent();
        Loaded += OnLoaded;
        Resized += OnWindowResized;

        _baseMinWidth = MinWidth;
        _baseMinHeight = MinHeight;
        ApplyScaledMinSize();
        UiScaleService.Instance.ScaleChanged += OnUiScaleChanged;
    }

    /// <summary>
    /// 最小サイズを UI 表示倍率に合わせる（縮小表示なら、同じ中身が収まる分だけウィンドウも小さくできる）。
    /// 音楽プレイヤーモード中は高さを固定しているため、解除時に戻す最小の高さだけを更新する。
    /// </summary>
    private void ApplyScaledMinSize()
    {
        var scale = UiScaleService.Instance.Scale;
        MinWidth = _baseMinWidth * scale;
        if (_isMusicPlayerModeApplied)
        {
            _savedMinHeight = _baseMinHeight * scale;
        }
        else
        {
            MinHeight = _baseMinHeight * scale;
        }
    }

    /// <summary>
    /// UI 表示倍率が変わったとき、最小サイズを合わせ直し、音楽プレイヤーモード中ならウィンドウサイズも新しい倍率に合わせ直す。
    /// （設定画面で倍率を変えた場合や、自動倍率でメインモニターの解像度・拡大率が変わった場合。通常モードはレイアウトが追従するのでウィンドウサイズは変えない）
    /// </summary>
    private void OnUiScaleChanged(object? sender, EventArgs e)
    {
        ApplyScaledMinSize();

        if (!_isMusicPlayerModeApplied)
        {
            return;
        }

        var scale = UiScaleService.Instance.Scale;
        var width = ClientSize.Width > 0
            ? ClientSize.Width * scale / _musicPlayerModeScale
            : MusicPlayerModeWidth * scale;
        _musicPlayerModeScale = scale;

        // 高さの固定を外して中身に合わせ直し、レイアウト確定後に再び固定する
        Width = width;
        MinHeight = 0;
        MaxHeight = double.PositiveInfinity;
        SizeToContent = SizeToContent.Height;
        Dispatcher.UIThread.Post(LockMusicPlayerHeight, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// ウィンドウサイズ変更時に、通常モードのサイズを記録する。
    /// 最大化時は WM_SIZE 処理の途中で WindowState の更新前にリサイズが通知されることがあるため、
    /// 状態が確定した後（Dispatcher の後続ジョブ）で判定する。
    /// </summary>
    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        Dispatcher.UIThread.Post(RecordNormalModeSize, DispatcherPriority.Background);
    }

    private void RecordNormalModeSize()
    {
        if (_isMusicPlayerModeApplied || WindowState != WindowState.Normal)
        {
            return;
        }

        var size = ClientSize;
        if (size.Width > 0 && size.Height > 0)
        {
            _normalModeSize = size;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // 音楽プレイヤーモード中は最大化・全画面を許可しない（タイトルバーのダブルクリックや Win+↑ などの経路も含めて戻す）
        if (change.Property == WindowStateProperty
            && _isMusicPlayerModeApplied
            && change.GetNewValue<WindowState>() is WindowState.Maximized or WindowState.FullScreen)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isMusicPlayerModeApplied && WindowState is WindowState.Maximized or WindowState.FullScreen)
                {
                    WindowState = WindowState.Normal;
                }
            });
        }
    }

    public MainWindowView(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;

        // ウィンドウが開いたときにデータベース読み込みを開始
        Opened += async (_, _) =>
        {
            await viewModel.StartLoadingAsync();
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _subscriptions.Dispose();
        _subscriptions = [];
        _pages.Clear();
        _pageHost.Children.Clear();
        CurrentContentPage = null;
        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is null)
        {
            return;
        }

        // ViewModel 側からのページ切り替え（DB 読み込みエラー時の Settings 遷移など）に追従する
        _viewModel
            .ObserveProperty(nameof(MainWindowViewModel.SelectedPage))
            .Subscribe(args => Dispatcher.UIThread.Post(() => { _ = NavigateToSelectedPageAsync(); }))
            .AddTo(_subscriptions);

        // 音楽プレイヤーモードの切り替えに合わせてウィンドウサイズを変更する
        var audioPanel = _viewModel.AudioPlayerPanelViewModel;
        audioPanel
            .ObserveProperty(nameof(AudioPlayerPanelViewModel.IsMusicPlayerMode))
            .Subscribe(_ => Dispatcher.UIThread.Post(() => ApplyMusicPlayerMode(audioPanel.IsMusicPlayerMode)))
            .AddTo(_subscriptions);

        if (IsLoaded)
        {
            _ = NavigateToSelectedPageAsync();
        }

        if (audioPanel.IsMusicPlayerMode != _isMusicPlayerModeApplied)
        {
            Dispatcher.UIThread.Post(() => ApplyMusicPlayerMode(audioPanel.IsMusicPlayerMode));
        }
    }

    /// <summary>
    /// 音楽プレイヤーモードの ON/OFF に合わせてウィンドウサイズを切り替える。
    /// ON: 最大化を禁止し、高さをオーディオパネルに合わせて固定して幅のみリサイズ可能にする。
    /// OFF: 音楽プレイヤーモード中にリサイズしても、切り替え前の通常モードのサイズ・最大化状態に戻す。
    /// </summary>
    private void ApplyMusicPlayerMode(bool enabled)
    {
        if (enabled == _isMusicPlayerModeApplied)
        {
            return;
        }

        if (enabled)
        {
            // 通常状態ならこの時点のサイズが通常モードのサイズ。
            // 最大化中は最大化前に記録しておいた通常状態のサイズ（_normalModeSize）をそのまま使う。
            if (WindowState == WindowState.Normal)
            {
                RecordNormalModeSize();
            }

            _isMusicPlayerModeApplied = true;
            _savedWindowState = WindowState;
            if (WindowState != WindowState.Normal)
            {
                // 最大化・最小化中はサイズ指定が効かないため通常状態に戻してから縮小する
                WindowState = WindowState.Normal;
            }

            _savedMinHeight = MinHeight;
            _savedMaxHeight = MaxHeight;
            _savedCanMaximize = CanMaximize;

            // 最大化ボタンを非活性にする
            CanMaximize = false;

            // 幅はプレイヤーモード用の既定値（を UI 表示倍率に合わせた値）にする（モード中はユーザーが変更可能）
            _musicPlayerModeScale = UiScaleService.Instance.Scale;
            Width = MusicPlayerModeWidth * _musicPlayerModeScale;

            MinHeight = 0;
            MaxHeight = double.PositiveInfinity;
            SizeToContent = SizeToContent.Height;

            // レイアウト確定後の高さで固定し、幅だけリサイズできるようにする
            Dispatcher.UIThread.Post(LockMusicPlayerHeight, DispatcherPriority.Loaded);
        }
        else
        {
            _isMusicPlayerModeApplied = false;

            SizeToContent = SizeToContent.Manual;
            MaxHeight = _savedMaxHeight;
            MinHeight = _savedMinHeight;
            CanMaximize = _savedCanMaximize;

            // 通常状態のまま通常モードのサイズに戻す（最大化はその後に行い、最大化解除時の復元サイズもこのサイズにする）
            if (_normalModeSize is { } size)
            {
                Width = size.Width;
                Height = size.Height;
            }

            if (_savedWindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
    }

    private void LockMusicPlayerHeight()
    {
        if (!_isMusicPlayerModeApplied)
        {
            return;
        }

        var height = ClientSize.Height;
        if (height <= 0)
        {
            return;
        }

        MinHeight = height;
        MaxHeight = height;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _ = NavigateToSelectedPageAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        UiScaleService.Instance.ScaleChanged -= OnUiScaleChanged;
        _subscriptions.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// サイドバー項目の選択に応じて ViewModel の表示ページを更新する
    /// </summary>
    private void OnMenuSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingMenu || _viewModel is null || sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.SelectedItem is ListBoxItem { Tag: MainPageKind page })
        {
            _viewModel.SelectedPage = page;
        }
    }

    /// <summary>
    /// ViewModel の SelectedPage に合わせてサイドバーの選択と表示ページを揃える
    /// </summary>
    private async Task NavigateToSelectedPageAsync()
    {
        if (_viewModel is null || !IsLoaded)
        {
            return;
        }

        SyncMenuSelection(_viewModel.SelectedPage);

        // 選択ページだけを表示し、他のページは非表示にする（Visual Tree からは外さない）
        var selected = GetOrCreatePage(_viewModel.SelectedPage);
        foreach (var page in _pages.Values)
        {
            page.IsVisible = ReferenceEquals(page, selected);
        }
        CurrentContentPage = selected;

        // NavigationPage にはページを重ねたホストページを 1 枚だけ載せる（Push 完了前の再入で二重に積まない）
        if (!_isHostPagePushed)
        {
            _isHostPagePushed = true;
            await MainNav.PushAsync(_hostPage, null);
        }
    }

    private void SyncMenuSelection(MainPageKind page)
    {
        _isSyncingMenu = true;
        try
        {
            SelectMenuItem(MainMenu, page);
            SelectMenuItem(SystemsMenu, page);
        }
        finally
        {
            _isSyncingMenu = false;
        }
    }

    private static void SelectMenuItem(ListBox listBox, MainPageKind page)
    {
        foreach (var item in listBox.Items)
        {
            if (item is ListBoxItem { Tag: MainPageKind kind } listBoxItem && kind == page)
            {
                listBox.SelectedItem = listBoxItem;
                return;
            }
        }

        // 該当項目が無いカテゴリは選択を解除する
        listBox.SelectedItem = null;
    }

    private ContentPage GetOrCreatePage(MainPageKind kind)
    {
        if (_pages.TryGetValue(kind, out var cached))
        {
            return cached;
        }

        var vm = _viewModel!;
        ContentPage page = kind switch
        {
            MainPageKind.MapList => new MapListPageView { DataContext = vm.MapListPageViewModel },
            MainPageKind.ImportExport => new ImportExportPageView { DataContext = vm.ImportExportPageViewModel },
            MainPageKind.BeatmapGeneration => new BeatmapGenerationPageView { DataContext = vm.BeatmapGenerationPageViewModel },
            MainPageKind.Settings => new SettingsPage { DataContext = vm.SettingsViewModel },
            MainPageKind.Licenses => new LicensesPage { DataContext = vm.LicensesViewModel },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        _pages[kind] = page;
        _pageHost.Children.Add(page);
        return page;
    }
}
