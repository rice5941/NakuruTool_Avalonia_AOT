using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NakuruTool_Avalonia_AOT.Features.Translate;
using NakuruTool_Avalonia_AOT.Features.Settings;

namespace NakuruTool_Avalonia_AOT
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            var languageService = LanguageService.Instance;
            ApplyLanguageCulture(languageService.CurrentLanguage);

            languageService.LanguageChanged += (_, _) =>
            {
                ApplyLanguageCulture(languageService.CurrentLanguage);
            };
        }

        /// <summary>
        /// 言語変更に応じてアプリ全体のカルチャを更新する
        /// </summary>
        private static void ApplyLanguageCulture(string languageCode)
        {
            var normalizedLanguageCode = LanguageService.Instance.NormalizeLanguageCode(languageCode);
            CultureInfo culture;

            try
            {
                culture = CultureInfo.GetCultureInfo(normalizedLanguageCode);
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.GetCultureInfo(LanguageService.DefaultLanguageCode);
            }

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            if (Current is App app)
            {
                app.UpdateSemiThemeLocale(culture);
            }
        }

        /// <summary>
        /// SemiTheme の Locale を更新する
        /// </summary>
        private void UpdateSemiThemeLocale(CultureInfo culture)
        {
            if (Styles.Count > 0)
            {
                foreach (var style in Styles)
                {
                    if (style is Semi.Avalonia.SemiTheme semiTheme)
                    {
                        semiTheme.Locale = culture;
                        break;
                    }
                }
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var composition = new Composition();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = composition.MainWindow;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                _ = singleViewPlatform;
            }

            // 設定からテーマを復元
            var isDarkTheme = SettingsService.Current?.IsDarkTheme ?? true;
            RequestedThemeVariant = isDarkTheme ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;

            base.OnFrameworkInitializationCompleted();
        }
    }
}