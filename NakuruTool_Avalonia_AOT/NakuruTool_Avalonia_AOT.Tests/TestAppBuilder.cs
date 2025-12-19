using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

// プロジェクト全体で使用するテストアプリケーションを設定
[assembly: AvaloniaTestApplication(typeof(NakuruTool_Avalonia_AOT.Tests.TestAppBuilder))]

namespace NakuruTool_Avalonia_AOT.Tests;

/// <summary>
/// ヘッドレステスト用のAppBuilder設定
/// Skiaレンダラーを有効にしてスクリーンショット撮影を可能にする
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia() // Skiaレンダラーを有効化（スクリーンショット撮影に必要）
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false // 実際のレンダリングを有効化
        });
}
