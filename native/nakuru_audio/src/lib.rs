// Copyright (c) 2025 NakuruTool Contributors
// Licensed under the MIT License. See LICENSE file in the project root.
//
// This library provides a C FFI wrapper for the rodio audio playback library.
// rodio is licensed under MIT OR Apache-2.0.

use parking_lot::Mutex;
use rodio::{OutputStreamBuilder, Sink, Source};
use std::fs::File;
use std::io::BufReader;
use std::str;
use symphonia::core::formats::FormatOptions;
use symphonia::core::io::MediaSourceStream;
use symphonia::core::meta::MetadataOptions;
use symphonia::core::probe::Hint;

/// Symphonia probe で再生時間を取得する（rodio の total_duration() が None の場合のフォールバック）
/// MP3 の Xing/VBRI ヘッダーから n_frames + time_base を読み取り Duration を返す
fn probe_duration_symphonia(path: &str) -> Option<std::time::Duration> {
    let file = File::open(path).ok()?;
    let mss = MediaSourceStream::new(Box::new(file), Default::default());

    let mut hint = Hint::new();
    if let Some(ext) = std::path::Path::new(path)
        .extension()
        .and_then(|e| e.to_str())
    {
        hint.with_extension(ext);
    }

    let probed = symphonia::default::get_probe()
        .format(&hint, mss, &FormatOptions::default(), &MetadataOptions::default())
        .ok()?;

    let track = probed.format.default_track()?;
    let n_frames = track.codec_params.n_frames?;
    let time_base = track.codec_params.time_base?;

    let time = time_base.calc_time(n_frames);
    let secs = time.seconds as f64 + time.frac;
    Some(std::time::Duration::from_secs_f64(secs))
}

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
    _stream: rodio::OutputStream,
    sink: Mutex<Sink>,
    duration: Mutex<Option<std::time::Duration>>,
    state: Mutex<NativeAudioPlayerState>,
    callback: Mutex<Option<StateChangedCallback>>,
    current_path: Mutex<Option<String>>,
    seek_offset: Mutex<f64>,
}

/// オーディオプレイヤーのインスタンスを作成
#[no_mangle]
pub extern "C" fn nakuru_audio_create() -> *mut AudioPlayer {
    match OutputStreamBuilder::open_default_stream() {
        Ok(stream) => {
            let sink = Sink::connect_new(&stream.mixer());

            let player = AudioPlayer {
                _stream: stream,
                sink: Mutex::new(sink),
                duration: Mutex::new(None),
                state: Mutex::new(NativeAudioPlayerState::Stopped),
                callback: Mutex::new(None),
                current_path: Mutex::new(None),
                seek_offset: Mutex::new(0.0),
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

        // Try to open and play the file
        match File::open(path_str) {
            Ok(file) => {
                let reader = BufReader::new(file);
                match rodio::Decoder::new(reader) {
                    Ok(source) => {
                        // Get duration before consuming source
                        // total_duration() は MP3 の Xing ヘッダーがない場合に None を返すため
                        // symphonia の probe 結果をフォールバックとして使用する
                        let total_duration = source.total_duration()
                            .or_else(|| probe_duration_symphonia(path_str));

                        // Create a new Sink and replace the old one
                        let new_sink = Sink::connect_new(&player._stream.mixer());
                        new_sink.append(source);
                        new_sink.play();

                        {
                            let mut sink = player.sink.lock();
                            // 旧Sinkの音量を新Sinkに引き継ぐ
                            new_sink.set_volume(sink.volume());
                            sink.stop();
                            *sink = new_sink;
                        }

                        // Save duration
                        *player.duration.lock() = total_duration;

                        // ファイルパスを保存（シーク時の再オープン用）
                        *player.current_path.lock() = Some(path_str.to_string());
                        *player.seek_offset.lock() = 0.0;

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

/// 現在の再生位置を取得 (秒単位)
#[no_mangle]
pub extern "C" fn nakuru_audio_get_position(player: *mut AudioPlayer) -> f64 {
    if player.is_null() {
        return 0.0;
    }
    unsafe {
        let player = &*player;
        let offset = *player.seek_offset.lock();
        let sink = player.sink.lock();
        offset + sink.get_pos().as_secs_f64()
    }
}

/// 現在の曲の総再生時間を取得 (秒単位)
#[no_mangle]
pub extern "C" fn nakuru_audio_get_duration(player: *mut AudioPlayer) -> f64 {
    if player.is_null() {
        return 0.0;
    }
    unsafe {
        let player = &*player;
        player.duration.lock().map(|d| d.as_secs_f64()).unwrap_or(0.0)
    }
}

/// 指定した位置にシーク (秒単位)
/// ファイルを再オープンしてデコーダーに同期シークした新しいSinkに差し替える
#[no_mangle]
pub extern "C" fn nakuru_audio_seek(player: *mut AudioPlayer, position_secs: f64) {
    if player.is_null() {
        return;
    }
    // NaN、無限大、負値はガード
    if position_secs.is_nan() || position_secs.is_infinite() || position_secs < 0.0 {
        return;
    }
    unsafe {
        let player = &*player;

        // 現在のファイルパスを取得
        let path = {
            let guard = player.current_path.lock();
            match guard.as_ref() {
                Some(p) => p.clone(),
                None => return,
            }
        };

        // ファイルを再オープンしてデコーダーを作成
        let file = match File::open(&path) {
            Ok(f) => f,
            Err(_) => return,
        };
        let reader = BufReader::new(file);
        let mut source = match rodio::Decoder::new(reader) {
            Ok(s) => s,
            Err(_) => return,
        };

        // デコーダーに直接シーク（同期操作 — Sink経由の非同期キューと異なり即座に完了）
        let target = std::time::Duration::from_secs_f64(position_secs);
        let _ = source.try_seek(target);

        // 現在のSinkの状態を取得
        let was_paused = {
            let state = player.state.lock();
            matches!(*state, NativeAudioPlayerState::Paused)
        };

        // 新しいSinkを作成してシーク済みソースを追加
        let new_sink = Sink::connect_new(&player._stream.mixer());
        {
            let mut sink = player.sink.lock();
            new_sink.set_volume(sink.volume());
            sink.stop();
            new_sink.append(source);
            if was_paused {
                new_sink.pause();
            } else {
                new_sink.play();
            }
            *sink = new_sink;
        }

        // シークオフセットを更新（get_position で使用）
        *player.seek_offset.lock() = position_secs;
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
