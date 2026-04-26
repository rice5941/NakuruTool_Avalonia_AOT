using NakuruTool_Avalonia_AOT.Features.AudioPlayer;
using NakuruTool_Avalonia_AOT.Features.BeatmapGenerator;
using NakuruTool_Avalonia_AOT.Features.ImportExport;
using NakuruTool_Avalonia_AOT.Features.Licenses;
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
        .Bind<AudioPlayerViewModel>().As(Singleton).To<AudioPlayerViewModel>()
        .Bind<AudioPlayerPanelViewModel>().As(Singleton).To<AudioPlayerPanelViewModel>()
        .Bind<ILicensesViewModel>().As(Singleton).To<LicensesViewModel>()
        .Bind<ImportExportPageViewModel>().As(Singleton).To<ImportExportPageViewModel>()

        // BeatmapGenerator Feature
        .Bind<IAudioRateChanger>().As(Singleton).To<FfmpegAudioRateChanger>()
        .Bind<IOsuFileRateConverter>().As(Singleton).To<OsuFileRateConverter>()
        .Bind<IOsbFileRateConverter>().As(Singleton).To<OsbFileRateConverter>()
        .Bind<IOsuFileAssetParser>().As(Singleton).To<OsuFileAssetParser>()
        .Bind<IBeatmapRateGenerator>().As(Singleton).To<BeatmapRateGenerator>()
        .Bind<BeatmapGenerationPageViewModel>().As(Singleton).To<BeatmapGenerationPageViewModel>()

        // サービスの登録
        .Bind<ISettingsService>().As(Singleton).To<SettingsService>()
        .Bind<IDatabaseService>().As(Singleton).To<DatabaseService>()
        .Bind<IGenerateCollectionService>().As(Singleton).To<GenerateCollectionService>()
        .Bind<IFilterPresetService>().As(Singleton).To<FilterPresetService>()
        .Bind<IAudioPlayerService>().As(Singleton).To<AudioPlayerService>()
        .Bind<IImportExportService>().As(Singleton).To<ImportExportService>()
        .Bind<IBeatmapDownloadService>().As(Singleton).To<BeatmapDownloadService>()

        // Root（エントリーポイント）の定義
        // MainWindow自体をDIで生成することで、コンストラクタ注入を可能にします
        .Root<MainWindowView>("MainWindow");
}
