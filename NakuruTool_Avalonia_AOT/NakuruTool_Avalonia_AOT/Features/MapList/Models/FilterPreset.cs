using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Models;

/// <summary>
/// フィルタプリセット（絞り込み条件とコレクション名のセット）
/// </summary>
public class FilterPreset
{
    /// <summary>
    /// プリセット名（ファイル名としても使用）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// コレクション名
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// フィルタ条件のリスト
    /// </summary>
    public List<FilterConditionData> Conditions { get; set; } = new();
}

/// <summary>
/// シリアライズ可能なFilterConditionデータ
/// </summary>
public class FilterConditionData
{
    /// <summary>
    /// フィルタ対象（文字列で保存）
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 比較タイプ（文字列で保存）
    /// </summary>
    public string ComparisonType { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public string ValueMax { get; set; } = string.Empty;

    /// <summary>
    /// ステータス値（文字列で保存）
    /// </summary>
    public string StatusValue { get; set; } = string.Empty;

    /// <summary>
    /// NOT条件フラグ
    /// </summary>
    public bool IsNot { get; set; }

    public bool BoolValue { get; set; }

    /// <summary>
    /// Collection型の選択値
    /// </summary>
    public string CollectionValue { get; set; } = string.Empty;

    /// <summary>
    /// スコア/精度フィルタ用のmod区分（文字列で保存）
    /// </summary>
    public string ScoreModCategory { get; set; } = string.Empty;

    /// <summary>
    /// スコア/精度フィルタ用のスコアシステム区分（文字列で保存）
    /// </summary>
    public string ScoreSystemCategory { get; set; } = string.Empty;

    /// <summary>
    /// 範囲比較時の最小値側の境界タイプ（文字列で保存）
    /// </summary>
    public string MinBoundaryType { get; set; } = string.Empty;

    /// <summary>
    /// 範囲比較時の最大値側の境界タイプ（文字列で保存）
    /// </summary>
    public string MaxBoundaryType { get; set; } = string.Empty;

    /// <summary>
    /// FilterConditionからデータを作成
    /// </summary>
    public static FilterConditionData FromFilterCondition(FilterCondition condition)
    {
        return new FilterConditionData
        {
            Target = condition.Target.ToString(),
            ComparisonType = condition.ComparisonType.ToString(),
            Value = condition.Value,
            ValueMax = condition.ValueMax,
            StatusValue = condition.StatusValue.ToString(),
            IsNot = condition.IsNot,
            BoolValue = condition.BoolValue,
            CollectionValue = condition.CollectionValue,
            ScoreModCategory = condition.ScoreModCategory.ToString(),
            ScoreSystemCategory = condition.ScoreSystemCategory.ToString(),
            MinBoundaryType = condition.MinBoundaryType.ToString(),
            MaxBoundaryType = condition.MaxBoundaryType.ToString()
        };
    }

    /// <summary>
    /// FilterConditionに変換
    /// </summary>
    public FilterCondition ToFilterCondition()
    {
        // 文字列からenumへ変換、失敗時はデフォルト値を使用
        var target = Enum.TryParse<FilterTarget>(Target, out var t) ? t : FilterTarget.KeyCount;
        var comparisonType = Enum.TryParse<ComparisonType>(ComparisonType, out var ct) ? ct : Models.ComparisonType.Equals;
        var statusValue = Enum.TryParse<BeatmapStatus>(StatusValue, out var sv) ? sv : BeatmapStatus.None;
        var scoreModCategory = Enum.TryParse<OsuDatabase.ModCategory>(ScoreModCategory, out var smc) ? smc : OsuDatabase.ModCategory.NoMod;
        var scoreSystemCategory = Enum.TryParse<OsuDatabase.ScoreSystemCategory>(ScoreSystemCategory, out var ssc) ? ssc : OsuDatabase.ScoreSystemCategory.Default;
        var minBoundaryType = Enum.TryParse<RangeBoundaryType>(MinBoundaryType, out var mbt) ? mbt : Models.RangeBoundaryType.Inclusive;
        var maxBoundaryType = Enum.TryParse<RangeBoundaryType>(MaxBoundaryType, out var mxbt) ? mxbt : Models.RangeBoundaryType.Inclusive;

        return new FilterCondition
        {
            Target = target,
            ComparisonType = comparisonType,
            Value = Value,
            ValueMax = ValueMax,
            StatusValue = statusValue,
            IsNot = IsNot,
            BoolValue = BoolValue,
            CollectionValue = CollectionValue,
            ScoreModCategory = scoreModCategory,
            ScoreSystemCategory = scoreSystemCategory,
            MinBoundaryType = minBoundaryType,
            MaxBoundaryType = maxBoundaryType
        };
    }
}

/// <summary>
/// NativeAOT対応のためのJSON Source Generatorコンテキスト
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FilterPreset))]
[JsonSerializable(typeof(List<FilterPreset>))]
[JsonSerializable(typeof(FilterConditionData))]
public partial class FilterPresetJsonContext : JsonSerializerContext
{
}
