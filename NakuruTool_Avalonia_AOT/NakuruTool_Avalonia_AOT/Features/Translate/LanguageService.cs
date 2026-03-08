using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Avalonia.Platform;
using NakuruTool_Avalonia_AOT.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Translate
{
    /// <summary>
    /// 言語管理サービス（シングルトン）
    /// </summary>
    public class LanguageService : INotifyPropertyChanged
    {
        public const string DefaultLanguageCode = "ja-JP";

        private sealed class LanguageDefinition
        {
            public LanguageDefinition(string code, string nativeName, string isoLanguageName)
            {
                Code = code;
                NativeName = nativeName;
                IsoLanguageName = isoLanguageName;
            }

            public string Code { get; }

            public string NativeName { get; }

            public string IsoLanguageName { get; }
        }

        private static readonly LanguageDefinition[] SupportedLanguages =
        [
            new("ja-JP", "日本語", "ja"),
            new("en-US", "English", "en"),
            new("zh-CN", "简体中文", "zh"),
            new("ko-KR", "한국어", "ko"),
            new("th-TH", "ไทย", "th"),
            new("vi-VN", "Tiếng Việt", "vi"),
            new("id-ID", "Bahasa Indonesia", "id"),
            new("fil-PH", "Filipino", "fil"),
            new("pt-BR", "Português (Brasil)", "pt"),
            new("es-ES", "Español (España)", "es"),
            new("ru-RU", "Русский", "ru"),
            new("fr-FR", "Français", "fr"),
            new("de-DE", "Deutsch", "de"),
            new("ms-MY", "Bahasa Melayu", "ms")
        ];

        private static readonly Dictionary<string, string> SupportedLanguageDisplayNames = CreateSupportedLanguageDisplayNames();

        private static readonly Dictionary<string, string> SupportedLanguageAliases = CreateSupportedLanguageAliases();

        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        public static readonly LanguageService Instance = new LanguageService();

        /// <summary>
        /// 現在の言語コード
        /// </summary>
        private string _currentLanguage = string.Empty;

        /// <summary>
        /// 言語リソースのディクショナリ
        /// </summary>
        private Dictionary<string, string> _resources = new(StringComparer.Ordinal);

        /// <summary>
        /// 言語変更イベント
        /// </summary>
        public event EventHandler? LanguageChanged;

        /// <summary>
        /// プロパティ変更イベント
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        public List<string> AvailableLanguages { get; } = CreateAvailableLanguages();

        /// <summary>
        /// 現在の言語コード
        /// </summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            private set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 最後に発生したエラーメッセージ（デバッグ用）
        /// </summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// プライベートコンストラクタ（シングルトン）
        /// </summary>
        private LanguageService()
        {
            ApplyLanguage(DefaultLanguageCode, raiseEvent: false);
        }

        /// <summary>
        /// 言語を変更する
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        public void ChangeLanguage(string languageCode)
        {
            ApplyLanguage(languageCode, raiseEvent: true);
        }

        public string NormalizeLanguageCode(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                return DefaultLanguageCode;
            }

            foreach (var supportedLanguage in SupportedLanguages)
            {
                if (string.Equals(supportedLanguage.Code, languageCode, StringComparison.OrdinalIgnoreCase))
                {
                    return supportedLanguage.Code;
                }
            }

            if (SupportedLanguageAliases.TryGetValue(languageCode, out var aliasedLanguageCode))
            {
                return aliasedLanguageCode;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(languageCode);

                if (SupportedLanguageDisplayNames.ContainsKey(culture.Name))
                {
                    return culture.Name;
                }

                if (SupportedLanguageAliases.TryGetValue(culture.Name, out aliasedLanguageCode))
                {
                    return aliasedLanguageCode;
                }

                foreach (var supportedLanguage in SupportedLanguages)
                {
                    if (string.Equals(supportedLanguage.IsoLanguageName, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(supportedLanguage.IsoLanguageName, culture.ThreeLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                    {
                        return supportedLanguage.Code;
                    }
                }
            }
            catch (CultureNotFoundException)
            {
            }

            return DefaultLanguageCode;
        }

        public string GetLanguageDisplayName(string? languageCode)
        {
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            if (SupportedLanguageDisplayNames.TryGetValue(normalizedLanguageCode, out var displayName))
            {
                return displayName;
            }

            return SupportedLanguageDisplayNames[DefaultLanguageCode];
        }

        /// <summary>
        /// 指定されたキーの翻訳文字列を取得する
        /// </summary>
        /// <param name="key">リソースキー</param>
        /// <returns>翻訳文字列（見つからない場合はキーを返す）</returns>
        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            if (_resources.TryGetValue(key, out var value))
            {
                return value;
            }

            System.Diagnostics.Debug.WriteLine($"Translation key not found: {key}");
            return $"[{key}]";
        }

        private void ApplyLanguage(string languageCode, bool raiseEvent)
        {
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            if (string.Equals(_currentLanguage, normalizedLanguageCode, StringComparison.Ordinal))
            {
                return;
            }

            _resources = LoadLanguageResources(normalizedLanguageCode);
            CurrentLanguage = normalizedLanguageCode;

            if (raiseEvent)
            {
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Dictionary<string, string> LoadLanguageResources(string languageCode)
        {
            var mergedResources = new Dictionary<string, string>(StringComparer.Ordinal);
            string? lastError = default;

            if (TryLoadFlattenedLanguage(DefaultLanguageCode, out var defaultResources, out var defaultError))
            {
                MergeResources(mergedResources, defaultResources);
            }
            else
            {
                lastError = defaultError;
            }

            Dictionary<string, string> languageResources = new(StringComparer.Ordinal);
            string? languageError = null;
            if (!string.Equals(languageCode, DefaultLanguageCode, StringComparison.Ordinal) &&
                TryLoadFlattenedLanguage(languageCode, out languageResources, out languageError))
            {
                MergeResources(mergedResources, languageResources);
            }
            else if (!string.Equals(languageCode, DefaultLanguageCode, StringComparison.Ordinal) && string.IsNullOrEmpty(languageError) == false)
            {
                lastError = languageError;
            }

            LastError = lastError;
            if (string.IsNullOrEmpty(lastError) == false)
            {
                Console.WriteLine(lastError);
            }

            Console.WriteLine($"Language loaded: {languageCode}, Keys: {mergedResources.Count}");
            return mergedResources;
        }

        private static bool TryLoadFlattenedLanguage(string languageCode, out Dictionary<string, string> resources, out string? errorMessage)
        {
            resources = new Dictionary<string, string>(StringComparer.Ordinal);
            errorMessage = null;

            try
            {
                const string assemblyName = "NakuruTool";
                var uri = new Uri($"avares://{assemblyName}/Features/Translate/Resources/Languages/{languageCode}.json");

                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var jsonContent = reader.ReadToEnd();
                var jsonData = JsonSerializer.Deserialize(jsonContent, LanguageJsonContext.Default.DictionaryStringJsonElement);

                if (jsonData != null)
                {
                    FlattenDictionary(jsonData, string.Empty, resources);
                }

                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
            {
                errorMessage = $"Language file not found: {languageCode}.json - {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load language '{languageCode}': {ex.Message}";
                return false;
            }
        }

        private static void MergeResources(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            foreach (var resource in source)
            {
                target[resource.Key] = resource.Value;
            }
        }

        /// <summary>
        /// ネストされたディクショナリをフラットに変換する
        /// </summary>
        /// <param name="data">JSONデータ</param>
        /// <param name="prefix">キーのプレフィックス</param>
        private static void FlattenDictionary(Dictionary<string, JsonElement> data, string prefix, Dictionary<string, string> target)
        {
            if (data == null) return;

            foreach (var kvp in data)
            {
                var key = kvp.Key;
                if (string.IsNullOrEmpty(key)) continue;

                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

                if (kvp.Value.ValueKind == JsonValueKind.Object)
                {
                    var nestedDict = new Dictionary<string, JsonElement>();
                    foreach (var prop in kvp.Value.EnumerateObject())
                    {
                        nestedDict[prop.Name] = prop.Value;
                    }

                    FlattenDictionary(nestedDict, fullKey, target);
                }
                else if (kvp.Value.ValueKind == JsonValueKind.String)
                {
                    target[fullKey] = kvp.Value.GetString() ?? string.Empty;
                }
                else if (kvp.Value.ValueKind != JsonValueKind.Null && kvp.Value.ValueKind != JsonValueKind.Undefined)
                {
                    target[fullKey] = kvp.Value.ToString();
                }
            }
        }

        private static Dictionary<string, string> CreateSupportedLanguageDisplayNames()
        {
            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in SupportedLanguages)
            {
                displayNames[language.Code] = language.NativeName;
            }

            return displayNames;
        }

        private static Dictionary<string, string> CreateSupportedLanguageAliases()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ja"] = "ja-JP",
                ["en"] = "en-US",
                ["zh"] = "zh-CN",
                ["ko"] = "ko-KR",
                ["th"] = "th-TH",
                ["vi"] = "vi-VN",
                ["id"] = "id-ID",
                ["fil"] = "fil-PH",
                ["pt"] = "pt-BR",
                ["es"] = "es-ES",
                ["ru"] = "ru-RU",
                ["fr"] = "fr-FR",
                ["de"] = "de-DE",
                ["ms"] = "ms-MY"
            };
        }

        private static List<string> CreateAvailableLanguages()
        {
            var languages = new List<string>(SupportedLanguages.Length);
            foreach (var language in SupportedLanguages)
            {
                languages.Add(language.Code);
            }

            return languages;
        }

        /// <summary>
        /// プロパティ変更通知
        /// </summary>
        /// <param name="propertyName">プロパティ名</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
