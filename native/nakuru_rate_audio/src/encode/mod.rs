use crate::error::Result;
use std::path::Path;

#[cfg(feature = "mp3")]
pub mod lame;
#[cfg(feature = "mp3")]
pub mod mp3;
#[cfg(feature = "ogg")]
pub mod ogg;
#[cfg(feature = "wav")]
pub mod wav;

pub trait AudioEncoder {
    fn write_interleaved_f32(&mut self, pcm: &[f32]) -> Result<()>;
    fn finalize(self: Box<Self>) -> Result<()>;
}

pub struct EncodeSpec {
    pub sample_rate: u32,
    pub channels: u16,
}

/// フォーマット enum (FFI 経由のため repr(C))
#[repr(C)]
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum OutputFormat {
    Wav = 0,
    OggVorbis = 1,
    Mp3 = 2,
}

/// factory: format に対応するエンコーダーを生成
pub fn create_encoder(
    format: OutputFormat,
    path: &Path,
    spec: EncodeSpec,
    quality: f32,
) -> Result<Box<dyn AudioEncoder>> {
    match format {
        #[cfg(feature = "wav")]
        OutputFormat::Wav => wav::WavEncoder::new(path, spec).map(|e| Box::new(e) as _),
        #[cfg(not(feature = "wav"))]
        OutputFormat::Wav => Err(crate::error::RateAudioError::UnsupportedOutputFormat),

        #[cfg(feature = "ogg")]
        OutputFormat::OggVorbis => {
            ogg::OggEncoder::new(path, spec, quality).map(|e| Box::new(e) as _)
        }
        #[cfg(not(feature = "ogg"))]
        OutputFormat::OggVorbis => Err(crate::error::RateAudioError::UnsupportedOutputFormat),

        #[cfg(feature = "mp3")]
        OutputFormat::Mp3 => mp3::Mp3Encoder::new(path, spec, quality).map(|e| Box::new(e) as _),
        #[cfg(not(feature = "mp3"))]
        OutputFormat::Mp3 => Err(crate::error::RateAudioError::UnsupportedOutputFormat),
    }
}
