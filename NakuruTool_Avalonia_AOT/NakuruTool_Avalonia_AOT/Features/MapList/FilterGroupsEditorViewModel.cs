using Avalonia.Collections;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// 条件の組（組の中は AND、組どうしは OR）を編集する ViewModel。
/// フィルタ画面とプリセット編集画面で共有する。
/// </summary>
public partial class FilterGroupsEditorViewModel : ViewModelBase
{
    /// <summary>
    /// 全組の条件の合計の上限
    /// </summary>
    public const int MaxConditions = 16;

    private readonly IDatabaseService _databaseService;
    private readonly Subject<Unit> _changedSubject = new();
    private readonly CompositeDisposable _groupSubscriptions = new();
    private FilterCondition[][] _activeGroups = [];
    private bool _isBatchUpdating;

    /// <summary>
    /// 条件の組（常に 1 つ以上。条件がないときは空の組が 1 つ）
    /// </summary>
    public AvaloniaList<FilterConditionGroup> Groups { get; } = new();

    /// <summary>
    /// 全組の条件の合計
    /// </summary>
    public int ConditionCount { get; private set; }

    /// <summary>
    /// 条件が 1 つ以上あるか
    /// </summary>
    public bool HasConditions => ConditionCount > 0;

    /// <summary>
    /// 組が 2 つ以上あるか（組の枠と見出しの表示切替用）
    /// </summary>
    public bool HasMultipleGroups { get; private set; }

    /// <summary>
    /// 組や条件が変わったときに発火する
    /// </summary>
    public Observable<Unit> Changed => _changedSubject;

    /// <summary>
    /// 7K 解析の値を使う条件があるか
    /// </summary>
    public bool UsesAnalyze7k
    {
        get
        {
            foreach (var group in Groups)
            {
                foreach (var condition in group.Conditions)
                {
                    if (FilterTargetInfo.IsAnalyze7k(condition.Target)) return true;
                }
            }
            return false;
        }
    }

    public FilterGroupsEditorViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;

        Groups.Add(new FilterConditionGroup());

        Groups.ObserveCollectionChanged()
            .Subscribe(_ =>
            {
                SubscribeGroups();
                OnChanged();
            })
            .AddTo(Disposables);

        SubscribeGroups();
        Refresh();

        _groupSubscriptions.AddTo(Disposables);
        _changedSubject.AddTo(Disposables);
    }

    /// <summary>
    /// Beatmapがいずれかの組のすべての条件に一致するか（値の入った条件がなければ一致）
    /// </summary>
    public bool Matches(Beatmap beatmap) => _activeGroups.MatchesAnyGroup(beatmap, _databaseService);

    /// <summary>
    /// プリセットの条件を読み込む（null なら空にする）
    /// </summary>
    public void Load(FilterPreset? preset)
    {
        RunBatch(() =>
        {
            Groups.Clear();
            if (preset != null)
            {
                foreach (var groupData in preset.GetGroups())
                {
                    if (groupData.Count == 0) continue;
                    var group = new FilterConditionGroup();
                    foreach (var conditionData in groupData)
                    {
                        group.Conditions.Add(conditionData.ToFilterCondition());
                    }
                    Groups.Add(group);
                }
            }
            if (Groups.Count == 0)
                Groups.Add(new FilterConditionGroup());
        });
    }

    /// <summary>
    /// 現在の条件からプリセットを作る
    /// </summary>
    public FilterPreset ToPreset(string presetName, string collectionName) =>
        FilterPreset.Create(
            presetName,
            collectionName,
            Groups.Select(g => g.Conditions.Select(FilterConditionData.FromFilterCondition).ToList()));

    private bool CanAddCondition() => ConditionCount < MaxConditions;

    /// <summary>
    /// 組に条件を追加する（組の指定がなければ最後の組）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddCondition))]
    private void AddCondition(FilterConditionGroup? group)
    {
        group ??= Groups[^1];
        group.Conditions.Add(new FilterCondition());
    }

    /// <summary>
    /// 条件を削除する。組が空になり、ほかに組があれば組も削除する。
    /// </summary>
    [RelayCommand]
    private void RemoveCondition(FilterCondition? condition)
    {
        if (condition is null) return;

        var group = Groups.FirstOrDefault(g => g.Conditions.Contains(condition));
        if (group is null) return;

        if (group.Conditions.Count == 1 && Groups.Count > 1)
        {
            Groups.Remove(group);
            return;
        }
        group.Conditions.Remove(condition);
    }

    /// <summary>
    /// 「または」の組を条件 1 つ入りで追加する。条件のない組しかなければ、その組に条件を追加する。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddCondition))]
    private void AddGroup()
    {
        if (Groups.Count == 1 && Groups[0].Conditions.Count == 0)
        {
            Groups[0].Conditions.Add(new FilterCondition());
            return;
        }

        var group = new FilterConditionGroup();
        group.Conditions.Add(new FilterCondition());
        Groups.Add(group);
    }

    private bool CanDuplicateGroup(FilterConditionGroup? group) =>
        group is { Conditions.Count: > 0 } && ConditionCount + group.Conditions.Count <= MaxConditions;

    /// <summary>
    /// 組を複製して直後に挿入する
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDuplicateGroup))]
    private void DuplicateGroup(FilterConditionGroup? group)
    {
        if (group is null) return;

        var copy = new FilterConditionGroup();
        foreach (var condition in group.Conditions)
        {
            copy.Conditions.Add(FilterConditionData.FromFilterCondition(condition).ToFilterCondition());
        }
        Groups.Insert(Groups.IndexOf(group) + 1, copy);
    }

    /// <summary>
    /// 組を削除する（最後の 1 つは条件だけを消す）
    /// </summary>
    [RelayCommand]
    private void RemoveGroup(FilterConditionGroup? group)
    {
        if (group is null) return;

        if (Groups.Count > 1)
            Groups.Remove(group);
        else
            group.Conditions.Clear();
    }

    /// <summary>
    /// すべての条件を削除する
    /// </summary>
    [RelayCommand]
    public void Clear() => Load(null);

    /// <summary>
    /// 複数の変更をまとめて 1 回の通知にする
    /// </summary>
    private void RunBatch(Action action)
    {
        _isBatchUpdating = true;
        try
        {
            action();
        }
        finally
        {
            _isBatchUpdating = false;
        }
        SubscribeGroups();
        OnChanged();
    }

    /// <summary>
    /// 各組の条件の追加・削除と、条件のプロパティ変更を購読し直す
    /// </summary>
    private void SubscribeGroups()
    {
        if (_isBatchUpdating) return;

        _groupSubscriptions.Clear();
        foreach (var group in Groups)
        {
            group.Conditions.ObserveCollectionChanged()
                .Subscribe(_ => OnChanged())
                .AddTo(_groupSubscriptions);
            group.Conditions.ObserveElementPropertyChanged()
                .Subscribe(_ => OnChanged())
                .AddTo(_groupSubscriptions);
        }
    }

    private void OnChanged()
    {
        if (_isBatchUpdating) return;

        Refresh();
        _changedSubject.OnNext(Unit.Default);
    }

    /// <summary>
    /// 組の番号・無視の表示・判定用の組・件数とコマンドの状態を更新する
    /// </summary>
    private void Refresh()
    {
        var count = 0;
        for (var i = 0; i < Groups.Count; i++)
        {
            var group = Groups[i];
            group.Number = i + 1;
            group.IsIgnored = group.Conditions.Count > 0 && !group.Conditions.Any(static c => c.IsActive);
            count += group.Conditions.Count;
        }

        _activeGroups = Groups.Select(static g => g.Conditions).BuildActiveGroups();

        if (ConditionCount != count)
        {
            ConditionCount = count;
            OnPropertyChanged(nameof(ConditionCount));
            OnPropertyChanged(nameof(HasConditions));
        }

        var hasMultipleGroups = Groups.Count > 1;
        if (HasMultipleGroups != hasMultipleGroups)
        {
            HasMultipleGroups = hasMultipleGroups;
            OnPropertyChanged(nameof(HasMultipleGroups));
        }

        AddConditionCommand.NotifyCanExecuteChanged();
        AddGroupCommand.NotifyCanExecuteChanged();
        DuplicateGroupCommand.NotifyCanExecuteChanged();
    }
}
