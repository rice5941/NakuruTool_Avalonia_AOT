use super::bungee_sys;
use crate::error::{RateAudioError, Result};
use std::ptr;

pub struct BungeeProcessor {
    handle: *mut bungee_sys::nakuru_bungee_stream_handle,
    channels: usize,
    max_input_frames: usize,
    /// Planar scratch buffers for deinterleaved input
    input_planar: Vec<Vec<f32>>,
    /// Planar scratch buffers for deinterleaved output
    output_planar: Vec<Vec<f32>>,
    /// Interleaved output scratch buffer
    interleaved_out: Vec<f32>,
}

// SAFETY: The C handle is not shared across threads; BungeeProcessor
// is !Sync by default (no &self concurrent access).  Send is safe
// because ownership is transferred wholesale.
unsafe impl Send for BungeeProcessor {}

impl BungeeProcessor {
    pub fn new(sample_rate: u32, channels: usize, max_input_frames: usize) -> Result<Self> {
        if sample_rate == 0 {
            return Err(RateAudioError::InvalidArgument(
                "sample_rate must be > 0".into(),
            ));
        }
        if channels == 0 {
            return Err(RateAudioError::InvalidArgument(
                "channels must be > 0".into(),
            ));
        }
        if max_input_frames == 0 {
            return Err(RateAudioError::InvalidArgument(
                "max_input_frames must be > 0".into(),
            ));
        }

        let sample_rate = to_i32(sample_rate as usize, "sample_rate")?;
        let channels_i32 = to_i32(channels, "channels")?;
        let max_input_frames_i32 = to_i32(max_input_frames, "max_input_frames")?;

        let handle = unsafe {
            bungee_sys::nakuru_bungee_stream_create(
                sample_rate,
                sample_rate,
                channels_i32,
                max_input_frames_i32,
            )
        };

        if handle.is_null() {
            return Err(RateAudioError::Stretch(
                "Failed to create Bungee stream".into(),
            ));
        }

        let output_capacity = max_input_frames * 3;

        Ok(Self {
            handle,
            channels,
            max_input_frames,
            input_planar: vec![Vec::with_capacity(max_input_frames); channels],
            output_planar: vec![Vec::with_capacity(output_capacity); channels],
            interleaved_out: Vec::with_capacity(output_capacity * channels),
        })
    }

    /// Interleaved f32 チャンクを処理し、結果を interleaved f32 で返す
    pub fn process_chunk(
        &mut self,
        interleaved_input: &[f32],
        speed: f64,
        pitch: f64,
    ) -> Result<&[f32]> {
        validate_ratio(speed, "speed")?;
        validate_ratio(pitch, "pitch")?;

        let input_frames = interleaved_input.len() / self.channels;
        if interleaved_input.len() != input_frames * self.channels {
            return Err(RateAudioError::InvalidArgument(
                "input length is not a multiple of channels".into(),
            ));
        }

        // Deinterleave input
        deinterleave(interleaved_input, self.channels, &mut self.input_planar);

        // Calculate output capacity with headroom
        let estimated_output_frames = (input_frames as f64 / speed).ceil() as usize;
        let output_capacity = estimated_output_frames
            .max(input_frames)
            .saturating_add(self.max_input_frames)
            .max(256);
        let input_frames_i32 = to_i32(input_frames, "input_frames")?;
        let output_capacity_i32 = to_i32(output_capacity, "output_capacity")?;

        // Resize output planar buffers
        for ch_buf in &mut self.output_planar {
            ch_buf.resize(output_capacity, 0.0);
        }

        // Build pointer arrays for C API
        let input_ptrs: Vec<*const f32> = self.input_planar.iter().map(|v| v.as_ptr()).collect();
        let output_ptrs: Vec<*mut f32> = self
            .output_planar
            .iter_mut()
            .map(|v| v.as_mut_ptr())
            .collect();

        let output_frames = unsafe {
            bungee_sys::nakuru_bungee_stream_process(
                self.handle,
                input_ptrs.as_ptr(),
                input_frames_i32,
                output_ptrs.as_ptr(),
                output_capacity_i32,
                speed,
                pitch,
            )
        };

        if output_frames < 0 {
            return Err(RateAudioError::Stretch(format!(
                "Bungee process returned error code {}",
                output_frames
            )));
        }

        let output_frames = output_frames as usize;

        // Interleave output
        self.interleaved_out.clear();
        append_interleaved(
            &self.output_planar,
            self.channels,
            output_frames,
            &mut self.interleaved_out,
        );

        Ok(&self.interleaved_out[..output_frames * self.channels])
    }

    /// 末尾の内部遅延を吐き出す（単一呼び出し）
    #[allow(dead_code)]
    pub fn finish(&mut self, speed: f64, pitch: f64) -> Result<&[f32]> {
        validate_ratio(speed, "speed")?;
        validate_ratio(pitch, "pitch")?;

        let output_capacity = self.max_input_frames;
        let output_capacity_i32 = to_i32(output_capacity, "output_capacity")?;
        self.interleaved_out.clear();

        for ch_buf in &mut self.output_planar {
            ch_buf.resize(output_capacity, 0.0);
        }

        let output_ptrs: Vec<*mut f32> = self
            .output_planar
            .iter_mut()
            .map(|v| v.as_mut_ptr())
            .collect();

        let output_frames = unsafe {
            bungee_sys::nakuru_bungee_stream_finish(
                self.handle,
                output_ptrs.as_ptr(),
                output_capacity_i32,
                speed,
                pitch,
            )
        };

        if output_frames < 0 {
            return Err(RateAudioError::Stretch(format!(
                "Bungee finish returned error code {}",
                output_frames
            )));
        }

        let output_frames = output_frames as usize;
        if output_frames > 0 {
            append_interleaved(
                &self.output_planar,
                self.channels,
                output_frames,
                &mut self.interleaved_out,
            );
        }

        Ok(&self.interleaved_out)
    }
}

impl Drop for BungeeProcessor {
    fn drop(&mut self) {
        if !self.handle.is_null() {
            unsafe {
                bungee_sys::nakuru_bungee_stream_destroy(self.handle);
            }
            self.handle = ptr::null_mut();
        }
    }
}

/// Deinterleave: interleaved [L0,R0,L1,R1,...] → planar [[L0,L1,...],[R0,R1,...]]
fn deinterleave(interleaved: &[f32], channels: usize, planar: &mut [Vec<f32>]) {
    let frames = interleaved.len() / channels;
    for ch_buf in planar.iter_mut() {
        ch_buf.clear();
        ch_buf.reserve(frames);
    }
    for frame in 0..frames {
        for ch in 0..channels {
            planar[ch].push(interleaved[frame * channels + ch]);
        }
    }
}

/// Interleave: planar [[L0,L1,...],[R0,R1,...]] → interleaved [L0,R0,L1,R1,...]
fn append_interleaved(planar: &[Vec<f32>], channels: usize, frames: usize, output: &mut Vec<f32>) {
    output.reserve(frames * channels);
    for frame in 0..frames {
        for ch in 0..channels {
            output.push(planar[ch][frame]);
        }
    }
}

fn validate_ratio(value: f64, name: &str) -> Result<()> {
    if value.is_finite() && value > 0.0 {
        Ok(())
    } else {
        Err(RateAudioError::InvalidArgument(format!(
            "{} must be > 0 and finite",
            name
        )))
    }
}

fn to_i32(value: usize, name: &str) -> Result<i32> {
    i32::try_from(value)
        .map_err(|_| RateAudioError::InvalidArgument(format!("{} exceeds i32 range", name)))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn generate_sine(sample_rate: u32, channels: usize, duration_secs: f32, freq: f32) -> Vec<f32> {
        let frames = (sample_rate as f32 * duration_secs) as usize;
        let mut output = Vec::with_capacity(frames * channels);
        for i in 0..frames {
            let t = i as f32 / sample_rate as f32;
            let sample = (2.0 * std::f32::consts::PI * freq * t).sin() * 0.5;
            for _ in 0..channels {
                output.push(sample);
            }
        }
        output
    }

    #[test]
    fn test_process_chunk_does_not_hang() {
        let sample_rate = 44100;
        let channels = 2;
        let mut proc = BungeeProcessor::new(sample_rate, channels, 16384).unwrap();

        // 1秒の440Hzサイン波
        let input = generate_sine(sample_rate, channels, 1.0, 440.0);

        // 1152フレームずつ（MP3パケットサイズ）処理
        let chunk_size = 1152 * channels;
        for chunk in input.chunks(chunk_size) {
            let output = proc.process_chunk(chunk, 1.1, 1.0).unwrap();
            assert!(
                !output.is_empty(),
                "process_chunk should produce output for non-empty input"
            );
        }
    }

    #[test]
    fn test_finish_does_not_hang() {
        let sample_rate = 44100;
        let channels = 2;
        let mut proc = BungeeProcessor::new(sample_rate, channels, 16384).unwrap();

        let input = generate_sine(sample_rate, channels, 0.5, 440.0);
        let chunk_size = 1152 * channels;
        for chunk in input.chunks(chunk_size) {
            let _ = proc.process_chunk(chunk, 1.1, 1.0).unwrap();
        }

        // finish should complete without hanging
        let tail = proc.finish(1.1, 1.0).unwrap();
        // tail may be empty or contain some frames - both are valid
        assert!(tail.len() <= 16384 * channels);
    }

    #[test]
    fn test_nc_mode_does_not_hang() {
        let sample_rate = 44100;
        let channels = 2;
        let mut proc = BungeeProcessor::new(sample_rate, channels, 16384).unwrap();

        let input = generate_sine(sample_rate, channels, 0.5, 440.0);
        let chunk_size = 1152 * channels;
        for chunk in input.chunks(chunk_size) {
            // NC mode: speed == pitch
            let _ = proc.process_chunk(chunk, 1.5, 1.5).unwrap();
        }

        let tail = proc.finish(1.5, 1.5).unwrap();
        assert!(tail.len() <= 16384 * channels);
    }

    /// 実際の osu! 楽曲を想定した長尺テスト（3分 × 1.5倍速）
    /// framesNeeded 蓄積バグ（+64 overshoot）が存在すると最終チャンクでハングする
    #[test]
    fn test_long_file_does_not_hang() {
        let sample_rate = 44100;
        let channels = 2;
        let mut proc = BungeeProcessor::new(sample_rate, channels, 16384).unwrap();

        // 3分間の音声（osu! の一般的な曲の長さ）
        let input = generate_sine(sample_rate, channels, 180.0, 440.0);

        let chunk_size = 1152 * channels; // MP3 パケットサイズ
        for chunk in input.chunks(chunk_size) {
            let _ = proc.process_chunk(chunk, 1.5, 1.0).unwrap();
        }
        // ここまで到達できればハングしていない
    }
}
