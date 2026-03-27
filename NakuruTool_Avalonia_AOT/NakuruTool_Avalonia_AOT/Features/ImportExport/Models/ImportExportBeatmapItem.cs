using CommunityToolkit.Mvvm.ComponentModel;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>beatmap の表示専用行モデル（Missing = DB未存在）</summary>
public sealed partial class ImportExportBeatmapItem : ObservableObject
{
    public int BeatmapSetId { get; init; }
    public int KeyCount { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TitleUnicode { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string ArtistUnicode { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Exists))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    public partial BeatmapDownloadState DownloadState { get; set; } = BeatmapDownloadState.NotExists;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>DBに存在する場合 true</summary>
    public bool Exists => DownloadState == BeatmapDownloadState.Exists;

    /// <summary>ダウンロード可能な場合 true</summary>
    public bool CanDownload => BeatmapSetId > 0 && (DownloadState == BeatmapDownloadState.NotExists || DownloadState == BeatmapDownloadState.Error);
}
