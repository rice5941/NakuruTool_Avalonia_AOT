pub mod symphonia_decoder;
pub use symphonia_decoder::{AudioSpec, SymphoniaDecoder};

use crate::error::Result;

/// デコードされた音声データのソース
pub trait AudioSource {
    /// オーディオスペック（サンプルレート、チャンネル数）
    fn spec(&self) -> AudioSpec;

    /// 総フレーム数（不明なら None）
    fn total_frames(&self) -> Option<u64>;

    /// interleaved f32 チャンクを読み取り、フレーム数を返す
    /// 0 が返ったら EOF
    fn read_interleaved_f32(&mut self, dst: &mut Vec<f32>) -> Result<usize>;
}
