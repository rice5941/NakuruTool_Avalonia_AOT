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
/// mp3-duration で MP3 ファイルの再生時間を取得する
/// minimp3 の total_duration() が常に None を返すためのフォールバック
/// ファイル末尾フレームが不完全 (UnexpectedEOF) な場合も、
/// エラー構造体の at_duration から取得できた時間を返す
fn probe_duration_mp3(path: &str) -> Option<std::time::Duration> {
    let extension = std::path::Path::new(path)
        .extension()
        .and_then(|ext| ext.to_str())?;

    if !extension.eq_ignore_ascii_case("mp3") {
        return None;
    }

    match mp3_duration::from_path(path) {
        Ok(d) => Some(d),
        Err(e) if e.at_duration.as_secs() > 0 => Some(e.at_duration),
        _ => None,
    }
}

/// ファイル先頭の 4 バイトを読み取り Ogg コンテナのシグネチャ "OggS" か判定する
/// 拡張子が `.mp3` でも実体が Ogg/Vorbis のケースに対応するための補助関数
fn is_ogg_container(path: &str) -> bool {
    use std::io::Read;

    let mut file = match std::fs::File::open(path) {
        Ok(f) => f,
        Err(_) => return false,
    };
    let mut magic = [0u8; 4];
    if file.read_exact(&mut magic).is_err() {
        return false;
    }
    &magic == b"OggS"
}

/// OGG/Vorbis ファイルの再生時間を取得する
/// lewton の total_duration() が常に None を返すためのフォールバック
fn probe_duration_ogg(path: &str) -> Option<std::time::Duration> {
    use std::io::{Read, Seek, SeekFrom};

    // 拡張子が `.ogg` / `.oga` の場合はそのまま処理。それ以外は先頭シグネチャで Ogg かどうかを判定。
    // 普通の MP3 で無駄にファイル全走査しないためにシグネチャチェックを入れる。
    let extension = std::path::Path::new(path)
        .extension()
        .and_then(|ext| ext.to_str());
    let is_ogg_ext = extension
        .map(|e| e.eq_ignore_ascii_case("ogg") || e.eq_ignore_ascii_case("oga"))
        .unwrap_or(false);
    if !is_ogg_ext && !is_ogg_container(path) {
        return None;
    }

    let mut file = std::fs::File::open(path).ok()?;
    let file_len = file.seek(SeekFrom::End(0)).ok()?;

    // 末尾最大 65536 バイトから最後の OggS ページを探す
    let search_start = file_len.saturating_sub(65536);
    file.seek(SeekFrom::Start(search_start)).ok()?;
    let buf_size = (file_len - search_start) as usize;
    let mut tail_buf = vec![0u8; buf_size];
    file.read_exact(&mut tail_buf).ok()?;

    // 最後の "OggS" を探す
    let last_page_offset = tail_buf
        .windows(4)
        .enumerate()
        .rev()
        .find(|(_, w)| *w == b"OggS")
        .map(|(i, _)| i)?;

    // グラニュール位置 = OggS のオフセット + 6
    let gp_start = last_page_offset + 6;
    if gp_start + 8 > tail_buf.len() {
        return None;
    }
    let granule_pos = i64::from_le_bytes(tail_buf[gp_start..gp_start + 8].try_into().ok()?);
    if granule_pos <= 0 {
        return None;
    }

    // Vorbis ID ヘッダーからサンプリングレートを取得
    file.seek(SeekFrom::Start(0)).ok()?;
    let mut head_buf = [0u8; 8192];
    let n = file.read(&mut head_buf).ok()?;
    let head_buf = &head_buf[..n];

    let id_pos = head_buf.windows(7).position(|w| w == b"\x01vorbis")?;
    let rate_start = id_pos + 7 + 4 + 1; // \x01vorbis(7) + version(4) + channels(1)
    if rate_start + 4 > head_buf.len() {
        return None;
    }
    let sample_rate = u32::from_le_bytes(head_buf[rate_start..rate_start + 4].try_into().ok()?);
    if sample_rate == 0 {
        return None;
    }

    Some(std::time::Duration::from_secs_f64(
        granule_pos as f64 / sample_rate as f64,
    ))
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
                        // mp3-duration によるフォールバックを使用する
                        let total_duration = source
                            .total_duration()
                            .or_else(|| probe_duration_mp3(path_str))
                            .or_else(|| probe_duration_ogg(path_str));

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
        player
            .duration
            .lock()
            .map(|d| d.as_secs_f64())
            .unwrap_or(0.0)
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
        let source = match rodio::Decoder::new(reader) {
            Ok(s) => s,
            Err(_) => return,
        };

        // skip_duration でデコード済みサンプルを読み飛ばしてシーク位置まで進める
        // minimp3 の try_seek() は NotSupported を返すため、skip_duration で代替する
        let target = std::time::Duration::from_secs_f64(position_secs);
        let source = source.skip_duration(target);

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
