using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NakuruTool_Avalonia_AOT.Features.MapList.Models;

/// <summary>
/// 条件の組。組の中の条件はすべて満たす（AND）必要があり、組どうしはいずれかを満たせばよい（OR）。
/// </summary>
public partial class FilterConditionGroup : ObservableObject
{
    /// <summary>
    /// 組の中の条件
    /// </summary>
    public AvaloniaList<FilterCondition> Conditions { get; } = new();

    /// <summary>
    /// 表示用の番号（1 始まり）。FilterGroupsEditorViewModel が並びの変更時に振り直す。
    /// </summary>
    [ObservableProperty]
    public partial int Number { get; set; } = 1;

    /// <summary>
    /// 条件はあるが値の入った条件が 1 つもなく、判定から外されているか
    /// </summary>
    [ObservableProperty]
    public partial bool IsIgnored { get; set; }
}
