using NakuruTool_Avalonia_AOT.Features.ImportExport.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport;

public interface IBeatmapDownloadService : IDisposable
{
    void EnqueueDownload(ImportExportBeatmapItem item, IReadOnlyList<ImportExportBeatmapItem> allItems);
    Task CancelAllAsync();
}
