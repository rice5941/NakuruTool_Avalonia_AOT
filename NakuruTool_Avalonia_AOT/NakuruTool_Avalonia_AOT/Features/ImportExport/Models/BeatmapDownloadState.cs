namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

public enum BeatmapDownloadState
{
    Exists,
    NotExists,
    Queued,
    /// <summary>順番が来たが、ダウンロード間隔や混雑のため開始を待っている。</summary>
    Waiting,
    Downloading,
    Downloaded,
    Error
}
