use symphonia::core::audio::{AudioBufferRef, Signal};
use symphonia::core::codecs::{Decoder, DecoderOptions, CODEC_TYPE_NULL};
use symphonia::core::errors::Error as SymphoniaError;
use symphonia::core::formats::{FormatOptions, FormatReader};
use symphonia::core::io::MediaSourceStream;
use symphonia::core::meta::MetadataOptions;
use symphonia::core::probe::Hint;

use super::AudioSource;
use crate::error::{RateAudioError, Result};

use std::fs::File;
use std::path::Path;

#[derive(Debug, Clone, Copy)]
pub struct AudioSpec {
    pub sample_rate: u32,
    pub channels: u16,
}

pub struct SymphoniaDecoder {
    reader: Box<dyn FormatReader>,
    decoder: Box<dyn Decoder>,
    track_id: u32,
    spec: AudioSpec,
    total_frames: Option<u64>,
}

impl SymphoniaDecoder {
    pub fn open(path: &Path) -> Result<Self> {
        let file = File::open(path).map_err(|e| {
            RateAudioError::Decode(format!("Failed to open file '{}': {}", path.display(), e))
        })?;
        let mss = MediaSourceStream::new(Box::new(file), Default::default());

        let mut hint = Hint::new();
        if let Some(ext) = path.extension().and_then(|e| e.to_str()) {
            hint.with_extension(ext);
        }

        let probed = symphonia::default::get_probe()
            .format(
                &hint,
                mss,
                &FormatOptions::default(),
                &MetadataOptions::default(),
            )
            .map_err(|e| {
                RateAudioError::UnsupportedInputFormat(format!(
                    "Failed to probe format for '{}': {}",
                    path.display(),
                    e
                ))
            })?;

        let reader = probed.format;

        let track = reader
            .tracks()
            .iter()
            .find(|t| t.codec_params.codec != CODEC_TYPE_NULL)
            .ok_or_else(|| {
                RateAudioError::Decode(format!(
                    "No supported audio track found in '{}'",
                    path.display()
                ))
            })?;

        let codec_params = &track.codec_params;
        let track_id = track.id;

        let sample_rate = codec_params.sample_rate.ok_or_else(|| {
            RateAudioError::Decode(format!("Unknown sample rate in '{}'", path.display()))
        })?;

        let channels = codec_params
            .channels
            .map(|ch| ch.count() as u16)
            .ok_or_else(|| {
                RateAudioError::Decode(format!("Unknown channel count in '{}'", path.display()))
            })?;

        let total_frames = codec_params.n_frames;

        let decoder = symphonia::default::get_codecs()
            .make(&codec_params, &DecoderOptions::default())
            .map_err(|e| {
                RateAudioError::Decode(format!(
                    "Failed to create decoder for '{}': {}",
                    path.display(),
                    e
                ))
            })?;

        Ok(Self {
            reader,
            decoder,
            track_id,
            spec: AudioSpec {
                sample_rate,
                channels,
            },
            total_frames,
        })
    }
}

impl AudioSource for SymphoniaDecoder {
    fn spec(&self) -> AudioSpec {
        self.spec
    }

    fn total_frames(&self) -> Option<u64> {
        self.total_frames
    }

    fn read_interleaved_f32(&mut self, dst: &mut Vec<f32>) -> Result<usize> {
        loop {
            let packet = match self.reader.next_packet() {
                Ok(packet) => packet,
                Err(SymphoniaError::IoError(ref e))
                    if e.kind() == std::io::ErrorKind::UnexpectedEof =>
                {
                    return Ok(0);
                }
                Err(SymphoniaError::ResetRequired) => {
                    return Ok(0);
                }
                Err(e) => {
                    return Err(RateAudioError::Decode(format!(
                        "Failed to read packet: {}",
                        e
                    )));
                }
            };

            if packet.track_id() != self.track_id {
                continue;
            }

            // DecodeError は非致命的（パケット破損やコーデックウォームアップ）。
            // スキップして次のパケットを読み続ける。
            let decoded = match self.decoder.decode(&packet) {
                Ok(decoded) => decoded,
                Err(SymphoniaError::DecodeError(_)) => {
                    continue;
                }
                Err(e) => {
                    return Err(RateAudioError::Decode(format!(
                        "Failed to decode packet: {}",
                        e
                    )));
                }
            };

            let frames = decoded.frames();
            // コーデックウォームアップ中は 0 フレームが返ることがある（Vorbis等）。
            // EOF ではないので次のパケットを読む。
            if frames == 0 {
                continue;
            }

            let channels = self.spec.channels as usize;

            dst.clear();
            dst.reserve(frames * channels);

            match decoded {
                AudioBufferRef::F32(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            dst.push(*buf.chan(ch).get(frame).unwrap_or(&0.0));
                        }
                    }
                }
                AudioBufferRef::S16(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&0);
                            dst.push(sample as f32 / 32768.0);
                        }
                    }
                }
                AudioBufferRef::S32(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&0);
                            dst.push(sample as f32 / 2_147_483_648.0);
                        }
                    }
                }
                AudioBufferRef::S8(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&0);
                            dst.push(sample as f32 / 128.0);
                        }
                    }
                }
                AudioBufferRef::U8(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&128);
                            dst.push((sample as f32 - 128.0) / 128.0);
                        }
                    }
                }
                AudioBufferRef::U16(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&32768);
                            dst.push((sample as f32 - 32768.0) / 32768.0);
                        }
                    }
                }
                AudioBufferRef::S24(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample =
                                buf.chan(ch).get(frame).copied().unwrap_or_default().inner();
                            dst.push(sample as f32 / 8_388_608.0);
                        }
                    }
                }
                AudioBufferRef::U24(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample =
                                buf.chan(ch).get(frame).copied().unwrap_or_default().inner();
                            dst.push((sample as f32 - 8_388_608.0) / 8_388_608.0);
                        }
                    }
                }
                AudioBufferRef::U32(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&2_147_483_648);
                            dst.push((sample as f64 - 2_147_483_648.0) as f32 / 2_147_483_648.0);
                        }
                    }
                }
                AudioBufferRef::F64(buf) => {
                    for frame in 0..frames {
                        for ch in 0..channels {
                            let sample = *buf.chan(ch).get(frame).unwrap_or(&0.0);
                            dst.push(sample as f32);
                        }
                    }
                }
            }

            return Ok(frames);
        }
    }
}
