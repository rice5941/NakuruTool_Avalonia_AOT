using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using System;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

public partial class RateGenerationViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial double? Rate { get; set; } = 1.25;

    [ObservableProperty]
    public partial bool IsFixedBpmMode { get; set; } = false;

    [ObservableProperty]
    public partial double? FixedBpm { get; set; } = 200.0;

    /// <summary>
    /// 元beatmapのBPM（親VMがセットする）
    /// </summary>
    [ObservableProperty]
    public partial double SourceBpm { get; set; }

    /// <summary>
    /// 固定BPMモード時の計算済みレート（SourceBpmが0以下の場合はnull）
    /// </summary>
    [ObservableProperty]
    public partial double? CalculatedRate { get; private set; }

    /// <summary>
    /// レート倍率モード時の計算済みBPM（SourceBpmが0以下の場合はnull）
    /// </summary>
    [ObservableProperty]
    public partial double? CalculatedBpm { get; private set; }

    /// <summary>ピッチを変更する（NC方式）。デフォルト false（DT方式）</summary>
    [ObservableProperty]
    public partial bool ChangePitch { get; set; } = false;

    [ObservableProperty]
    public partial bool IsHighQualityMp3 { get; set; } = false;

    [ObservableProperty]
    public partial bool IsHpOverrideEnabled { get; set; } = false;

    [ObservableProperty]
    public partial double? HpValue { get; set; } = 8.0;

    [ObservableProperty]
    public partial bool IsOdOverrideEnabled { get; set; } = false;

    [ObservableProperty]
    public partial double? OdValue { get; set; } = 5.0;

    // --- 入力範囲定義 ---
    public double RateMin { get; }= 0.5;
    public double FixedBpmMin { get; }= 10.0;
    public double HpMin { get; }= 0.0;
    public double HpMax { get; }= 10.0;
    public double OdMin { get; }= 0.0;
    public double OdMax { get; }= 10.0;

    // --- バリデーション ---
    public bool RateHasError => !IsFixedBpmMode && (!Rate.HasValue || Rate < RateMin);
    public bool FixedBpmHasError => IsFixedBpmMode && (!FixedBpm.HasValue || FixedBpm < FixedBpmMin);
    public bool HpHasError => IsHpOverrideEnabled && (!HpValue.HasValue || HpValue < HpMin || HpValue > HpMax);
    public bool OdHasError => IsOdOverrideEnabled && (!OdValue.HasValue || OdValue < OdMin || OdValue > OdMax);
    public bool HasValidationErrors => RateHasError || FixedBpmHasError || HpHasError || OdHasError;

    private void NotifyValidationErrors()
    {
        OnPropertyChanged(nameof(RateHasError));
        OnPropertyChanged(nameof(FixedBpmHasError));
        OnPropertyChanged(nameof(HpHasError));
        OnPropertyChanged(nameof(OdHasError));
        OnPropertyChanged(nameof(HasValidationErrors));
    }

    public RateGenerationOptions ToOptions() => new()
    {
        Rate = IsFixedBpmMode ? null : Rate ?? 1.0,
        TargetBpm = IsFixedBpmMode ? FixedBpm ?? 180.0 : null,
        HpOverride = IsHpOverrideEnabled ? HpValue ?? 8.0 : null,
        OdOverride = IsOdOverrideEnabled ? OdValue ?? 8.0 : null,
        ChangePitch = ChangePitch,
        Mp3VbrQuality = IsHighQualityMp3 ? 0 : 4,
    };

    private void UpdateCalculatedRate()
    {
        CalculatedRate = SourceBpm > 0 && FixedBpm.HasValue
            ? Math.Round(FixedBpm.Value / SourceBpm, 2) : null;
    }

    private void UpdateCalculatedBpm()
    {
        CalculatedBpm = SourceBpm > 0 && Rate.HasValue
            ? Math.Round(SourceBpm * Rate.Value, 0) : null;
    }

    partial void OnRateChanged(double? value)
    {
        UpdateCalculatedBpm();
        NotifyValidationErrors();
    }

    partial void OnFixedBpmChanged(double? value)
    {
        UpdateCalculatedRate();
        NotifyValidationErrors();
    }

    partial void OnIsFixedBpmModeChanged(bool value)
    {
        NotifyValidationErrors();
    }

    partial void OnSourceBpmChanged(double value)
    {
        UpdateCalculatedRate();
        UpdateCalculatedBpm();
    }

    partial void OnHpValueChanged(double? value)
    {
        NotifyValidationErrors();
    }

    partial void OnIsHpOverrideEnabledChanged(bool value)
    {
        NotifyValidationErrors();
    }

    partial void OnOdValueChanged(double? value)
    {
        NotifyValidationErrors();
    }

    partial void OnIsOdOverrideEnabledChanged(bool value)
    {
        NotifyValidationErrors();
    }
}
