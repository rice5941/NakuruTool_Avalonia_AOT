using CommunityToolkit.Mvvm.ComponentModel;
using NakuruTool_Avalonia_AOT.Features.Analyze7k;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
    DrainTime,
    /// <summary>7K Prism の Reg 基準Overall（精度 96.5%、等速）</summary>
    ManiaPrismOverall,
    /// <summary>7K Prism の LN 基準Overall（測定不能の譜面は一致しない）</summary>
    ManiaPrismLnOverall,
    /// <summary>7K Prism の基準スキル値が最大の軸（5ジャンルと LN から選択）</summary>
    ManiaPrismTopGenre,
    /// <summary>7K Prism の基準スキル値（比較対象で 5ジャンルか LN を選び、範囲で絞り込む）</summary>
    ManiaPrismGenreSkill,
    /// <summary>7K Prism のジャンル濃度（%。比較対象で 5ジャンルから選び、範囲で絞り込む）</summary>
    ManiaPrismGenreConcentration,
    /// <summary>7K 段位推定の Regular 段位</summary>
    ManiaDanRegular,
    /// <summary>7K 段位推定の LN 段位（測定不能の譜面は一致しない）</summary>
    ManiaDanLn,
    /// <summary>7K 段位推定の Regular ジャンル（Jack / Tech / Speed / Stream から選択）</summary>
    ManiaDanRegularGenre,
    /// <summary>7K 段位推定の LN ジャンル（General / Tech / Inverse / Release から選択）</summary>
    ManiaDanLnGenre
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
        FilterTarget.ManiaPrismOverall => true,
        FilterTarget.ManiaPrismLnOverall => true,
        FilterTarget.ManiaPrismGenreSkill => true,
        FilterTarget.ManiaPrismGenreConcentration => true,
        FilterTarget.ManiaDanRegular => true,
        FilterTarget.ManiaDanLn => true,
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
        FilterTarget.ManiaPrismOverall => false,
        FilterTarget.ManiaPrismLnOverall => false,
        FilterTarget.ManiaPrismGenreSkill => false,
        FilterTarget.ManiaPrismGenreConcentration => false,
        FilterTarget.ManiaDanRegular => false,
        FilterTarget.ManiaDanLn => false,
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
        FilterTarget.ManiaPrismOverall => true,
        FilterTarget.ManiaPrismLnOverall => true,
        FilterTarget.ManiaPrismGenreSkill => true,
        FilterTarget.ManiaPrismGenreConcentration => true,
        FilterTarget.ManiaDanRegular => true,
        FilterTarget.ManiaDanLn => true,
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

    private static readonly string[] s_prismGenreChoices = Enum.GetNames<PrismGenre>();
    private static readonly string[] s_prismSkillAxisChoices = ManiaChartAnalysis.SkillAxes;
    private static readonly string[] s_regularDanGenreChoices = ["Jack", "Tech", "Speed", "Stream"];
    private static readonly string[] s_lnDanGenreChoices = ["General", "Tech", "Inverse", "Release"];

    /// <summary>
    /// 固定の選択肢から1つ選ぶフィルタ対象かどうか
    /// </summary>
    public static bool IsChoiceType(FilterTarget target) => target is
        FilterTarget.ManiaPrismTopGenre or FilterTarget.ManiaDanRegularGenre or FilterTarget.ManiaDanLnGenre;

    /// <summary>
    /// 選択式のフィルタ対象の選択肢（ジャンル名。表示とプリセット保存の両方に使う）
    /// </summary>
    public static IReadOnlyList<string> ChoiceOptions(FilterTarget target) => target switch
    {
        FilterTarget.ManiaPrismTopGenre => s_prismSkillAxisChoices,
        FilterTarget.ManiaDanRegularGenre => s_regularDanGenreChoices,
        FilterTarget.ManiaDanLnGenre => s_lnDanGenreChoices,
        _ => [],
    };

    /// <summary>
    /// 比較対象（ジャンル）を選んでから範囲で絞り込むフィルタ対象かどうか
    /// </summary>
    public static bool HasCompareTarget(FilterTarget target) => target is
        FilterTarget.ManiaPrismGenreSkill or FilterTarget.ManiaPrismGenreConcentration;

    /// <summary>
    /// 比較対象の選択肢（基準スキル値は 5ジャンル＋LN、ジャンル濃度は 5ジャンル）
    /// </summary>
    public static IReadOnlyList<string> CompareOptions(FilterTarget target) => target switch
    {
        FilterTarget.ManiaPrismGenreSkill => s_prismSkillAxisChoices,
        FilterTarget.ManiaPrismGenreConcentration => s_prismGenreChoices,
        _ => [],
    };

    /// <summary>
    /// 7K 解析の値を使うフィルタ対象かどうか（解析結果が更新されたら絞り込み直す）
    /// </summary>
    public static bool IsAnalyze7k(FilterTarget target) => target is
        FilterTarget.ManiaPrismOverall or FilterTarget.ManiaPrismLnOverall or FilterTarget.ManiaPrismTopGenre or
        FilterTarget.ManiaPrismGenreSkill or FilterTarget.ManiaPrismGenreConcentration or
        FilterTarget.ManiaDanRegular or FilterTarget.ManiaDanLn or
        FilterTarget.ManiaDanRegularGenre or FilterTarget.ManiaDanLnGenre;
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
    /// 選択式の対象の選択値（ジャンル名）
    /// </summary>
    [ObservableProperty]
    private string _choiceValue = string.Empty;

    /// <summary>
    /// 比較対象（ジャンル別の基準スキル値・ジャンル濃度で、どのジャンルの値を比べるか）
    /// </summary>
    [ObservableProperty]
    private string _compareValue = string.Empty;

    /// <summary>
    /// NOT条件（チェック時にマッチ結果を反転）
    /// </summary>
    [ObservableProperty]
    private bool _isNot = false;

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

    private static readonly ObservableCollection<string> s_collectionNames = new();

    /// <summary>
    /// 利用可能なコレクション名一覧（全インスタンスで共有）
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ObservableCollection<string> CollectionNames => s_collectionNames;

    /// <summary>
    /// コレクション名リストをクリアし、再構築用の参照を返す
    /// </summary>
    public static ObservableCollection<string> GetSharedCollectionNames()
    {
        s_collectionNames.Clear();
        return s_collectionNames;
    }

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
    /// 選択式（固定の選択肢から1つ選ぶ）かどうか
    /// </summary>
    public bool IsChoiceType => FilterTargetInfo.IsChoiceType(Target);

    /// <summary>
    /// 選択式の対象の選択肢（ComboBoxのItemsSource用）
    /// </summary>
    public IReadOnlyList<string> ChoiceOptions => FilterTargetInfo.ChoiceOptions(Target);

    /// <summary>
    /// 比較対象（ジャンル）を選ぶかどうか
    /// </summary>
    public bool HasCompareTarget => FilterTargetInfo.HasCompareTarget(Target);

    /// <summary>
    /// 比較対象の選択肢（ComboBoxのItemsSource用）
    /// </summary>
    public IReadOnlyList<string> CompareOptions => FilterTargetInfo.CompareOptions(Target);

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

    /// <summary>
    /// 一致と範囲を切り替えられるかどうか（両方に対応する対象だけ切替を表示する）
    /// </summary>
    public bool CanSwitchComparison => SupportsEquals && SupportsRange && !IsCollectionType;

    private static readonly Func<string, bool> s_canParseInt = static s => int.TryParse(s, out _);
    private static readonly Func<string, bool> s_canParseDouble = static s => double.TryParse(s, out _);
    private static readonly Func<string, bool> s_canParseDate = static s => DateTime.TryParse(s, out _);
    private static readonly Func<string, bool> s_canParseDuration = static s => DrainTimeConverter.TryParseToSeconds(s, out _);

    /// <summary>
    /// 判定に使う値が入力されているか。未入力の条件は判定から外す
    /// （OR の組で未入力の条件が全件一致にならないようにするため）。
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActive => Target switch
    {
        FilterTarget.Collection => !string.IsNullOrEmpty(CollectionValue),
        FilterTarget.Status or FilterTarget.IsPlayed => true,
        FilterTarget.ManiaPrismTopGenre => !string.IsNullOrEmpty(ChoiceValue),
        FilterTarget.ManiaDanRegularGenre => Enum.TryParse<RegularDanGenre>(ChoiceValue, out var regular) && regular != RegularDanGenre.None,
        FilterTarget.ManiaDanLnGenre => Enum.TryParse<LnDanGenre>(ChoiceValue, out var ln) && ln != LnDanGenre.None,
        FilterTarget.KeyCount or FilterTarget.BestScore or FilterTarget.PlayCount => HasInput(s_canParseInt),
        _ when FilterTargetInfo.IsStringType(Target) => !string.IsNullOrEmpty(Value),
        _ when FilterTargetInfo.IsDateType(Target) => HasInput(s_canParseDate),
        _ when FilterTargetInfo.IsDurationType(Target) => HasInput(s_canParseDuration),
        _ when FilterTargetInfo.IsNumericType(Target) => HasInput(s_canParseDouble),
        _ => true
    };

    /// <summary>一致なら値、範囲なら最小値か最大値のどちらかが解釈できるか</summary>
    private bool HasInput(Func<string, bool> canParse) =>
        ComparisonType == ComparisonType.Equals
            ? canParse(Value)
            : canParse(Value) || canParse(ValueMax);

    partial void OnTargetChanged(FilterTarget value)
    {
        OnPropertyChanged(nameof(SupportsRange));
        OnPropertyChanged(nameof(SupportsEquals));
        OnPropertyChanged(nameof(IsDateType));
        OnPropertyChanged(nameof(IsNumericType));
        OnPropertyChanged(nameof(IsStringType));
        OnPropertyChanged(nameof(IsBoolType));
        OnPropertyChanged(nameof(IsEnumType));
        OnPropertyChanged(nameof(IsChoiceType));
        OnPropertyChanged(nameof(ChoiceOptions));
        OnPropertyChanged(nameof(HasCompareTarget));
        OnPropertyChanged(nameof(CompareOptions));
        OnPropertyChanged(nameof(IsCollectionType));
        OnPropertyChanged(nameof(IsDurationType));
        OnPropertyChanged(nameof(IsNonCollectionEqual));
        OnPropertyChanged(nameof(IsScoreOrAccTarget));
        OnPropertyChanged(nameof(CanSwitchComparison));

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
        ChoiceValue = ChoiceOptions.Count > 0 ? ChoiceOptions[0] : string.Empty;
        CompareValue = CompareOptions.Count > 0 ? CompareOptions[0] : string.Empty;
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
    /// Beatmapがこの条件に一致するかどうかを判定（NOT は呼び出し側で反転する）。
    /// 未入力の条件は true を返す。CollectionフィルタはFilterConditionExtensionsで処理するため、ここではtrueを返す
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
            FilterTarget.ManiaPrismOverall => MatchesNullableDouble(beatmap.ChartAnalysis?.PrismOverall),
            FilterTarget.ManiaPrismLnOverall => MatchesNullableDouble(beatmap.ChartAnalysis?.PrismLnOverall),
            FilterTarget.ManiaPrismTopGenre => MatchesPrismTopGenre(beatmap.ChartAnalysis),
            FilterTarget.ManiaPrismGenreSkill => MatchesNullableDouble(beatmap.ChartAnalysis?.SkillValueOf(CompareValue)),
            FilterTarget.ManiaPrismGenreConcentration => MatchesNullableDouble(beatmap.ChartAnalysis?.ConcentrationOf(CompareValue)),
            FilterTarget.ManiaDanRegular => MatchesNullableDouble(beatmap.ChartAnalysis?.RegularDan),
            FilterTarget.ManiaDanLn => MatchesNullableDouble(beatmap.ChartAnalysis?.LnDan),
            FilterTarget.ManiaDanRegularGenre => MatchesRegularDanGenre(beatmap.ChartAnalysis),
            FilterTarget.ManiaDanLnGenre => MatchesLnDanGenre(beatmap.ChartAnalysis),
            FilterTarget.Collection => true, // CollectionフィルタはViewModelで処理
            _ => true
        };
    }

    /// <summary>
    /// 値がない譜面（7K 以外・未解析・測定不能）は、条件が入力されていれば一致しない
    /// </summary>
    private bool MatchesNullableDouble(double? value)
    {
        if (!HasInput(s_canParseDouble)) return true;
        return value is { } v && MatchesDouble(v);
    }

    /// <summary>基準スキル値（5ジャンルと LN）が最大の軸が選んだものと一致するか</summary>
    private bool MatchesPrismTopGenre(ManiaChartAnalysis? analysis)
    {
        if (string.IsNullOrEmpty(ChoiceValue)) return true;
        return analysis?.TopSkillAxis == ChoiceValue;
    }

    private bool MatchesRegularDanGenre(ManiaChartAnalysis? analysis)
    {
        if (!Enum.TryParse<RegularDanGenre>(ChoiceValue, out var genre) || genre == RegularDanGenre.None) return true;
        return analysis is not null && analysis.HasRegularGenre(genre);
    }

    private bool MatchesLnDanGenre(ManiaChartAnalysis? analysis)
    {
        if (!Enum.TryParse<LnDanGenre>(ChoiceValue, out var genre) || genre == LnDanGenre.None) return true;
        return analysis is not null && analysis.HasLnGenre(genre);
    }

    private bool MatchesString(string value)
    {
        if (string.IsNullOrEmpty(Value)) return true;
        return value.Contains(Value, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesNumeric(int value)
    {
        if (ComparisonType == ComparisonType.Equals)
        {
            if (!int.TryParse(Value, out var target)) return true;
            return value == target;
        }

        var hasMin = int.TryParse(Value, out var min);
        var hasMax = int.TryParse(ValueMax, out var max);
        return MatchesRange(value, hasMin, min, hasMax, max);
    }

    private bool MatchesDouble(double value)
    {
        if (ComparisonType == ComparisonType.Equals)
        {
            if (!double.TryParse(Value, out var target)) return true;
            return Math.Abs(value - target) < 0.001;
        }

        var hasMin = double.TryParse(Value, out var min);
        var hasMax = double.TryParse(ValueMax, out var max);
        return MatchesRange(value, hasMin, min, hasMax, max);
    }

    /// <summary>
    /// LN率のマッチング（0～100の整数入力にも対応）
    /// </summary>
    private bool MatchesLongNoteRate(double value)
    {
        if (ComparisonType == ComparisonType.Equals)
        {
            if (!double.TryParse(Value, out var target)) return true;
            return Math.Abs(value - target / 100.0) < 0.001;
        }

        var hasMin = double.TryParse(Value, out var min);
        var hasMax = double.TryParse(ValueMax, out var max);
        return MatchesRange(value, hasMin, min / 100.0, hasMax, max / 100.0);
    }

    private bool MatchesDateTime(DateTime? value)
    {
        // 日付のみで比較する（時間部分は無視）
        if (ComparisonType == ComparisonType.Equals)
        {
            if (!DateTime.TryParse(Value, out var target)) return true;
            return value.HasValue && value.Value.Date == target.Date;
        }

        var hasMin = DateTime.TryParse(Value, out var min);
        var hasMax = DateTime.TryParse(ValueMax, out var max);
        if (!hasMin && !hasMax) return true;
        if (!value.HasValue) return false;

        var date = value.Value.Date;
        if (hasMin && !(MinBoundaryType == RangeBoundaryType.Inclusive ? date >= min.Date : date > min.Date)) return false;
        if (hasMax && !(MaxBoundaryType == RangeBoundaryType.Inclusive ? date <= max.Date : date < max.Date)) return false;
        return true;
    }

    /// <summary>
    /// mm:ss形式の時間長マッチング
    /// </summary>
    private bool MatchesDuration(int valueSeconds)
    {
        if (ComparisonType == ComparisonType.Equals)
        {
            if (!DrainTimeConverter.TryParseToSeconds(Value, out var target)) return true;
            return valueSeconds == target;
        }

        var hasMin = DrainTimeConverter.TryParseToSeconds(Value, out var min);
        var hasMax = DrainTimeConverter.TryParseToSeconds(ValueMax, out var max);
        return MatchesRange(valueSeconds, hasMin, min, hasMax, max);
    }

    /// <summary>
    /// 範囲比較。最小値・最大値は片方だけでもよく、入力のない側は制限しない。
    /// </summary>
    private bool MatchesRange(double value, bool hasMin, double min, bool hasMax, double max)
    {
        if (hasMin && !(MinBoundaryType == RangeBoundaryType.Inclusive ? value >= min : value > min)) return false;
        if (hasMax && !(MaxBoundaryType == RangeBoundaryType.Inclusive ? value <= max : value < max)) return false;
        return true;
    }
}
