using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;

namespace NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;

/// <summary>
/// 共通 BeatmapList View が必要とする ViewModel 契約。
/// MapList / BeatmapGenerationPage の双方が実装し、同一の View を共有する。
/// </summary>
public interface IBeatmapListViewModel : INotifyPropertyChanged
{
    IAvaloniaReadOnlyList<Beatmap> ShowBeatmaps { get; }
    Beatmap? SelectedBeatmap { get; set; }

    int TotalCount { get; }
    int FilteredCount { get; }

    int CurrentPage { get; set; }
    int PageCount { get; }
    int PageSize { get; set; }
    IAvaloniaReadOnlyList<int> PageSizes { get; }

    IRelayCommand PreviousPageCommand { get; }
    IRelayCommand NextPageCommand { get; }

    ModCategory SelectedModCategory { get; set; }
    ScoreSystemCategory SelectedScoreSystemCategory { get; set; }

    // ---- ContextMenu (Copy / Open / Generate) 共通契約 ----
    /// <summary>選択譜面のダウンロード URL をクリップボードへコピーするコマンド。</summary>
    IRelayCommand CopyDownloadUrlCommand { get; }
    /// <summary>選択譜面のフォルダをエクスプローラで開くコマンド。</summary>
    IRelayCommand OpenInExplorerCommand { get; }
    /// <summary>選択譜面を生成するコマンド（生成ページ側では No-op の場合あり）。</summary>
    IRelayCommand GenerateBeatmapCommand { get; }

    /// <summary>クリップボード書き込みデリゲートを差し替える（View から注入）。</summary>
    void SetClipboardWriter(Func<string, Task>? writer);
    /// <summary>右クリック対象の譜面を選択状態にする。</summary>
    void SelectBeatmapForContextMenu(Beatmap beatmap);
    /// <summary>ContextMenu 表示前のターゲット確定。表示可否を返す。</summary>
    bool TryPrepareContextMenu(Beatmap beatmap);
    /// <summary>ContextMenu のターゲットをクリアする。</summary>
    void ClearContextMenuBeatmap();
}
