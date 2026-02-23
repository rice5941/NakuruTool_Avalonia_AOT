using CommunityToolkit.Mvvm.ComponentModel;

namespace NakuruTool_Avalonia_AOT.Features.ImportExport.Models;

/// <summary>インポート対象ファイルの行モデル</summary>
public partial class ImportFileItem : ObservableObject
{
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    public string FilePath { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string CollectionName { get; init; } = string.Empty;
    public int BeatmapCount { get; init; }
    public CollectionExchangeData? ParsedData { get; init; }
}
