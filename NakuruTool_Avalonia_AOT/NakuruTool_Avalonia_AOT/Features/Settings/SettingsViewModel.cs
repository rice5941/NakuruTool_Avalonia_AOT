using Avalonia;
using Avalonia.Collections;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;
using Semi.Avalonia;
using System;
using System.Reflection;
using static System.Net.WebRequestMethods;

namespace NakuruTool_Avalonia_AOT.Features.Settings;

public interface ISettingsViewModel : IDisposable
{
    IAvaloniaReadOnlyList<string> LanguageKeys { get; }
    string SelectedLanguageKey { get; }
    string SelectedFolderPath { get; }
    string OsuPathErrorMessage { get; }
    bool HasOsuPathError { get; }
    bool AutoPlayOnSelect { get; set; }
    bool PreferUnicode { get; set; }
    IAvaloniaReadOnlyList<string> MirrorUrls { get; }
    string SelectedMirrorUrl { get; set; }
    bool AutoBatchGenerateOnStartup { get; set; }
    string AppVersion { get; }
}

public partial class SettingsViewModel : ViewModelBase, ISettingsViewModel
{
    public IAvaloniaReadOnlyList<string> LanguageKeys { get; } = new AvaloniaList<string>(LanguageService.Instance.AvailableLanguages);

    public IAvaloniaReadOnlyList<string> MirrorUrls { get; } = new AvaloniaList<string>(
    [
        "https://catboy.best/d/",
        "https://api.nerinyan.moe/d/",
    ]);

    public string AppVersion { get; } =
        typeof(SettingsViewModel).Assembly.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "unknown";

    [ObservableProperty]
    public partial string SelectedLanguageKey { get; set; }

    partial void OnSelectedLanguageKeyChanged(string value)
    {
        if (string.IsNullOrEmpty(value) == false && _isInitialized)
        {
            UpdateSettingData();
        }
    }

    [ObservableProperty]
    public partial string SelectedFolderPath { get; set; }

    partial void OnSelectedFolderPathChanged(string value)
    {
        if (_isInitialized)
        {
            if (string.IsNullOrEmpty(value) == false)
            {
                UpdateSettingData();
            }
            UpdateOsuPathErrorMessage();
        }
    }

    [ObservableProperty]
    public partial string OsuPathErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOsuPathError { get; set; } = false;

    [ObservableProperty]
    public partial bool AutoPlayOnSelect { get; set; } = true;

    partial void OnAutoPlayOnSelectChanged(bool value)
    {
        if (_isInitialized)
        {
            UpdateSettingData();
        }
    }

    [ObservableProperty]
    public partial bool PreferUnicode { get; set; } = false;

    partial void OnPreferUnicodeChanged(bool value)
    {
        if (_isInitialized)
        {
            UpdateSettingData();
        }
    }

    [ObservableProperty]
    public partial string SelectedMirrorUrl { get; set; }

    partial void OnSelectedMirrorUrlChanged(string value)
    {
        if (_isInitialized)
        {
            UpdateSettingData();
        }
    }

    [ObservableProperty]
    public partial bool AutoBatchGenerateOnStartup { get; set; } = false;

    partial void OnAutoBatchGenerateOnStartupChanged(bool value)
    {
        if (_isInitialized)
        {
            UpdateSettingData();
        }
    }

    private readonly ISettingsService _settingsService;
    private bool _isInitialized = false;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        var settingsData = settingsService.SettingsData;
        SelectedLanguageKey = settingsData.LanguageKey;
        SelectedFolderPath = settingsData.OsuFolderPath;
        AutoPlayOnSelect = settingsData.AutoPlayOnSelect;
        PreferUnicode = settingsData.PreferUnicode;
        SelectedMirrorUrl = settingsData.BeatmapMirrorUrl;
        AutoBatchGenerateOnStartup = settingsData.AutoBatchGenerateOnStartup;

        UpdateOsuPathErrorMessage();

        _isInitialized = true;
    }

    private void UpdateSettingData()
    {
        var app = Application.Current;
        var settingData = new SettingsData
        {
            LanguageKey = SelectedLanguageKey,
            OsuFolderPath = SelectedFolderPath,
            // AudioVolumeはAudioPlayerViewModelが管理するため、現在の設定値を引き継ぐ
            AudioVolume = _settingsService.SettingsData.AudioVolume,
            AutoPlayOnSelect = AutoPlayOnSelect,
            PreferUnicode = PreferUnicode,
            BeatmapMirrorUrl = SelectedMirrorUrl,
            IsDarkTheme = app?.ActualThemeVariant == ThemeVariant.Dark,
            AutoBatchGenerateOnStartup = AutoBatchGenerateOnStartup,
        };

        _settingsService.SaveSettings(settingData);
    }

    private void UpdateOsuPathErrorMessage()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
        {
            OsuPathErrorMessage = LangServiceInstance.GetString("Settings.ErrorOsuPathEmpty");
        }
        else
        {
            OsuPathErrorMessage = string.Empty;
        }

        HasOsuPathError = string.IsNullOrEmpty(OsuPathErrorMessage) ? false : true;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Application.Current;
        if (app is null) return;
        var theme = app.ActualThemeVariant;
        app.RequestedThemeVariant = theme == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        app.UnregisterFollowSystemTheme();
        UpdateSettingData();
    }
}
