using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// 譜面インデックス → ローカルスコア区間 の常駐ストア。
/// <para>
/// メモリ設計: 全スコアを 1 本の <see cref="LocalScore"/> 配列 (32B/件) に連続配置し、
/// 譜面ごとの区間は CSR 形式 (<c>_entryStarts</c>) で表す。譜面配列と同じ並びの
/// <c>_beatmapToEntry</c> (4B/譜面) で O(1) に引けるため、MD5 文字列はストア側で保持しない。
/// </para>
/// </summary>
public sealed class LocalScoreStore
{
    public static readonly LocalScoreStore Empty = new([], [0], []);

    private readonly LocalScore[] _scores;
    private readonly int[] _entryStarts;
    private readonly int[] _beatmapToEntry;

    private LocalScoreStore(LocalScore[] scores, int[] entryStarts, int[] beatmapToEntry)
    {
        _scores = scores;
        _entryStarts = entryStarts;
        _beatmapToEntry = beatmapToEntry;
    }

    /// <summary>
    /// パース結果を譜面インデックスに対応付けてストアを構築する。
    /// osu!.db に存在しない譜面のエントリは参照されないまま残る（区間情報のみ）。
    /// </summary>
    public static LocalScoreStore Build(ScoresDbParseResult parsed, IReadOnlyDictionary<string, int> beatmapIndex, int beatmapCount)
    {
        if (parsed.EntryCount == 0 || beatmapCount == 0)
        {
            return Empty;
        }

        var beatmapToEntry = new int[beatmapCount];
        beatmapToEntry.AsSpan().Fill(-1);

        var md5s = parsed.BeatmapMd5s;
        for (int entry = 0; entry < md5s.Length; entry++)
        {
            var md5 = md5s[entry];
            if (!string.IsNullOrEmpty(md5) &&
                beatmapIndex.TryGetValue(md5, out var index) &&
                (uint)index < (uint)beatmapCount &&
                beatmapToEntry[index] < 0)
            {
                beatmapToEntry[index] = entry;
            }
        }

        return new LocalScoreStore(parsed.Scores, parsed.EntryStarts, beatmapToEntry);
    }

    /// <summary>
    /// 指定譜面インデックスのスコア（ReplayScore 降順）を返す。無ければ空。
    /// </summary>
    public ReadOnlySpan<LocalScore> GetScores(int beatmapIndex)
    {
        if ((uint)beatmapIndex >= (uint)_beatmapToEntry.Length)
        {
            return ReadOnlySpan<LocalScore>.Empty;
        }

        int entry = _beatmapToEntry[beatmapIndex];
        if (entry < 0)
        {
            return ReadOnlySpan<LocalScore>.Empty;
        }

        int start = _entryStarts[entry];
        return _scores.AsSpan(start, _entryStarts[entry + 1] - start);
    }
}
