using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            
            var languageService = LanguageService.Instance;
            // 初期言語をSemiThemeに反映
            UpdateSemiThemeLocale(languageService.CurrentLanguage);
            
            // 言語変更イベントをサブスクライブ
            languageService.LanguageChanged += (_, _) =>
            {
                UpdateSemiThemeLocale(languageService.CurrentLanguage);
            };
        }
        
        /// <summary>
        /// SemiThemeのLocaleを更新する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        private void UpdateSemiThemeLocale(string languageCode)
        {
            if (Styles.Count > 0)
            {
                foreach (var style in Styles)
                {
                    if (style is Semi.Avalonia.SemiTheme semiTheme)
                    {
                        semiTheme.Locale = new CultureInfo(languageCode);
                        break;
                    }
                }
            }
        }
        
        public override void OnFrameworkInitializationCompleted()
        {
            // Pure.DIのCompositionクラスをインスタンス化
            var composition = new Composition();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // DIコンテナからMainWindow（と依存するViewModel）を一括生成
                desktop.MainWindow = composition.MainWindow;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                // モバイル/Web等の場合も同様に解決可能（別途Root定義が必要）
                // singleViewPlatform.MainView = new MainView { DataContext = composition.MainViewModel };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}