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
    /// Beatmapがすべての条件に一致するかを判定する（NOT反転・Collectionフィルタ対応）
    /// </summary>
    /// <param name="conditions">評価する条件リスト</param>
    /// <param name="beatmap">評価対象のBeatmap</param>
    /// <param name="databaseService">DB参照用</param>
    public static bool MatchesAll(
        this IEnumerable<FilterCondition> conditions,
        Beatmap beatmap,
        IDatabaseService databaseService)
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
