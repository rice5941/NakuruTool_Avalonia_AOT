use std::fs::File;
use std::io::BufWriter;
use std::num::{NonZeroU32, NonZeroU8};
use std::path::Path;
use vorbis_rs::{
    VorbisBitrateManagementStrategy, VorbisEncoder as VorbisEnc, VorbisEncoderBuilder,
};

use super::{AudioEncoder, EncodeSpec};
use crate::error::{RateAudioError, Result};

pub struct OggEncoder {
    encoder: Option<VorbisEnc<BufWriter<File>>>,
    channels: usize,
}

impl OggEncoder {
    pub fn new(path: &Path, spec: EncodeSpec, quality: f32) -> Result<Self> {
        let sink =
            BufWriter::new(File::create(path).map_err(|e| {
                RateAudioError::Encode(format!("Failed to create OGG file: {}", e))
            })?);

        let sample_rate = NonZeroU32::new(spec.sample_rate).ok_or_else(|| {
            RateAudioError::InvalidArgument("sample_rate must be non-zero".into())
        })?;
        let channels = NonZeroU8::new(spec.channels as u8)
            .ok_or_else(|| RateAudioError::InvalidArgument("channels must be non-zero".into()))?;

        // default-features = false なので stream-serial-rng は無効。
        // new_with_serial を使用し、固定シリアルを与える。
        let mut builder = VorbisEncoderBuilder::new_with_serial(sample_rate, channels, sink, 0);
        builder.bitrate_management_strategy(VorbisBitrateManagementStrategy::QualityVbr {
            target_quality: quality.clamp(0.0, 1.0),
        });
        let encoder = builder.build().map_err(|e| {
            RateAudioError::Encode(format!("Failed to build Vorbis encoder: {}", e))
        })?;

        Ok(Self {
            encoder: Some(encoder),
            channels: spec.channels as usize,
        })
    }
}

impl AudioEncoder for OggEncoder {
    fn write_interleaved_f32(&mut self, pcm: &[f32]) -> Result<()> {
        let encoder = self
            .encoder
            .as_mut()
            .ok_or_else(|| RateAudioError::Encode("OGG encoder already finalized".into()))?;

        let ch = self.channels;
        if ch == 0 {
            return Err(RateAudioError::InvalidArgument("channels is zero".into()));
        }
        let frames = pcm.len() / ch;
        if frames == 0 {
            return Ok(());
        }

        // interleaved → planar 変換
        let mut planar: Vec<Vec<f32>> = vec![Vec::with_capacity(frames); ch];
        for frame in 0..frames {
            for c in 0..ch {
                planar[c].push(pcm[frame * ch + c]);
            }
        }

        encoder.encode_audio_block(planar).map_err(|e| {
            RateAudioError::Encode(format!("Vorbis encode_audio_block failed: {}", e))
        })?;
        Ok(())
    }

    fn finalize(mut self: Box<Self>) -> Result<()> {
        if let Some(encoder) = self.encoder.take() {
            encoder
                .finish()
                .map_err(|e| RateAudioError::Encode(format!("Vorbis finish failed: {}", e)))?;
        }
        Ok(())
    }
}
