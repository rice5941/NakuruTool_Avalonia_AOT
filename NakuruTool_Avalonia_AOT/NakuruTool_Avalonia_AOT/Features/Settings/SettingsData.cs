using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace NakuruTool_Avalonia_AOT.Features.Settings
{
    public interface ISettingsData : INotifyPropertyChanged
    {
        string OsuFolderPath { get; set; }
        string LanguageKey { get; set; }
        int AudioVolume { get; set; }
        bool AutoPlayOnSelect { get; set; }
    }

    public partial class SettingsData : ObservableObject, ISettingsData
    {
        // Jsonシリアライズ使用時は[ObservableProperty]使用不可
        private string _osuFolderPath = string.Empty;
        private string _languageKey = "ja-JP";
        private int _audioVolume = 50;
        private bool _autoPlayOnSelect = true;

        public string OsuFolderPath
        {
            get => _osuFolderPath;
            set => SetProperty(ref _osuFolderPath, value);
        }

        public string LanguageKey
        {
            get => _languageKey;
            set => SetProperty(ref _languageKey, value);
        }

        public int AudioVolume
        {
            get => _audioVolume;
            set => SetProperty(ref _audioVolume, value);
        }

        public bool AutoPlayOnSelect
        {
            get => _autoPlayOnSelect;
            set => SetProperty(ref _autoPlayOnSelect, value);
        }

        public void Update(SettingsData newData)
        {
            OsuFolderPath = newData.OsuFolderPath;
            LanguageKey = newData.LanguageKey;
            AudioVolume = newData.AudioVolume;
            AutoPlayOnSelect = newData.AutoPlayOnSelect;
        }
    }

    /// <summary>
    /// NativeAOT対応のためのJSON Source Generatorコンテキスト
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SettingsData))]
    public partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}