using Avalonia;
using Avalonia.Collections;
using Avalonia.Styling;
using Semi.Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using NakuruTool_Avalonia_AOT.Features.Translate;

namespace NakuruTool_Avalonia_AOT.Features.Settings;

public partial class SettingsViewModel : ViewModelBase
{
    public IAvaloniaReadOnlyList<string> LanguageKeys { get; } = new AvaloniaList<string>(LanguageService.Instance.AvailableLanguages);

    [ObservableProperty]
    private string _selectedLanguageKey;

    [ObservableProperty]
    private string _selectedFolderPath;

    [ObservableProperty]
    private string _osuPathErrorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasOsuPathError = false;

    private readonly ISettingsService _settingsService;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        var settingsData = settingsService.SettingsData;
        _selectedLanguageKey = settingsData.LanguageKey;
        _selectedFolderPath = settingsData.OsuFolderPath;

        UpdateOsuPathErrorMessage();
    }

    partial void OnSelectedFolderPathChanged(string value)
    {
        if (string.IsNullOrEmpty(value) == false)
        {
            UpdateSettingData();
        }
        UpdateOsuPathErrorMessage();
    }

    partial void OnSelectedLanguageKeyChanged(string value)
    {
        if (string.IsNullOrEmpty(value) == false)
        {
            UpdateSettingData();
        }
    }

    private void UpdateSettingData()
    {
        var settingData = new SettingsData
        {
            LanguageKey = SelectedLanguageKey,
            OsuFolderPath = SelectedFolderPath
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
