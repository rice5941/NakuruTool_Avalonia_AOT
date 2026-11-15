using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// 列ヘッダーの中身に付けたツールチップを、ヘッダーセル全体（<see cref="DataGridColumnHeader"/>）に付け替える。
/// DataGrid には列ごとにヘッダーセルのスタイルを指定する手段がないため、添付プロパティで祖先のヘッダーに設定する。
/// </summary>
public sealed class ColumnHeaderToolTip
{
    /// <summary>ツールチップを付けたヘッダーセルに付けるクラス（カーソルの変更などに使う）</summary>
    public const string HeaderClass = "column-help";

    public static readonly AttachedProperty<object?> TipProperty =
        AvaloniaProperty.RegisterAttached<ColumnHeaderToolTip, Control, object?>("Tip");

    static ColumnHeaderToolTip()
    {
        TipProperty.Changed.AddClassHandler<Control>(OnTipChanged);
    }

    public static object? GetTip(Control control) => control.GetValue(TipProperty);

    public static void SetTip(Control control, object? value) => control.SetValue(TipProperty, value);

    private static void OnTipChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.AttachedToVisualTree -= OnAttachedToVisualTree;
        control.AttachedToVisualTree += OnAttachedToVisualTree;
        if (control.IsAttachedToVisualTree())
        {
            Apply(control);
        }
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
        {
            Apply(control);
        }
    }

    private static void Apply(Control control)
    {
        if (control.FindAncestorOfType<DataGridColumnHeader>() is not { } header)
        {
            return;
        }

        var tip = GetTip(control);
        ToolTip.SetTip(header, tip);
        header.Classes.Set(HeaderClass, tip is not null);
    }
}
