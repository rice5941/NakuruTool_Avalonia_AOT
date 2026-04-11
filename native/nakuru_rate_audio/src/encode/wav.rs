use hound::{SampleFormat, WavSpec, WavWriter};
use std::fs::File;
use std::io::BufWriter;
use std::path::Path;

use super::{AudioEncoder, EncodeSpec};
use crate::error::{RateAudioError, Result};

pub struct WavEncoder {
    writer: WavWriter<BufWriter<File>>,
}

impl WavEncoder {
    pub fn new(path: &Path, spec: EncodeSpec) -> Result<Self> {
        let wav_spec = WavSpec {
            channels: spec.channels,
            sample_rate: spec.sample_rate,
            bits_per_sample: 32,
            sample_format: SampleFormat::Float,
        };
        let writer = WavWriter::create(path, wav_spec)
            .map_err(|e| RateAudioError::Encode(format!("Failed to create WAV file: {}", e)))?;
        Ok(Self { writer })
    }
}

impl AudioEncoder for WavEncoder {
    fn write_interleaved_f32(&mut self, pcm: &[f32]) -> Result<()> {
        for &sample in pcm {
            self.writer
                .write_sample(sample)
                .map_err(|e| RateAudioError::Encode(format!("WAV write_sample failed: {}", e)))?;
        }
        Ok(())
    }

    fn finalize(self: Box<Self>) -> Result<()> {
        self.writer
            .finalize()
            .map_err(|e| RateAudioError::Encode(format!("WAV finalize failed: {}", e)))?;
        Ok(())
    }
}
