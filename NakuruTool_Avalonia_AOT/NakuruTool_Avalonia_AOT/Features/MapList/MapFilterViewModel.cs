using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// フィルタ機能のViewModel
/// </summary>
public partial class MapFilterViewModel : ViewModelBase
{
    /// <summary>
    /// 条件の組の編集（プリセット編集画面と共有の部品）
    /// </summary>
    public FilterGroupsEditorViewModel Editor { get; }

    public static FilterTarget[] FilterTargets => Enum.GetValues<FilterTarget>();

    public static ComparisonType[] ComparisonTypes => Enum.GetValues<ComparisonType>();

    public static BeatmapStatus[] StatusOptions => Enum.GetValues<BeatmapStatus>();

    /// <summary>
    /// フィルタ条件が変更されたときに発火するObservable
    /// </summary>
    public Observable<Unit> FilterChanged => Editor.Changed;

    private readonly IFilterPresetService _presetService;
    private readonly IDatabaseService _databaseService;

    [ObservableProperty]
    public partial FilterPreset? SelectedPreset { get; set; }

    /// <summary>
    /// プリセット編集画面切替コマンド（MapListPageViewModelから注入される）
    /// </summary>
    [ObservableProperty]
    public partial IRelayCommand? TogglePresetEditorCommand { get; set; }

    partial void OnSelectedPresetChanged(FilterPreset? value)
    {
        // 「なし」を選択した場合は条件をクリア
        Editor.Load(value);
    }

    /// <summary>
    /// 利用可能なプリセット一覧（先頭に「未選択」を含む）
    /// </summary>
    public AvaloniaList<FilterPreset?> PresetsWithNone { get; } = new();

    public MapFilterViewModel(IFilterPresetService presetService, IDatabaseService databaseService)
    {
        _presetService = presetService;
        _databaseService = databaseService;

        // プリセットリストの変更を監視して、PresetsWithNoneを更新
        UpdatePresetsWithNone();
        _presetService.Presets.ObserveCollectionChanged()
            .Subscribe(_ => UpdatePresetsWithNone())
            .AddTo(Disposables);

        Editor = new FilterGroupsEditorViewModel(databaseService).AddTo(Disposables);
    }

    /// <summary>
    /// Beatmapがフィルタ条件に一致するかどうかを判定
    /// </summary>
    public bool Matches(Beatmap beatmap) => Editor.Matches(beatmap);

    /// <summary>7K 解析の値を使う条件があるか</summary>
    public bool UsesAnalyze7k => Editor.UsesAnalyze7k;

    /// <summary>
    /// コレクション名リストを更新
    /// </summary>
    public void RefreshCollectionNames()
    {
        FilterConditionExtensions.ClearCollectionCache();

        var names = FilterCondition.GetSharedCollectionNames();
        foreach (var col in _databaseService.OsuCollections)
        {
            names.Add(col.Name);
        }
    }

    /// <summary>
    /// 現在の絞り込み条件からFilterPresetを作成
    /// </summary>
    public FilterPreset CreatePreset(string presetName, string collectionName) =>
        Editor.ToPreset(presetName, collectionName);

    /// <summary>
    /// プリセットを保存
    /// </summary>
    public bool SavePreset(FilterPreset preset)
    {
        return _presetService.SavePreset(preset);
    }

    /// <summary>
    /// プリセット一覧を更新（先頭にnullを追加）
    /// </summary>
    private void UpdatePresetsWithNone()
    {
        PresetsWithNone.Clear();
        PresetsWithNone.Add(null); // 未選択を表すnull

        foreach (var preset in _presetService.Presets)
        {
            PresetsWithNone.Add(preset);
        }
    }
}
