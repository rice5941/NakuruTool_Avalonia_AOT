use super::lame::{LameGlobalFlags, LameLibrary};
use std::fs::File;
use std::io::{BufWriter, Seek, SeekFrom, Write};
use std::path::Path;

use super::{AudioEncoder, EncodeSpec};
use crate::error::{RateAudioError, Result};

pub struct Mp3Encoder {
    gfp: *mut LameGlobalFlags,
    writer: BufWriter<File>,
    encode_buffer: Vec<u8>,
    channels: usize,
    write_vbr_tag: bool,
    closed: bool,
}

// Safety: Mp3Encoder は単一スレッドからのみ使用され、
// gfp ポインタは LAME ライブラリ内部で管理される
unsafe impl Send for Mp3Encoder {}

impl Mp3Encoder {
    pub fn new(path: &Path, spec: EncodeSpec, quality: f32) -> Result<Self> {
        let channels = usize::from(spec.channels);
        if !(1..=2).contains(&channels) {
            return Err(RateAudioError::Encode(format!(
                "MP3 supports only mono/stereo input, got {} channels",
                spec.channels
            )));
        }

        let lame = LameLibrary::get_or_load()?;
        let api = &lame.api;

        let gfp = unsafe { (api.lame_init)() };
        if gfp.is_null() {
            return Err(RateAudioError::Encode("lame_init returned null".into()));
        }

        let init_result = unsafe {
            check_lame_code(
                "lame_set_num_channels",
                (api.lame_set_num_channels)(gfp, i32::from(spec.channels)),
            )?;
            check_lame_code(
                "lame_set_in_samplerate",
                (api.lame_set_in_samplerate)(
                    gfp,
                    to_i32(spec.sample_rate as usize, "sample_rate")?,
                ),
            )?;
            check_lame_code("lame_set_quality", (api.lame_set_quality)(gfp, 2))?;
            check_lame_code("lame_set_VBR", (api.lame_set_VBR)(gfp, 4))?;
            check_lame_code(
                "lame_set_VBR_quality",
                (api.lame_set_VBR_quality)(gfp, quality.clamp(0.0, 9.0)),
            )?;
            check_lame_code("lame_set_bWriteVbrTag", (api.lame_set_bWriteVbrTag)(gfp, 1))?;
            check_lame_code("lame_init_params", (api.lame_init_params)(gfp))
        };
        if let Err(err) = init_result {
            unsafe {
                (api.lame_close)(gfp);
            }
            return Err(err);
        }

        let writer = BufWriter::new(File::create(path).map_err(|e| {
            unsafe {
                (api.lame_close)(gfp);
            }
            RateAudioError::Encode(format!("Failed to create MP3 file: {}", e))
        })?);

        Ok(Self {
            gfp,
            writer,
            encode_buffer: vec![0u8; 128 * 1024],
            channels,
            write_vbr_tag: true,
            closed: false,
        })
    }

    fn close_lame(&mut self) {
        if !self.closed && !self.gfp.is_null() {
            if let Ok(lame) = LameLibrary::get_or_load() {
                unsafe {
                    (lame.api.lame_close)(self.gfp);
                }
            }
            self.gfp = std::ptr::null_mut();
            self.closed = true;
        }
    }
}

impl AudioEncoder for Mp3Encoder {
    fn write_interleaved_f32(&mut self, pcm: &[f32]) -> Result<()> {
        let lame = LameLibrary::get_or_load()?;
        let api = &lame.api;

        if pcm.len() % self.channels != 0 {
            return Err(RateAudioError::Encode(
                "PCM length is not a multiple of channel count".into(),
            ));
        }

        let frames = pcm.len() / self.channels;
        if frames == 0 {
            return Ok(());
        }

        // 出力バッファのサイズ: 1.25 * frames + 7200 (LAME 推奨)
        let required = (1.25 * frames as f64) as usize + 7200;
        if self.encode_buffer.len() < required {
            self.encode_buffer.resize(required, 0);
        }
        let frames_i32 = to_i32(frames, "frames")?;
        let buffer_len_i32 = to_i32(self.encode_buffer.len(), "encode_buffer length")?;

        let bytes_written = unsafe {
            (api.lame_encode_buffer_interleaved_ieee_float)(
                self.gfp,
                pcm.as_ptr(),
                frames_i32,
                self.encode_buffer.as_mut_ptr(),
                buffer_len_i32,
            )
        };

        if bytes_written < 0 {
            return Err(RateAudioError::Encode(format!(
                "lame_encode_buffer_interleaved_ieee_float failed with code {}",
                bytes_written
            )));
        }

        if bytes_written > 0 {
            self.writer
                .write_all(&self.encode_buffer[..bytes_written as usize])
                .map_err(|e| RateAudioError::Encode(format!("MP3 write failed: {}", e)))?;
        }

        Ok(())
    }

    fn finalize(mut self: Box<Self>) -> Result<()> {
        let lame = LameLibrary::get_or_load()?;
        let api = &lame.api;

        // 残りデータを flush
        let flush_bytes = unsafe {
            (api.lame_encode_flush)(
                self.gfp,
                self.encode_buffer.as_mut_ptr(),
                to_i32(self.encode_buffer.len(), "encode_buffer length")?,
            )
        };

        if flush_bytes < 0 {
            // Drop が lame_close を呼ぶ
            return Err(RateAudioError::Encode(format!(
                "lame_encode_flush failed with code {}",
                flush_bytes
            )));
        }

        if flush_bytes > 0 {
            self.writer
                .write_all(&self.encode_buffer[..flush_bytes as usize])
                .map_err(|e| RateAudioError::Encode(format!("MP3 flush write failed: {}", e)))?;
        }

        self.writer
            .flush()
            .map_err(|e| RateAudioError::Encode(format!("MP3 flush failed: {}", e)))?;

        // VBR タグ書き戻し
        if self.write_vbr_tag {
            let mut tag_buf = vec![0u8; 2880]; // LAME VBR tag max size
            let tag_size = unsafe {
                (api.lame_get_lametag_frame)(
                    self.gfp as *const _,
                    tag_buf.as_mut_ptr(),
                    tag_buf.len(),
                )
            };

            if tag_size > 0 && tag_size <= tag_buf.len() {
                self.writer.seek(SeekFrom::Start(0)).map_err(|e| {
                    RateAudioError::Encode(format!("Failed to seek for VBR tag: {}", e))
                })?;
                self.writer.write_all(&tag_buf[..tag_size]).map_err(|e| {
                    RateAudioError::Encode(format!("Failed to write VBR tag: {}", e))
                })?;
                self.writer.flush().map_err(|e| {
                    RateAudioError::Encode(format!("Failed to flush VBR tag: {}", e))
                })?;
            }
        }

        self.close_lame();
        Ok(())
    }
}

impl Drop for Mp3Encoder {
    fn drop(&mut self) {
        self.close_lame();
    }
}

fn check_lame_code(operation: &str, code: i32) -> Result<()> {
    if code < 0 {
        Err(RateAudioError::Encode(format!(
            "{} failed with code {}",
            operation, code
        )))
    } else {
        Ok(())
    }
}

fn to_i32(value: usize, name: &str) -> Result<i32> {
    i32::try_from(value).map_err(|_| RateAudioError::Encode(format!("{} exceeds i32 range", name)))
}
