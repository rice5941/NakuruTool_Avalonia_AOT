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

namespace NakuruTool_Avalonia_AOT.Features.Settings;

public interface ISettingsViewModel : IDisposable
{
    IAvaloniaReadOnlyList<string> LanguageKeys { get; }
    string SelectedLanguageKey { get; }
    string SelectedFolderPath { get; }
    string OsuPathErrorMessage { get; }
    bool HasOsuPathError { get; }
}

public partial class SettingsViewModel : ViewModelBase, ISettingsViewModel
{
    public IAvaloniaReadOnlyList<string> LanguageKeys { get; } = new AvaloniaList<string>(LanguageService.Instance.AvailableLanguages);

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

    private readonly ISettingsService _settingsService;
    private readonly IDatabaseService _databaseService;
    private bool _isInitialized = false;

    public SettingsViewModel(ISettingsService settingsService, IDatabaseService databaseService)
    {
        _settingsService = settingsService;
        _databaseService = databaseService;

        var settingsData = settingsService.SettingsData;
        SelectedLanguageKey = settingsData.LanguageKey;
        SelectedFolderPath = settingsData.OsuFolderPath;

        UpdateOsuPathErrorMessage();

        _isInitialized = true;
    }

    private void UpdateSettingData()
    {
        var settingData = new SettingsData
        {
            LanguageKey = SelectedLanguageKey,
            OsuFolderPath = SelectedFolderPath,
            // AudioVolumeはAudioPlayerViewModelが管理するため、現在の設定値を引き継ぐ
            AudioVolume = _settingsService.SettingsData.AudioVolume
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
    }
}
