namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

public enum BeatmapDownloadState
{
    Exists,
    NotExists,
    Queued,
    Downloading,
    Downloaded,
    Error
}
