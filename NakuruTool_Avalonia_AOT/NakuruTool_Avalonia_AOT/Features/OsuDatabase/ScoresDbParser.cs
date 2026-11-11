using System;
using System.IO;
using System.Runtime.CompilerServices;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// scores.db のパース結果（CSR 形式）。
/// エントリ i のスコアは <c>Scores[EntryStarts[i]..EntryStarts[i + 1]]</c> で、ReplayScore 降順に整列済み。
/// mania スコアが1件以上あるエントリのみを含む。
/// </summary>
public sealed class ScoresDbParseResult
{
    public static readonly ScoresDbParseResult Empty = new([], [0], []);

    public ScoresDbParseResult(string[] beatmapMd5s, int[] entryStarts, LocalScore[] scores)
    {
        BeatmapMd5s = beatmapMd5s;
        EntryStarts = entryStarts;
        Scores = scores;
    }

    /// <summary>エントリごとの譜面 MD5</summary>
    public string[] BeatmapMd5s { get; }

    /// <summary>エントリ開始位置（長さ = エントリ数 + 1）</summary>
    public int[] EntryStarts { get; }

    /// <summary>全エントリのスコアを連続配置した配列</summary>
    public LocalScore[] Scores { get; }

    public int EntryCount => BeatmapMd5s.Length;
}

/// <summary>
/// scores.dbパーサー（アンマネージドメモリ使用）。
/// 1パス目で件数のみ数えて配列をちょうどの長さで確保し、2パス目で値を詰める。
/// スコア内の文字列（譜面MD5・プレイヤー名・リプレイMD5）はデコードせず読み飛ばす。
/// </summary>
public sealed class ScoresDbParser : IDisposable
{
    private const byte ManiaRuleset = 3;
    private const int TargetPractice = 1 << 23;

    /// <summary>
    /// scores.dbファイルを読み込む
    /// </summary>
    public ScoresDbParseResult ReadScoresDb(string filePath, Action<string, int>? progressCallback = null)
    {
        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.ScoresLoading"), 0);

        var fileInfo = new FileInfo(filePath);
        int fileSize = (int)fileInfo.Length;

        using var buffer = new UnmanagedBuffer(fileSize);

        // ファイル全体を一括読み込み
        using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.None))
        {
            int totalRead = 0;
            while (totalRead < fileSize)
            {
                int bytesRead = buffer.ReadFromStream(fileStream, totalRead, fileSize - totalRead);
                if (bytesRead == 0)
                    throw new InvalidDataException("Unexpected end of file during read");
                totalRead += bytesRead;
            }
        }

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.ScoresLoading"), 5);

        return Parse(buffer.GetBufferSpan(), progressCallback);
    }

    /// <summary>
    /// scores.db のバイト列をパースする（テスト用に internal 公開）
    /// </summary>
    internal static ScoresDbParseResult Parse(ReadOnlySpan<byte> buffer, Action<string, int>? progressCallback = null)
    {
        int pos = 0;

        // ヘッダー: version(int) + beatmapCount(int)
        pos += 4;
        int beatmapCount = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;
        int bodyStart = pos;

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.ScoresLoaded"), beatmapCount), 10);

        // ---- パス1: mania スコアを含むエントリ数と mania スコア総数を数える ----
        int entryCount = 0;
        int totalScores = 0;
        for (int i = 0; i < beatmapCount; i++)
        {
            BinaryReaderHelper.SkipString(buffer, ref pos);
            int scoreCount = BitConverter.ToInt32(buffer.Slice(pos, 4));
            pos += 4;

            int maniaCount = 0;
            for (int j = 0; j < scoreCount; j++)
            {
                if (SkipScore(buffer, ref pos) == ManiaRuleset)
                    maniaCount++;
            }

            if (maniaCount > 0)
            {
                entryCount++;
                totalScores += maniaCount;
            }
        }

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), 0, beatmapCount), 20);

        if (entryCount == 0)
        {
            return ScoresDbParseResult.Empty;
        }

        // ---- パス2: ちょうどの長さで確保して値を詰める ----
        var md5s = new string[entryCount];
        var entryStarts = new int[entryCount + 1];
        var scores = new LocalScore[totalScores];

        pos = bodyStart;
        int entry = 0;
        int scoreIndex = 0;
        for (int i = 0; i < beatmapCount; i++)
        {
            int md5Pos = pos;
            BinaryReaderHelper.SkipString(buffer, ref pos);
            int scoreCount = BitConverter.ToInt32(buffer.Slice(pos, 4));
            pos += 4;

            int start = scoreIndex;
            for (int j = 0; j < scoreCount; j++)
            {
                if (TryReadManiaScore(buffer, ref pos, out var score))
                    scores[scoreIndex++] = score;
            }

            int count = scoreIndex - start;
            if (count > 0)
            {
                // MD5 文字列はスコアを持つエントリのみデコードする
                md5s[entry] = BinaryReaderHelper.ReadStringFromSpan(buffer, ref md5Pos);
                entryStarts[entry] = start;
                entry++;

                if (count > 1)
                    scores.AsSpan(start, count).Sort(CompareByScoreDescending);
            }

            if (i % 1000 == 0 || i == beatmapCount - 1)
            {
                int progress = 20 + (int)((double)(i + 1) / beatmapCount * 30);
                progressCallback?.Invoke(
                    string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), i + 1, beatmapCount),
                    progress);
            }
        }
        entryStarts[entryCount] = scoreIndex;

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.ScoresCompleted"), entryCount), 50);

        return new ScoresDbParseResult(md5s, entryStarts, scores);
    }

    /// <summary>スコア降順、同点は古いプレイを上位とする</summary>
    private static int CompareByScoreDescending(LocalScore a, LocalScore b)
    {
        int c = b.ReplayScore.CompareTo(a.ReplayScore);
        return c != 0 ? c : a.TimestampTicks.CompareTo(b.TimestampTicks);
    }

    /// <summary>
    /// スコア1件を読み飛ばし、Ruleset を返す
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte SkipScore(ReadOnlySpan<byte> buffer, ref int pos)
    {
        byte ruleset = buffer[pos++];
        pos += 4; // version
        BinaryReaderHelper.SkipString(buffer, ref pos); // beatmap MD5
        BinaryReaderHelper.SkipString(buffer, ref pos); // player name
        BinaryReaderHelper.SkipString(buffer, ref pos); // replay MD5
        pos += 2 * 6; // 300/100/50/Geki/Katu/Miss
        pos += 4;     // score
        pos += 2;     // max combo
        pos += 1;     // perfect combo
        int mods = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;
        SkipScoreTail(buffer, ref pos, mods);
        return ruleset;
    }

    /// <summary>
    /// スコア1件を読み、mania の場合のみ LocalScore を返す（非 mania は読み飛ばし）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadManiaScore(ReadOnlySpan<byte> buffer, ref int pos, out LocalScore score)
    {
        if (buffer[pos] != ManiaRuleset)
        {
            SkipScore(buffer, ref pos);
            score = default;
            return false;
        }

        pos += 1; // ruleset
        pos += 4; // version
        BinaryReaderHelper.SkipString(buffer, ref pos); // beatmap MD5
        BinaryReaderHelper.SkipString(buffer, ref pos); // player name
        BinaryReaderHelper.SkipString(buffer, ref pos); // replay MD5

        ushort count300 = BitConverter.ToUInt16(buffer.Slice(pos, 2)); pos += 2;
        ushort count100 = BitConverter.ToUInt16(buffer.Slice(pos, 2)); pos += 2;
        ushort count50 = BitConverter.ToUInt16(buffer.Slice(pos, 2)); pos += 2;
        ushort countGeki = BitConverter.ToUInt16(buffer.Slice(pos, 2)); pos += 2;
        ushort countKatu = BitConverter.ToUInt16(buffer.Slice(pos, 2)); pos += 2;
        ushort countMiss = BitConverter.ToUInt16(buffer.Slice(pos, 2)); pos += 2;

        int replayScore = BitConverter.ToInt32(buffer.Slice(pos, 4)); pos += 4;
        pos += 2; // max combo
        pos += 1; // perfect combo
        int mods = BitConverter.ToInt32(buffer.Slice(pos, 4)); pos += 4;

        BinaryReaderHelper.SkipString(buffer, ref pos); // life bar graph (空文字列)
        long timestampTicks = BitConverter.ToInt64(buffer.Slice(pos, 8)); pos += 8;
        pos += 4; // 常に -1
        pos += 8; // online score id
        if ((mods & TargetPractice) != 0)
            pos += 8; // Target Practice の追加情報 (double)

        score = new LocalScore(timestampTicks, replayScore, mods,
            countGeki, count300, countKatu, count100, count50, countMiss);
        return true;
    }

    /// <summary>
    /// Mods より後ろ（life bar ～ online score id ～ TP 追加情報）を読み飛ばす
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SkipScoreTail(ReadOnlySpan<byte> buffer, ref int pos, int mods)
    {
        BinaryReaderHelper.SkipString(buffer, ref pos); // life bar graph
        pos += 8; // timestamp
        pos += 4; // 常に -1
        pos += 8; // online score id
        if ((mods & TargetPractice) != 0)
            pos += 8;
    }

    public void Dispose()
    {
        // 現在はリソース解放不要（UnmanagedBufferはusingで管理）
    }
}
