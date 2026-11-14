using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NakuruTool_Avalonia_AOT.Features.Analyze7k;

/// <summary>
/// 解析結果の永続キャッシュ（JSON Lines、追記のみ、1 譜面 1 行）。
/// ファイル名にエンジンと C# 側の版のハッシュを含め、版が変わったら別ファイルを使う（古いファイルは削除）。
/// 複数スレッドから使ってよい。
/// </summary>
public sealed class Analyze7kCache : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ManiaChartAnalysis> _charts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failures = new(StringComparer.Ordinal);
    /// <summary>ファイルの最後の記録が失敗になっている譜面とその理由（同じ失敗を重ねて書かないため）</summary>
    private readonly Dictionary<string, string> _recordedFailures = new(StringComparer.Ordinal);
    private StreamWriter? _writer;

    public Analyze7kCache(string directory, string versionKey)
    {
        Directory = directory;
        VersionKey = versionKey;
        FilePath = Path.Combine(directory, $"results-{versionKey}.jsonl");
    }

    public string Directory { get; }
    public string VersionKey { get; }
    public string FilePath { get; }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _charts.Count;
            }
        }
    }

    /// <summary>版の文字列からファイル名用のハッシュを作る</summary>
    public static string MakeVersionKey(string engineVersion, string logicVersion)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{engineVersion}|{logicVersion}")))[..16];

    /// <summary>キャッシュファイルを読み込む。別の版のファイルは削除する。</summary>
    public void Load()
    {
        System.IO.Directory.CreateDirectory(Directory);
        foreach (var old in System.IO.Directory.EnumerateFiles(Directory, "results-*.jsonl"))
        {
            if (!string.Equals(Path.GetFullPath(old), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        if (File.Exists(FilePath))
        {
            using var reader = new StreamReader(FilePath, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                Analyze7kCacheRecord? record;
                try
                {
                    record = JsonSerializer.Deserialize(line, Analyze7kJsonContext.Default.Analyze7kCacheRecord);
                }
                catch (JsonException)
                {
                    // 書き込み途中で終了した最後の行などは無視する
                    continue;
                }
                if (record is not null)
                    Apply(record);
            }
        }

        // 書きかけで終わった最後の行（強制終了など）に続けて書くと次の行も読めなくなるため、改行で終わっていなければ改行を足す
        var endsWithoutNewLine = false;
        if (File.Exists(FilePath))
        {
            using var existing = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (existing.Length > 0)
            {
                existing.Seek(-1, SeekOrigin.End);
                endsWithoutNewLine = existing.ReadByte() != '\n';
            }
        }

        _writer = new StreamWriter(new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
        if (endsWithoutNewLine)
            _writer.WriteLine();
    }

    private void Apply(Analyze7kCacheRecord record)
    {
        if (string.IsNullOrEmpty(record.Md5))
            return;
        if (record.Reason is { } reason)
        {
            _failures[record.Md5] = reason;
            _recordedFailures[record.Md5] = reason;
            _charts.Remove(record.Md5);
            return;
        }
        _recordedFailures.Remove(record.Md5);
        if (record.PrismOverall is not { } overall)
            return;

        _charts[record.Md5] = new ManiaChartAnalysis
        {
            PrismOverall = overall,
            PrismLnOverall = record.PrismLnOverall,
            GenreSkills = GenreValues.FromArray(record.GenreSkills),
            GenreTendencies = GenreValues.FromArray(record.GenreTendencies),
            RegularDan = record.RegularDan,
            LnDan = record.LnDan,
            PrimaryFamily = ParseFamily(record.PrimaryFamily),
            RegularGenres = (RegularDanGenre)(record.RegularGenres ?? 0),
            LnGenres = (LnDanGenre)(record.LnGenres ?? 0),
            Eligible = record.Eligible ?? true,
            Vibro = record.Vibro ?? false,
        };
        _failures.Remove(record.Md5);
    }

    private static DanSide? ParseFamily(string? value) => value switch
    {
        "regular" => DanSide.Regular,
        "ln" => DanSide.Ln,
        _ => null,
    };

    private static string? FormatFamily(DanSide? value) => value switch
    {
        DanSide.Regular => "regular",
        DanSide.Ln => "ln",
        _ => null,
    };

    private void Append(Analyze7kCacheRecord record)
    {
        lock (_lock)
        {
            // 再試行した譜面が前回と同じ理由で失敗したときは、ファイルに書き足さない（起動のたびに行が増えないように）
            var alreadyRecorded = record.Reason is { } reason &&
                                  _recordedFailures.TryGetValue(record.Md5, out var last) && last == reason;
            Apply(record);
            if (alreadyRecorded || _writer is null)
                return;
            try
            {
                _writer.WriteLine(JsonSerializer.Serialize(record, Analyze7kJsonContext.Default.Analyze7kCacheRecord));
            }
            catch (IOException)
            {
                // 書き込めなくてもメモリ上の結果は使える
            }
        }
    }

    /// <summary>解析結果を保存する</summary>
    public void Add(string md5, ManiaChartAnalysis analysis)
        => Append(new Analyze7kCacheRecord
        {
            Md5 = md5,
            PrismOverall = analysis.PrismOverall,
            PrismLnOverall = analysis.PrismLnOverall,
            GenreSkills = analysis.GenreSkills.ToArray(),
            GenreTendencies = analysis.GenreTendencies.ToArray(),
            RegularDan = analysis.RegularDan,
            LnDan = analysis.LnDan,
            PrimaryFamily = FormatFamily(analysis.PrimaryFamily),
            Eligible = analysis.Eligible,
            Vibro = analysis.Vibro,
            RegularGenres = (int)analysis.RegularGenres,
            LnGenres = (int)analysis.LnGenres,
        });

    /// <summary>解析できなかった理由を記録する</summary>
    public void AddFailure(string md5, string reason)
        => Append(new Analyze7kCacheRecord { Md5 = md5, Reason = reason });

    /// <summary>保存済みの解析結果（同じ譜面には同じインスタンスを返す）</summary>
    public bool TryGet(string md5, out ManiaChartAnalysis analysis)
    {
        lock (_lock)
        {
            return _charts.TryGetValue(md5, out analysis!);
        }
    }

    /// <summary>解析できなかった理由（なければ null）</summary>
    public string? GetFailure(string md5)
    {
        lock (_lock)
        {
            return _failures.GetValueOrDefault(md5);
        }
    }

    /// <summary>再試行してよい失敗（ファイルなし・エンジンの一時的な失敗）をメモリ上から消す</summary>
    public void ForgetRetryableFailures()
    {
        lock (_lock)
        {
            var keys = new List<string>();
            foreach (var (md5, reason) in _failures)
            {
                if (Analyze7kFailure.IsRetryable(reason))
                    keys.Add(md5);
            }
            foreach (var key in keys)
                _failures.Remove(key);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
