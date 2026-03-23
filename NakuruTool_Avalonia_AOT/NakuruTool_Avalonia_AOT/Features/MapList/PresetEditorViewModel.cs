using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.MapList.Models;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZLinq;

namespace NakuruTool_Avalonia_AOT.Features.MapList;

/// <summary>
/// プリセット編集画面のViewModel
/// プリセット選択・条件編集・保存・削除・一括生成を担当する
/// </summary>
public partial class PresetEditorViewModel : ViewModelBase
{
    private const int MaxConditions = 8;

    private readonly IFilterPresetService _presetService;
    private readonly IDatabaseService _databaseService;
    private readonly IGenerateCollectionService _generateCollectionService;

    [ObservableProperty]
    public partial FilterPreset? SelectedPreset { get; set; }

    [ObservableProperty]
    public partial string EditingPresetName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBatchGenerating { get; set; } = false;

    [ObservableProperty]
    public partial int BatchGenerationProgress { get; set; } = 0;

    [ObservableProperty]
    public partial string BatchGenerationStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditorStatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// 編集中のフィルタ条件リスト
    /// </summary>
    public AvaloniaList<FilterCondition> EditingConditions { get; } = new();

    /// <summary>
    /// プリセット一覧（_presetService.Presetsへの参照）
    /// </summary>
    public AvaloniaList<FilterPreset> Presets { get; }



    /// <summary>
    /// 編集パネルのIsVisible制御用
    /// </summary>
    public bool HasSelectedPreset => SelectedPreset != null;

    /// <summary>
    /// 一括生成中の操作無効化バインディング用
    /// </summary>
    public bool IsNotBatchGenerating => !IsBatchGenerating;

    public PresetEditorViewModel(
        IFilterPresetService presetService,
        IDatabaseService databaseService,
        IGenerateCollectionService generateCollectionService)
    {
        _presetService = presetService;
        _databaseService = databaseService;
        _generateCollectionService = generateCollectionService;

        // Presetsはpresetサービスの同一インスタンス参照
        Presets = presetService.Presets;

        // EditingConditions変更時のコマンド更新
        EditingConditions.ObserveCollectionChanged()
            .Subscribe(_ =>
            {
                AddConditionCommand.NotifyCanExecuteChanged();
                SavePresetCommand.NotifyCanExecuteChanged();
            })
            .AddTo(Disposables);

        // Presets変更時の一括生成コマンド更新
        _presetService.Presets.ObserveCollectionChanged()
            .Subscribe(_ => BatchGenerateCollectionsCommand.NotifyCanExecuteChanged())
            .AddTo(Disposables);
    }

    partial void OnSelectedPresetChanged(FilterPreset? value)
    {
        if (value != null)
        {
            EditingPresetName = value.Name;
            LoadConditions(value);
        }
        else
        {
            EditingPresetName = string.Empty;
            EditingConditions.Clear();
        }

        // 操作結果メッセージをクリア（次の操作開始時クリア方針）
        EditorStatusMessage = string.Empty;

        SavePresetCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasSelectedPreset));
    }

    partial void OnEditingPresetNameChanged(string value)
    {
        SavePresetCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBatchGeneratingChanged(bool value)
    {
        BatchGenerateCollectionsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsNotBatchGenerating));
    }

    private bool CanSavePreset() =>
        SelectedPreset != null
        && !string.IsNullOrWhiteSpace(EditingPresetName)
        && EditingConditions.Count > 0;

    private bool CanAddCondition => EditingConditions.Count < MaxConditions;

    private bool CanBatchGenerate() =>
        !IsBatchGenerating
        && Presets.Count > 0;

    /// <summary>
    /// 編集中の名前・コレクション名・条件でプリセットを保存。
    /// 名前変更時はRenamePresetで重複チェック付きアトミック操作。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSavePreset))]
    private void SavePreset()
    {
        if (SelectedPreset == null) return;

        // 操作結果メッセージをクリア
        EditorStatusMessage = string.Empty;

        var oldName = SelectedPreset.Name;
        var newName = EditingPresetName.Trim();

        var conditionDataList = EditingConditions
            .Select(FilterConditionData.FromFilterCondition)
            .ToList();

        var preset = new FilterPreset
        {
            Name = newName,
            CollectionName = newName,  // プリセット名 = コレクション名
            Conditions = conditionDataList
        };

        bool success;

        if (oldName != newName)
        {
            // 名前変更を伴う保存 → RenamePresetで重複チェック付きアトミック操作
            success = _presetService.RenamePreset(oldName, preset);
            if (!success)
            {
                // RenamePreset失敗 = 新名が既存プリセットと重複
                EditorStatusMessage = string.Format(
                    LangServiceInstance.GetString("Preset.Status.DuplicateName"), newName);
                return;
            }
        }
        else
        {
            // 名前変更なし → 通常のSavePreset（同名上書き）
            success = _presetService.SavePreset(preset);
        }

        if (success)
        {
            // 保存後、新しいプリセットを選択状態にする
            SelectedPreset = _presetService.Presets.FirstOrDefault(p => p.Name == newName);
            EditorStatusMessage = LangServiceInstance.GetString("Preset.Status.Saved");
        }
        else
        {
            EditorStatusMessage = LangServiceInstance.GetString("Preset.Status.SaveFailed");
        }
    }

    /// <summary>
    /// 新規プリセットを作成する
    /// </summary>
    [RelayCommand]
    private void AddNewPreset()
    {
        EditorStatusMessage = string.Empty;

        // 一意なデフォルト名を生成
        var baseName = LangServiceInstance.GetString("Preset.NewPresetName");
        var name = baseName;
        var counter = 1;
        while (_presetService.Presets.Any(p => p.Name == name))
        {
            name = $"{baseName} ({counter++})";
        }

        var preset = new FilterPreset
        {
            Name = name,
            CollectionName = name,
            Conditions = new List<FilterConditionData>()
        };

        var success = _presetService.SavePreset(preset);
        if (success)
        {
            SelectedPreset = _presetService.Presets.FirstOrDefault(p => p.Name == name);
            EditorStatusMessage = LangServiceInstance.GetString("Preset.Status.Saved");
        }
    }

    /// <summary>
    /// プリセットを削除する。引数があればそれを、なければ SelectedPreset を削除する。
    /// </summary>
    [RelayCommand]
    private void DeletePreset(FilterPreset? target)
    {
        var preset = target ?? SelectedPreset;
        if (preset == null) return;

        // 操作結果メッセージをクリア
        EditorStatusMessage = string.Empty;

        var name = preset.Name;
        var success = _presetService.DeletePreset(name);

        if (success)
        {
            if (SelectedPreset == preset)
            {
                SelectedPreset = null;
            }
            EditorStatusMessage = string.Format(
                LangServiceInstance.GetString("Preset.Status.Deleted"), name);
        }
        else
        {
            EditorStatusMessage = LangServiceInstance.GetString("Preset.Status.DeleteFailed");
        }
    }

    /// <summary>
    /// EditingConditionsに新規FilterConditionを追加
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddCondition))]
    private void AddCondition()
    {
        EditingConditions.Add(new FilterCondition());
    }

    /// <summary>
    /// 指定条件をEditingConditionsから削除
    /// </summary>
    [RelayCommand]
    private void RemoveCondition(FilterCondition condition)
    {
        EditingConditions.Remove(condition);
        AddConditionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// EditingConditions.Clear()
    /// </summary>
    [RelayCommand]
    private void ClearConditions()
    {
        EditingConditions.Clear();
        AddConditionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 全プリセットの条件でbeatmapをフィルタし、各コレクションを生成
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanBatchGenerate))]
    private async Task BatchGenerateCollectionsAsync()
    {
        IsBatchGenerating = true;
        BatchGenerationProgress = 0;
        BatchGenerationStatusMessage = string.Empty;
        EditorStatusMessage = string.Empty;

        try
        {
            var presets = _presetService.Presets.ToArray();
            var totalPresets = presets.Length;
            var successCount = 0;

            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];

                BatchGenerationStatusMessage = string.Format(
                    LangServiceInstance.GetString("Preset.Status.BulkProgress"),
                    i + 1, totalPresets, preset.Name);
                BatchGenerationProgress = (int)((double)i / totalPresets * 100);

                var filteredBeatmaps = FilterBeatmapsByPreset(preset);

                if (filteredBeatmaps.Length > 0 && !string.IsNullOrWhiteSpace(preset.CollectionName))
                {
                    var success = await _generateCollectionService.GenerateCollection(
                        preset.CollectionName, filteredBeatmaps);
                    if (success) successCount++;
                }
            }

            BatchGenerationProgress = 100;
            var failCount = totalPresets - successCount;
            BatchGenerationStatusMessage = string.Format(
                LangServiceInstance.GetString("Preset.Status.BulkCompleted"),
                successCount, failCount);
        }
        catch (Exception ex)
        {
            BatchGenerationStatusMessage = string.Format(
                LangServiceInstance.GetString("Preset.Status.BulkFailed"),
                ex.Message);
        }
        finally
        {
            IsBatchGenerating = false;
        }
    }

    /// <summary>
    /// プリセットのConditionsをEditingConditionsに読み込む
    /// </summary>
    private void LoadConditions(FilterPreset preset)
    {
        EditingConditions.Clear();
        foreach (var conditionData in preset.Conditions)
        {
            var condition = conditionData.ToFilterCondition();
            EditingConditions.Add(condition);
        }
    }

    /// <summary>
    /// プリセットの条件でbeatmapをフィルタして返す（一括生成用）
    /// </summary>
    private Beatmap[] FilterBeatmapsByPreset(FilterPreset preset)
    {
        var conditions = preset.Conditions
            .Select(cd => cd.ToFilterCondition())
            .ToArray();

        if (conditions.Length == 0)
            return Array.Empty<Beatmap>();

        // コレクション条件用のHashSetキャッシュ（プリセットごとにリセット）
        HashSet<string>? collectionMd5Cache = null;
        string? cachedCollectionName = null;

        return _databaseService.Beatmaps.AsValueEnumerable()
            .Where(beatmap =>
            {
                foreach (var condition in conditions)
                {
                    if (condition.Target == FilterTarget.Collection)
                    {
                        if (string.IsNullOrEmpty(condition.CollectionValue)) continue;

                        if (cachedCollectionName != condition.CollectionValue)
                        {
                            var col = _databaseService.OsuCollections
                                .AsValueEnumerable()
                                .FirstOrDefault(c => c.Name == condition.CollectionValue);
                            collectionMd5Cache = col != null
                                ? new HashSet<string>(col.BeatmapMd5s, StringComparer.OrdinalIgnoreCase)
                                : new HashSet<string>();
                            cachedCollectionName = condition.CollectionValue;
                        }

                        if (!collectionMd5Cache!.Contains(beatmap.MD5Hash))
                            return false;
                    }
                    else if (!condition.Matches(beatmap))
                    {
                        return false;
                    }
                }
                return true;
            })
            .ToArray();
    }



    private bool _disposed;

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose(); // CompositeDisposable破棄
    }
}
