using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>エクスポート対象コレクションの行モデル</summary>
public partial class ExportCollectionItem : ObservableObject
{
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    public string Name { get; init; } = string.Empty;
    public string[] BeatmapMd5s { get; init; } = Array.Empty<string>();
}
