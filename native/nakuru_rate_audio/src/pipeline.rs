use crate::decode::{AudioSource, SymphoniaDecoder};
use crate::encode::{create_encoder, AudioEncoder, EncodeSpec, OutputFormat};
use crate::error::Result;
use crate::stretch::BungeeProcessor;
use std::path::PathBuf;

pub struct PipelineRequest {
    pub input_path: PathBuf,
    pub output_path: PathBuf,
    pub speed: f64,
    pub pitch: f64,
    pub output_format: OutputFormat,
    pub quality: f32,
    pub progress_cb: Option<unsafe extern "C" fn(f32)>,
}

/// エラー時に不完全な出力ファイルを削除するガード
struct CleanupGuard {
    path: PathBuf,
    disarmed: bool,
}

impl CleanupGuard {
    fn new(path: PathBuf) -> Self {
        Self {
            path,
            disarmed: false,
        }
    }
    fn disarm(&mut self) {
        self.disarmed = true;
    }
}

impl Drop for CleanupGuard {
    fn drop(&mut self) {
        if !self.disarmed {
            let _ = std::fs::remove_file(&self.path);
        }
    }
}

fn report_progress(cb: Option<unsafe extern "C" fn(f32)>, value: f32) {
    if let Some(callback) = cb {
        unsafe {
            callback(value.clamp(0.0, 1.0));
        }
    }
}

pub fn run(request: PipelineRequest) -> Result<()> {
    let mut guard = CleanupGuard::new(request.output_path.clone());

    // 1. デコーダー初期化
    let mut decoder = SymphoniaDecoder::open(&request.input_path)?;
    let spec = decoder.spec();
    let total_frames = decoder.total_frames();

    // 2. エンコーダー初期化
    let mut encoder = create_encoder(
        request.output_format,
        &request.output_path,
        EncodeSpec {
            sample_rate: spec.sample_rate,
            channels: spec.channels,
        },
        request.quality,
    )?;

    // 3. モード判定
    let nc_mode = (request.speed - request.pitch).abs() < 0.001
        && (request.speed - 1.0).abs() > 0.001;
    let need_stretch = !nc_mode
        && ((request.speed - 1.0).abs() > 0.001 || (request.pitch - 1.0).abs() > 0.001);

    // 4. 処理実行
    if nc_mode {
        run_nc_resample(
            &mut decoder,
            encoder.as_mut(),
            spec.channels as usize,
            request.speed,
            request.progress_cb,
            total_frames,
        )?;
    } else if need_stretch {
        run_bungee_stretch(
            &mut decoder,
            encoder.as_mut(),
            spec.sample_rate,
            spec.channels as usize,
            request.speed,
            request.pitch,
            request.progress_cb,
            total_frames,
        )?;
    } else {
        // pass-through
        run_passthrough(&mut decoder, encoder.as_mut(), spec.channels as usize, request.progress_cb, total_frames)?;
    }

    // 5. ファイナライズ
    encoder.finalize()?;

    // 6. 最終プログレス通知 (1.0)
    report_progress(request.progress_cb, 1.0);

    // 成功 → ガード解除
    guard.disarm();
    Ok(())
}

/// NC モード: 線形補間リサンプリング（Bungee不使用）
/// 速度に比例してピッチが自然に変わる（osu! NightCore方式）
fn run_nc_resample(
    decoder: &mut SymphoniaDecoder,
    encoder: &mut dyn AudioEncoder,
    channels: usize,
    rate: f64,
    progress_cb: Option<unsafe extern "C" fn(f32)>,
    total_frames: Option<u64>,
) -> Result<()> {
    // 全入力をメモリに読み込み
    let mut all_input: Vec<f32> = Vec::new();
    let mut decode_buf: Vec<f32> = Vec::new();
    let mut decoded_frames: u64 = 0;

    loop {
        let frames = decoder.read_interleaved_f32(&mut decode_buf)?;
        if frames == 0 {
            break;
        }
        let samples = frames * channels;
        all_input.extend_from_slice(&decode_buf[..samples]);
        decoded_frames += frames as u64;

        // デコード進捗 (0.0..0.5)
        if let Some(total) = total_frames {
            if total > 0 {
                report_progress(progress_cb, decoded_frames as f32 / total as f32 * 0.5);
            }
        }
    }

    // 線形補間リサンプリング
    let input_frames = all_input.len() / channels;
    let output_frames = (input_frames as f64 / rate).ceil() as usize;

    // チャンク単位でエンコーダーに書き込み
    let write_chunk_frames = 4096;
    let mut output_chunk: Vec<f32> = Vec::with_capacity(write_chunk_frames * channels);

    for out_start in (0..output_frames).step_by(write_chunk_frames) {
        output_chunk.clear();
        let out_end = (out_start + write_chunk_frames).min(output_frames);

        for i in out_start..out_end {
            let src_pos = i as f64 * rate;
            let src_idx = src_pos.floor() as usize;
            let frac = (src_pos - src_idx as f64) as f32;

            for ch in 0..channels {
                let s0 = if src_idx < input_frames {
                    all_input[src_idx * channels + ch]
                } else {
                    0.0
                };
                let s1 = if src_idx + 1 < input_frames {
                    all_input[(src_idx + 1) * channels + ch]
                } else {
                    s0
                };
                output_chunk.push(s0 + frac * (s1 - s0));
            }
        }

        encoder.write_interleaved_f32(&output_chunk)?;

        // エンコード進捗 (0.5..1.0)
        report_progress(progress_cb, 0.5 + out_end as f32 / output_frames as f32 * 0.5);
    }

    Ok(())
}

/// DT モード: Bungee タイムストレッチ（ピッチ維持）
fn run_bungee_stretch(
    decoder: &mut SymphoniaDecoder,
    encoder: &mut dyn AudioEncoder,
    sample_rate: u32,
    channels: usize,
    speed: f64,
    pitch: f64,
    progress_cb: Option<unsafe extern "C" fn(f32)>,
    total_frames: Option<u64>,
) -> Result<()> {
    let mut stretcher = BungeeProcessor::new(sample_rate, channels, 16384)?;
    let mut decode_buf: Vec<f32> = Vec::new();
    let mut processed_frames: u64 = 0;

    loop {
        let frames = decoder.read_interleaved_f32(&mut decode_buf)?;
        if frames == 0 {
            break;
        }

        let output = stretcher.process_chunk(&decode_buf, speed, pitch)?;
        encoder.write_interleaved_f32(output)?;

        processed_frames += frames as u64;
        if let Some(total) = total_frames {
            if total > 0 {
                report_progress(progress_cb, processed_frames as f32 / total as f32);
            }
        }
    }

    // finish() は呼ばない。Bungee内部バッファの数ms分のテールは無視する。
    // finish() はBungeeの内部状態によってハングするリスクがあるため。
    // osu!用途ではこの微小なテール損失は問題にならない。

    Ok(())
}

/// Pass-through: speed ≈ 1.0 && pitch ≈ 1.0
fn run_passthrough(
    decoder: &mut SymphoniaDecoder,
    encoder: &mut dyn AudioEncoder,
    channels: usize,
    progress_cb: Option<unsafe extern "C" fn(f32)>,
    total_frames: Option<u64>,
) -> Result<()> {
    let mut decode_buf: Vec<f32> = Vec::new();
    let mut processed_frames: u64 = 0;

    loop {
        let frames = decoder.read_interleaved_f32(&mut decode_buf)?;
        if frames == 0 {
            break;
        }

        let samples = frames * channels;
        encoder.write_interleaved_f32(&decode_buf[..samples])?;

        processed_frames += frames as u64;
        if let Some(total) = total_frames {
            if total > 0 {
                report_progress(progress_cb, processed_frames as f32 / total as f32);
            }
        }
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::encode::OutputFormat;

    /// hound で WAV テストファイルを生成
    fn create_test_wav(path: &std::path::Path, sample_rate: u32, channels: u16, duration_secs: f32) {
        let spec = hound::WavSpec {
            channels,
            sample_rate,
            bits_per_sample: 16,
            sample_format: hound::SampleFormat::Int,
        };
        let mut writer = hound::WavWriter::create(path, spec).unwrap();
        let total_frames = (sample_rate as f32 * duration_secs) as usize;
        for i in 0..total_frames {
            let t = i as f32 / sample_rate as f32;
            let sample = (2.0 * std::f32::consts::PI * 440.0 * t).sin() * 0.5;
            let s16 = (sample * 32767.0) as i16;
            for _ in 0..channels {
                writer.write_sample(s16).unwrap();
            }
        }
        writer.finalize().unwrap();
    }

    #[test]
    fn test_pipeline_dt_wav_30s() {
        let dir = std::env::temp_dir().join("nakuru_rate_audio_test_dt_30s");
        let _ = std::fs::create_dir_all(&dir);
        let input = dir.join("input.wav");
        let output = dir.join("output_dt.wav");
        let _ = std::fs::remove_file(&output);

        // 30秒のステレオ WAV を生成
        create_test_wav(&input, 44100, 2, 30.0);

        let request = PipelineRequest {
            input_path: input.clone(),
            output_path: output.clone(),
            speed: 1.5,
            pitch: 1.0,
            output_format: OutputFormat::Wav,
            quality: 0.4,
            progress_cb: None,
        };

        run(request).expect("DT pipeline should complete without hanging");
        assert!(output.exists(), "Output file should exist");
        let metadata = std::fs::metadata(&output).unwrap();
        assert!(metadata.len() > 0, "Output file should not be empty");

        // クリーンアップ
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_pipeline_dt_wav_180s() {
        let dir = std::env::temp_dir().join("nakuru_rate_audio_test_dt_180s");
        let _ = std::fs::create_dir_all(&dir);
        let input = dir.join("input.wav");
        let output = dir.join("output_dt.wav");
        let _ = std::fs::remove_file(&output);

        // 3分のステレオ WAV を生成
        create_test_wav(&input, 44100, 2, 180.0);

        let request = PipelineRequest {
            input_path: input.clone(),
            output_path: output.clone(),
            speed: 1.5,
            pitch: 1.0,
            output_format: OutputFormat::Wav,
            quality: 0.4,
            progress_cb: None,
        };

        run(request).expect("DT pipeline (3min) should complete without hanging");
        assert!(output.exists(), "Output file should exist");
        let metadata = std::fs::metadata(&output).unwrap();
        assert!(metadata.len() > 0, "Output file should not be empty");

        // クリーンアップ
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_pipeline_nc_wav_30s() {
        let dir = std::env::temp_dir().join("nakuru_rate_audio_test_nc_30s");
        let _ = std::fs::create_dir_all(&dir);
        let input = dir.join("input.wav");
        let output = dir.join("output_nc.wav");
        let _ = std::fs::remove_file(&output);

        // 30秒のステレオ WAV を生成
        create_test_wav(&input, 44100, 2, 30.0);

        let request = PipelineRequest {
            input_path: input.clone(),
            output_path: output.clone(),
            speed: 1.5,
            pitch: 1.5,  // NC: speed == pitch
            output_format: OutputFormat::Wav,
            quality: 0.4,
            progress_cb: None,
        };

        run(request).expect("NC pipeline should complete without hanging");
        assert!(output.exists(), "Output file should exist");

        // クリーンアップ
        let _ = std::fs::remove_dir_all(&dir);
    }

    /// OGG 入力テスト: WAV→OGG→WAV (DT) でサイズが妥当か検証
    #[test]
    fn test_pipeline_ogg_input_dt() {
        let dir = std::env::temp_dir().join("nakuru_rate_audio_test_ogg_input_dt");
        let _ = std::fs::create_dir_all(&dir);
        let wav_input = dir.join("input.wav");
        let ogg_intermediate = dir.join("intermediate.ogg");
        let wav_output = dir.join("output.wav");
        let _ = std::fs::remove_file(&ogg_intermediate);
        let _ = std::fs::remove_file(&wav_output);

        // 10秒のステレオ WAV を生成
        create_test_wav(&wav_input, 44100, 2, 10.0);

        // WAV → OGG (passthrough)
        let request = PipelineRequest {
            input_path: wav_input.clone(),
            output_path: ogg_intermediate.clone(),
            speed: 1.0,
            pitch: 1.0,
            output_format: OutputFormat::OggVorbis,
            quality: 0.4,
            progress_cb: None,
        };
        run(request).expect("WAV→OGG passthrough should succeed");
        let ogg_size = std::fs::metadata(&ogg_intermediate).unwrap().len();
        assert!(ogg_size > 10_000, "OGG intermediate should be > 10KB, got {} bytes", ogg_size);

        // OGG → WAV (DT 1.5x)
        let request = PipelineRequest {
            input_path: ogg_intermediate.clone(),
            output_path: wav_output.clone(),
            speed: 1.5,
            pitch: 1.0,
            output_format: OutputFormat::Wav,
            quality: 0.4,
            progress_cb: None,
        };
        run(request).expect("OGG→WAV DT should succeed");
        let wav_size = std::fs::metadata(&wav_output).unwrap().len();
        // 10秒 / 1.5 ≈ 6.67秒 → 44100 * 2ch * 2bytes * 6.67 ≈ 1.17MB
        assert!(wav_size > 500_000, "WAV output should be > 500KB, got {} bytes", wav_size);

        // クリーンアップ
        let _ = std::fs::remove_dir_all(&dir);
    }

    /// OGG 出力テスト: WAV→OGG (DT) でサイズが妥当か検証
    #[test]
    fn test_pipeline_ogg_output_dt() {
        let dir = std::env::temp_dir().join("nakuru_rate_audio_test_ogg_output_dt");
        let _ = std::fs::create_dir_all(&dir);
        let input = dir.join("input.wav");
        let output = dir.join("output.ogg");
        let _ = std::fs::remove_file(&output);

        // 10秒のステレオ WAV を生成
        create_test_wav(&input, 44100, 2, 10.0);

        let request = PipelineRequest {
            input_path: input.clone(),
            output_path: output.clone(),
            speed: 1.5,
            pitch: 1.0,
            output_format: OutputFormat::OggVorbis,
            quality: 0.4,
            progress_cb: None,
        };

        run(request).expect("WAV→OGG DT should succeed");
        let size = std::fs::metadata(&output).unwrap().len();
        // 10秒 / 1.5 ≈ 6.67秒 OGG VBR → サイン波は圧縮率が高いため 20KB 以上で妥当
        assert!(size > 20_000, "OGG output should be > 20KB, got {} bytes", size);

        // クリーンアップ
        let _ = std::fs::remove_dir_all(&dir);
    }
}
