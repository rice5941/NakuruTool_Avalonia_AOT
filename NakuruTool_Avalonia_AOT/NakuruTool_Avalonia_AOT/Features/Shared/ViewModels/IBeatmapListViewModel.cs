using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
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

    /// <summary>ローカルランキング表示モード（BPM / LN率 / 履歴 / プレイ済み列を隠し、ランキング用 DataGrid を表示）。</summary>
    bool IsLocalRankingMode { get; set; }

    /// <summary>7K 解析結果の表示モード（Prism 列・段位推定列を出し、履歴・プレイ済み列を隠す）。ローカルランキング表示とは排他。</summary>
    bool IsAnalysisMode { get; set; }

    /// <summary>選択譜面のローカルランキング（選択中 ScoreSystem / Mod で絞り込み、スコア降順）。</summary>
    IAvaloniaReadOnlyList<LocalRankingEntry> LocalRanking { get; }

    /// <summary><see cref="LocalRanking"/> が1件以上あるか。</summary>
    bool HasLocalRanking { get; }

    /// <summary>Prism 列を基準スキル値の横棒で表示する（false はジャンル濃度の五角形）。</summary>
    bool ShowGenreSkillValues { get; set; }

    /// <summary>7K 解析の進み具合（解析中だけ値がある。null の間はヘッダーに出さない）。</summary>
    Analyze7kProgressDisplay? Analyze7kStatus { get; }

    // ---- ContextMenu (OfficialPage / Open / Generate) 共通契約 ----
    /// <summary>選択譜面の公式 beatmapset ページを既定のブラウザで開くコマンド。</summary>
    IRelayCommand OpenOfficialPageCommand { get; }
    /// <summary>選択譜面のフォルダをエクスプローラで開くコマンド。</summary>
    IRelayCommand OpenInExplorerCommand { get; }
    /// <summary>選択譜面を生成するコマンド（生成ページ側では No-op の場合あり）。</summary>
    IRelayCommand GenerateBeatmapCommand { get; }

    /// <summary>右クリック対象の譜面を選択状態にする。</summary>
    void SelectBeatmapForContextMenu(Beatmap beatmap);
    /// <summary>ContextMenu 表示前のターゲット確定。表示可否を返す。</summary>
    bool TryPrepareContextMenu(Beatmap beatmap);
    /// <summary>ContextMenu のターゲットをクリアする。</summary>
    void ClearContextMenuBeatmap();
}
