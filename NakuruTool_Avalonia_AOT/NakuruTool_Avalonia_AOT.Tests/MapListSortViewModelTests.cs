using System;
using NakuruTool_Avalonia_AOT.Features.MapList.Sorting;
using R3;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// 設計書 §9.7 — <see cref="MapListSortViewModel"/> の R3 発火・Reset・active rule 抽出を固定する。
/// </summary>
public class MapListSortViewModelTests
{
    private static (MapListSortViewModel Vm, Func<int> GetCount, IDisposable Subscription) CreateVm()
    {
        var vm = new MapListSortViewModel();
        var count = 0;
        var sub = vm.SortChanged.Subscribe(_ => count++);
        return (vm, () => count, sub);
    }

    [Fact]
    public void SortChanged_FiresOnce_WhenSingleRuleFieldChanged()
    {
        var (vm, getCount, sub) = CreateVm();
        try
        {
            vm.Primary.Field = SortField.Title;

            Assert.Equal(1, getCount());
        }
        finally
        {
            sub.Dispose();
            vm.Dispose();
        }
    }

    [Fact]
    public void SortChanged_FiresOnce_WhenSingleRuleDirectionChanged()
    {
        var (vm, getCount, sub) = CreateVm();
        try
        {
            vm.Primary.Direction = SortDirection.Descending;

            Assert.Equal(1, getCount());
        }
        finally
        {
            sub.Dispose();
            vm.Dispose();
        }
    }

    [Fact]
    public void Reset_FiresSortChangedExactlyOnce_EvenWhenAllRulesWereActive()
    {
        var vm = new MapListSortViewModel();
        // 先に 3 スロット全て active にしておく
        vm.Primary.Field = SortField.Title;
        vm.Secondary.Field = SortField.Artist;
        vm.Tertiary.Field = SortField.BPM;

        var count = 0;
        using var sub = vm.SortChanged.Subscribe(_ => count++);

        vm.ResetCommand.Execute(null);

        Assert.Equal(1, count);
        vm.Dispose();
    }

    [Fact]
    public void Reset_RestoresAllRulesToNoneAscending()
    {
        var vm = new MapListSortViewModel();
        vm.Primary.Field = SortField.Title;
        vm.Primary.Direction = SortDirection.Descending;
        vm.Secondary.Field = SortField.Artist;
        vm.Secondary.Direction = SortDirection.Descending;
        vm.Tertiary.Field = SortField.BPM;
        vm.Tertiary.Direction = SortDirection.Descending;

        vm.ResetCommand.Execute(null);

        Assert.Equal(SortField.None, vm.Primary.Field);
        Assert.Equal(SortDirection.Ascending, vm.Primary.Direction);
        Assert.Equal(SortField.None, vm.Secondary.Field);
        Assert.Equal(SortDirection.Ascending, vm.Secondary.Direction);
        Assert.Equal(SortField.None, vm.Tertiary.Field);
        Assert.Equal(SortDirection.Ascending, vm.Tertiary.Direction);

        vm.Dispose();
    }

    [Fact]
    public void GetActiveRules_PreservesPriorityOrder()
    {
        var vm = new MapListSortViewModel();
        // Primary は None のまま。Secondary / Tertiary を active に。
        vm.Secondary.Field = SortField.Artist;
        vm.Tertiary.Field = SortField.BPM;

        var active = vm.GetActiveRules();

        Assert.Equal(2, active.Length);
        Assert.Same(vm.Secondary, active[0]);
        Assert.Same(vm.Tertiary, active[1]);

        vm.Dispose();
    }

    [Fact]
    public void GetActiveRules_ReturnsEmptyArray_WhenAllNone()
    {
        var vm = new MapListSortViewModel();

        var active = vm.GetActiveRules();

        Assert.Empty(active);

        vm.Dispose();
    }

    [Fact]
    public void Dispose_StopsSortChangedNotifications()
    {
        var vm = new MapListSortViewModel();
        var count = 0;
        using var sub = vm.SortChanged.Subscribe(_ => count++);

        vm.Dispose();

        // Dispose 後にプロパティを変更しても発火しない
        vm.Primary.Field = SortField.Title;
        vm.Secondary.Direction = SortDirection.Descending;

        Assert.Equal(0, count);
    }

    [Fact]
    public void HasActiveRule_ReflectsCurrentState()
    {
        var vm = new MapListSortViewModel();
        try
        {
            Assert.False(vm.HasActiveRule);

            vm.Primary.Field = SortField.Title;
            Assert.True(vm.HasActiveRule);

            vm.Primary.Field = SortField.None;
            Assert.False(vm.HasActiveRule);

            vm.Tertiary.Field = SortField.BPM;
            Assert.True(vm.HasActiveRule);

            vm.ResetCommand.Execute(null);
            Assert.False(vm.HasActiveRule);
        }
        finally
        {
            vm.Dispose();
        }
    }
}
