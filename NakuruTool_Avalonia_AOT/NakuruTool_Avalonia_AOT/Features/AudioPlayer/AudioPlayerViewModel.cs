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
    public partial int Volume { get; set; } = 50;
    partial void OnVolumeChanged(int value) => _audioPlayerService.Volume = value;

    private readonly IAudioPlayerService _audioPlayerService;
    private readonly ISettingsService _settingsService;

    public AudioPlayerViewModel(
        IAudioPlayerService audioPlayerService,
        ISettingsService settingsService)
    {
        _audioPlayerService = audioPlayerService;
        _settingsService = settingsService;

        // オーディオ再生状態の監視
        _audioPlayerService.StateChanged
            .Subscribe(state => IsPlaying = state == AudioPlayerState.Playing)
            .AddTo(Disposables);

        // 初期音量を設定
        _audioPlayerService.Volume = Volume;
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
