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
using System.Collections.ObjectModel;
using System.Linq;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// フィルタ機能のViewModel
/// </summary>
public partial class MapFilterViewModel : ViewModelBase
{
    private const int MaxConditions = 8;

    [ObservableProperty]
    public partial AvaloniaList<FilterCondition> Conditions { get; set; } = new();

    private bool CanAddCondition => Conditions.Count < MaxConditions;

    public static FilterTarget[] FilterTargets => Enum.GetValues<FilterTarget>();

    public static ComparisonType[] ComparisonTypes => Enum.GetValues<ComparisonType>();

    public static BeatmapStatus[] StatusOptions => Enum.GetValues<BeatmapStatus>();

    private readonly Subject<Unit> _filterChangedSubject = new();

    /// <summary>
    /// フィルタ条件が変更されたときに発火するObservable
    /// </summary>
    public Observable<Unit> FilterChanged => _filterChangedSubject;

    private readonly IFilterPresetService _presetService;
    private readonly IDatabaseService _databaseService;

    // Collectionフィルタ用MD5キャッシュ
    private HashSet<string>? _collectionMd5Cache;
    private string? _cachedCollectionName;

    /// <summary>
    /// 利用可能なコレクション名一覧
    /// </summary>
    public ObservableCollection<string> CollectionNames { get; } = new();

    [ObservableProperty]
    public partial FilterPreset? SelectedPreset { get; set; }

    /// <summary>
    /// プリセット編集画面切替コマンド（MapListPageViewModelから注入される）
    /// </summary>
    [ObservableProperty]
    public partial IRelayCommand? TogglePresetEditorCommand { get; set; }

    partial void OnSelectedPresetChanged(FilterPreset? value)
    {
        if (value != null)
        {
            LoadPreset(value);
        }
        else
        {
            // 「なし」を選択した場合は条件をクリア
            Conditions.Clear();
        }
    }

    /// <summary>
    /// 利用可能なプリセット一覧（先頭に「未選択」を含む）
    /// </summary>
    public AvaloniaList<FilterPreset?> PresetsWithNone { get; } = new();

    public MapFilterViewModel(IFilterPresetService presetService, IDatabaseService databaseService)
    {
        _presetService = presetService;
        _databaseService = databaseService;

        // コレクション名リストを初期化
        RefreshCollectionNames();

        // プリセットリストの変更を監視して、PresetsWithNoneを更新
        UpdatePresetsWithNone();
        _presetService.Presets.ObserveCollectionChanged()
            .Subscribe(_ => UpdatePresetsWithNone())
            .AddTo(Disposables);

        // コレクション変更を監視
        Conditions.ObserveCollectionChanged()
            .Subscribe(_ =>
            {
                AddConditionCommand.NotifyCanExecuteChanged();
                NotifyFilterChanged();
            })
            .AddTo(Disposables);

        // 要素のPropertyChangedを監視
        Conditions.ObserveElementPropertyChanged()
            .Subscribe(_ => NotifyFilterChanged())
            .AddTo(Disposables);

        _filterChangedSubject.AddTo(Disposables);
    }

    [RelayCommand(CanExecute = nameof(CanAddCondition))]
    private void AddCondition()
    {
        var condition = new FilterCondition();
        Conditions.Add(condition);
    }

    [RelayCommand]
    private void RemoveCondition(FilterCondition condition)
    {
        Conditions.Remove(condition);
        AddConditionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearConditions()
    {
        Conditions.Clear();
        AddConditionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Beatmapがすべてのフィルタ条件に一致するかどうかを判定
    /// </summary>
    public bool Matches(Beatmap beatmap)
    {
        if (Conditions.Count == 0) return true;
        
        foreach (var condition in Conditions)
        {
            if (condition.Target == FilterTarget.Collection)
            {
                if (!MatchesCollection(beatmap, condition.CollectionValue))
                    return false;
            }
            else if (!condition.Matches(beatmap))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// コレクションによるフィルタリング（HashSetキャッシュでO(1)ルックアップ）
    /// </summary>
    private bool MatchesCollection(Beatmap beatmap, string collectionName)
    {
        if (string.IsNullOrEmpty(collectionName)) return true;

        if (_cachedCollectionName != collectionName)
        {
            var col = _databaseService.OsuCollections
                .AsValueEnumerable()
                .FirstOrDefault(c => c.Name == collectionName);
            _collectionMd5Cache = col != null
                ? new HashSet<string>(col.BeatmapMd5s, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();
            _cachedCollectionName = collectionName;
        }
        return _collectionMd5Cache!.Contains(beatmap.MD5Hash);
    }

    /// <summary>
    /// コレクション名リストを更新
    /// </summary>
    public void RefreshCollectionNames()
    {
        CollectionNames.Clear();
        _collectionMd5Cache = null;
        _cachedCollectionName = null;

        foreach (var col in _databaseService.OsuCollections)
        {
            CollectionNames.Add(col.Name);
        }
    }

    private void NotifyFilterChanged()
    {
        _filterChangedSubject.OnNext(Unit.Default);
    }

    /// <summary>
    /// プリセットから条件を読み込み
    /// </summary>
    private void LoadPreset(FilterPreset preset)
    {
        Conditions.Clear();

        foreach (var conditionData in preset.Conditions)
        {
            var condition = conditionData.ToFilterCondition();
            Conditions.Add(condition);
        }
    }

    /// <summary>
    /// 現在の絞り込み条件からFilterPresetを作成
    /// </summary>
    public FilterPreset CreatePreset(string presetName, string collectionName)
    {
        var conditionDataList = Conditions
            .Select(FilterConditionData.FromFilterCondition)
            .ToList();

        return new FilterPreset
        {
            Name = presetName,
            CollectionName = collectionName,
            Conditions = conditionDataList
        };
    }

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
