using System;
using System.IO;
using System.Threading;

namespace NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;

/// <summary>
/// ffmpeg.exe の絶対パスを解決・キャッシュする。
/// 解決対象は <see cref="AppContext.BaseDirectory"/> 直下のみ（PATH フォールバックなし）。
/// </summary>
internal static class FfmpegBinaryLocator
{
    private static readonly Lazy<string> s_ffmpegPath = new(
        () => ResolveOrThrow("ffmpeg.exe"),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>ffmpeg.exe の絶対パス。初回呼び出しで解決しキャッシュ。</summary>
    internal static string GetFfmpegPath() => s_ffmpegPath.Value;

    private static string ResolveOrThrow(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{fileName} not found at {path}", path);
        }
        return path;
    }
}
