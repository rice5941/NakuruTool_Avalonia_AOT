using R3;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NakuruTool_Avalonia_AOT.Features.AudioPlayer;

/// <summary>
/// rodio + csbindgenを使用したオーディオ再生サービス
/// </summary>
public sealed unsafe class AudioPlayerService : IAudioPlayerService
{
    private AudioPlayer* _playerHandle;
    private readonly Subject<AudioPlayerState> _stateSubject = new();
    private AudioPlayerState _currentState;
    private readonly CompositeDisposable _disposables = [];
    private bool _disposed;

    public Observable<AudioPlayerState> StateChanged => _stateSubject;
    public AudioPlayerState CurrentState => _currentState;

    public int Volume
    {
        get => (int)(NativeMethods.nakuru_audio_get_volume(_playerHandle) * 100f);
        set
        {
            var volumeF = value / 100f;
            NativeMethods.nakuru_audio_set_volume(_playerHandle, volumeF);
        }
    }

    public AudioPlayerService()
    {
        _playerHandle = NativeMethods.nakuru_audio_create();
        if (_playerHandle == null)
        {
            throw new InvalidOperationException("Failed to create native audio player");
        }

        _currentState = AudioPlayerState.Stopped;
    }

    private void SetState(AudioPlayerState newState)
    {
        _currentState = newState;
        _stateSubject.OnNext(newState);
    }

    public void Play(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.WriteLine("AudioPlayerService: ファイルパスが空です");
            SetState(AudioPlayerState.Error);
            return;
        }

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"AudioPlayerService: ファイルが見つかりません: {filePath}");
            SetState(AudioPlayerState.Error);
            return;
        }

        unsafe
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(filePath);
            fixed (byte* ptr = utf8Bytes)
            {
                int result = NativeMethods.nakuru_audio_play(_playerHandle, ptr, utf8Bytes.Length);
                if (result != 0)
                {
                    SetState(AudioPlayerState.Error);
                }
                else
                {
                    SetState(AudioPlayerState.Playing);
                }
            }
        }
    }

    public void Pause()
    {
        unsafe
        {
            NativeMethods.nakuru_audio_pause(_playerHandle);
            SetState(AudioPlayerState.Paused);
        }
    }

    public void Resume()
    {
        if (_currentState == AudioPlayerState.Paused)
        {
            unsafe
            {
                NativeMethods.nakuru_audio_resume(_playerHandle);
                SetState(AudioPlayerState.Playing);
            }
        }
    }

    public void Stop()
    {
        unsafe
        {
            NativeMethods.nakuru_audio_stop(_playerHandle);
            SetState(AudioPlayerState.Stopped);
        }
    }

    public void TogglePlayPause()
    {
        if (_currentState == AudioPlayerState.Playing)
        {
            Pause();
        }
        else if (_currentState == AudioPlayerState.Paused)
        {
            Resume();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        unsafe
        {
            if (_playerHandle != null)
            {
                NativeMethods.nakuru_audio_destroy(_playerHandle);
                _playerHandle = null;
            }
        }

        _disposables.Dispose();
        _stateSubject.Dispose();
    }
}
