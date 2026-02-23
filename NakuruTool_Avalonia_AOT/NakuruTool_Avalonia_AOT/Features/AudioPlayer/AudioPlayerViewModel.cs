using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NakuruTool_Avalonia_AOT.Features.OsuDatabase;
using NakuruTool_Avalonia_AOT.Features.Settings;
using NakuruTool_Avalonia_AOT.Features.Shared.Extensions;
using NakuruTool_Avalonia_AOT.Features.Shared.ViewModels;
using R3;
using System;
using System.IO;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer;

/// <summary>
/// オーディオ再生コントロール用のViewModel
/// </summary>
public partial class AudioPlayerViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsPlaying { get; set; } = false;

    [ObservableProperty]
    public partial int Volume { get; set; }

    partial void OnVolumeChanged(int value)
    {
        _audioPlayerService.Volume = value;
        // ボリューム変更を設定ファイルに保存（R3 Debounceで300ms間隔）
        _volumeSaveSubject.OnNext(value);
    }

    private readonly IAudioPlayerService _audioPlayerService;
    private readonly ISettingsService _settingsService;
    private readonly Subject<int> _volumeSaveSubject = new();

    public AudioPlayerViewModel(
        IAudioPlayerService audioPlayerService,
        ISettingsService settingsService)
    {
        _audioPlayerService = audioPlayerService;
        _settingsService = settingsService;

        // 設定ファイルから初期音量を読み込み
        Volume = _settingsService.SettingsData.AudioVolume;

        // オーディオ再生状態の監視
        _audioPlayerService.StateChanged
            .Subscribe(state => IsPlaying = state == AudioPlayerState.Playing)
            .AddTo(Disposables);

        // ボリューム変更を300msデバウンスして設定保存
        _volumeSaveSubject
            .Debounce(TimeSpan.FromMilliseconds(300))
            .Subscribe(SaveVolume)
            .AddTo(Disposables);

        // 初期音量をサービスに反映
        _audioPlayerService.Volume = Volume;
    }

    private void SaveVolume(int volume)
    {
        if (_settingsService.SettingsData is SettingsData settingsData)
        {
            settingsData.AudioVolume = volume;
            _settingsService.SaveSettings(settingsData);
        }
    }

    [RelayCommand]
    private void TogglePlayPause() => _audioPlayerService.TogglePlayPause();

    [RelayCommand]
    private void StopAudio() => _audioPlayerService.Stop();

    /// <summary>
    /// 指定された譜面のオーディオを再生
    /// </summary>
    public void PlayBeatmapAudio(Beatmap? beatmap)
    {
        if (beatmap == null) return;

        var osuFolderPath = _settingsService.SettingsData.OsuFolderPath;
        if (string.IsNullOrEmpty(osuFolderPath)) return;
        if (string.IsNullOrEmpty(beatmap.AudioFilename)) return;

        var audioPath = Path.Combine(osuFolderPath, "Songs", beatmap.FolderName, beatmap.AudioFilename);
        _audioPlayerService.Play(audioPath);
    }
}
