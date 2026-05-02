using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Sorting;

/// <summary>
/// MapList の 3 スロット固定ソート ViewModel。
/// 各 SortRule の変更を Merge して <see cref="SortChanged"/> を発火する。
/// Reset 中は <see cref="_isBatchUpdating"/> でガードし、最後に 1 回だけ発火する。
/// </summary>
internal sealed partial class MapListSortViewModel : ViewModelBase
{
    public const int RuleCount = 3;

    public SortRule Primary { get; }
    public SortRule Secondary { get; }
    public SortRule Tertiary { get; }

    /// <summary>
    /// ItemsControl 等で一括描画したい場合のための読み取り専用コレクション。
    /// 順序は Primary -> Secondary -> Tertiary。
    /// </summary>
    public IReadOnlyList<SortRule> Rules { get; }

    public static SortField[] SortFields { get; } = Enum.GetValues<SortField>();
    public static SortDirection[] SortDirections { get; } = Enum.GetValues<SortDirection>();

    private readonly Subject<Unit> _sortChangedSubject;

    /// <summary>
    /// ソート条件が変更されたときに発火する Observable。
    /// </summary>
    public Observable<Unit> SortChanged { get; }

    private bool _hasActiveRule;

    /// <summary>
    /// 1 つ以上の SortRule が active かどうか。
    /// </summary>
    public bool HasActiveRule
    {
        get => _hasActiveRule;
        private set => SetProperty(ref _hasActiveRule, value);
    }

    private bool _isBatchUpdating;

    public MapListSortViewModel()
    {
        Primary = new SortRule();
        Secondary = new SortRule();
        Tertiary = new SortRule();
        Rules = new[] { Primary, Secondary, Tertiary };

        _sortChangedSubject = new Subject<Unit>();
        SortChanged = _sortChangedSubject;

        // 各スロットの PropertyChanged を Merge し、Reset バッチ中はスキップ。
        Observable.Merge(
                Primary.ObservePropertyChanged(),
                Secondary.ObservePropertyChanged(),
                Tertiary.ObservePropertyChanged())
            .Where(_ => !_isBatchUpdating)
            .Subscribe(_ => RaiseSortChanged())
            .AddTo(Disposables);

        _sortChangedSubject.AddTo(Disposables);
    }

    /// <summary>
    /// 全スロットを <see cref="SortField.None"/> / <see cref="SortDirection.Ascending"/> に戻し、
    /// SortChanged を 1 回だけ発火する。
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        _isBatchUpdating = true;
        try
        {
            Primary.Field = SortField.None;
            Primary.Direction = SortDirection.Ascending;
            Secondary.Field = SortField.None;
            Secondary.Direction = SortDirection.Ascending;
            Tertiary.Field = SortField.None;
            Tertiary.Direction = SortDirection.Ascending;
        }
        finally
        {
            _isBatchUpdating = false;
        }
        RaiseSortChanged();
    }

    /// <summary>
    /// active な SortRule のみを Primary -> Secondary -> Tertiary の順で抜き出した snapshot を返す。
    /// </summary>
    public SortRule[] GetActiveRules()
    {
        var count = 0;
        if (Primary.IsActive) count++;
        if (Secondary.IsActive) count++;
        if (Tertiary.IsActive) count++;

        if (count == 0)
        {
            return Array.Empty<SortRule>();
        }

        var result = new SortRule[count];
        var index = 0;
        if (Primary.IsActive) result[index++] = Primary;
        if (Secondary.IsActive) result[index++] = Secondary;
        if (Tertiary.IsActive) result[index] = Tertiary;
        return result;
    }

    private void RaiseSortChanged()
    {
        HasActiveRule = Primary.IsActive || Secondary.IsActive || Tertiary.IsActive;
        _sortChangedSubject.OnNext(Unit.Default);
    }
}
