//! C FFI for NakuruTool's 7K chart analysis.
//! Calculation code comes unchanged from Mania Tracker (MIT); see analyze.rs.

pub mod analyze;

use std::panic::{catch_unwind, AssertUnwindSafe};

/// UTF-8 bytes owned by Rust. Release with `a7k_free`.
#[repr(C)]
pub struct A7kBuffer {
    pub ptr: *mut u8,
    pub len: usize,
    pub capacity: usize,
}

pub const A7K_OK: i32 = 0;
pub const A7K_INVALID_ARGUMENT: i32 = 1;
pub const A7K_INVALID_UTF8: i32 = 2;
pub const A7K_INVALID_REQUEST: i32 = 3;
pub const A7K_PANIC: i32 = 4;
pub const A7K_ANALYSIS_FAILED: i32 = 5;

fn write_buffer(out: *mut A7kBuffer, text: String) {
    let mut bytes = text.into_bytes();
    bytes.shrink_to_fit();
    let buffer = A7kBuffer { ptr: bytes.as_mut_ptr(), len: bytes.len(), capacity: bytes.capacity() };
    std::mem::forget(bytes);
    unsafe { out.write(buffer) };
}

fn error_json(message: &str) -> String {
    serde_json::json!({ "error": message }).to_string()
}

/// Analyze one chart. `osu` is the raw .osu file (UTF-8, BOM allowed) and `request`
/// is a UTF-8 JSON request (`{"od": x}`, see analyze::analyze). On return `out` holds a JSON
/// result, or `{"error": ...}` when the status is not A7K_OK.
///
/// # Safety
/// `osu` and `request` must point to readable memory of the given lengths and
/// `out` must be writable. Safe to call from several threads at once.
#[no_mangle]
pub unsafe extern "C" fn a7k_analyze(
    osu: *const u8,
    osu_len: usize,
    request: *const u8,
    request_len: usize,
    out: *mut A7kBuffer,
) -> i32 {
    if out.is_null() {
        return A7K_INVALID_ARGUMENT;
    }
    if osu.is_null() || request.is_null() {
        write_buffer(out, error_json("null argument"));
        return A7K_INVALID_ARGUMENT;
    }
    let osu = std::slice::from_raw_parts(osu, osu_len);
    let request = std::slice::from_raw_parts(request, request_len);
    let outcome = catch_unwind(AssertUnwindSafe(|| {
        let Ok(text) = std::str::from_utf8(osu) else {
            return (A7K_INVALID_UTF8, error_json("osu file is not valid UTF-8"));
        };
        let Ok(request) = serde_json::from_slice::<serde_json::Value>(request) else {
            return (A7K_INVALID_REQUEST, error_json("invalid request JSON"));
        };
        match analyze::analyze(text, &request) {
            Ok(value) => (A7K_OK, value.to_string()),
            Err(message) => (A7K_ANALYSIS_FAILED, error_json(&message)),
        }
    }));
    let (status, text) = outcome.unwrap_or_else(|_| (A7K_PANIC, error_json("panic in analysis")));
    write_buffer(out, text);
    status
}

/// Engine version string for cache keys.
///
/// # Safety
/// `out` must be writable.
#[no_mangle]
pub unsafe extern "C" fn a7k_engine_version(out: *mut A7kBuffer) -> i32 {
    if out.is_null() {
        return A7K_INVALID_ARGUMENT;
    }
    write_buffer(out, analyze::engine_version());
    A7K_OK
}

/// Release a buffer returned by this library.
///
/// # Safety
/// `buffer` must come from this library and be released only once.
#[no_mangle]
pub unsafe extern "C" fn a7k_free(buffer: A7kBuffer) {
    if !buffer.ptr.is_null() {
        drop(Vec::from_raw_parts(buffer.ptr, buffer.len, buffer.capacity));
    }
}
