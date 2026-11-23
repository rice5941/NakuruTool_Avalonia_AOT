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
    /// フィルタ条件のリスト（条件の組が 1 つのとき。v3.1 以前と同じ形式）
    /// </summary>
    public List<FilterConditionData> Conditions { get; set; } = new();

    /// <summary>
    /// 条件の組のリスト（条件の組が 2 つ以上のとき。このとき <see cref="Conditions"/> は空）。
    /// 古いバージョンでは条件なしとして読まれ、誤った条件でコレクションが作られることはない。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FilterConditionGroupData>? Groups { get; set; }

    /// <summary>
    /// 条件の組を返す。<see cref="Groups"/> がなければ <see cref="Conditions"/> を 1 つの組として扱う。
    /// </summary>
    public List<List<FilterConditionData>> GetGroups()
    {
        if (Groups is { Count: > 0 })
            return Groups.ConvertAll(g => g.Conditions ?? new List<FilterConditionData>());
        return new List<List<FilterConditionData>> { Conditions ?? new List<FilterConditionData>() };
    }

    /// <summary>
    /// 条件の組からプリセットを作る。空の組は除き、組が 1 つ以下なら <see cref="Conditions"/>、
    /// 2 つ以上なら <see cref="Groups"/> に入れる。
    /// </summary>
    public static FilterPreset Create(string name, string collectionName, IEnumerable<List<FilterConditionData>> groups)
    {
        var nonEmpty = new List<List<FilterConditionData>>();
        foreach (var group in groups)
        {
            if (group.Count > 0) nonEmpty.Add(group);
        }

        var preset = new FilterPreset { Name = name, CollectionName = collectionName };
        if (nonEmpty.Count > 1)
            preset.Groups = nonEmpty.ConvertAll(g => new FilterConditionGroupData { Conditions = g });
        else if (nonEmpty.Count == 1)
            preset.Conditions = nonEmpty[0];
        return preset;
    }

    /// <summary>
    /// 7K 解析の値（Prism・段位推定）を使う条件を含むか。
    /// 起動時の自動一括生成では、このプリセットは解析が終わってから生成する。
    /// </summary>
    public bool UsesAnalyze7k() =>
        GetGroups().Exists(group =>
            group.Exists(c => Enum.TryParse<FilterTarget>(c.Target, out var target) && FilterTargetInfo.IsAnalyze7k(target)));
}

/// <summary>
/// シリアライズ可能な条件の組
/// </summary>
public class FilterConditionGroupData
{
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
    /// 選択式の対象の選択値（ジャンル名）
    /// </summary>
    public string ChoiceValue { get; set; } = string.Empty;

    /// <summary>
    /// 比較対象（ジャンル別の基準スキル値・ジャンル濃度のジャンル名）
    /// </summary>
    public string CompareValue { get; set; } = string.Empty;

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
            ChoiceValue = condition.ChoiceValue,
            CompareValue = condition.CompareValue,
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

        var condition = new FilterCondition
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
        // Target の設定で選択値・比較対象が先頭の選択肢に戻るため、最後に設定する
        if (!string.IsNullOrEmpty(ChoiceValue))
            condition.ChoiceValue = ChoiceValue;
        if (!string.IsNullOrEmpty(CompareValue))
            condition.CompareValue = CompareValue;
        return condition;
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
[JsonSerializable(typeof(FilterConditionGroupData))]
public partial class FilterPresetJsonContext : JsonSerializerContext
{
}
