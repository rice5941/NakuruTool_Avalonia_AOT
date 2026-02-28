using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// パース前の生ビートマップデータを保持する構造体（バッファ内相対オフセット）
/// </summary>
public readonly struct RawBeatmapData
{
    public readonly int StartOffset;
    public readonly int EndOffset;

    public RawBeatmapData(int startOffset, int endOffset)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
    }
}

/// <summary>
/// osu!.dbパーサー
/// シングルスレッドで一括読み込み + 並列インスタンス展開（アンマネージドメモリ使用）
/// </summary>
public sealed class OsuDbParser : IDisposable
{
    public int OsuVersion { get; private set; }
    public int FolderCount { get; private set; }
    public bool AccountUnlocked { get; private set; }
    public DateTime UnlockDate { get; private set; }
    public string PlayerName { get; private set; } = string.Empty;
    public int BeatmapCount { get; private set; }

    /// <summary>
    /// 一括読み込み→並列インスタンス展開を行うメソッド（アンマネージドメモリ版）
    /// </summary>
    public Beatmap[] ReadAndProcessChunked(string filePath, Action<string, int>? progressCallback = null)
    {
        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuLoading"), 0);

        // ファイル全体を一括読み込み
        var fileInfo = new FileInfo(filePath);
        int fileSize = (int)fileInfo.Length;

        using var buffer = new UnmanagedBuffer(fileSize);

        // 大規模ファイルの一括読み込みに最適化（1MBバッファ、None）
        // コールドスタートを意識. SequentialScanのほうが遅延大.
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

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuLoading"), 10);

        // ヘッダー読み込み
        var bufferSpan = buffer.GetBufferSpan();
        if (bufferSpan.Length < 17)
        {
            throw new InvalidDataException("Invalid osu!.db file: header too small");
        }

        int pos = 0;
        OsuVersion = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;
        FolderCount = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;
        AccountUnlocked = bufferSpan[pos++] != 0;
        UnlockDate = ReadDateTimeFromSpan(bufferSpan, ref pos);
        PlayerName = ReadStringFromSpan(bufferSpan, ref pos);
        BeatmapCount = BitConverter.ToInt32(bufferSpan.Slice(pos, 4));
        pos += 4;

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.OsuVersion"), OsuVersion), 12);
        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.BeatmapCount"), BeatmapCount), 15);

        int osuVersion = OsuVersion;
        int bufferLength = bufferSpan.Length;

        // シングルスレッドで全ビートマップのオフセットをスキャン
        var rawDataList = new RawBeatmapData[BeatmapCount];
        int actualCount = 0;

        for (int i = 0; i < BeatmapCount; i++)
        {
            int startOffset = pos;

            if (!TrySkipBeatmapEntry(bufferSpan, ref pos, bufferLength, osuVersion))
            {
                throw new InvalidDataException($"Failed to parse beatmap entry at index {i}, offset {startOffset}");
            }

            rawDataList[actualCount++] = new RawBeatmapData(startOffset, pos);

            // 進捗通知（5000件ごと）
            if (actualCount % 5000 == 0)
            {
                int progress = 15 + (int)((double)actualCount / BeatmapCount * 35);
                progressCallback?.Invoke(
                    string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), actualCount, BeatmapCount),
                    progress);
            }
        }

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), actualCount, BeatmapCount), 50);

        // 並列でインスタンス展開
        var results = new Beatmap?[actualCount];
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        unsafe
        {
            byte* bufferPtr = buffer.GetBufferPtr();
            int bufferSize = buffer.BufferSize;

            Parallel.For(0, actualCount, parallelOptions, i =>
            {
                results[i] = ParseBeatmapFromBuffer(bufferPtr, bufferSize, rawDataList[i], osuVersion);
            });
        }

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), actualCount, actualCount), 80);

        // 結果を収集（有効なビートマップのみ）
        var resultArray = new Beatmap[actualCount];
        int resultCount = 0;

        for (int i = 0; i < actualCount; i++)
        {
            var beatmap = results[i];
            if (beatmap != null &&
                !string.IsNullOrEmpty(beatmap.MD5Hash) &&
                beatmap.KeyCount != 0)
            {
                resultArray[resultCount++] = beatmap;
            }
        }

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuSorting"), 82);

        // MD5Hashでソート（重複排除の準備）
        var validSpan = resultArray.AsSpan(0, resultCount);
        validSpan.Sort(static (a, b) => string.CompareOrdinal(a.MD5Hash, b.MD5Hash));

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuSorting"), 90);

        // 隣接比較で重複排除
        int uniqueCount = 0;
        for (int i = 0; i < resultCount; i++)
        {
            if (i == 0 || !string.Equals(resultArray[i].MD5Hash, resultArray[i - 1].MD5Hash, StringComparison.Ordinal))
            {
                if (uniqueCount != i)
                {
                    resultArray[uniqueCount] = resultArray[i];
                }
                uniqueCount++;
            }
        }

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuCompleted"), 100);

        // 配列サイズを縮小して返す
        if (uniqueCount == resultCount && resultCount == resultArray.Length)
        {
            return resultArray;
        }
        return resultArray.AsSpan(0, uniqueCount).ToArray();
    }

    /// <summary>
    /// バッファ内でビートマップエントリをスキップ（境界チェック付き）
    /// </summary>
    private bool TrySkipBeatmapEntry(ReadOnlySpan<byte> buffer, ref int pos, int bufferLength, int osuVersion)
    {
        int startPos = pos;

        try
        {
            if (osuVersion < 20191106)
            {
                if (pos + 4 > bufferLength) { pos = startPos; return false; }
                pos += 4;
            }

            // 9つの文字列をスキップ
            for (int i = 0; i < 9; i++)
            {
                if (!TrySkipString(buffer, ref pos, bufferLength)) { pos = startPos; return false; }
            }

            // RankedStatus(1) + CirclesCount(2) + SlidersCount(2) + SpinnersCount(2) = 7
            // LastModifiedTime(8) = 8
            // 合計 15バイト
            if (pos + 15 > bufferLength) { pos = startPos; return false; }
            pos += 15;

            if (osuVersion >= 20140609)
            {
                // AR(4) + CS(4) + HP(4) + OD(4) = 16
                if (pos + 16 > bufferLength) { pos = startPos; return false; }
                pos += 16;
            }
            else
            {
                if (pos + 4 > bufferLength) { pos = startPos; return false; }
                pos += 4;
            }

            // SliderVelocity(8)
            if (pos + 8 > bufferLength) { pos = startPos; return false; }
            pos += 8;

            // StarRating dictionaries
            if (osuVersion >= 20140609)
            {
                for (int dictIndex = 0; dictIndex < 4; dictIndex++)
                {
                    if (!TrySkipIntDoubleDictionary(buffer, ref pos, bufferLength)) { pos = startPos; return false; }
                }
            }

            // DrainTime(4) + TotalTime(4) + AudioPreviewTime(4) = 12
            if (pos + 12 > bufferLength) { pos = startPos; return false; }
            pos += 12;

            // TimingPoints
            if (pos + 4 > bufferLength) { pos = startPos; return false; }
            int timingPointCount = BitConverter.ToInt32(buffer.Slice(pos, 4));
            pos += 4;
            if (timingPointCount < 0 || timingPointCount > 100000) { pos = startPos; return false; }
            int timingPointBytes = timingPointCount * 17;
            if (pos + timingPointBytes > bufferLength) { pos = startPos; return false; }
            pos += timingPointBytes;

            // BeatmapId(4) + BeatmapSetId(4) + ThreadId(4) = 12
            if (pos + 12 > bufferLength) { pos = startPos; return false; }
            pos += 12;
            
            // 4 Grades (4バイト)
            if (pos + 4 > bufferLength) { pos = startPos; return false; }
            pos += 4;
            
            // LocalOffset(2) + StackLeniency(4) + Ruleset(1) = 7
            if (pos + 7 > bufferLength) { pos = startPos; return false; }
            pos += 7;

            // Source, Tags
            if (!TrySkipString(buffer, ref pos, bufferLength)) { pos = startPos; return false; }
            if (!TrySkipString(buffer, ref pos, bufferLength)) { pos = startPos; return false; }
            
            // OnlineOffset(2)
            if (pos + 2 > bufferLength) { pos = startPos; return false; }
            pos += 2;
            
            // TitleFont
            if (!TrySkipString(buffer, ref pos, bufferLength)) { pos = startPos; return false; }

            // IsUnplayed(1) + LastPlayed(8) + IsOsz2(1) = 10
            if (pos + 10 > bufferLength) { pos = startPos; return false; }
            pos += 10;

            // FolderName
            if (!TrySkipString(buffer, ref pos, bufferLength)) { pos = startPos; return false; }

            // LastCheckedAgainstOsuRepo(8) + 5 bool flags(5) = 13
            if (pos + 13 > bufferLength) { pos = startPos; return false; }
            pos += 13;

            if (osuVersion < 20140609)
            {
                if (pos + 2 > bufferLength) { pos = startPos; return false; }
                pos += 2;
            }

            // unknown int(4) + ManiaScrollSpeed(1) = 5
            if (pos + 5 > bufferLength) { pos = startPos; return false; }
            pos += 5;

            return true;
        }
        catch
        {
            pos = startPos;
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySkipString(ReadOnlySpan<byte> buffer, ref int pos, int bufferLength)
    {
        if (pos >= bufferLength) return false;
        byte prefix = buffer[pos++];
        if (prefix == 0x0b)
        {
            if (!TryReadULEB128(buffer, ref pos, bufferLength, out uint length)) return false;
            if (pos + (int)length > bufferLength) { pos--; return false; }
            pos += (int)length;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadULEB128(ReadOnlySpan<byte> buffer, ref int pos, int bufferLength, out uint result)
    {
        result = 0;
        int shift = 0;

        while (pos < bufferLength)
        {
            byte b = buffer[pos++];
            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                return true;

            shift += 7;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySkipIntDoubleDictionary(ReadOnlySpan<byte> buffer, ref int pos, int bufferLength)
    {
        if (pos + 4 > bufferLength) return false;
        int count = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;

        for (int i = 0; i < count; i++)
        {
            if (pos >= bufferLength) return false;
            byte keyType = buffer[pos++];

            if (keyType == 0x08)
            {
                if (pos + 4 > bufferLength) return false;
                pos += 4;
            }

            if (pos >= bufferLength) return false;
            byte valueType = buffer[pos++];
            if (valueType == 0x0D)
            {
                if (pos + 8 > bufferLength) return false;
                pos += 8;
            }
            else if (valueType == 0x0C)
            {
                if (pos + 4 > bufferLength) return false;
                pos += 4;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTime ReadDateTimeFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
    {
        long ticks = BitConverter.ToInt64(buffer.Slice(pos, 8));
        pos += 8;
        try { return new DateTime(ticks, DateTimeKind.Utc); }
        catch { return DateTime.MinValue; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadStringFromSpan(ReadOnlySpan<byte> buffer, ref int pos)
        => BinaryReaderHelper.ReadStringFromSpan(buffer, ref pos);

    /// <summary>
    /// アンマネージドバッファからBeatmapをパース
    /// </summary>
    private static unsafe Beatmap? ParseBeatmapFromBuffer(byte* bufferPtr, int bufferSize, RawBeatmapData raw, int osuVersion)
    {
        ReadOnlySpan<byte> buffer = new ReadOnlySpan<byte>(bufferPtr, bufferSize);
        int pos = raw.StartOffset;

        try
        {
            if (osuVersion < 20191106)
            {
                pos += 4;
            }

            string artist = ReadStringFromBuffer(buffer, ref pos);
            string artistUnicode = ReadStringFromBuffer(buffer, ref pos);
            string title = ReadStringFromBuffer(buffer, ref pos);
            string titleUnicode = ReadStringFromBuffer(buffer, ref pos);
            string creator = ReadStringFromBuffer(buffer, ref pos);
            string difficulty = ReadStringFromBuffer(buffer, ref pos);
            string audioFilename = ReadStringFromBuffer(buffer, ref pos);
            string md5Hash = ReadStringFromBuffer(buffer, ref pos);
            string osuFileName = ReadStringFromBuffer(buffer, ref pos);

            byte rankedStatus = buffer[pos++];
            ushort circlesCount = ReadUInt16FromBuffer(buffer, ref pos);
            ushort slidersCount = ReadUInt16FromBuffer(buffer, ref pos);
            ushort spinnersCount = ReadUInt16FromBuffer(buffer, ref pos);
            DateTime lastModifiedTime = ReadDateTimeFromBuffer(buffer, ref pos);

            float circleSize;
            float hp;
            float od;
            if (osuVersion >= 20140609)
            {
                pos += 4; // AR
                circleSize = ReadSingleFromBuffer(buffer, ref pos);
                hp = ReadSingleFromBuffer(buffer, ref pos);
                od = ReadSingleFromBuffer(buffer, ref pos);
            }
            else
            {
                pos++;
                circleSize = buffer[pos++];
                hp = buffer[pos++];
                od = buffer[pos++];
            }

            pos += 8; // SliderVelocity

            double maniaStarRating = 0;
            if (osuVersion >= 20140609)
            {
                maniaStarRating = ReadStarRatingDictionaryForMania(buffer, ref pos);
            }

            int drainTimeSec = ReadInt32FromBuffer(buffer, ref pos);
            pos += 8; // TotalTime + AudioPreviewTime

            double bpm = ReadTimingPointsAndCalculateBPM(buffer, ref pos);

            int beatmapId = ReadInt32FromBuffer(buffer, ref pos);
            int beatmapSetId = ReadInt32FromBuffer(buffer, ref pos);
            pos += 4; // ThreadId
            pos += 3; // StandardGrade, TaikoGrade, CatchGrade
            byte maniaGrade = buffer[pos++];
            pos += 6; // LocalOffset + StackLeniency
            byte ruleset = buffer[pos++];

            ReadStringFromBuffer(buffer, ref pos); // Source (skip)
            ReadStringFromBuffer(buffer, ref pos); // Tags (skip)
            pos += 2; // OnlineOffset
            ReadStringFromBuffer(buffer, ref pos); // TitleFont (skip)

            bool isUnplayed = buffer[pos++] != 0;
            DateTime lastPlayed = ReadDateTimeFromBuffer(buffer, ref pos);
            pos++; // IsOsz2
            string folderName = ReadStringFromBuffer(buffer, ref pos);
            pos += 8; // LastCheckedAgainstOsuRepo
            pos += 5; // 5 bool flags

            if (osuVersion < 20140609)
            {
                pos += 2;
            }

            pos += 5; // unknown int + ManiaScrollSpeed

            if (ruleset != 3)
                return null;

            int keyCount = (int)circleSize;
            if (keyCount == 0)
                return null;

            double longNoteRate = 0;
            int totalNotes = circlesCount + slidersCount + spinnersCount;
            if (totalNotes > 0)
            {
                longNoteRate = (double)slidersCount / totalNotes;
            }

            return new Beatmap
            {
                MD5Hash = md5Hash,
                Title = title,
                TitleUnicode = titleUnicode,
                Artist = artist,
                ArtistUnicode = artistUnicode,
                Version = difficulty,
                Creator = creator,
                BPM = bpm,
                Difficulty = maniaStarRating,
                BeatmapSetId = beatmapSetId,
                BeatmapId = beatmapId,
                Status = ConvertRankedStatus(rankedStatus),
                IsPlayed = !isUnplayed,
                LastPlayed = lastPlayed == DateTime.MinValue ? null : lastPlayed,
                LastModifiedTime = lastModifiedTime == DateTime.MinValue ? null : lastModifiedTime,
                FolderName = folderName,
                AudioFilename = audioFilename,
                OsuFileName = osuFileName,
                Grade = ConvertGradeToString(maniaGrade),
                KeyCount = keyCount,
                LongNoteRate = longNoteRate,
                OD = od,
                HP = hp,
                DrainTimeSeconds = drainTimeSec,
                BestScore = 0,
                BestAccuracy = 0,
                PlayCount = 0
            };
        }
        catch
        {
            return null;
        }
    }

    #region バッファ読み取りメソッド (静的メソッド、並列処理用)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32FromBuffer(ReadOnlySpan<byte> buffer, ref int pos)
    {
        int value = BitConverter.ToInt32(buffer.Slice(pos, 4));
        pos += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadUInt16FromBuffer(ReadOnlySpan<byte> buffer, ref int pos)
    {
        ushort value = BitConverter.ToUInt16(buffer.Slice(pos, 2));
        pos += 2;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadSingleFromBuffer(ReadOnlySpan<byte> buffer, ref int pos)
    {
        float value = BitConverter.ToSingle(buffer.Slice(pos, 4));
        pos += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ReadDoubleFromBuffer(ReadOnlySpan<byte> buffer, ref int pos)
    {
        double value = BitConverter.ToDouble(buffer.Slice(pos, 8));
        pos += 8;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTime ReadDateTimeFromBuffer(ReadOnlySpan<byte> buffer, ref int pos)
        => BinaryReaderHelper.ReadDateTimeFromSpan(buffer, ref pos);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadStringFromBuffer(ReadOnlySpan<byte> buffer, ref int pos)
        => BinaryReaderHelper.ReadStringFromSpan(buffer, ref pos);

    private static double ReadStarRatingDictionaryForMania(ReadOnlySpan<byte> buffer, ref int pos)
    {
        double maniaRating = 0;

        for (int dictIndex = 0; dictIndex < 4; dictIndex++)
        {
            int count = ReadInt32FromBuffer(buffer, ref pos);
            for (int i = 0; i < count; i++)
            {
                byte keyType = buffer[pos++];

                int mods = 0;
                if (keyType == 0x08)
                {
                    mods = ReadInt32FromBuffer(buffer, ref pos);
                }
                
                byte valueType = buffer[pos++];
                double starRating = 0;
                if (valueType == 0x0D)
                {
                    starRating = ReadDoubleFromBuffer(buffer, ref pos);
                }
                else if (valueType == 0x0C)
                {
                    starRating = ReadSingleFromBuffer(buffer, ref pos);
                }

                if (dictIndex == 3 && mods == 0)
                {
                    maniaRating = starRating;
                }
            }
        }

        return maniaRating;
    }

    private static double ReadTimingPointsAndCalculateBPM(ReadOnlySpan<byte> buffer, ref int pos)
    {
        int count = ReadInt32FromBuffer(buffer, ref pos);
        double bpm = 120.0;

        for (int i = 0; i < count; i++)
        {
            double beatLength = ReadDoubleFromBuffer(buffer, ref pos);
            pos += 8; // Offset
            bool inherited = buffer[pos++] == 0;

            if (i == 0 || (inherited && beatLength > 0))
            {
                if (beatLength > 0)
                {
                    bpm = 60000.0 / beatLength;
                }
            }
        }

        return bpm;
    }

    #endregion

    #region 変換ヘルパー

    private static BeatmapStatus ConvertRankedStatus(byte status)
    {
        return status switch
        {
            4 => BeatmapStatus.Ranked,
            5 => BeatmapStatus.Approved,
            6 => BeatmapStatus.Qualified,
            7 => BeatmapStatus.Loved,
            2 or 3 => BeatmapStatus.Pending,
            _ => BeatmapStatus.None
        };
    }

    private static string ConvertGradeToString(byte grade)
    {
        return grade switch
        {
            0 => "SS+",
            1 => "SS",
            2 => "S+",
            3 => "S",
            4 => "A",
            5 => "B",
            6 => "C",
            7 => "D",
            _ => string.Empty
        };
    }

    #endregion

    public void Dispose()
    {
        // 現在はリソース解放不要（UnmanagedBufferはusingで管理）
    }
}
