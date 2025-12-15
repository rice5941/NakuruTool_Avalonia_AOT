using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        private static readonly Lazy<LanguageService> _instance = new Lazy<LanguageService>(() => new LanguageService());

        /// <summary>
        /// シングルトンインスタンスの取得
        /// </summary>
        public static LanguageService Instance => _instance.Value;

        /// <summary>
        /// 現在の言語コード
        /// </summary>
        private string _currentLanguage = "ja-JP";

        /// <summary>
        /// 言語リソースのディクショナリ
        /// </summary>
        private Dictionary<string, string> _resources = new Dictionary<string, string>();

        /// <summary>
        /// 言語変更イベント
        /// </summary>
        public event EventHandler? LanguageChanged;

        /// <summary>
        /// プロパティ変更イベント
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        public List<string> AvailableLanguages { get; } = new List<string>
        {
            "ja-JP", // 日本語
            "en-US", // 英語
            "zh-CN", // 中国語（簡体字）
        };

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
            // デフォルト言語（日本語）を読み込み
            LoadLanguage("ja-JP");
        }

        /// <summary>
        /// 言語を変更する
        /// </summary>
        /// <param name="languageCode">言語コード（例: "ja-JP", "en-US", "zh-CN"）</param>
        public void ChangeLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || _currentLanguage == languageCode)
            {
                return;
            }

            LoadLanguage(languageCode);
            CurrentLanguage = languageCode;

            // 言語変更イベントを発火
            LanguageChanged?.Invoke(this, EventArgs.Empty);
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

            // キーが見つからない場合はキーをそのまま返す（デバッグ用）
            System.Diagnostics.Debug.WriteLine($"Translation key not found: {key}");
            return $"[{key}]";
        }

        /// <summary>
        /// 言語リソースを読み込む
        /// </summary>
        /// <param name="languageCode">言語コード</param>
        private void LoadLanguage(string languageCode)
        {
            try
            {
                // NativeAOT対応：アセンブリ名を直接指定
                const string assemblyName = "NakuruTool_Avalonia_AOT";
                var uri = new Uri($"avares://{assemblyName}/Features/Translate/Resources/Languages/{languageCode}.json");

                string jsonContent;
                try
                {
                    // AvaloniaのAssetLoaderでリソースファイルを読み込む（UTF-8エンコーディングを明示）
                    using var stream = AssetLoader.Open(uri);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    jsonContent = reader.ReadToEnd();
                }
                catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
                {
                    LastError = $"Language file not found: {uri} - {ex.Message}";
                    Console.WriteLine(LastError);
                    // ファイルが見つからない場合はデフォルト（日本語）を試す
                    if (languageCode != "ja-JP")
                    {
                        LoadLanguage("ja-JP"); // 再帰的にデフォルトを読み込む
                    }
                    return;
                }

                // JSONをディクショナリにデシリアライズ（NativeAOT対応のSource Generator使用）
                var jsonData = JsonSerializer.Deserialize(jsonContent, LanguageJsonContext.Default.DictionaryStringJsonElement);

                // フラットなディクショナリに変換
                _resources.Clear();
                if (jsonData != null)
                {
                    FlattenDictionary(jsonData, string.Empty);
                }

                LastError = null;
                Console.WriteLine($"Language loaded: {languageCode}, Keys: {_resources.Count}");
            }
            catch (Exception ex)
            {
                LastError = $"Failed to load language '{languageCode}': {ex.Message}";
                Console.WriteLine(LastError);
            }
        }

        /// <summary>
        /// ネストされたディクショナリをフラットに変換する
        /// </summary>
        /// <param name="data">JSONデータ</param>
        /// <param name="prefix">キーのプレフィックス</param>
        private void FlattenDictionary(Dictionary<string, JsonElement> data, string prefix)
        {
            if (data == null) return;

            foreach (var kvp in data)
            {
                var key = kvp.Key;
                if (string.IsNullOrEmpty(key)) continue;

                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

                if (kvp.Value.ValueKind == JsonValueKind.Object)
                {
                    // ネストされたオブジェクトを再帰的に処理
                    var nestedDict = new Dictionary<string, JsonElement>();
                    foreach (var prop in kvp.Value.EnumerateObject())
                    {
                        nestedDict[prop.Name] = prop.Value;
                    }
                    FlattenDictionary(nestedDict, fullKey);
                }
                else if (kvp.Value.ValueKind == JsonValueKind.String)
                {
                    _resources[fullKey] = kvp.Value.GetString() ?? string.Empty;
                }
                else if (kvp.Value.ValueKind != JsonValueKind.Null && kvp.Value.ValueKind != JsonValueKind.Undefined)
                {
                    _resources[fullKey] = kvp.Value.ToString();
                }
            }
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
