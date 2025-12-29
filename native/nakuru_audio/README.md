# nakuru_audio

C FFI wrapper for the [rodio](https://github.com/RustAudio/rodio) audio playback library.

## Overview

`nakuru_audio` provides a simple C-compatible API for audio playback functionality, allowing .NET applications to use the rodio library through P/Invoke. This library is specifically designed for use with NativeAOT compiled .NET applications.

## Features

- Cross-platform audio playback (Windows, Linux, macOS)
- Simple C FFI API
- Play/Pause/Resume/Stop controls
- Volume control (0.0 - 1.0+)
- State change callbacks
- Support for multiple audio formats (MP3, WAV, FLAC, Vorbis, etc.)

## Building

```bash
cargo build --release
```

The compiled library will be located at:
- Windows: `target/release/nakuru_audio.dll`
- Linux: `target/release/libnakuru_audio.so`
- macOS: `target/release/libnakuru_audio.dylib`

## C API

See `NativeMethods.g.cs` in the parent .NET project for the auto-generated C# bindings.

### Example

```c
// Create player
AudioPlayer* player = nakuru_audio_create();

// Play audio file
const char* path = "music.mp3";
nakuru_audio_play(player, (const uint8_t*)path, strlen(path));

// Set volume to 50%
nakuru_audio_set_volume(player, 0.5f);

// Pause
nakuru_audio_pause(player);

// Resume
nakuru_audio_resume(player);

// Stop and destroy
nakuru_audio_stop(player);
nakuru_audio_destroy(player);
```

## Integration with .NET

This library is automatically built and copied by the parent .NET project's MSBuild targets. C# bindings are generated using [csbindgen](https://github.com/Cysharp/csbindgen).

## Dependencies

- **rodio** (MIT OR Apache-2.0): Cross-platform audio playback
- **parking_lot** (MIT OR Apache-2.0): Efficient synchronization primitives
- **csbindgen** (MIT): C# binding generator

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for full dependency information.

## License

This library is licensed under the MIT License. See [LICENSE](LICENSE) for details.

The underlying rodio library is dual-licensed under MIT OR Apache-2.0.
