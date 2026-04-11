# Third-Party Notices

This library (nakuru_rate_audio) uses the following third-party dependencies.

## Bungee

- License: MPL-2.0
- Repository: https://github.com/bungee-audio-stretch/bungee
- Used for time-stretching / pitch-preserving audio processing

## Eigen

- License: MPL-2.0
- Repository: https://gitlab.com/libeigen/eigen
- Used transitively by Bungee for linear algebra operations

## PFFFT

- License: BSD-like (FFTPACK derivative)
- Repository: https://bitbucket.org/jpommier/pffft
- Used transitively by Bungee for FFT processing

## Symphonia

- License: MPL-2.0
- Repository: https://github.com/pdeljanov/Symphonia
- Used for MP3 / OGG / WAV decoding

## hound

- License: Apache-2.0
- Repository: https://github.com/ruuda/hound
- Used for WAV encoding

## vorbis_rs

- License: BSD-3-Clause
- Repository: https://github.com/ComunidadAylas/vorbis-rs
- Used for OGG Vorbis encoding

## libloading

- License: ISC
- Repository: https://github.com/nagisa/rust_libloading
- Used to dynamically load libmp3lame.dll

## once_cell

- License: MIT OR Apache-2.0
- Repository: https://github.com/matklad/once_cell
- Used for lazy static initialization

## thiserror

- License: MIT OR Apache-2.0
- Repository: https://github.com/dtolnay/thiserror
- Used for error definitions

## bindgen

- License: BSD-3-Clause
- Repository: https://github.com/rust-lang/rust-bindgen
- Used at build time to generate Rust FFI bindings

## csbindgen

- License: MIT
- Repository: https://github.com/Cysharp/csbindgen
- Used at build time to generate C# P/Invoke bindings

## cc

- License: MIT OR Apache-2.0
- Repository: https://github.com/rust-lang/cc-rs
- Used at build time to compile the C++ bridge

## cmake

- License: MIT OR Apache-2.0
- Repository: https://github.com/rust-lang/cmake-rs
- Used at build time to configure and build Bungee

---

For full license texts, refer to each upstream repository and this crate's Cargo.lock.