using System;
using System.Collections.Generic;

namespace NakuruTool_Avalonia_AOT.Features.Settings;

/// <summary>
/// 譜面 (.osz) のダウンロードに使用するミラーの定義。
/// URL の末尾に beatmapset ID を連結するとダウンロード URL になる。
/// </summary>
public static class BeatmapMirrors
{
    /// <summary>hinai: .osz をバイナリで直接返すプロキシエンドポイント。</summary>
    /// <remarks>https://mirror.hinamizawa.ai/docs/beatmap-download</remarks>
    public const string Hinai = "https://mirror.hinamizawa.ai/api/v1/hinai/d/";

    public const string Nerinyan = "https://api.nerinyan.moe/d/";

    public const string Default = Hinai;

    public static IReadOnlyList<string> All { get; } = [Hinai, Nerinyan];

    /// <summary>
    /// 未知・廃止済みのミラー (旧既定の catboy など) を既定ミラーに置き換える。
    /// </summary>
    public static string Normalize(string? mirrorUrl)
    {
        foreach (var known in All)
        {
            if (string.Equals(known, mirrorUrl, StringComparison.OrdinalIgnoreCase))
                return known;
        }
        return Default;
    }

    public static string GetDownloadUrl(string? mirrorUrl, int beatmapSetId)
        => $"{Normalize(mirrorUrl)}{beatmapSetId}";

    /// <summary>
    /// ミラーが 503 (配信できるミラーが無い) を返したときに、直接ダウンロード URL を問い合わせる URL。
    /// hinai の JSON エンドポイント <c>/d/{id}</c> はレート制限の対象外で、
    /// <c>{"success":true,"download_url":"..."}</c> を返す。代替経路が無いミラーでは null。
    /// </summary>
    public static string? GetFallbackResolveUrl(string? mirrorUrl, int beatmapSetId)
        => Normalize(mirrorUrl) == Hinai ? $"{HinaiResolveBase}{beatmapSetId}" : null;

    private const string HinaiResolveBase = "https://mirror.hinamizawa.ai/d/";
}
