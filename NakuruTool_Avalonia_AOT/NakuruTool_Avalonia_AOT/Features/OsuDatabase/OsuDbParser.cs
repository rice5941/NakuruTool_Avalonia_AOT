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
/// パース前の生ビートマップデータを保持する構造体
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

    public int Length => EndOffset - StartOffset;
}

/// <summary>
/// HDD最適化されたosu!.dbパーサー
/// シングルスレッドで一括読み込み + 並列変換
/// </summary>
public sealed class OsuDbParser : IDisposable
{
    private byte[]? _buffer;
    private int _bufferLength;
    private bool _disposed;

    public int OsuVersion { get; private set; }
    public int FolderCount { get; private set; }
    public bool AccountUnlocked { get; private set; }
    public DateTime UnlockDate { get; private set; }
    public string PlayerName { get; private set; } = string.Empty;
    public int BeatmapCount { get; private set; }

    /// <summary>
    /// ファイルを一括読み込みし、各ビートマップのオフセットを解析
    /// </summary>
    public RawBeatmapData[] ReadAndScan(string filePath, Action<string, int>? progressCallback = null)
    {
        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuLoading"), 0);

        var fileBytes = File.ReadAllBytes(filePath);
        _buffer = fileBytes;
        _bufferLength = fileBytes.Length;

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuLoading"), 5);

        int pos = 0;

        OsuVersion = ReadInt32(ref pos);
        FolderCount = ReadInt32(ref pos);
        AccountUnlocked = ReadBoolean(ref pos);
        UnlockDate = ReadDateTime(ref pos);
        PlayerName = ReadString(ref pos);
        BeatmapCount = ReadInt32(ref pos);

        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.OsuVersion"), OsuVersion), 10);
        progressCallback?.Invoke(string.Format(LanguageService.Instance.GetString("Loading.BeatmapCount"), BeatmapCount), 12);

        var rawDataList = new RawBeatmapData[BeatmapCount];
        
        for (int i = 0; i < BeatmapCount; i++)
        {
            int startOffset = pos;
            SkipBeatmapEntry(ref pos, OsuVersion);
            rawDataList[i] = new RawBeatmapData(startOffset, pos);

            if (i % 5000 == 0 || i == BeatmapCount - 1)
            {
                int progress = 12 + (int)((double)i / BeatmapCount * 28);
                progressCallback?.Invoke(
                    string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), i + 1, BeatmapCount),
                    progress);
            }
        }

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuLoading"), 40);

        return rawDataList;
    }

    /// <summary>
    /// 並列でBeatmapインスタンスに変換
    /// </summary>
    public Beatmap[] ConvertToBeamapsParallel(RawBeatmapData[] rawDataArray, Action<string, int>? progressCallback = null)
    {
        if (_buffer == null)
            throw new InvalidOperationException("ReadAndScan must be called first");

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuLoading"), 40);

        int totalCount = rawDataArray.Length;
        var results = new Beatmap?[totalCount];
        int processedCount = 0;

        Parallel.For(0, totalCount, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            () => 0,
            (i, state, localCount) =>
            {
                var raw = rawDataArray[i];
                var beatmap = ParseBeatmapFromBuffer(_buffer, raw);
                results[i] = beatmap;
                return localCount + 1;
            },
            localCount =>
            {
                int current = Interlocked.Add(ref processedCount, localCount);
                if (current % 5000 == 0 || current == totalCount)
                {
                    int progress = 40 + (int)((double)current / totalCount * 50);
                    progressCallback?.Invoke(
                        string.Format(LanguageService.Instance.GetString("Loading.BeatmapProcessing"), current, totalCount),
                        progress);
                }
            });

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuSorting"), 92);

        var validList = new List<Beatmap>(totalCount);
        var seenHashes = new HashSet<string>(totalCount, StringComparer.Ordinal);

        for (int i = 0; i < totalCount; i++)
        {
            var beatmap = results[i];
            if (beatmap != null && 
                !string.IsNullOrEmpty(beatmap.MD5Hash) && 
                beatmap.KeyCount != 0 &&
                seenHashes.Add(beatmap.MD5Hash))
            {
                validList.Add(beatmap);
            }
        }

        var beatmapArray = validList.ToArray();
        Array.Sort(beatmapArray, static (a, b) => string.CompareOrdinal(a.MD5Hash, b.MD5Hash));

        progressCallback?.Invoke(LanguageService.Instance.GetString("Loading.OsuCompleted"), 100);

        return beatmapArray;
    }

    private Beatmap? ParseBeatmapFromBuffer(byte[] buffer, RawBeatmapData raw)
    {
        int pos = raw.StartOffset;

        try
        {
            if (OsuVersion < 20191106)
            {
                pos += 4;
            }

            string artist = ReadString(buffer, ref pos);
            ReadString(buffer, ref pos); // artistUnicode (skip)
            string title = ReadString(buffer, ref pos);
            ReadString(buffer, ref pos); // titleUnicode (skip)
            string creator = ReadString(buffer, ref pos);
            string difficulty = ReadString(buffer, ref pos);
            ReadString(buffer, ref pos); // audioFileName (skip)
            string md5Hash = ReadString(buffer, ref pos);
            ReadString(buffer, ref pos); // fileName (skip)

            byte rankedStatus = buffer[pos++];
            ushort circlesCount = ReadUInt16(buffer, ref pos);
            ushort slidersCount = ReadUInt16(buffer, ref pos);
            ushort spinnersCount = ReadUInt16(buffer, ref pos);
            pos += 8; // LastModifiedTime

            float circleSize;
            if (OsuVersion >= 20140609)
            {
                pos += 4; // AR
                circleSize = ReadSingle(buffer, ref pos);
                pos += 8; // HP + OD
            }
            else
            {
                pos++;
                circleSize = buffer[pos++];
                pos += 2;
            }

            pos += 8; // SliderVelocity

            double maniaStarRating = 0;
            if (OsuVersion >= 20140609)
            {
                maniaStarRating = ReadStarRatingDictionaryForMania(buffer, ref pos);
            }

            pos += 12; // DrainTime + TotalTime + AudioPreviewTime

            double bpm = ReadTimingPointsAndCalculateBPM(buffer, ref pos);

            int beatmapId = ReadInt32(buffer, ref pos);
            int beatmapSetId = ReadInt32(buffer, ref pos);
            pos += 4; // ThreadId
            pos += 3; // StandardGrade, TaikoGrade, CatchGrade
            byte maniaGrade = buffer[pos++];
            pos += 6; // LocalOffset + StackLeniency
            byte ruleset = buffer[pos++];
            
            ReadString(buffer, ref pos); // Source (skip)
            ReadString(buffer, ref pos); // Tags (skip)
            pos += 2; // OnlineOffset
            ReadString(buffer, ref pos); // TitleFont (skip)
            
            bool isUnplayed = buffer[pos++] != 0;
            DateTime lastPlayed = ReadDateTime(buffer, ref pos);
            pos++; // IsOsz2
            string folderName = ReadString(buffer, ref pos);
            pos += 8; // LastCheckedAgainstOsuRepo
            pos += 5; // 5 bool flags

            if (OsuVersion < 20140609)
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
                Artist = artist,
                Version = difficulty,
                Creator = creator,
                BPM = bpm,
                Difficulty = maniaStarRating,
                BeatmapSetId = beatmapSetId,
                BeatmapId = beatmapId,
                Status = ConvertRankedStatus(rankedStatus),
                IsPlayed = !isUnplayed,
                LastPlayed = lastPlayed == DateTime.MinValue ? null : lastPlayed,
                LastModifiedTime = null,
                FolderName = folderName,
                Grade = ConvertGradeToString(maniaGrade),
                KeyCount = keyCount,
                LongNoteRate = longNoteRate,
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

    private void SkipBeatmapEntry(ref int pos, int osuVersion)
    {
        try
        {
            if (osuVersion < 20191106)
            {
                pos += 4;
            }

            for (int i = 0; i < 9; i++)
            {
                SkipString(ref pos);
            }

            pos += 7;  // RankedStatus + CirclesCount + SlidersCount + SpinnersCount
            pos += 8;  // LastModifiedTime

            if (osuVersion >= 20140609)
            {
                pos += 16;
            }
            else
            {
                pos += 4;
            }

            pos += 8; // SliderVelocity

            if (osuVersion >= 20140609)
            {
                for (int dictIndex = 0; dictIndex < 4; dictIndex++)
                {
                    SkipIntDoubleDictionary(ref pos);
                }
            }

            pos += 12; // DrainTime + TotalTime + AudioPreviewTime

            int timingPointCount = ReadInt32(ref pos);
            if (timingPointCount < 0 || timingPointCount > 100000)
            {
                throw new InvalidOperationException($"Invalid timingPointCount={timingPointCount}");
            }
            pos += timingPointCount * 17;

            pos += 12; // BeatmapId + BeatmapSetId + ThreadId
            pos += 4;  // 4 Grades
            pos += 7;  // LocalOffset + StackLeniency + Ruleset

            SkipString(ref pos); // Source
            SkipString(ref pos); // Tags
            pos += 2; // OnlineOffset
            SkipString(ref pos); // TitleFont

            pos += 10; // IsUnplayed + LastPlayed + IsOsz2

            SkipString(ref pos); // FolderName

            pos += 13; // LastCheckedAgainstOsuRepo + 5 bool flags

            if (osuVersion < 20140609)
            {
                pos += 2;
            }

            pos += 5; // unknown int + ManiaScrollSpeed
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to skip beatmap entry: {ex.Message}", ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipIntDoubleDictionary(ref int pos)
    {
        if (_buffer == null) throw new InvalidOperationException();
        
        int count = ReadInt32(ref pos);
        
        for (int i = 0; i < count; i++)
        {
            byte keyType = _buffer[pos++];
            if (keyType == 0x08)
            {
                pos += 4;
            }
            
            byte valueType = _buffer[pos++];
            if (valueType == 0x0D)
            {
                pos += 8;
            }
            else if (valueType == 0x0C)
            {
                pos += 4;
            }
        }
    }

    #region バッファ読み取りメソッド (インスタンスメソッド)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadInt32(ref int pos)
    {
        if (_buffer == null) throw new InvalidOperationException();
        int value = BitConverter.ToInt32(_buffer, pos);
        pos += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBoolean(ref int pos)
    {
        if (_buffer == null) throw new InvalidOperationException();
        return _buffer[pos++] != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTime ReadDateTime(ref int pos)
    {
        if (_buffer == null) throw new InvalidOperationException();
        long ticks = BitConverter.ToInt64(_buffer, pos);
        pos += 8;
        try { return new DateTime(ticks, DateTimeKind.Utc); }
        catch { return DateTime.MinValue; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadString(ref int pos)
    {
        if (_buffer == null) throw new InvalidOperationException();
        return ReadString(_buffer, ref pos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipString(ref int pos)
    {
        if (_buffer == null) throw new InvalidOperationException();
        byte prefix = _buffer[pos++];
        if (prefix == 0x0b)
        {
            uint length = ReadULEB128(_buffer, ref pos);
            pos += (int)length;
        }
    }

    #endregion

    #region バッファ読み取りメソッド (静的メソッド、並列処理用)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(byte[] buffer, ref int pos)
    {
        int value = BitConverter.ToInt32(buffer, pos);
        pos += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ReadInt16(byte[] buffer, ref int pos)
    {
        short value = BitConverter.ToInt16(buffer, pos);
        pos += 2;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadUInt16(byte[] buffer, ref int pos)
    {
        ushort value = BitConverter.ToUInt16(buffer, pos);
        pos += 2;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ReadSingle(byte[] buffer, ref int pos)
    {
        float value = BitConverter.ToSingle(buffer, pos);
        pos += 4;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ReadDouble(byte[] buffer, ref int pos)
    {
        double value = BitConverter.ToDouble(buffer, pos);
        pos += 8;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DateTime ReadDateTime(byte[] buffer, ref int pos)
    {
        long ticks = BitConverter.ToInt64(buffer, pos);
        pos += 8;
        try { return new DateTime(ticks, DateTimeKind.Utc); }
        catch { return DateTime.MinValue; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadString(byte[] buffer, ref int pos)
    {
        byte prefix = buffer[pos++];
        if (prefix == 0x00)
        {
            return string.Empty;
        }
        else if (prefix == 0x0b)
        {
            uint length = ReadULEB128(buffer, ref pos);
            if (length == 0)
                return string.Empty;
            string str = Encoding.UTF8.GetString(buffer, pos, (int)length);
            pos += (int)length;
            return str;
        }
        return string.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadULEB128(byte[] buffer, ref int pos)
    {
        uint result = 0;
        int shift = 0;

        while (true)
        {
            byte b = buffer[pos++];
            result |= (uint)(b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                break;

            shift += 7;
        }

        return result;
    }

    private static double ReadStarRatingDictionaryForMania(byte[] buffer, ref int pos)
    {
        double maniaRating = 0;

        for (int dictIndex = 0; dictIndex < 4; dictIndex++)
        {
            int count = ReadInt32(buffer, ref pos);
            for (int i = 0; i < count; i++)
            {
                byte keyType = buffer[pos++];
                int mods = 0;
                if (keyType == 0x08)
                {
                    mods = ReadInt32(buffer, ref pos);
                }
                
                byte valueType = buffer[pos++];
                double starRating = 0;
                if (valueType == 0x0D)
                {
                    starRating = ReadDouble(buffer, ref pos);
                }
                else if (valueType == 0x0C)
                {
                    starRating = ReadSingle(buffer, ref pos);
                }

                if (dictIndex == 3 && mods == 0)
                {
                    maniaRating = starRating;
                }
            }
        }

        return maniaRating;
    }

    private static double ReadTimingPointsAndCalculateBPM(byte[] buffer, ref int pos)
    {
        int count = ReadInt32(buffer, ref pos);
        double bpm = 120.0;

        for (int i = 0; i < count; i++)
        {
            double beatLength = ReadDouble(buffer, ref pos);
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
            9 => "SS+",
            8 => "SS",
            7 => "S+",
            6 => "S",
            5 => "A",
            4 => "B",
            3 => "C",
            2 => "D",
            _ => string.Empty
        };
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _buffer = null;
            _disposed = true;
        }
    }
}
