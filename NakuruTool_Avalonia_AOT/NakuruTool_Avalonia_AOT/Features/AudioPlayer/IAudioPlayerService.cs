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

    /// <summary>
    /// 再生が自然終了した時に通知するObservable（手動Stop()では通知しない）
    /// </summary>
    Observable<Unit> PlaybackCompleted { get; }

    /// <summary>
    /// 現在の再生位置を取得（秒）
    /// </summary>
    double GetPosition();

    /// <summary>
    /// 曲の総再生時間を取得（秒）
    /// </summary>
    double GetDuration();

    /// <summary>
    /// 指定位置にシーク（秒）
    /// </summary>
    void Seek(double positionSeconds);
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
