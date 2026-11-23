using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.OsuDatabase;

/// <summary>
/// osu! の Songs フォルダの場所。osu!.db の譜面のフォルダ名（<see cref="Beatmap.FolderName"/>）はこのフォルダからの相対パス。
/// osu! stable は Songs フォルダを設定（osu!.&lt;Windows のユーザー名&gt;.cfg の <c>BeatmapDirectory</c>）で別の場所に移せるので、
/// osu! フォルダ直下の Songs とは限らない（譜面の多いユーザーは別ドライブに置いていることが多い）。
/// 複数スレッドから使ってよい。
/// </summary>
public static class OsuSongsFolder
{
    /// <summary>osu! の既定の Songs フォルダ名（osu! フォルダ直下）</summary>
    public const string DefaultFolderName = "Songs";

    private const string ConfigKey = "BeatmapDirectory";
    private const string ConfigPrefix = "osu!.";
    private const string ConfigExtension = ".cfg";

    private static readonly ConcurrentDictionary<string, string> s_cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// osu! フォルダの Songs フォルダのパス。結果は osu! フォルダごとに覚えておく（DB の読み込みのたびに <see cref="Refresh"/> で読み直す）。
    /// </summary>
    public static string Resolve(string osuFolderPath)
    {
        if (string.IsNullOrWhiteSpace(osuFolderPath))
            return Path.Combine(osuFolderPath ?? string.Empty, DefaultFolderName);
        return s_cache.GetOrAdd(osuFolderPath, static path => ResolveCore(path, Environment.UserName));
    }

    /// <summary>osu! の設定を読み直して Songs フォルダを決め直す（DB の読み込み時。osu! で設定を変えた後に反映するため）</summary>
    public static string Refresh(string osuFolderPath)
    {
        s_cache.Clear();
        return Resolve(osuFolderPath);
    }

    /// <summary>
    /// osu! の設定から Songs フォルダを決める。設定に <c>BeatmapDirectory</c> がない、またはそのフォルダがなければ osu! フォルダ直下の Songs。
    /// 設定ファイルは、<paramref name="userName"/> のもの（osu! が使う名前）を先に、なければ他の osu!.*.cfg を新しい順に見る
    /// （別の PC から osu! フォルダを移した場合など、ファイル名のユーザー名が今の Windows のユーザー名と違うことがある）。
    /// </summary>
    internal static string ResolveCore(string osuFolderPath, string? userName)
    {
        var fallback = Path.Combine(osuFolderPath, DefaultFolderName);
        foreach (var config in ConfigFiles(osuFolderPath, userName))
        {
            if (ReadBeatmapDirectory(config) is not { } value)
                continue;
            if (ToDirectoryPath(osuFolderPath, value) is { } directory && Directory.Exists(directory))
                return directory;
        }
        return fallback;
    }

    /// <summary>osu! フォルダの osu!.&lt;ユーザー名&gt;.cfg（<paramref name="userName"/> のものを先頭に、残りは更新日時の新しい順）</summary>
    private static List<string> ConfigFiles(string osuFolderPath, string? userName)
    {
        var files = new List<(string Path, DateTime Modified)>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(osuFolderPath, "*" + ConfigExtension))
            {
                if (IsUserConfigFileName(Path.GetFileName(path)))
                    files.Add((path, File.GetLastWriteTimeUtc(path)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }

        var preferred = string.IsNullOrEmpty(userName) ? null : ConfigPrefix + userName + ConfigExtension;
        files.Sort((a, b) =>
        {
            var aPreferred = preferred is not null && string.Equals(Path.GetFileName(a.Path), preferred, StringComparison.OrdinalIgnoreCase);
            var bPreferred = preferred is not null && string.Equals(Path.GetFileName(b.Path), preferred, StringComparison.OrdinalIgnoreCase);
            if (aPreferred != bPreferred)
                return aPreferred ? -1 : 1;
            return b.Modified.CompareTo(a.Modified);
        });

        var result = new List<string>(files.Count);
        foreach (var (path, _) in files)
            result.Add(path);
        return result;
    }

    /// <summary>osu!.&lt;ユーザー名&gt;.cfg か（osu! 全体の設定の osu!.cfg は除く）</summary>
    internal static bool IsUserConfigFileName(string fileName)
        => fileName.Length > ConfigPrefix.Length + ConfigExtension.Length &&
           fileName.StartsWith(ConfigPrefix, StringComparison.OrdinalIgnoreCase) &&
           fileName.EndsWith(ConfigExtension, StringComparison.OrdinalIgnoreCase);

    private static string? ReadBeatmapDirectory(string configPath)
    {
        try
        {
            // osu! の起動中でも読めるよう、書き込み・削除を妨げずに開く
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return ParseBeatmapDirectory(ReadLines(reader));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    /// <summary>設定ファイルの行（<c>キー = 値</c>）から BeatmapDirectory の値を読む。なければ null。</summary>
    internal static string? ParseBeatmapDirectory(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var text = line.AsSpan().Trim();
            if (text.IsEmpty || text[0] == '#')
                continue;
            var equals = text.IndexOf('=');
            if (equals < 0 || !text[..equals].Trim().Equals(ConfigKey, StringComparison.OrdinalIgnoreCase))
                continue;
            var value = text[(equals + 1)..].Trim().Trim('"').Trim();
            return value.IsEmpty ? null : value.ToString();
        }
        return null;
    }

    /// <summary>BeatmapDirectory の値をパスにする（相対パスは osu! フォルダから）。パスとして使えなければ null。</summary>
    internal static string? ToDirectoryPath(string osuFolderPath, string value)
    {
        try
        {
            return Path.IsPathFullyQualified(value) ? value : Path.Combine(osuFolderPath, value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
