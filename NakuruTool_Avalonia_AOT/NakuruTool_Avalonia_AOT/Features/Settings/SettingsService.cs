using NakuruTool_Avalonia_AOT.Features.Translate;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using R3;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace NakuruTool_Avalonia_AOT.Features.Settings;

public interface ISettingsService : IDisposable
{     
    ISettingsData SettingsData { get; }
    bool SaveSettings(SettingsData settings);
    bool CheckSettingsPath();
    string GetSettingsPath();
}

/// <summary>
/// 設定の保存と読み込みを管理するサービス
/// </summary>
public sealed class SettingsService : ISettingsService
{
    /// <summary>
    /// Converter等のDI外コンポーネントからSettings値を参照するための静的アクセサ。
    /// UnicodeDisplayConverter専用。他の用途での使用は推奨しない。
    /// </summary>
    internal static ISettingsData? Current { get; private set; }

    private readonly string _settingsPath;
    private readonly CompositeDisposable _disposables = [];
    private bool _disposed;

    public ISettingsData SettingsData { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public SettingsService()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        // settingsフォルダを作成（実行ファイル直下）
        var settingsFolder = Path.Combine(appDirectory, "settings");
            
        // フォルダが存在しない場合は作成
        if (!Directory.Exists(settingsFolder))
        {
            Directory.CreateDirectory(settingsFolder);
        }

        _settingsPath = Path.Combine(settingsFolder, "settings.json");

        var settingsData = LoadSettings();
    settingsData.LanguageKey = LanguageService.Instance.NormalizeLanguageCode(settingsData.LanguageKey);
        SettingsData = settingsData;

        LanguageService.Instance.ChangeLanguage(SettingsData.LanguageKey);

        // Converter用の静的アクセサを設定
        Current = settingsData;

        // R3拡張メソッドを使用して言語変更を監視
        settingsData.ObservePropertyAndSubscribe(
            nameof(ISettingsData.LanguageKey),
            () => LanguageService.Instance.ChangeLanguage(SettingsData.LanguageKey), _disposables);
    }

    /// <summary>
    /// 設定をファイルから読み込む
    /// </summary>
    private SettingsData LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsData);
                return settings ?? new SettingsData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定の読み込みに失敗しました: {ex.Message}");
        }

        return new SettingsData();
    }

    /// <summary>
    /// 設定をファイルに保存する
    /// </summary>
    public bool SaveSettings(SettingsData settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.LanguageKey = LanguageService.Instance.NormalizeLanguageCode(settings.LanguageKey);

        if (SettingsData is SettingsData impl)
        {
            impl.Update(settings);
        }

        try
        {
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.SettingsData);
            File.WriteAllText(_settingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定の保存に失敗しました: {ex.Message}");
            return false;
        }
    }

    public bool CheckSettingsPath() => File.Exists(_settingsPath);
    
    public string GetSettingsPath() => _settingsPath;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposables.Dispose();
    }
}