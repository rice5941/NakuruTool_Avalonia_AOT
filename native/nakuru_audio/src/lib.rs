// Copyright (c) 2025 NakuruTool Contributors
// Licensed under the MIT License. See LICENSE file in the project root.
//
// This library provides a C FFI wrapper for the rodio audio playback library.
// rodio is licensed under MIT OR Apache-2.0.

use parking_lot::Mutex;
use rodio::{OutputStreamBuilder, Sink};
use std::fs::File;
use std::io::BufReader;
use std::str;

/// オーディオプレイヤーの状態
#[repr(C)]
#[derive(Clone, Copy)]
pub enum NativeAudioPlayerState {
    Stopped = 0,
    Playing = 1,
    Paused = 2,
    Error = 3,
}

/// 状態変化コールバック関数型
pub type StateChangedCallback = unsafe extern "C" fn(NativeAudioPlayerState);

/// オーディオプレイヤー
pub struct AudioPlayer {
    sink: Mutex<Sink>,
    state: Mutex<NativeAudioPlayerState>,
    callback: Mutex<Option<StateChangedCallback>>,
}

/// オーディオプレイヤーのインスタンスを作成
#[no_mangle]
pub extern "C" fn nakuru_audio_create() -> *mut AudioPlayer {
    match OutputStreamBuilder::open_default_stream() {
        Ok(stream_handle) => {
            let sink = Sink::connect_new(&stream_handle.mixer());

            // Keep stream_handle alive for the lifetime of the program
            Box::leak(Box::new(stream_handle));

            let player = AudioPlayer {
                sink: Mutex::new(sink),
                state: Mutex::new(NativeAudioPlayerState::Stopped),
                callback: Mutex::new(None),
            };
            Box::into_raw(Box::new(player))
        }
        Err(_) => std::ptr::null_mut(),
    }
}

/// オーディオプレイヤーのインスタンスを破棄
#[no_mangle]
pub extern "C" fn nakuru_audio_destroy(player: *mut AudioPlayer) {
    if !player.is_null() {
        unsafe {
            let _ = Box::from_raw(player);
        }
    }
}

/// 状態変化コールバックを登録
#[no_mangle]
pub extern "C" fn nakuru_audio_set_state_callback(
    player: *mut AudioPlayer,
    callback: StateChangedCallback,
) {
    if !player.is_null() {
        unsafe {
            let player = &*player;
            *player.callback.lock() = Some(callback);
        }
    }
}

/// オーディオファイルを再生
#[no_mangle]
pub extern "C" fn nakuru_audio_play(
    player: *mut AudioPlayer,
    file_path: *const u8,
    path_len: i32,
) -> i32 {
    if player.is_null() || file_path.is_null() {
        return -1;
    }

    let path_bytes = unsafe { std::slice::from_raw_parts(file_path, path_len as usize) };
    let path_str = match str::from_utf8(path_bytes) {
        Ok(s) => s,
        Err(_) => {
            return -1;
        }
    };

    unsafe {
        let player = &*player;

        // Stop current playback
        {
            let sink = player.sink.lock();
            sink.stop();
        }

        // Try to open and play the file
        match File::open(path_str) {
            Ok(file) => {
                let reader = BufReader::new(file);
                match rodio::Decoder::new(reader) {
                    Ok(source) => {
                        let sink = player.sink.lock();
                        sink.append(source);
                        sink.play();

                        // Notify state change
                        let callback = *player.callback.lock();
                        if let Some(cb) = callback {
                            cb(NativeAudioPlayerState::Playing);
                        }
                        *player.state.lock() = NativeAudioPlayerState::Playing;
                        return 0;
                    }
                    Err(_) => {
                        return -1;
                    }
                }
            }
            Err(_) => {
                return -1;
            }
        }
    }
}

/// 再生を一時停止
#[no_mangle]
pub extern "C" fn nakuru_audio_pause(player: *mut AudioPlayer) {
    if !player.is_null() {
        unsafe {
            let player = &*player;
            let sink = player.sink.lock();
            sink.pause();

            let callback = *player.callback.lock();
            if let Some(cb) = callback {
                cb(NativeAudioPlayerState::Paused);
            }
            *player.state.lock() = NativeAudioPlayerState::Paused;
        }
    }
}

/// 再生を再開
#[no_mangle]
pub extern "C" fn nakuru_audio_resume(player: *mut AudioPlayer) {
    if !player.is_null() {
        unsafe {
            let player = &*player;
            let sink = player.sink.lock();
            sink.play();

            let callback = *player.callback.lock();
            if let Some(cb) = callback {
                cb(NativeAudioPlayerState::Playing);
            }
            *player.state.lock() = NativeAudioPlayerState::Playing;
        }
    }
}

/// 再生を停止
#[no_mangle]
pub extern "C" fn nakuru_audio_stop(player: *mut AudioPlayer) {
    if !player.is_null() {
        unsafe {
            let player = &*player;
            let sink = player.sink.lock();
            sink.stop();

            let callback = *player.callback.lock();
            if let Some(cb) = callback {
                cb(NativeAudioPlayerState::Stopped);
            }
            *player.state.lock() = NativeAudioPlayerState::Stopped;
        }
    }
}

/// 音量を設定 (0.0 - 1.0以上)
#[no_mangle]
pub extern "C" fn nakuru_audio_set_volume(player: *mut AudioPlayer, volume: f32) {
    if !player.is_null() {
        unsafe {
            let player = &*player;
            let sink = player.sink.lock();
            sink.set_volume(volume);
        }
    }
}

/// 現在の音量を取得 (0.0 - 1.0以上)
#[no_mangle]
pub extern "C" fn nakuru_audio_get_volume(player: *mut AudioPlayer) -> f32 {
    if player.is_null() {
        return 0.0;
    }

    unsafe {
        let player = &*player;
        let sink = player.sink.lock();
        sink.volume()
    }
}

/// 現在の状態を取得
#[no_mangle]
pub extern "C" fn nakuru_audio_get_state(player: *mut AudioPlayer) -> NativeAudioPlayerState {
    if player.is_null() {
        return NativeAudioPlayerState::Error;
    }

    unsafe {
        let player = &*player;
        let state_guard = player.state.lock();
        let state = *state_guard;

        // Check if sink is empty (playback finished)
        if let NativeAudioPlayerState::Playing = state {
            let sink = player.sink.lock();
            if sink.empty() {
                return NativeAudioPlayerState::Stopped;
            }
        }

        state
    }
}
