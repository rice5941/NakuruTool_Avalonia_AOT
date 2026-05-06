// Copyright (c) 2025 NakuruTool Contributors
// Licensed under the MIT License. See LICENSE file in the project root.
//
// This library provides a C FFI wrapper for the rodio audio playback library.
// rodio is licensed under MIT OR Apache-2.0.

use parking_lot::Mutex;
use rodio::{OutputStreamBuilder, Sink, Source};
use std::fs::File;
use std::io::{BufReader, Cursor};
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

// ─────────────────────────────────────────────────────────────────────────────
// OGG/Vorbis コメントヘッダー UTF-8 サニタイズ
//
// 一部のエンコーダー (例: 古い変換ツール) は Vorbis コメントヘッダーの vendor
// 文字列または user_comment 文字列に不正な UTF-8 を書き込むことがある。
// lewton (rodio が OGG/Vorbis 用に使用するデコーダー) は厳格に UTF-8 を検証し
// `BadHeader(Utf8DecodeError)` で再生を拒否するため、rodio の `Decoder::new`
// は `UnrecognizedFormat` を返してしまう。
//
// 本ヘルパーは:
//   1. Ogg ページを走査してパケットを再構築する。
//   2. `\x03vorbis` で始まるコメントパケットを検出し、vendor / user_comment
//      文字列内の不正な UTF-8 バイトを ASCII '?' (0x3F) に置換する。
//   3. 影響を受けたページの CRC32 を再計算する。
// 置換はバイト長を変えないため、segment_table を変更する必要はない。
// ─────────────────────────────────────────────────────────────────────────────

/// バッファ内の OGG ストリームを走査し、Vorbis コメントヘッダーの不正な UTF-8
/// を `'?'` に置換し、影響を受けたページの CRC32 を再計算する。
/// OGG でない場合や問題が無い場合はバッファを変更しない。
fn sanitize_ogg_vorbis_utf8(buf: &mut [u8]) {
    if buf.len() < 4 || &buf[0..4] != b"OggS" {
        return;
    }

    // 1. 全 Ogg ページを列挙する。
    struct PageInfo {
        page_start: usize,
        page_end: usize,
        payload_start: usize,
        segments: Vec<u8>,
    }
    let mut pages: Vec<PageInfo> = Vec::new();
    let mut pos = 0;
    while pos + 27 <= buf.len() {
        if &buf[pos..pos + 4] != b"OggS" {
            break;
        }
        let seg_count = buf[pos + 26] as usize;
        let seg_start = pos + 27;
        let payload_start = seg_start + seg_count;
        if payload_start > buf.len() {
            break;
        }
        let segments: Vec<u8> = buf[seg_start..payload_start].to_vec();
        let payload_len: usize = segments.iter().map(|&b| b as usize).sum();
        let page_end = payload_start + payload_len;
        if page_end > buf.len() {
            break;
        }
        pages.push(PageInfo {
            page_start: pos,
            page_end,
            payload_start,
            segments,
        });
        pos = page_end;
    }

    // 2. パケットの物理バイト範囲を再構築する (ページを跨ぐ可能性がある)。
    struct PacketRange {
        spans: Vec<(usize, usize)>,
        page_indices: Vec<usize>,
    }
    let mut packets: Vec<PacketRange> = Vec::new();
    let mut cur_spans: Vec<(usize, usize)> = Vec::new();
    let mut cur_pages: Vec<usize> = Vec::new();

    for (pi, page) in pages.iter().enumerate() {
        let continued = (buf[page.page_start + 5] & 0x01) != 0;
        if !continued && !cur_spans.is_empty() {
            packets.push(PacketRange {
                spans: std::mem::take(&mut cur_spans),
                page_indices: std::mem::take(&mut cur_pages),
            });
        }
        cur_pages.push(pi);

        let mut span_start = page.payload_start;
        let mut payload_pos = page.payload_start;
        for &s in &page.segments {
            payload_pos += s as usize;
            if s < 255 {
                cur_spans.push((span_start, payload_pos));
                packets.push(PacketRange {
                    spans: std::mem::take(&mut cur_spans),
                    page_indices: std::mem::take(&mut cur_pages),
                });
                cur_pages.push(pi);
                span_start = payload_pos;
            }
        }
        if span_start < payload_pos {
            cur_spans.push((span_start, payload_pos));
            // cur_pages には pi を保持したまま次ページで継続
        } else {
            // パケット境界でちょうど終わった: cur_pages から pi を取り除く
            cur_pages.pop();
        }
    }
    if !cur_spans.is_empty() {
        packets.push(PacketRange {
            spans: cur_spans,
            page_indices: cur_pages,
        });
    }

    // 3. コメントパケットを検出してサニタイズし、変更があればバッファに書き戻す。
    let mut affected_pages: Vec<usize> = Vec::new();
    for packet in &packets {
        let total_len: usize = packet.spans.iter().map(|(s, e)| e - s).sum();
        if total_len < 7 {
            continue;
        }
        // パケット先頭 7 バイトを抽出 ('\x03vorbis' か判定)
        let mut head = [0u8; 7];
        let mut filled = 0;
        for &(s, e) in &packet.spans {
            let take = (7 - filled).min(e - s);
            head[filled..filled + take].copy_from_slice(&buf[s..s + take]);
            filled += take;
            if filled == 7 {
                break;
            }
        }
        if !(head[0] == 0x03 && &head[1..7] == b"vorbis") {
            continue;
        }

        // パケット全体を読み出して解析する。
        let mut packet_data: Vec<u8> = Vec::with_capacity(total_len);
        for &(s, e) in &packet.spans {
            packet_data.extend_from_slice(&buf[s..e]);
        }

        let mut p = 7;
        let mut modified = false;

        // vendor_string
        if p + 4 > packet_data.len() {
            continue;
        }
        let vendor_len = u32::from_le_bytes([
            packet_data[p],
            packet_data[p + 1],
            packet_data[p + 2],
            packet_data[p + 3],
        ]) as usize;
        p += 4;
        if p + vendor_len > packet_data.len() {
            continue;
        }
        modified |= sanitize_utf8_in_range(&mut packet_data, p, p + vendor_len);
        p += vendor_len;

        // user_comment_list
        if p + 4 <= packet_data.len() {
            let comment_count = u32::from_le_bytes([
                packet_data[p],
                packet_data[p + 1],
                packet_data[p + 2],
                packet_data[p + 3],
            ]) as usize;
            p += 4;
            for _ in 0..comment_count {
                if p + 4 > packet_data.len() {
                    break;
                }
                let clen = u32::from_le_bytes([
                    packet_data[p],
                    packet_data[p + 1],
                    packet_data[p + 2],
                    packet_data[p + 3],
                ]) as usize;
                p += 4;
                if p + clen > packet_data.len() {
                    break;
                }
                modified |= sanitize_utf8_in_range(&mut packet_data, p, p + clen);
                p += clen;
            }
        }

        if !modified {
            continue;
        }

        // 4. 物理範囲に書き戻す。
        let mut data_pos = 0;
        for &(s, e) in &packet.spans {
            let len = e - s;
            buf[s..e].copy_from_slice(&packet_data[data_pos..data_pos + len]);
            data_pos += len;
        }
        for &pi in &packet.page_indices {
            if !affected_pages.contains(&pi) {
                affected_pages.push(pi);
            }
        }
    }

    // 5. 影響を受けたページの CRC32 を再計算する。
    for &pi in &affected_pages {
        let page = &pages[pi];
        recompute_ogg_crc(&mut buf[page.page_start..page.page_end]);
    }
}

/// `data[start..end]` を UTF-8 として検証し、不正なバイトを ASCII '?' に置換する。
/// 1 バイトでも置換した場合に `true` を返す。
fn sanitize_utf8_in_range(data: &mut [u8], start: usize, end: usize) -> bool {
    let mut modified = false;
    let mut cursor = start;
    while cursor < end {
        match str::from_utf8(&data[cursor..end]) {
            Ok(_) => break,
            Err(e) => {
                let bad = cursor + e.valid_up_to();
                data[bad] = b'?';
                modified = true;
                cursor = bad + 1;
            }
        }
    }
    modified
}

/// Ogg ページの CRC32 を再計算してヘッダーに書き込む。
/// `page` は 1 ページ全体 (ヘッダー + segment_table + payload) を含むスライス。
fn recompute_ogg_crc(page: &mut [u8]) {
    page[22] = 0;
    page[23] = 0;
    page[24] = 0;
    page[25] = 0;
    let crc = ogg_crc32(page);
    page[22..26].copy_from_slice(&crc.to_le_bytes());
}

/// Ogg 仕様の CRC32 (多項式 0x04C11DB7、初期値 0、反射なし、最終 XOR なし)。
fn ogg_crc32(data: &[u8]) -> u32 {
    let table = ogg_crc_table();
    let mut crc: u32 = 0;
    for &b in data {
        crc = (crc << 8) ^ table[((crc >> 24) as u8 ^ b) as usize];
    }
    crc
}

const fn ogg_crc_table() -> [u32; 256] {
    let mut table = [0u32; 256];
    let mut i = 0;
    while i < 256 {
        let mut r: u32 = (i as u32) << 24;
        let mut j = 0;
        while j < 8 {
            if (r & 0x80000000) != 0 {
                r = (r << 1) ^ 0x04C11DB7;
            } else {
                r <<= 1;
            }
            j += 1;
        }
        table[i] = r;
        i += 1;
    }
    table
}

/// ファイルパスから Decoder を作成する。
/// OGG コンテナの場合のみ全バイトをメモリ上に読み込み、Vorbis コメントヘッダーの
/// 不正な UTF-8 を補正してから lewton に渡す。それ以外はファイルストリームのまま。
enum AudioReader {
    File(BufReader<File>),
    Memory(Cursor<Vec<u8>>),
}

fn open_audio_reader(path: &str) -> std::io::Result<AudioReader> {
    use std::io::Read;
    if is_ogg_container(path) {
        let mut file = File::open(path)?;
        let mut buf = Vec::new();
        file.read_to_end(&mut buf)?;
        sanitize_ogg_vorbis_utf8(&mut buf);
        Ok(AudioReader::Memory(Cursor::new(buf)))
    } else {
        let file = File::open(path)?;
        Ok(AudioReader::File(BufReader::new(file)))
    }
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
        match open_audio_reader(path_str) {
            Ok(reader) => {
                let decoder_result = match reader {
                    AudioReader::File(r) => rodio::Decoder::new(r)
                        .map(|d| Box::new(d) as Box<dyn Source<Item = f32> + Send>),
                    AudioReader::Memory(r) => rodio::Decoder::new(r)
                        .map(|d| Box::new(d) as Box<dyn Source<Item = f32> + Send>),
                };
                match decoder_result {
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

        // ファイルを再オープンしてデコーダーを作成 (OGG は UTF-8 補正済みバッファ経由)
        let reader = match open_audio_reader(&path) {
            Ok(r) => r,
            Err(_) => return,
        };
        let source: Box<dyn Source<Item = f32> + Send> = match reader {
            AudioReader::File(r) => match rodio::Decoder::new(r) {
                Ok(s) => Box::new(s),
                Err(_) => return,
            },
            AudioReader::Memory(r) => match rodio::Decoder::new(r) {
                Ok(s) => Box::new(s),
                Err(_) => return,
            },
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

#[cfg(test)]
mod tests {
    use super::*;

    /// 不正な UTF-8 を含む Vorbis コメントヘッダーを持つ実ファイルを
    /// サニタイズすると不正バイトが消え、ページ CRC が更新されることを確認する。
    /// 対象ファイル: agent_tmp/.../CHRISTMAS EVE.ogg
    /// (vendor 文字列に Shift-JIS 由来と思われる不正バイト列を含む)
    #[test]
    fn sanitize_fixes_bad_utf8_in_real_file() {
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../agent_tmp/1924746 Various Artist - 14K2S Satellite 10K Convert Pt4/CHRISTMAS EVE.ogg");
        if !path.exists() {
            // テストデータが無い環境ではスキップ
            eprintln!("skip: test fixture not found: {}", path.display());
            return;
        }
        let original = std::fs::read(&path).expect("read");

        // コメントパケットは page 2 (offset 58)、payload 開始は offset 103、
        // vendor 文字列内の offset 144..156 に不正な UTF-8 が含まれることが既知。
        const COMMENT_PAYLOAD_START: usize = 103;
        // 期待される不正バイト位置 (vendor 文字列内の orphan continuation byte)
        // vendor 文字列は offset 114..158, 内容: "Xiph.Org libVorbis I 20150105 (\xE2\x9B\x81E\x9B\x84\xE2\x9B\x81E\x9B\x84)"
        // \xE2\x9B\x81 は U+26C1 (valid)、続く E (0x45) も valid、その後の \x9B\x84 は orphan continuation。
        let bad_offsets: [usize; 4] = [149, 150, 155, 156];
        for &i in &bad_offsets {
            assert_ne!(original[i], b'?', "fixture must contain non-'?' byte at {}", i);
        }
        // サニタイズ前は vendor 文字列全体として UTF-8 不正
        let vendor_len_offset = COMMENT_PAYLOAD_START + 7; // '\x03vorbis'(7)
        let vendor_len = u32::from_le_bytes([
            original[vendor_len_offset],
            original[vendor_len_offset + 1],
            original[vendor_len_offset + 2],
            original[vendor_len_offset + 3],
        ]) as usize;
        let vendor_start = vendor_len_offset + 4;
        let vendor_end = vendor_start + vendor_len;
        assert!(
            std::str::from_utf8(&original[vendor_start..vendor_end]).is_err(),
            "fixture must initially have invalid UTF-8 in vendor (else this test is meaningless)"
        );

        // サニタイズ後は valid UTF-8 になる
        let mut buf = original.clone();
        sanitize_ogg_vorbis_utf8(&mut buf);
        let _ = std::str::from_utf8(&buf[vendor_start..vendor_end])
            .expect("vendor string must be valid UTF-8 after sanitization");

        // 不正バイト位置は '?' に置換されている
        for &i in &bad_offsets {
            assert_eq!(buf[i], b'?', "byte at {} should be replaced with '?'", i);
        }

        // ファイルサイズは変わらない
        assert_eq!(buf.len(), original.len());

        // 影響を受けた page (page 2) の CRC は再計算され、整合性が保たれる
        // page 2 開始 offset は 58。再計算後の CRC (offset 58+22..58+26) は
        // payload 領域 (offset 58+22..58+26 を 0 にした全ページ) から導出可能。
        let page2_start = 58usize;
        // 末尾 offset を計算
        let seg_count = buf[page2_start + 26] as usize;
        let payload_start = page2_start + 27 + seg_count;
        let payload_len: usize = buf[page2_start + 27..payload_start]
            .iter()
            .map(|&b| b as usize)
            .sum();
        let page2_end = payload_start + payload_len;
        let mut page = buf[page2_start..page2_end].to_vec();
        let stored_crc = u32::from_le_bytes([page[22], page[23], page[24], page[25]]);
        page[22..26].fill(0);
        let computed_crc = ogg_crc32(&page);
        assert_eq!(
            stored_crc, computed_crc,
            "page 2 CRC must match recomputed CRC after sanitization"
        );

        // サニタイズ前のファイルは vendor の不正 UTF-8 で CRC は変わっていないが、
        // バイト書き換え後に CRC を更新しないと OGG パーサが破損ストリームと判定する。
        // ここでは「サニタイズ後の CRC が正しい」ことが本質。
    }

    #[test]
    fn sanitize_is_noop_for_non_ogg_buffer() {
        let mut buf = b"NOT_OGG_AT_ALL".to_vec();
        let snapshot = buf.clone();
        sanitize_ogg_vorbis_utf8(&mut buf);
        assert_eq!(buf, snapshot);
    }

    #[test]
    fn ogg_crc32_matches_known_vector() {
        // CRC-32/MPEG-2 (Ogg と同じ多項式 0x04C11DB7・反射なし・XOR なし) の標準ベクター。
        // "123456789" → 0x89A1897F
        assert_eq!(ogg_crc32(b"123456789"), 0x89A1897F);
    }

    #[test]
    fn sanitize_utf8_in_range_replaces_orphan_continuations() {
        // 0x9B は単独では invalid な continuation byte
        let mut data = b"abc\x9Bdef\xE2\x9B\x81xyz".to_vec();
        let len = data.len();
        let modified = sanitize_utf8_in_range(&mut data, 0, len);
        assert!(modified);
        assert!(std::str::from_utf8(&data).is_ok());
        // 0x9B (offset 3) は '?' に置換、E2 9B 81 (offset 7..10) は維持
        assert_eq!(data[3], b'?');
        assert_eq!(&data[7..10], &[0xE2, 0x9B, 0x81]);
    }
}

