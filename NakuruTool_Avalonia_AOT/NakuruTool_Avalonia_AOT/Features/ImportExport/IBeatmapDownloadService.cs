using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public interface IBeatmapDownloadService : IDisposable
{
    /// <summary>ワーカーの現在の状況 (待機理由など)。</summary>
    DownloadActivity CurrentActivity { get; }

    /// <summary><see cref="CurrentActivity"/> が変化したときに UI スレッドで発生する。</summary>
    event EventHandler<DownloadActivity>? ActivityChanged;

    void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems);
    Task CancelAllAsync();
}
