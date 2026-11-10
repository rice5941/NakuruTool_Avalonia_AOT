using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>通信開始から保存完了までの時間と、その応答に含まれるレート制限ヘッダー。</summary>
public sealed record BeatmapDownloadResult(
    TimeSpan Duration,
    string? RateLimitLimit,
    string? RateLimitRemaining,
    string? RateLimitReset);
