using R3;
using System;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer;

/// <summary>
/// オーディオ再生サービスのインターフェース
/// </summary>
public interface IAudioPlayerService : IDisposable
{
    /// <summary>
    /// 再生状態を監視するObservable
    /// </summary>
    Observable<AudioPlayerState> StateChanged { get; }

    /// <summary>
    /// 現在の再生状態
    /// </summary>
    AudioPlayerState CurrentState { get; }

    /// <summary>
    /// 音量 (0 - 100)
    /// </summary>
    int Volume { get; set; }

    /// <summary>
    /// 指定されたオーディオファイルを再生
    /// </summary>
    void Play(string filePath);

    /// <summary>
    /// 再生を一時停止
    /// </summary>
    void Pause();

    /// <summary>
    /// 再生を再開
    /// </summary>
    void Resume();

    /// <summary>
    /// 再生を停止
    /// </summary>
    void Stop();

    /// <summary>
    /// 再生/一時停止を切り替え
    /// </summary>
    void TogglePlayPause();
}

/// <summary>
/// オーディオプレイヤーの状態
/// </summary>
public enum AudioPlayerState
{
    Stopped,
    Playing,
    Paused,
    Error
}
