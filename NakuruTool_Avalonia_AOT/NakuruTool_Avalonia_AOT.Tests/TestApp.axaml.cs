using Avalonia;
using Avalonia.Markup.Xaml;

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// ヘッドレステスト用のApplicationクラス
/// </summary>
public class TestApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
