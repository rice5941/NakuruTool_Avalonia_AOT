using System;
using System.Diagnostics;

namespace NakuruTool_Avalonia_AOT.Features.Shared;

/// <summary>
/// osu! 公式サイトのページを既定のブラウザで開くヘルパー。
/// </summary>
internal static class OsuWebPageLauncher
{
    private const string BeatmapSetUrlFormat = "https://osu.ppy.sh/beatmapsets/{0}";

    /// <summary>
    /// beatmapset の公式ページ URL を返す。ID が不正 (0 以下) の場合は null。
    /// </summary>
    public static string? GetBeatmapSetUrl(int beatmapSetId) =>
        beatmapSetId > 0 ? string.Format(BeatmapSetUrlFormat, beatmapSetId) : null;

    /// <summary>
    /// beatmapset の公式ページを既定のブラウザで開く。開けた場合は true。
    /// </summary>
    public static bool OpenBeatmapSetPage(int beatmapSetId)
    {
        var url = GetBeatmapSetUrl(beatmapSetId);
        if (url is null)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OsuWebPageLauncher] Failed to open {url}: {ex.Message}");
            return false;
        }
    }
}
