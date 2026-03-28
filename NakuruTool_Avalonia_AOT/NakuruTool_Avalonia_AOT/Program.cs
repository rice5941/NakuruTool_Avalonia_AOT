using System;
using Avalonia;
using Avalonia.LinuxFramebuffer;
#if DEBUG
using HotAvalonia;
#endif

namespace NakuruTool_Avalonia_AOT
{
    internal sealed class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var builder = BuildAvaloniaApp();

            if (Array.Exists(args, arg => arg == "--drm"))
            {
                return builder.StartLinuxDrm(args, card: null, scaling: 1.0);
            }

            return builder.StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
#if DEBUG
                .UseHotReload()
#endif
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
