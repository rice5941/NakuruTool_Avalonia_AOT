using NakuruTool_Avalonia_AOT.Features.MainWindow;
using NakuruTool_Avalonia_AOT.Features.MapList;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using Pure.DI;
using static Pure.DI.Lifetime;

namespace NakuruTool_Avalonia_AOT;
partial class Composition
{
    private static void Setup() => DI.Setup("Composition")
        // ViewModelの登録（通常はSingletonかScoped）
        .Bind<MainWindowViewModel>().As(Singleton).To<MainWindowViewModel>()
        .Bind<ISettingsViewModel>().As(Singleton).To<SettingsViewModel>()
        .Bind<IDatabaseLoadingViewModel>().As(Singleton).To<DatabaseLoadingViewModel>()
        .Bind<IMapListViewModel>().As(Singleton).To<MapListViewModel>()
        .Bind<MapListPageViewModel>().As(Singleton).To<MapListPageViewModel>()

        // サービスの登録
        .Bind<ISettingsService>().As(Singleton).To<SettingsService>()
        .Bind<IDatabaseService>().As(Singleton).To<DatabaseService>()

        // Root（エントリーポイント）の定義
        // MainWindow自体をDIで生成することで、コンストラクタ注入を可能にします
        .Root<MainWindowView>("MainWindow");
}
