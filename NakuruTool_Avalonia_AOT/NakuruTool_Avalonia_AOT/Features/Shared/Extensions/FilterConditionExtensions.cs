using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using System;
using System.Collections.Generic;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.Shared.Extensions;

public static class FilterConditionExtensions
{
    // コレクションフィルタ用MD5キャッシュ（アプリ内共有、RefreshCollectionNames呼び出し時にクリア）
    private static readonly Dictionary<string, HashSet<string>> s_collectionMd5Cache = new();

    /// <summary>キャッシュをクリアする（DBリロード時に呼び出す）</summary>
    public static void ClearCollectionCache() => s_collectionMd5Cache.Clear();

    /// <summary>
    /// 条件の組から、値の入った条件だけを残した組の配列を作る（値の入った条件がない組は除く）。
    /// 絞り込みは全譜面に対して回るため、条件が変わったときに一度だけ作って <see cref="MatchesAnyGroup"/> に渡す。
    /// </summary>
    public static FilterCondition[][] BuildActiveGroups(this IEnumerable<IEnumerable<FilterCondition>> groups)
    {
        var result = new List<FilterCondition[]>();
        foreach (var group in groups)
        {
            var active = new List<FilterCondition>();
            foreach (var condition in group)
            {
                if (condition.IsActive) active.Add(condition);
            }
            if (active.Count > 0) result.Add(active.ToArray());
        }
        return result.ToArray();
    }

    /// <summary>
    /// Beatmapがいずれかの組のすべての条件に一致するかを判定する（NOT反転・Collectionフィルタ対応）。
    /// 組が 1 つもなければ（条件がない・すべて未入力）すべての譜面が一致する。
    /// </summary>
    /// <param name="activeGroups"><see cref="BuildActiveGroups"/> で作った組</param>
    /// <param name="beatmap">評価対象のBeatmap</param>
    /// <param name="databaseService">DB参照用</param>
    public static bool MatchesAnyGroup(
        this FilterCondition[][] activeGroups,
        Beatmap beatmap,
        IDatabaseService databaseService)
    {
        if (activeGroups.Length == 0) return true;

        foreach (var group in activeGroups)
        {
            if (MatchesAll(group, beatmap, databaseService)) return true;
        }
        return false;
    }

    /// <summary>
    /// Beatmapが組のすべての条件に一致するかを判定する
    /// </summary>
    private static bool MatchesAll(FilterCondition[] conditions, Beatmap beatmap, IDatabaseService databaseService)
    {
        foreach (var condition in conditions)
        {
            var matched = condition.Target == FilterTarget.Collection
                ? MatchesCollection(beatmap, condition.CollectionValue, databaseService)
                : condition.Matches(beatmap);

            if (condition.IsNot) matched = !matched;
            if (!matched) return false;
        }
        return true;
    }

    /// <summary>
    /// コレクションによるフィルタリング（HashSetキャッシュでO(1)ルックアップ）
    /// </summary>
    /// <param name="beatmap">評価対象のBeatmap</param>
    /// <param name="collectionName">コレクション名</param>
    /// <param name="databaseService">DB参照用</param>
    private static bool MatchesCollection(
        Beatmap beatmap,
        string collectionName,
        IDatabaseService databaseService)
    {
        if (string.IsNullOrEmpty(collectionName)) return true;

        if (!s_collectionMd5Cache.TryGetValue(collectionName, out var md5Set))
        {
            var col = databaseService.OsuCollections
                .AsValueEnumerable()
                .FirstOrDefault(c => c.Name == collectionName);
            md5Set = col != null
                ? new HashSet<string>(col.BeatmapMd5s, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();
            s_collectionMd5Cache[collectionName] = md5Set;
        }
        return md5Set.Contains(beatmap.MD5Hash);
    }
}
