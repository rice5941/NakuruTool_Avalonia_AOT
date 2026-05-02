using CommunityToolkit.Mvvm.ComponentModel;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Sorting;

/// <summary>
/// 単一のソート規則（フィールド + 方向）
/// </summary>
internal sealed partial class SortRule : ObservableObject
{
    [ObservableProperty]
    public partial SortField Field { get; set; } = SortField.None;

    [ObservableProperty]
    public partial SortDirection Direction { get; set; } = SortDirection.Ascending;

    public bool IsActive => Field != SortField.None;
}
