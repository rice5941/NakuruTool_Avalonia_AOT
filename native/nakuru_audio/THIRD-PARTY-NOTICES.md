# Third-Party Notices

This library (nakuru_audio) uses the following third-party dependencies:

## rodio

- **License**: MIT OR Apache-2.0
- **Repository**: https://github.com/RustAudio/rodio
- **Copyright**: The Rodio contributors

rodio is licensed under either of:
- Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
- MIT license (http://opensource.org/licenses/MIT)

at your option.

## cpal

- **License**: Apache-2.0
- **Repository**: https://github.com/RustAudio/cpal
- Used by rodio for cross-platform audio I/O

## minimp3_fixed

- **License**: CC0-1.0 OR MIT
- **Repository**: https://github.com/nicedoc/minimp3-rs
- **Copyright**: minimp3 contributors
- Used by rodio for MP3 decoding

## mp3-duration

- **License**: MIT
- **Repository**: https://github.com/agersant/mp3-duration
- **Copyright**: Copyright (c) Antoine Gersant
- Used directly for measuring MP3 playback duration

## hound

- **License**: Apache-2.0
- **Repository**: https://github.com/ruuda/hound
- **Copyright**: Copyright (c) Ruud van Asseldonk
- Used by rodio for WAV decoding

## parking_lot

- **License**: MIT OR Apache-2.0
- **Repository**: https://github.com/Amanieu/parking_lot
- **Copyright**: The parking_lot contributors

## lewton

- **License**: MIT OR Apache-2.0
- **Repository**: https://github.com/RustAudio/lewton
- **Copyright**: The lewton contributors
- Used by rodio for OGG Vorbis decoding

## ogg

- **License**: BSD-3-Clause
- **Repository**: https://github.com/RustAudio/ogg
- **Copyright**: The ogg crate contributors
- Used by lewton for OGG container parsing

## byteorder

- **License**: Unlicense OR MIT
- **Repository**: https://github.com/BurntSushi/byteorder
- **Copyright**: Andrew Gallant
- Used by ogg for byte-order reading

## tinyvec

- **License**: Zlib OR Apache-2.0 OR MIT
- **Repository**: https://github.com/Soveu/tinyvec
- **Copyright**: The tinyvec contributors
- Used by lewton for small vector optimization

## tinyvec_macros

- **License**: MIT OR Apache-2.0 OR Zlib
- **Repository**: https://github.com/Soveu/tinyvec
- **Copyright**: The tinyvec_macros contributors
- Used by tinyvec

---

For the full license texts of these dependencies, please refer to their respective repositories or the `Cargo.lock` file in this project.
