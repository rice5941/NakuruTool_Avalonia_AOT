using LibVLCSharp.Shared;
using R3;
using System;
using System.Diagnostics;
using System.IO;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer;

/// <summary>
/// LibVLCSharpを使用したオーディオ再生サービス
/// </summary>
public sealed class AudioPlayerService : IAudioPlayerService
{
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private readonly ReactiveProperty<AudioPlayerState> _stateProperty;
    private readonly CompositeDisposable _disposables = [];
    private Media? _currentMedia;
    private bool _disposed;

    public Observable<AudioPlayerState> StateChanged => _stateProperty;
    public AudioPlayerState CurrentState => _stateProperty.Value;

    public int Volume
    {
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    public AudioPlayerService()
    {
        Core.Initialize();
        _libVLC = new LibVLC("--no-video");
        _mediaPlayer = new MediaPlayer(_libVLC);
        _stateProperty = new ReactiveProperty<AudioPlayerState>(AudioPlayerState.Stopped);

        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.Paused += OnPaused;
        _mediaPlayer.Stopped += OnStopped;
        _mediaPlayer.EncounteredError += OnError;
    }

    private void OnPlaying(object? sender, EventArgs e)
        => _stateProperty.Value = AudioPlayerState.Playing;

    private void OnPaused(object? sender, EventArgs e)
        => _stateProperty.Value = AudioPlayerState.Paused;

    private void OnStopped(object? sender, EventArgs e)
        => _stateProperty.Value = AudioPlayerState.Stopped;

    private void OnError(object? sender, EventArgs e)
    {
        Debug.WriteLine("AudioPlayerService: 再生エラーが発生しました");
        _stateProperty.Value = AudioPlayerState.Error;
    }

    public void Play(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.WriteLine("AudioPlayerService: ファイルパスが空です");
            return;
        }

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"AudioPlayerService: ファイルが見つかりません: {filePath}");
            return;
        }

        Stop();

        _currentMedia?.Dispose();
        _currentMedia = new Media(_libVLC, new Uri(filePath));
        _mediaPlayer.Media = _currentMedia;
        _mediaPlayer.Play();
    }

    public void Pause() => _mediaPlayer.Pause();

    public void Resume()
    {
        if (_stateProperty.Value == AudioPlayerState.Paused)
        {
            _mediaPlayer.Play();
        }
    }

    public void Stop() => _mediaPlayer.Stop();

    public void TogglePlayPause()
    {
        if (_stateProperty.Value == AudioPlayerState.Playing)
        {
            Pause();
        }
        else if (_stateProperty.Value == AudioPlayerState.Paused)
        {
            Resume();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mediaPlayer.Playing -= OnPlaying;
        _mediaPlayer.Paused -= OnPaused;
        _mediaPlayer.Stopped -= OnStopped;
        _mediaPlayer.EncounteredError -= OnError;

        _disposables.Dispose();
        _stateProperty.Dispose();
        _mediaPlayer.Stop();
        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();
        _libVLC.Dispose();
    }
}
