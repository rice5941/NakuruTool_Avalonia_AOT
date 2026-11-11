using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.Licenses;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
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

    private CompositeDisposable _subscriptions = [];
    private MainWindowViewModel? _viewModel;

    /// <summary>ListBox の選択をコードから同期している間は SelectionChanged を無視する</summary>
    private bool _isSyncingMenu;

    /// <summary>NavigationPage の遷移中に要求されたページ切り替えを取りこぼさないためのフラグ</summary>
    private bool _isNavigating;

    public MainWindowView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
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

        if (IsLoaded)
        {
            _ = NavigateToSelectedPageAsync();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _ = NavigateToSelectedPageAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
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
    /// ViewModel の SelectedPage に合わせてサイドバーの選択と NavigationPage の表示ページを揃える
    /// </summary>
    private async Task NavigateToSelectedPageAsync()
    {
        if (_viewModel is null || !IsLoaded)
        {
            return;
        }

        SyncMenuSelection(_viewModel.SelectedPage);

        if (_isNavigating)
        {
            // 遷移完了後のループで最新の SelectedPage が反映される
            return;
        }

        _isNavigating = true;
        try
        {
            // 遷移中に選択が変わった場合も、最終的に SelectedPage と一致するまで繰り返す
            while (_viewModel is not null)
            {
                var page = GetOrCreatePage(_viewModel.SelectedPage);
                if (ReferenceEquals(MainNav.CurrentPage, page))
                {
                    break;
                }

                if (MainNav.StackDepth == 0)
                {
                    await MainNav.PushAsync(page, null);
                }
                else
                {
                    await MainNav.ReplaceAsync(page, null);
                }
            }
        }
        finally
        {
            _isNavigating = false;
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

        // ページ名はサイドバーで分かるためナビゲーションバーは表示しない（従来の TabControl と同じ表示領域を確保する）
        NavigationPage.SetHasNavigationBar(page, false);
        NavigationPage.SetHasBackButton(page, false);

        _pages[kind] = page;
        return page;
    }
}
