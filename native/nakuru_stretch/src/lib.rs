// Copyright (c) 2025 NakuruTool Contributors
// Licensed under the MIT License. See LICENSE file in the project root.
//
// This library provides a C FFI wrapper for the SignalsmithStretch
// time-stretching library. SignalsmithStretch is licensed under the MIT License.

use signalsmith_stretch::Stretch;
use std::panic::{catch_unwind, AssertUnwindSafe};

/// チャンク処理の基本ブロックサイズ（フレーム数）。
/// process() に渡す出力チャンクの長さ。入力チャンクは playback_rate 倍。
const BLOCK_FRAMES: usize = 1024;

/// Rust側で確保した出力バッファ。C#側で読み取り後、必ず nakuru_stretch_free で解放すること。
#[repr(C)]
#[derive(Clone, Copy)]
pub struct NakuruStretchResult {
    /// 出力データへのポインタ（インターリーブ f32）。エラー時は null。
    pub data: *mut f32,
    /// 出力の総サンプル数（= frames * channels）。エラー時は 0。
    pub len: i32,
}

/// タイムストレッチ処理（ピッチ維持・速度変更）
///
/// seek() + process() + flush() 方式で固定長音声をストレッチする。
///
/// # 処理フロー
/// 1. preset_default() で初期化、set_transpose_factor(1.0) でピッチ維持
/// 2. seek() で先頭 input_latency 分をプリロール
/// 3. process() ループ: 入力チャンク(playback_rate * BLOCK_FRAMES) → 出力チャンク(BLOCK_FRAMES)
/// 4. 入力全消化後、末尾 input_latency 分のゼロ入力を追加で process()
/// 5. flush() で残り output_latency 分を取得
///
/// # パラメータ
/// - `input_ptr`: インターリーブ f32 PCM入力
/// - `input_len`: 入力の総サンプル数（= frames * channels）
/// - `channels`: チャンネル数 (1 or 2)
/// - `sample_rate`: サンプリングレート
/// - `playback_rate`: 速度倍率 (> 0.0, 例: 1.5 = 1.5倍速)
///
/// # 戻り値
/// 成功時: data = Rust確保バッファ, len = 出力総サンプル数
/// 失敗時: data = null, len = 0
#[no_mangle]
pub extern "C" fn nakuru_stretch_process(
    input_ptr: *const f32,
    input_len: i32,
    channels: i32,
    sample_rate: i32,
    playback_rate: f64,
) -> NakuruStretchResult {
    let fail = NakuruStretchResult {
        data: std::ptr::null_mut(),
        len: 0,
    };

    let outcome = catch_unwind(AssertUnwindSafe(|| {
        if input_ptr.is_null()
            || input_len <= 0
            || channels <= 0
            || sample_rate <= 0
            || playback_rate <= 0.0
        {
            return fail;
        }

        let input_len = input_len as usize;
        let ch = channels as usize;
        let sr = sample_rate as u32;

        if !(1..=2).contains(&channels) || input_len % ch != 0 {
            return fail;
        }

        let input = unsafe { std::slice::from_raw_parts(input_ptr, input_len) };
        let input_frames = input_len / ch;

        // --- 1. 初期化 ---
        let mut stretcher = Stretch::preset_default(channels as u32, sr);
        stretcher.set_transpose_factor(1.0, None);

        let input_latency = stretcher.input_latency();
        let output_latency = stretcher.output_latency();

        // 出力フレーム数の見積もり（メインループ + flush 分）
        let main_output_frames = (input_frames as f64 / playback_rate).ceil() as usize;
        let total_output_capacity = main_output_frames + output_latency;
        let mut output = Vec::<f32>::with_capacity(total_output_capacity * ch);

        // --- 2. seek() でプリロール ---
        // 先頭 input_latency フレーム分を seek に渡す（実際の入力がそれより短い場合はその分だけ）
        let seek_frames = input_latency.min(input_frames);
        let seek_samples = seek_frames * ch;
        stretcher.seek(&input[..seek_samples], playback_rate);

        // --- 3. process() ループ ---
        // process() は入力チャンクと出力チャンクの比率でストレッチ量を決定：
        //   入力 N フレーム、出力 M フレーム → M/N の比率でストレッチ
        //   playback_rate = 1.5 なら、入力 1500 : 出力 1000
        let output_block = BLOCK_FRAMES;
        let mut input_pos: usize = 0;

        // メイン入力を消化
        while input_pos < input_frames {
            let input_block = (output_block as f64 * playback_rate).round() as usize;
            let remaining = input_frames - input_pos;
            let actual_input = input_block.min(remaining);
            // 入力が足りない場合は出力も比率に応じて縮小
            let actual_output = if actual_input < input_block {
                (actual_input as f64 / playback_rate).ceil() as usize
            } else {
                output_block
            };

            let in_start = input_pos * ch;
            let in_end = (input_pos + actual_input) * ch;
            let mut out_buf = vec![0.0f32; actual_output * ch];

            stretcher.process(&input[in_start..in_end], &mut out_buf);
            output.extend_from_slice(&out_buf);

            input_pos += actual_input;
        }

        // --- 4. 末尾 input_latency 分のゼロ入力を追加で process() ---
        // ストレッチャー内部に残っている入力をすべて押し出す
        let tail_frames = input_latency;
        let mut tail_pos: usize = 0;
        while tail_pos < tail_frames {
            let input_block = (output_block as f64 * playback_rate).round() as usize;
            let remaining = tail_frames - tail_pos;
            let actual_input = input_block.min(remaining);
            let actual_output = if actual_input < input_block {
                (actual_input as f64 / playback_rate).ceil() as usize
            } else {
                output_block
            };

            let zero_in = vec![0.0f32; actual_input * ch];
            let mut out_buf = vec![0.0f32; actual_output * ch];

            stretcher.process(&zero_in, &mut out_buf);
            output.extend_from_slice(&out_buf);

            tail_pos += actual_input;
        }

        // --- 5. flush() で残り output_latency 分を取得 ---
        let mut flush_buf = vec![0.0f32; output_latency * ch];
        stretcher.flush(&mut flush_buf);
        output.extend_from_slice(&flush_buf);

        // --- 出力を確定 ---
        // 目標フレーム数にトリミング（process/flushで若干多く出ることがある）
        let target_samples = main_output_frames * ch;
        output.truncate(target_samples);

        let boxed = output.into_boxed_slice();
        let len = boxed.len() as i32;
        let ptr = Box::into_raw(boxed) as *mut f32;

        NakuruStretchResult { data: ptr, len }
    }));

    outcome.unwrap_or(fail)
}

/// Rust側で確保したバッファを解放する。
/// nakuru_stretch_process の戻り値に対して必ず呼ぶこと。
#[no_mangle]
pub extern "C" fn nakuru_stretch_free(result: NakuruStretchResult) {
    if result.data.is_null() || result.len <= 0 {
        return;
    }
    unsafe {
        let slice = std::ptr::slice_from_raw_parts_mut(result.data, result.len as usize);
        drop(Box::from_raw(slice));
    }
}
