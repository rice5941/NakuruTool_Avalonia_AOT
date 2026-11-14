using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>
/// 段位推定の Regular / LN ジャンルの判定。
/// Mania Tracker player/skill-ratings.ts の 7K tile 判定の移植。
/// </summary>
public static class DanBuckets
{
    public const string Jack = "jack";
    public const string Tech = "tech";
    public const string Speed = "speed";
    public const string Stream = "stream";
    public const string LnGeneral = "lngeneral";
    public const string LnTech = "lntech";
    public const string LnInverse = "lninverse";
    public const string LnRelease = "lnrelease";

    /// <summary>
    /// 譜面の段位ジャンルの ID を返す。bucketFeatures が無い（または版が違う）場合は null（判定できない）。
    /// </summary>
    public static IReadOnlyList<string>? BucketIds(DanVerdict? verdict, DanSide side)
    {
        var parsed = verdict?.BucketFeatures;
        if (parsed is null || parsed.Version != 1)
            return null;

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var p in parsed.Patterns ?? [])
        {
            scores[p.Id] = Math.Max(scores.GetValueOrDefault(p.Id, 0), p.Score);
        }
        double Score(string id) => scores.GetValueOrDefault(id, 0);

        if (side == DanSide.Ln)
        {
            var lnRatio = parsed.LnRatio ?? 0;
            var result = new List<string>();
            void Add(string key, params string[] tags)
            {
                foreach (var tag in tags)
                {
                    if (Score(tag) >= 0.5 && lnRatio >= 0.375)
                    {
                        result.Add(key);
                        return;
                    }
                }
            }
            Add(LnGeneral, "lngeneral", "ln");
            Add(LnTech, "lntech");
            Add(LnInverse, "lninverse");
            Add(LnRelease, "lnrelease");
            return result;
        }

        var tiles = RiceTiles(parsed, scores);
        if (tiles is not null)
        {
            var shape = parsed.RiceShape;
            if (tiles is [Stream] && (shape?.SpeedPeak ?? 0) >= 0.2)
                return [Stream, Speed];
            if (tiles is [Tech] && (shape?.RollSections ?? 0) >= 2)
                return [Tech, Speed];
            return tiles;
        }

        var clusters = parsed.Clusters ?? [];
        double total = 0;
        foreach (var c in clusters)
        {
            if (c.Importance > 0)
                total += c.Importance;
        }
        double? Share(string pattern)
        {
            if (total == 0)
                return null;
            double sum = 0;
            foreach (var c in clusters)
            {
                if (c.Importance > 0 && c.Pattern.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    sum += c.Importance;
            }
            return sum / total;
        }

        var jack = Share("jack");
        var stream = Share("stream");
        var category = (parsed.ClusterCategory ?? string.Empty).Trim();
        var veto = Score("jack") >= 0.8 || (jack is { } j ? j >= 0.4 : Score("chordjack") >= 0.8);
        var tech = category.Length > 0
            ? category.Contains("tech", StringComparison.OrdinalIgnoreCase)
            : Score("tech") >= 0.5 && !veto;
        var chord = Share("chordstream");

        var ids = new List<string>();
        if (jack is { } jk ? jk >= 0.4 : Score("jack") >= 0.5 || Score("chordjack") >= 0.8)
            ids.Add(Jack);
        if (tech)
            ids.Add(Tech);
        if (Score("delay") >= 0.25)
            ids.Add(Speed);
        if (stream is { } st
                ? st >= 0.4
                : Score("bracket") >= 0.5 || (Score("chordstream") >= 0.5 && (chord is null || chord >= 0.4)))
            ids.Add(Stream);
        return ids;
    }

    /// <summary>Regular の段位ジャンル（判定材料がなければ None）</summary>
    public static RegularDanGenre RegularGenres(DanVerdict? verdict)
    {
        var result = RegularDanGenre.None;
        foreach (var id in BucketIds(verdict, DanSide.Regular) ?? [])
        {
            result |= id switch
            {
                Jack => RegularDanGenre.Jack,
                Tech => RegularDanGenre.Tech,
                Speed => RegularDanGenre.Speed,
                Stream => RegularDanGenre.Stream,
                _ => RegularDanGenre.None,
            };
        }
        return result;
    }

    /// <summary>LN の段位ジャンル（判定材料がなければ None）</summary>
    public static LnDanGenre LnGenres(DanVerdict? verdict)
    {
        var result = LnDanGenre.None;
        foreach (var id in BucketIds(verdict, DanSide.Ln) ?? [])
        {
            result |= id switch
            {
                LnGeneral => LnDanGenre.General,
                LnTech => LnDanGenre.Tech,
                LnInverse => LnDanGenre.Inverse,
                LnRelease => LnDanGenre.Release,
                _ => LnDanGenre.None,
            };
        }
        return result;
    }

    /// <summary>rice_tiles(): クラスタと配置特徴から Regular の分類を決める。判定できなければ null。</summary>
    private static IReadOnlyList<string>? RiceTiles(BucketFeatures parsed, Dictionary<string, double> scores)
    {
        double Score(string id) => scores.GetValueOrDefault(id, 0);
        var category = (parsed.ClusterCategory ?? string.Empty).ToLowerInvariant().Trim();
        IReadOnlyList<ClusterInfo> clusters = parsed.Clusters ?? [];

        var rice = new List<ClusterInfo>();
        foreach (var c in clusters)
        {
            if (c.Pattern.ToLowerInvariant() is "chordstream" or "stream" or "jack" or "jacks")
                rice.Add(c);
        }

        static double Weight(IReadOnlyList<ClusterInfo> cs)
        {
            double sum = 0;
            foreach (var c in cs)
                sum += Math.Max(0, c.Importance);
            return sum;
        }

        if (Weight(rice) > 0 && Weight(rice) >= 0.3 * Weight(clusters) && (parsed.LnRatio ?? 0) < 0.5)
            clusters = rice;

        double total = Weight(clusters);
        if (total == 0 || category.Length == 0)
            return null;

        double Share(Func<ClusterInfo, bool> test)
        {
            double sum = 0;
            foreach (var c in clusters)
            {
                if (c.Importance > 0 && test(c))
                    sum += c.Importance;
            }
            return sum / total;
        }

        double jack = Share(c => c.Pattern.Contains("jack", StringComparison.OrdinalIgnoreCase));
        double single = Share(c => c.Pattern.Equals("stream", StringComparison.OrdinalIgnoreCase));
        double mixed = Share(c => c.Mixed == true);
        double dense = Share(c => c.Label.Contains("dense", StringComparison.OrdinalIgnoreCase));
        double chord = Share(c => c.Pattern.Contains("chordstream", StringComparison.OrdinalIgnoreCase));

        var shape = parsed.RiceShape;
        double? two = shape?.TwoBack;
        double? tech = shape?.TechSections;
        double speedPeak = shape?.SpeedPeak ?? 0;
        double rollSections = shape?.RollSections ?? 0;
        double motifRepeat = shape?.MotifRepeat ?? 0;
        double delay = Score("delay");

        bool jackHeadline = category.Contains("jack") &&
                            (!category.Contains("tech") || jack >= 0.4 || Score("jack") >= 0.9);
        if (jackHeadline || jack >= 0.5)
        {
            if (delay >= 0.9 && speedPeak >= 0.75)
            {
                if (rollSections >= 2)
                    return [Speed];
                if (category.Contains("tech"))
                    return [Tech];
            }
            return [Jack];
        }

        if (two is null)
        {
            if (delay >= 0.9 || single >= 0.4)
                return [Speed];
        }
        else
        {
            if ((two < 0.15 && (tech ?? 0) < 0.4) || (delay >= 0.9 && two < 0.2))
                return [Speed];
            if (delay >= 0.7 && motifRepeat >= 0.3)
                return [Speed];
            if (delay >= 0.9 && two < 0.3)
                return [Tech, Speed];
        }

        if (delay >= 0.7 && jack >= 0.3)
            return [Speed, Jack];

        bool denseHeadline = category.Contains("dense chordstream");
        bool light = !denseHeadline && category.Contains("chordstream") && (tech ?? 0) < 0.25;
        if (jack < 0.4 && !category.Contains("tech") && (denseHeadline || light) &&
            mixed >= 0.25 && dense < 0.5 && two is { } t && t >= 0.3)
        {
            if (light && mixed >= 0.45)
                return [Tech, Stream];
            bool jacked = jack >= 0.2 || (light && jack >= 0.15 && Score("jack") >= 0.5);
            return jacked ? [Stream, Jack] : [Stream];
        }

        if (jack >= 0.4 || (category.Contains("tech") && dense < 0.9) || (mixed >= 0.25 && dense < 0.5))
            return [Tech];

        bool denseChart = dense >= 0.5 || chord >= 0.85;
        if (two is { } tw && tw < (denseChart ? 0.2 : 0.25))
            return [Tech];
        if (tech is { } te && te >= (denseChart ? 0.5 : 0.3))
            return [Tech];
        if (mixed >= 0.25 && single >= 0.25 && jack >= 0.1)
            return [Tech];
        if (chord >= 0.4 || Score("bracket") >= 0.5)
            return [Stream];
        return [Tech];
    }
}
