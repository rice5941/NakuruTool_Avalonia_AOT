using Avalonia.Collections;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

public interface IFilterPresetService
{
    /// <summary>
    /// 利用可能なプリセット一覧
    /// </summary>
    AvaloniaList<FilterPreset> Presets { get; }

    /// <summary>
    /// プリセットを保存
    /// </summary>
    bool SavePreset(FilterPreset preset);

    /// <summary>
    /// プリセットを削除
    /// </summary>
    bool DeletePreset(string presetName);

    /// <summary>
    /// プリセットを読み込み
    /// </summary>
    void LoadPresets();
}

/// <summary>
/// フィルタプリセットの保存・読み込みを管理するサービス
/// </summary>
public class FilterPresetService : IFilterPresetService
{
    private readonly string _presetsPath;

    public AvaloniaList<FilterPreset> Presets { get; } = new();

    public FilterPresetService()
    {
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var presetsFolder = Path.Combine(appDirectory, "presets");

        // フォルダが存在しない場合は作成
        if (!Directory.Exists(presetsFolder))
        {
            Directory.CreateDirectory(presetsFolder);
        }

        _presetsPath = presetsFolder;

        // 起動時にプリセットを読み込み
        LoadPresets();
    }

    /// <summary>
    /// プリセットを保存
    /// </summary>
    public bool SavePreset(FilterPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            return false;
        }

        try
        {
            // ファイル名として使えない文字を置換
            var fileName = GetSafeFileName(preset.Name);
            var filePath = Path.Combine(_presetsPath, $"{fileName}.json");

            var json = JsonSerializer.Serialize(preset, FilterPresetJsonContext.Default.FilterPreset);
            File.WriteAllText(filePath, json);

            // リストを更新
            var existingPreset = Presets.FirstOrDefault(p => p.Name == preset.Name);
            if (existingPreset != null)
            {
                var index = Presets.IndexOf(existingPreset);
                Presets[index] = preset;
            }
            else
            {
                Presets.Add(preset);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"プリセットの保存に失敗しました: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// プリセットを削除
    /// </summary>
    public bool DeletePreset(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        try
        {
            var fileName = GetSafeFileName(presetName);
            var filePath = Path.Combine(_presetsPath, $"{fileName}.json");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var preset = Presets.FirstOrDefault(p => p.Name == presetName);
            if (preset != null)
            {
                Presets.Remove(preset);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"プリセットの削除に失敗しました: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// プリセットフォルダから全プリセットを読み込み
    /// </summary>
    public void LoadPresets()
    {
        Presets.Clear();

        try
        {
            if (!Directory.Exists(_presetsPath))
            {
                return;
            }

            var files = Directory.GetFiles(_presetsPath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var preset = JsonSerializer.Deserialize(json, FilterPresetJsonContext.Default.FilterPreset);

                    if (preset != null && !string.IsNullOrWhiteSpace(preset.Name))
                    {
                        Presets.Add(preset);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"プリセット読み込みエラー ({file}): {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"プリセットフォルダの読み込みに失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// ファイル名として使えない文字を置換
    /// </summary>
    private string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = fileName;

        foreach (var c in invalidChars)
        {
            safeName = safeName.Replace(c, '_');
        }

        return safeName;
    }
}
