mod decode;
mod encode;
mod error;
mod pipeline;
mod stretch;

use encode::OutputFormat;
use error::RateAudioError;
use pipeline::PipelineRequest;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::path::PathBuf;

thread_local! {
    static LAST_ERROR: std::cell::RefCell<Option<String>> = std::cell::RefCell::new(None);
}

fn decode_path_arg<'a>(
    ptr: *const u8,
    len: i32,
    name: &'static str,
) -> std::result::Result<&'a str, RateAudioError> {
    if ptr.is_null() {
        return Err(RateAudioError::NullArgument(name));
    }

    let len = usize::try_from(len)
        .map_err(|_| RateAudioError::InvalidArgument(format!("{} length is invalid", name)))?;
    if len == 0 {
        return Err(RateAudioError::InvalidArgument(format!(
            "{} length must be > 0",
            name
        )));
    }

    let bytes = unsafe { std::slice::from_raw_parts(ptr, len) };
    std::str::from_utf8(bytes).map_err(|_| RateAudioError::InvalidUtf8(name))
}

fn set_last_error(err: &RateAudioError) {
    LAST_ERROR.with(|e| {
        *e.borrow_mut() = Some(err.to_string());
    });
}

fn clear_last_error() {
    LAST_ERROR.with(|e| {
        *e.borrow_mut() = None;
    });
}

#[no_mangle]
pub extern "C" fn nakuru_rate_audio_convert(
    input_path: *const u8,
    input_path_len: i32,
    output_path: *const u8,
    output_path_len: i32,
    rate: f64,
    mode: i32,
    output_format: i32,
    quality: f32,
    progress_cb: Option<unsafe extern "C" fn(f32)>,
) -> i32 {
    let outcome = catch_unwind(AssertUnwindSafe(|| {
        // バリデーション
        if rate <= 0.0 || !rate.is_finite() {
            let err = RateAudioError::InvalidArgument("rate must be > 0 and finite".into());
            set_last_error(&err);
            return err.code();
        }

        let in_path = match decode_path_arg(input_path, input_path_len, "input_path") {
            Ok(path) => path,
            Err(err) => {
                set_last_error(&err);
                return err.code();
            }
        };
        let out_path = match decode_path_arg(output_path, output_path_len, "output_path") {
            Ok(path) => path,
            Err(err) => {
                set_last_error(&err);
                return err.code();
            }
        };

        let (speed, pitch) = match mode {
            0 => (rate, 1.0),  // DT: テンポのみ
            1 => (rate, rate), // NC: テンポ+ピッチ
            _ => {
                let err = RateAudioError::InvalidArgument(format!("invalid mode: {}", mode));
                set_last_error(&err);
                return err.code();
            }
        };

        let fmt = match output_format {
            0 => OutputFormat::Wav,
            1 => OutputFormat::OggVorbis,
            2 => OutputFormat::Mp3,
            _ => {
                let err = RateAudioError::InvalidArgument(format!(
                    "invalid output_format: {}",
                    output_format
                ));
                set_last_error(&err);
                return err.code();
            }
        };

        let request = PipelineRequest {
            input_path: PathBuf::from(in_path),
            output_path: PathBuf::from(out_path),
            speed,
            pitch,
            output_format: fmt,
            quality,
            progress_cb,
        };

        match pipeline::run(request) {
            Ok(()) => {
                clear_last_error();
                0
            }
            Err(e) => {
                let code = e.code();
                set_last_error(&e);
                code
            }
        }
    }));

    match outcome {
        Ok(code) => code,
        Err(_) => {
            let err = RateAudioError::Panic;
            set_last_error(&err);
            err.code() // -11
        }
    }
}

#[no_mangle]
pub extern "C" fn nakuru_rate_audio_get_last_error(buf: *mut u8, buf_len: i32) -> i32 {
    let outcome = catch_unwind(AssertUnwindSafe(|| {
        LAST_ERROR.with(|e| {
            let err = e.borrow();
            match err.as_ref() {
                None => 0,
                Some(msg) => {
                    let bytes = msg.as_bytes();
                    let buf_len = match usize::try_from(buf_len) {
                        Ok(len) if !buf.is_null() && len > 0 => len,
                        _ => return -(bytes.len() as i32),
                    };
                    if bytes.len() > buf_len {
                        return -(bytes.len() as i32);
                    }
                    unsafe {
                        std::ptr::copy_nonoverlapping(bytes.as_ptr(), buf, bytes.len());
                    }
                    bytes.len() as i32
                }
            }
        })
    }));
    match outcome {
        Ok(v) => v,
        Err(_) => 0, // panic 時はエラーなし扱い
    }
}

#[no_mangle]
pub extern "C" fn nakuru_rate_audio_is_mp3_available() -> i32 {
    let outcome = catch_unwind(AssertUnwindSafe(|| {
        #[cfg(feature = "mp3")]
        {
            match encode::lame::LameLibrary::get_or_load() {
                Ok(_) => 1,
                Err(_) => 0,
            }
        }
        #[cfg(not(feature = "mp3"))]
        {
            0
        }
    }));
    match outcome {
        Ok(v) => v,
        Err(_) => 0, // panic 時は利用不可扱い
    }
}
