using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace NakuruTool_Avalonia_AOT.Features.Settings;
public interface ISettingsData : INotifyPropertyChanged
{
    string OsuFolderPath { get; set; }
    string LanguageKey { get; set; }
    int AudioVolume { get; set; }
    bool AutoPlayOnSelect { get; set; }
    bool PreferUnicode { get; set; }
    bool IsDarkTheme { get; set; }
    string BeatmapMirrorUrl { get; set; }
    bool AutoBatchGenerateOnStartup { get; set; }
    bool Analyze7kEnabled { get; set; }
    int Analyze7kParallelism { get; set; }
    bool Analyze7kShowConcentration { get; set; }
}

public partial class SettingsData : ObservableObject, ISettingsData
{
    // Jsonシリアライズ使用時は[ObservableProperty]使用不可
    private string _osuFolderPath = string.Empty;
    private string _languageKey = "ja-JP";
    private int _audioVolume = 10;
    private bool _autoPlayOnSelect = true;
    private bool _preferUnicode = false;
    private bool _isDarkTheme = true;
    private string _beatmapMirrorUrl = BeatmapMirrors.Default;
    private bool _autoBatchGenerateOnStartup = false;
    private bool _analyze7kEnabled = true;
    private int _analyze7kParallelism;
    private bool _analyze7kShowConcentration;

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

    public bool PreferUnicode
    {
        get => _preferUnicode;
        set => SetProperty(ref _preferUnicode, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    public string BeatmapMirrorUrl
    {
        get => _beatmapMirrorUrl;
        set => SetProperty(ref _beatmapMirrorUrl, value);
    }

    public bool AutoBatchGenerateOnStartup
    {
        get => _autoBatchGenerateOnStartup;
        set => SetProperty(ref _autoBatchGenerateOnStartup, value);
    }

    /// <summary>7K 譜面の解析（Prism・段位推定）を有効にする</summary>
    public bool Analyze7kEnabled
    {
        get => _analyze7kEnabled;
        set => SetProperty(ref _analyze7kEnabled, value);
    }

    /// <summary>7K 解析の並列数（0 は自動）。設定ファイルでのみ変更する。</summary>
    public int Analyze7kParallelism
    {
        get => _analyze7kParallelism;
        set => SetProperty(ref _analyze7kParallelism, value);
    }

    /// <summary>Prism 列をジャンル濃度の五角形で表示する（既定の false は基準スキル値の横棒）</summary>
    public bool Analyze7kShowConcentration
    {
        get => _analyze7kShowConcentration;
        set => SetProperty(ref _analyze7kShowConcentration, value);
    }

    public void Update(SettingsData newData)
    {
        OsuFolderPath = newData.OsuFolderPath;
        LanguageKey = newData.LanguageKey;
        AudioVolume = newData.AudioVolume;
        AutoPlayOnSelect = newData.AutoPlayOnSelect;
        PreferUnicode = newData.PreferUnicode;
        IsDarkTheme = newData.IsDarkTheme;
        BeatmapMirrorUrl = newData.BeatmapMirrorUrl;
        AutoBatchGenerateOnStartup = newData.AutoBatchGenerateOnStartup;
        Analyze7kEnabled = newData.Analyze7kEnabled;
        Analyze7kParallelism = newData.Analyze7kParallelism;
        Analyze7kShowConcentration = newData.Analyze7kShowConcentration;
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
