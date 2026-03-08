using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Converters;
using System;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Models;

/// <summary>
/// フィルタの比較条件
/// </summary>
public enum ComparisonType
{
    /// <summary>
    /// 完全一致（文字列の場合は部分一致）
    /// </summary>
    Equals,
    
    /// <summary>
    /// 範囲指定
    /// </summary>
    Range
}

/// <summary>
/// 範囲比較の境界タイプ
/// </summary>
public enum RangeBoundaryType
{
    /// <summary>
    /// 以上/以下（≤）
    /// </summary>
    Inclusive,

    /// <summary>
    /// より大きい/未満（&lt;）
    /// </summary>
    Exclusive
}

/// <summary>
/// フィルタ対象のプロパティ
/// </summary>
public enum FilterTarget
{
    KeyCount,
    Status,
    Title,
    Version,
    Artist,
    Creator,
    TitleVersionArtistCreator,
    BPM,
    Difficulty,
    LongNoteRate,
    BestAccuracy,
    BestScore,
    IsPlayed,
    LastPlayed,
    LastModifiedTime,
    PlayCount,
    Collection,
    OD,
    HP,
    DrainTime
}

/// <summary>
/// フィルタ対象の情報を提供するヘルパークラス
/// </summary>
public static class FilterTargetInfo
{
    /// <summary>
    /// フィルタ対象が範囲指定をサポートするかどうか
    /// </summary>
    public static bool SupportsRange(FilterTarget target) => target switch
    {
        FilterTarget.KeyCount => true,
        FilterTarget.Status => false,
        FilterTarget.Title => false,
        FilterTarget.Artist => false,
        FilterTarget.Version => false,
        FilterTarget.Creator => false,
        FilterTarget.TitleVersionArtistCreator => false,
        FilterTarget.BPM => true,
        FilterTarget.Difficulty => true,
        FilterTarget.LongNoteRate => true,
        FilterTarget.BestAccuracy => true,
        FilterTarget.BestScore => true,
        FilterTarget.IsPlayed => false,
        FilterTarget.LastPlayed => true,
        FilterTarget.LastModifiedTime => true,
        FilterTarget.PlayCount => true,
        FilterTarget.Collection => false,
        FilterTarget.OD => true,
        FilterTarget.HP => true,
        FilterTarget.DrainTime => true,
        _ => false
    };

    /// <summary>
    /// フィルタ対象が等価比較をサポートするかどうか
    /// </summary>
    public static bool SupportsEquals(FilterTarget target) => target switch
    {
        FilterTarget.KeyCount => true,
        FilterTarget.Status => true,
        FilterTarget.Title => true,
        FilterTarget.Artist => true,
        FilterTarget.Version => true,
        FilterTarget.Creator => true,
        FilterTarget.TitleVersionArtistCreator => true,
        FilterTarget.BPM => true,
        FilterTarget.Difficulty => false,
        FilterTarget.LongNoteRate => false,
        FilterTarget.BestAccuracy => true,
        FilterTarget.BestScore => true,
        FilterTarget.IsPlayed => true,
        FilterTarget.LastPlayed => false,
        FilterTarget.LastModifiedTime => false,
        FilterTarget.PlayCount => true,
        FilterTarget.Collection => true,
        FilterTarget.OD => true,
        FilterTarget.HP => true,
        FilterTarget.DrainTime => true,
        _ => true
    };

    /// <summary>
    /// フィルタ対象が日付型かどうか
    /// </summary>
    public static bool IsDateType(FilterTarget target) => target switch
    {
        FilterTarget.LastPlayed => true,
        FilterTarget.LastModifiedTime => true,
        _ => false
    };

    /// <summary>
    /// フィルタ対象がCollection型かどうか
    /// </summary>
    public static bool IsCollectionType(FilterTarget target) => target == FilterTarget.Collection;

    /// <summary>
    /// フィルタ対象が数値型かどうか
    /// </summary>
    public static bool IsNumericType(FilterTarget target) => target switch
    {
        FilterTarget.KeyCount => true,
        FilterTarget.BPM => true,
        FilterTarget.Difficulty => true,
        FilterTarget.LongNoteRate => true,
        FilterTarget.BestAccuracy => true,
        FilterTarget.BestScore => true,
        FilterTarget.PlayCount => true,
        FilterTarget.OD => true,
        FilterTarget.HP => true,
        _ => false
    };

    /// <summary>
    /// フィルタ対象が文字列型かどうか
    /// </summary>
    public static bool IsStringType(FilterTarget target) => target switch
    {
        FilterTarget.Title => true,
        FilterTarget.Artist => true,
        FilterTarget.Version => true,
        FilterTarget.Creator => true,
        FilterTarget.TitleVersionArtistCreator => true,
        _ => false
    };

    /// <summary>
    /// フィルタ対象がBool型かどうか
    /// </summary>
    public static bool IsBoolType(FilterTarget target) => target switch
    {
        FilterTarget.IsPlayed => true,
        _ => false
    };

    /// <summary>
    /// フィルタ対象がEnum型かどうか
    /// </summary>
    public static bool IsEnumType(FilterTarget target) => target switch
    {
        FilterTarget.Status => true,
        _ => false
    };

    /// <summary>
    /// フィルタ対象が時間長型（mm:ss形式）かどうか
    /// </summary>
    public static bool IsDurationType(FilterTarget target) => target switch
    {
        FilterTarget.DrainTime => true,
        _ => false
    };
}

/// <summary>
/// 単一のフィルタ条件を表すクラス
/// </summary>
public partial class FilterCondition : ObservableObject
{
    /// <summary>
    /// 各フィルタ条件の一意のID（RadioButtonのGroupName用）
    /// </summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 利用可能なModCategory一覧（ComboBoxのItemsSource用）
    /// </summary>
    public static ModCategory[] ModCategories => Enum.GetValues<ModCategory>();

    /// <summary>
    /// 利用可能なScoreSystemCategory一覧（ComboBoxのItemsSource用）
    /// </summary>
    public static ScoreSystemCategory[] ScoreSystemCategories => Enum.GetValues<ScoreSystemCategory>();

    [ObservableProperty]
    private FilterTarget _target = FilterTarget.KeyCount;

    [ObservableProperty]
    private ComparisonType _comparisonType = ComparisonType.Equals;

    /// <summary>
    /// 等価比較時の値、または範囲比較時の最小値
    /// </summary>
    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>
    /// 範囲比較時の最大値
    /// </summary>
    [ObservableProperty]
    private string _valueMax = string.Empty;

    /// <summary>
    /// BeatmapStatusの選択値（Status用）
    /// </summary>
    [ObservableProperty]
    private BeatmapStatus _statusValue = BeatmapStatus.Ranked;

    /// <summary>
    /// Bool型の選択値（IsPlayed用）
    /// </summary>
    [ObservableProperty]
    private bool _boolValue = true;

    /// <summary>
    /// Collection型の選択値（Collection用）
    /// </summary>
    [ObservableProperty]
    private string _collectionValue = string.Empty;

    /// <summary>
    /// スコア/精度フィルタ用のmod区分（BestScore/BestAccuracy対象時のみ使用）
    /// </summary>
    [ObservableProperty]
    private ModCategory _scoreModCategory = ModCategory.NoMod;

    /// <summary>
    /// スコア/精度フィルタ用のスコアシステム区分（BestScore/BestAccuracy対象時のみ使用）
    /// </summary>
    [ObservableProperty]
    private ScoreSystemCategory _scoreSystemCategory = ScoreSystemCategory.Default;

    /// <summary>
    /// 範囲比較時の最小値側の境界タイプ（≤ or <）
    /// </summary>
    [ObservableProperty]
    private RangeBoundaryType _minBoundaryType = RangeBoundaryType.Inclusive;

    /// <summary>
    /// 範囲比較時の最大値側の境界タイプ（≤ or <）
    /// </summary>
    [ObservableProperty]
    private RangeBoundaryType _maxBoundaryType = RangeBoundaryType.Inclusive;

    /// <summary>
    /// 利用可能なRangeBoundaryType一覧（ComboBoxのItemsSource用）
    /// </summary>
    public static RangeBoundaryType[] BoundaryTypes { get; } = Enum.GetValues<RangeBoundaryType>();

    /// <summary>
    /// 日付型の最小値（CalendarDatePicker用）
    /// </summary>
    public DateTime? DateValue
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Value))
                return null;
            
            if (DateTime.TryParse(Value, out var dt))
                return dt.Date;
            
            return null;
        }
        set
        {
            var newValue = value?.Date.ToString("yyyy-MM-dd") ?? string.Empty;
            if (Value != newValue)
            {
                Value = newValue;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 日付型の最大値（CalendarDatePicker用）
    /// </summary>
    public DateTime? DateValueMax
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ValueMax))
                return null;
            
            if (DateTime.TryParse(ValueMax, out var dt))
                return dt.Date;
            
            return null;
        }
        set
        {
            var newValue = value?.Date.ToString("yyyy-MM-dd") ?? string.Empty;
            if (ValueMax != newValue)
            {
                ValueMax = newValue;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 範囲指定をサポートするかどうか
    /// </summary>
    public bool SupportsRange => FilterTargetInfo.SupportsRange(Target);

    /// <summary>
    /// 等価比較をサポートするかどうか
    /// </summary>
    public bool SupportsEquals => FilterTargetInfo.SupportsEquals(Target);

    /// <summary>
    /// 範囲比較かどうか
    /// </summary>
    public bool IsRangeComparison => ComparisonType == ComparisonType.Range;

    /// <summary>
    /// 日付型かどうか
    /// </summary>
    public bool IsDateType => FilterTargetInfo.IsDateType(Target);

    /// <summary>
    /// 数値型かどうか
    /// </summary>
    public bool IsNumericType => FilterTargetInfo.IsNumericType(Target);

    /// <summary>
    /// 文字列型かどうか
    /// </summary>
    public bool IsStringType => FilterTargetInfo.IsStringType(Target);

    /// <summary>
    /// Bool型かどうか
    /// </summary>
    public bool IsBoolType => FilterTargetInfo.IsBoolType(Target);

    /// <summary>
    /// Enum型かどうか
    /// </summary>
    public bool IsEnumType => FilterTargetInfo.IsEnumType(Target);

    /// <summary>
    /// Collection型かどうか
    /// </summary>
    public bool IsCollectionType => FilterTargetInfo.IsCollectionType(Target);

    /// <summary>
    /// 時間長型（mm:ss形式入力）かどうか
    /// </summary>
    public bool IsDurationType => FilterTargetInfo.IsDurationType(Target);

    /// <summary>
    /// BestScore/BestAccuracy型かどうか（mod選択UIの表示制御用）
    /// </summary>
    public bool IsScoreOrAccTarget => Target == FilterTarget.BestScore || Target == FilterTarget.BestAccuracy;

    /// <summary>
    /// Collection以外かつ等価比較かどうか（値入力エリアの表示制御用）
    /// </summary>
    public bool IsNonCollectionEqual => !IsCollectionType && !IsRangeComparison;

    partial void OnTargetChanged(FilterTarget value)
    {
        OnPropertyChanged(nameof(SupportsRange));
        OnPropertyChanged(nameof(SupportsEquals));
        OnPropertyChanged(nameof(IsDateType));
        OnPropertyChanged(nameof(IsNumericType));
        OnPropertyChanged(nameof(IsStringType));
        OnPropertyChanged(nameof(IsBoolType));
        OnPropertyChanged(nameof(IsEnumType));
        OnPropertyChanged(nameof(IsCollectionType));
        OnPropertyChanged(nameof(IsDurationType));
        OnPropertyChanged(nameof(IsNonCollectionEqual));
        OnPropertyChanged(nameof(IsScoreOrAccTarget));

        // SupportsEqualsがfalseでSupportsRangeがtrueの場合は範囲比較に強制
        if (!SupportsEquals && SupportsRange)
        {
            ComparisonType = ComparisonType.Range;
        }
        // 範囲指定をサポートしない場合は等価比較に強制
        else if (!SupportsRange && ComparisonType == ComparisonType.Range)
        {
            ComparisonType = ComparisonType.Equals;
        }

        // 値をリセット
        Value = string.Empty;
        ValueMax = string.Empty;
        CollectionValue = string.Empty;
        MinBoundaryType = RangeBoundaryType.Inclusive;
        MaxBoundaryType = RangeBoundaryType.Inclusive;
    }

    partial void OnComparisonTypeChanged(ComparisonType value)
    {
        OnPropertyChanged(nameof(IsRangeComparison));
        OnPropertyChanged(nameof(IsNonCollectionEqual));
    }

    partial void OnValueChanged(string value)
    {
        // 日付型の場合、DateValueの変更も通知
        if (IsDateType)
        {
            OnPropertyChanged(nameof(DateValue));
        }
    }

    partial void OnValueMaxChanged(string value)
    {
        // 日付型の場合、DateValueMaxの変更も通知
        if (IsDateType)
        {
            OnPropertyChanged(nameof(DateValueMax));
        }
    }

    /// <summary>
    /// Beatmapがこの条件に一致するかどうかを判定
    /// CollectionフィルタはMapFilterViewModelのMatchesCollectionで処理するため、ここではtrueを返す
    /// </summary>
    public bool Matches(Beatmap beatmap)
    {
        return Target switch
        {
            FilterTarget.KeyCount => MatchesNumeric(beatmap.KeyCount),
            FilterTarget.Status => beatmap.Status == StatusValue,
            FilterTarget.Title => MatchesString(beatmap.Title) || MatchesString(beatmap.TitleUnicode),
            FilterTarget.Artist => MatchesString(beatmap.Artist) || MatchesString(beatmap.ArtistUnicode),
            FilterTarget.Version => MatchesString(beatmap.Version),
            FilterTarget.Creator => MatchesString(beatmap.Creator),
            FilterTarget.TitleVersionArtistCreator => MatchesString(beatmap.Title) || MatchesString(beatmap.TitleUnicode) || MatchesString(beatmap.Version) || MatchesString(beatmap.Artist) || MatchesString(beatmap.ArtistUnicode) || MatchesString(beatmap.Creator),
            FilterTarget.BPM => MatchesDouble(beatmap.BPM),
            FilterTarget.Difficulty => MatchesDouble(beatmap.Difficulty),
            FilterTarget.LongNoteRate => MatchesLongNoteRate(beatmap.LongNoteRate),
            FilterTarget.BestAccuracy => MatchesDouble(beatmap.GetBestAccuracy(ScoreSystemCategory, ScoreModCategory)),
            FilterTarget.BestScore => MatchesNumeric(beatmap.GetBestScore(ScoreSystemCategory, ScoreModCategory)),
            FilterTarget.IsPlayed => beatmap.IsPlayed == BoolValue,
            FilterTarget.LastPlayed => MatchesDateTime(beatmap.LastPlayed),
            FilterTarget.LastModifiedTime => MatchesDateTime(beatmap.LastModifiedTime),
            FilterTarget.PlayCount => MatchesNumeric(beatmap.PlayCount),
            FilterTarget.OD => MatchesDouble(beatmap.OD),
            FilterTarget.HP => MatchesDouble(beatmap.HP),
            FilterTarget.DrainTime => MatchesDuration(beatmap.DrainTimeSeconds),
            FilterTarget.Collection => true, // CollectionフィルタはViewModelで処理
            _ => true
        };
    }

    private bool MatchesString(string value)
    {
        if (string.IsNullOrEmpty(Value)) return true;
        return value.Contains(Value, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesNumeric(int value)
    {
        if (string.IsNullOrEmpty(Value)) return true;
        if (!int.TryParse(Value, out var min)) return true;

        if (ComparisonType == ComparisonType.Equals)
        {
            return value == min;
        }
        else
        {
            var minOk = MinBoundaryType == RangeBoundaryType.Inclusive ? value >= min : value > min;
            if (string.IsNullOrEmpty(ValueMax) || !int.TryParse(ValueMax, out var max))
            {
                return minOk;
            }
            var maxOk = MaxBoundaryType == RangeBoundaryType.Inclusive ? value <= max : value < max;
            return minOk && maxOk;
        }
    }

    private bool MatchesDouble(double value)
    {
        if (string.IsNullOrEmpty(Value)) return true;
        if (!double.TryParse(Value, out var min)) return true;

        if (ComparisonType == ComparisonType.Equals)
        {
            return Math.Abs(value - min) < 0.001;
        }
        else
        {
            var minOk = MinBoundaryType == RangeBoundaryType.Inclusive ? value >= min : value > min;
            if (string.IsNullOrEmpty(ValueMax) || !double.TryParse(ValueMax, out var max))
            {
                return minOk;
            }
            var maxOk = MaxBoundaryType == RangeBoundaryType.Inclusive ? value <= max : value < max;
            return minOk && maxOk;
        }
    }

    /// <summary>
    /// LN率のマッチング（0～100の整数入力にも対応）
    /// </summary>
    private bool MatchesLongNoteRate(double value)
    {
        if (string.IsNullOrEmpty(Value)) return true;
        if (!double.TryParse(Value, out var inputMin)) return true;

        var min = inputMin / 100.0;

        if (ComparisonType == ComparisonType.Equals)
        {
            return Math.Abs(value - min) < 0.001;
        }
        else
        {
            var minOk = MinBoundaryType == RangeBoundaryType.Inclusive ? value >= min : value > min;
            if (string.IsNullOrEmpty(ValueMax) || !double.TryParse(ValueMax, out var inputMax))
            {
                return minOk;
            }
            
            var max = inputMax / 100.0;
            var maxOk = MaxBoundaryType == RangeBoundaryType.Inclusive ? value <= max : value < max;
            return minOk && maxOk;
        }
    }

    private bool MatchesDateTime(DateTime? value)
    {
        if (!value.HasValue) return false;
        if (string.IsNullOrWhiteSpace(Value)) return true;
        
        // 日付のみをパース（時間部分は無視）
        if (!DateTime.TryParse(Value, out var min)) return true;

        if (ComparisonType == ComparisonType.Equals)
        {
            return value.Value.Date == min.Date;
        }
        else
        {
            var minOk = MinBoundaryType == RangeBoundaryType.Inclusive ? value.Value.Date >= min.Date : value.Value.Date > min.Date;
            if (string.IsNullOrWhiteSpace(ValueMax))
            {
                return minOk;
            }
            
            if (!DateTime.TryParse(ValueMax, out var max))
            {
                return minOk;
            }
            
            var maxOk = MaxBoundaryType == RangeBoundaryType.Inclusive ? value.Value.Date <= max.Date : value.Value.Date < max.Date;
            return minOk && maxOk;
        }
    }

    /// <summary>
    /// mm:ss形式の時間長マッチング
    /// </summary>
    private bool MatchesDuration(int valueSeconds)
    {
        if (string.IsNullOrEmpty(Value)) return true;
        if (!DrainTimeConverter.TryParseToSeconds(Value, out var min)) return true;

        if (ComparisonType == ComparisonType.Equals)
        {
            return valueSeconds == min;
        }
        else
        {
            var minOk = MinBoundaryType == RangeBoundaryType.Inclusive ? valueSeconds >= min : valueSeconds > min;
            if (string.IsNullOrEmpty(ValueMax) || !DrainTimeConverter.TryParseToSeconds(ValueMax, out var max))
            {
                return minOk;
            }
            var maxOk = MaxBoundaryType == RangeBoundaryType.Inclusive ? valueSeconds <= max : valueSeconds < max;
            return minOk && maxOk;
        }
    }
}
