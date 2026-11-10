using NakuruTool_Avalonia_AOT.Features.Translate;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>
/// ダウンロード失敗の理由。表示文言は持たず、表示時に現在の言語で翻訳する。
/// </summary>
/// <param name="ResourceKey">翻訳キー。</param>
/// <param name="StatusCode">文言に埋め込む HTTP ステータスコード ({0})。</param>
public sealed record DownloadError(string ResourceKey, int? StatusCode = null)
{
    private const string KeyPrefix = "ImportExport.DownloadError.";

    public static DownloadError OsuFolderNotSet { get; } = new(KeyPrefix + "OsuFolderNotSet");

    /// <summary>ダウンロード中に発生した例外を、表示用の理由に変換する。</summary>
    public static DownloadError FromException(Exception ex) => ex switch
    {
        OszDownloadException { Reason: OszDownloadFailure.NotFound } => new(KeyPrefix + "NotFound"),
        OszDownloadException { Reason: OszDownloadFailure.RateLimited } e => new(KeyPrefix + "RateLimited", e.StatusCode),
        OszDownloadException { Reason: OszDownloadFailure.Unavailable } e => new(KeyPrefix + "Unavailable", e.StatusCode),
        OszDownloadException { Reason: OszDownloadFailure.NotOsz } => new(KeyPrefix + "NotOsz"),
        OszDownloadException { Reason: OszDownloadFailure.FallbackLimitReached } => new(KeyPrefix + "FallbackLimit"),
        OszDownloadException e => new(KeyPrefix + "Http", e.StatusCode),
        TaskCanceledException or TimeoutException => new(KeyPrefix + "Timeout"),
        HttpRequestException => new(KeyPrefix + "Network"),
        IOException or UnauthorizedAccessException => new(KeyPrefix + "SaveFailed"),
        _ => new(KeyPrefix + "Unknown"),
    };

    public string ToMessage()
    {
        var format = LanguageService.Instance.GetString(ResourceKey);
        return StatusCode is { } code
            ? string.Format(CultureInfo.CurrentCulture, format, code)
            : format;
    }
}
