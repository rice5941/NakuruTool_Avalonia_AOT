using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using Xunit;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// <see cref="RateGenerationViewModel"/> の入力バリデーション
/// （特にレート倍率の小数3桁制限）と <c>UpdateCalculatedRate</c> の3桁丸めを固定する。
/// </summary>
public class RateGenerationViewModelTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.234)]
    [InlineData(2.000)]
    [InlineData(0.5)]
    public void RateHasError_IsFalse_WhenRateHasAtMost3Decimals(double rate)
    {
        var vm = new RateGenerationViewModel { Rate = rate };

        Assert.False(vm.RateHasError);
    }

    [Theory]
    [InlineData(1.2345)]
    [InlineData(1.0001)]
    [InlineData(0.9999)]
    public void RateHasError_IsTrue_WhenRateHasMoreThan3Decimals(double rate)
    {
        var vm = new RateGenerationViewModel { Rate = rate };

        Assert.True(vm.RateHasError);
    }

    [Fact]
    public void RateHasError_IsTrue_WhenRateBelowMinimum()
    {
        var vm = new RateGenerationViewModel { Rate = 0.4 };

        Assert.True(vm.RateHasError);
    }

    [Fact]
    public void RateHasError_IsTrue_WhenRateIsNull()
    {
        var vm = new RateGenerationViewModel { Rate = null };

        Assert.True(vm.RateHasError);
    }

    [Fact]
    public void RateHasError_IsFalse_InFixedBpmMode_EvenWith4Decimals()
    {
        var vm = new RateGenerationViewModel
        {
            IsFixedBpmMode = true,
            Rate = 1.2345,
        };

        Assert.False(vm.RateHasError);
    }

    [Fact]
    public void RateHasError_IsFalse_InFixedBpmMode_EvenWhenRateIsNull()
    {
        var vm = new RateGenerationViewModel
        {
            IsFixedBpmMode = true,
            Rate = null,
        };

        Assert.False(vm.RateHasError);
    }

    [Fact]
    public void UpdateCalculatedRate_RoundsToAtMost3Decimals_WhenExact()
    {
        var vm = new RateGenerationViewModel
        {
            SourceBpm = 200.0,
            FixedBpm = 250.0,
        };

        Assert.Equal(1.25, vm.CalculatedRate);
    }

    [Fact]
    public void UpdateCalculatedRate_RoundsAwayFromZero_To3Decimals()
    {
        // 200 / 175 = 1.142857... → 1.143 (AwayFromZero)
        var vm = new RateGenerationViewModel
        {
            SourceBpm = 175.0,
            FixedBpm = 200.0,
        };

        Assert.Equal(1.143, vm.CalculatedRate);
    }

    [Fact]
    public void UpdateCalculatedRate_IsNull_WhenSourceBpmIsZero()
    {
        var vm = new RateGenerationViewModel
        {
            SourceBpm = 0.0,
            FixedBpm = 200.0,
        };

        Assert.Null(vm.CalculatedRate);
    }
}
