using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// 条件の組（組の中は AND、組どうしは OR）の編集部品。フィルタ画面とプリセット編集画面で共有する。
/// </summary>
public partial class FilterGroupsEditorView : UserControl
{
    public FilterGroupsEditorView()
    {
        InitializeComponent();
    }
}
