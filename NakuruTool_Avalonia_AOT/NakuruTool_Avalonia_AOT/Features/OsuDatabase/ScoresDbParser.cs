using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// scores.dbパーサー（アンマネージドメモリ使用）
/// </summary>
public sealed class ScoresDbParser : IDisposable
{
    /// <summary>
    /// scores.dbファイルを読み込む
    /// </summary>
    public ScoresDatabase ReadScoresDb(string filePath, Action<string, int>? progressCallback = null)
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

        var bufferSpan = buffer.GetBufferSpan();
        int pos = 0;

        // ヘッダー読み込み
        int osuVersion = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;

        int beatmapCount = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;

        var scoresDatabase = new ScoresDatabase
        {
            OsuVersion = osuVersion
        };

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.ScoresLoaded"), beatmapCount), 10);

        // 各ビートマップのスコアを読み込み
        for (int i = 0; i < beatmapCount; i++)
        {
            string beatmapMd5 = ReadStringFromSpan(bufferSpan, ref pos);
            int scoreCount = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
            pos += 4;

            var scoreList = new List<ScoreData>(scoreCount);

            for (int j = 0; j < scoreCount; j++)
            {
                var score = ReadScoreFromSpan(bufferSpan, ref pos);
                scoreList.Add(score);
            }

            if (!string.IsNullOrEmpty(beatmapMd5) && scoreList.Count > 0)
            {
                scoresDatabase.Scores[beatmapMd5] = scoreList;
            }

            // 進捗通知（1000件ごと）
            if (i % 1000 == 0 || i == beatmapCount - 1)
            {
                int progress = 10 + (int)((double)(i + 1) / beatmapCount * 35);
                progressCallback?.Invoke(
                    string.Format(LanguageService.Instance.GetString("Loading.ScoresApplying"), i + 1, beatmapCount),
                    progress);
            }
        }

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.ScoresCompleted"), scoresDatabase.Scores.Count), 50);

        return scoresDatabase;
    }

    /// <summary>
    /// スコアデータを読み込む
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ScoreData ReadScoreFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
    {
        byte ruleset = buffer[pos++];
        int osuVersion = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;

        string beatmapMD5Hash = ReadStringFromSpan(buffer, ref pos);
        string playerName = ReadStringFromSpan(buffer, ref pos);
        string replayMD5Hash = ReadStringFromSpan(buffer, ref pos);

        ushort count300 = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;
        ushort count100 = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;
        ushort count50 = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;
        ushort countGeki = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;
        ushort countKatu = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;
        ushort countMiss = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;

        int replayScore = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;

        ushort combo = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;

        bool perfectCombo = buffer[pos++] != 0;

        int mods = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;

        // 空の文字列をスキップ
        ReadStringFromSpan(buffer, ref pos);

        DateTime scoreTimestamp = ReadDateTimeFromSpan(buffer, ref pos);

        // 4バイトスキップ
        pos += 4;

        long scoreId = BitConverter.ToInt64(buffer.Slice(pos, 8));
        pos += 8;

        return new ScoreData
        {
            Ruleset = ruleset,
            OsuVersion = osuVersion,
            BeatmapMD5Hash = beatmapMD5Hash,
            PlayerName = playerName,
            ReplayMD5Hash = replayMD5Hash,
            Count300 = count300,
            Count100 = count100,
            Count50 = count50,
            CountGeki = countGeki,
            CountKatu = countKatu,
            CountMiss = countMiss,
            ReplayScore = replayScore,
            Combo = combo,
            PerfectCombo = perfectCombo,
            Mods = mods,
            ScoreTimestamp = scoreTimestamp,
            ScoreId = scoreId
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTime ReadDateTimeFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
        => BinaryReaderHelper.ReadDateTimeFromSpan(buffer, ref pos);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadStringFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
        => BinaryReaderHelper.ReadStringFromSpan(buffer, ref pos);

    public void Dispose()
    {
        // 現在はリソース解放不要（UnmanagedBufferはusingで管理）
    }
}