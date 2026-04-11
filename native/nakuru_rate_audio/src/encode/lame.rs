use crate::error::{RateAudioError, Result};
use libloading::Library;
use once_cell::sync::OnceCell;

#[repr(C)]
pub struct LameGlobalFlags {
    _private: [u8; 0],
}

#[allow(non_snake_case)]
pub struct LameApi {
    pub lame_init: unsafe extern "C" fn() -> *mut LameGlobalFlags,
    pub lame_close: unsafe extern "C" fn(*mut LameGlobalFlags) -> i32,
    pub lame_set_num_channels: unsafe extern "C" fn(*mut LameGlobalFlags, i32) -> i32,
    pub lame_set_in_samplerate: unsafe extern "C" fn(*mut LameGlobalFlags, i32) -> i32,
    pub lame_set_quality: unsafe extern "C" fn(*mut LameGlobalFlags, i32) -> i32,
    pub lame_set_VBR: unsafe extern "C" fn(*mut LameGlobalFlags, i32) -> i32,
    pub lame_set_VBR_quality: unsafe extern "C" fn(*mut LameGlobalFlags, f32) -> i32,
    pub lame_set_bWriteVbrTag: unsafe extern "C" fn(*mut LameGlobalFlags, i32) -> i32,
    pub lame_init_params: unsafe extern "C" fn(*mut LameGlobalFlags) -> i32,
    pub lame_encode_buffer_interleaved_ieee_float:
        unsafe extern "C" fn(*mut LameGlobalFlags, *const f32, i32, *mut u8, i32) -> i32,
    pub lame_encode_flush: unsafe extern "C" fn(*mut LameGlobalFlags, *mut u8, i32) -> i32,
    pub lame_get_lametag_frame:
        unsafe extern "C" fn(*const LameGlobalFlags, *mut u8, usize) -> usize,
}

pub struct LameLibrary {
    _library: Library,
    pub api: LameApi,
}

// Safety: LameLibrary は immutable な関数ポインタのみ保持し、
// Library のライフタイム内でのみ使用される
unsafe impl Send for LameLibrary {}
unsafe impl Sync for LameLibrary {}

/// グローバルキャッシュ。mp3 encoder 作成時のみロードされる
static LAME_LIBRARY: OnceCell<std::result::Result<LameLibrary, String>> = OnceCell::new();

impl LameLibrary {
    /// exe 隣接の libmp3lame.dll をロードし、OnceCell にキャッシュ
    pub fn get_or_load() -> Result<&'static LameLibrary> {
        LAME_LIBRARY
            .get_or_init(|| Self::load().map_err(|e| e.to_string()))
            .as_ref()
            .map_err(|e| RateAudioError::DependencyUnavailable(e.clone()))
    }

    fn load() -> Result<Self> {
        let lib_name = if cfg!(windows) {
            "libmp3lame.dll"
        } else {
            "libmp3lame.so"
        };

        // 1. exe 隣接ディレクトリを優先探索
        let lib = match std::env::current_exe()
            .ok()
            .and_then(|p| p.parent().map(|d| d.join(lib_name)))
            .and_then(|p| unsafe { Library::new(&p).ok() })
        {
            Some(lib) => lib,
            None => unsafe { Library::new(lib_name) }.map_err(|e| {
                RateAudioError::DependencyUnavailable(format!("{} not found: {}", lib_name, e))
            })?,
        };

        // 各シンボルをロード
        unsafe {
            let api = LameApi {
                lame_init: *lib
                    .get(b"lame_init\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_close: *lib
                    .get(b"lame_close\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_set_num_channels: *lib
                    .get(b"lame_set_num_channels\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_set_in_samplerate: *lib
                    .get(b"lame_set_in_samplerate\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_set_quality: *lib
                    .get(b"lame_set_quality\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_set_VBR: *lib
                    .get(b"lame_set_VBR\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_set_VBR_quality: *lib
                    .get(b"lame_set_VBR_quality\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_set_bWriteVbrTag: *lib
                    .get(b"lame_set_bWriteVbrTag\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_init_params: *lib
                    .get(b"lame_init_params\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_encode_buffer_interleaved_ieee_float: *lib
                    .get(b"lame_encode_buffer_interleaved_ieee_float\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_encode_flush: *lib
                    .get(b"lame_encode_flush\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
                lame_get_lametag_frame: *lib
                    .get(b"lame_get_lametag_frame\0")
                    .map_err(|e| RateAudioError::DependencyUnavailable(e.to_string()))?,
            };
            Ok(Self { _library: lib, api })
        }
    }
}
