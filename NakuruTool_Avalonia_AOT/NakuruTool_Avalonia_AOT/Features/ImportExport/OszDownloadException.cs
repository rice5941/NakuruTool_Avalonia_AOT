using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

/// <summary>ミラーの応答が原因でダウンロードできなかった理由。</summary>
public enum OszDownloadFailure
{
    /// <summary>ミラーに譜面が存在しない (404)。</summary>
    NotFound,

    /// <summary>再試行しても 429 (レート制限) が続いた。</summary>
    RateLimited,

    /// <summary>503: 代替経路を含め、この譜面を配信できるミラーが無かった。</summary>
    Unavailable,

    /// <summary>その他の HTTP エラー。</summary>
    HttpError,

    /// <summary>.osz ではない応答 (HTML / JSON) が返された。</summary>
    NotOsz,

    /// <summary>代替経路 (osu.direct) のダウンロード予算の残りが少ない、または超過した。</summary>
    FallbackLimitReached,
}

/// <summary>
/// ミラーの応答が原因のダウンロード失敗。表示用の文言は持たず、
/// UI 側で <see cref="Reason"/> から翻訳済みメッセージを組み立てる。
/// </summary>
public sealed class OszDownloadException(OszDownloadFailure reason, int statusCode)
    : Exception($"{reason} (HTTP {statusCode})")
{
    public OszDownloadFailure Reason { get; } = reason;

    public int StatusCode { get; } = statusCode;
}
