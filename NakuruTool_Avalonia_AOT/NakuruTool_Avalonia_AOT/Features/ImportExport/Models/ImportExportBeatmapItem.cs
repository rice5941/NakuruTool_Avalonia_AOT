namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>beatmap の表示専用行モデル（Missing = DB未存在）</summary>
public sealed record ImportExportBeatmapItem
{
    public int KeyCount { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Creator { get; init; } = string.Empty;

    /// <summary>DBに存在する場合 true</summary>
    public bool Exists { get; init; }
}
