using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// フィルタ機能のViewModel
/// </summary>
public partial class MapFilterViewModel : ViewModelBase
{
    private const int MaxConditions = 8;

    [ObservableProperty]
    private AvaloniaList<FilterCondition> _conditions = new();

    private bool CanAddCondition => Conditions.Count < MaxConditions;

    public static FilterTarget[] FilterTargets => Enum.GetValues<FilterTarget>();

    public static ComparisonType[] ComparisonTypes => Enum.GetValues<ComparisonType>();

    public static BeatmapStatus[] StatusOptions => Enum.GetValues<BeatmapStatus>();

    private readonly Subject<Unit> _filterChangedSubject = new();

    /// <summary>
    /// フィルタ条件が変更されたときに発火するObservable
    /// </summary>
    public Observable<Unit> FilterChanged => _filterChangedSubject;

    public MapFilterViewModel()
    {
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
            if (!condition.Matches(beatmap))
            {
                return false;
            }
        }
        return true;
    }

    private void NotifyFilterChanged()
    {
        _filterChangedSubject.OnNext(Unit.Default);
    }
}
