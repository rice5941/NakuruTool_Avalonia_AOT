# Third-Party Notices

このプロジェクトは、以下のオープンソースソフトウェアを使用しています。

---

## .NET / C# ライブラリ

### Avalonia UI
- **Version**: 11.3.12
- **License**: MIT License
- **Repository**: https://github.com/AvaloniaUI/Avalonia
- **Copyright**: Copyright (c) The Avalonia Project

クロスプラットフォームUIフレームワーク。

---

### CommunityToolkit.Mvvm
- **Version**: 8.4.0
- **License**: MIT License
- **Repository**: https://github.com/CommunityToolkit/dotnet
- **Copyright**: Copyright (c) .NET Foundation and Contributors

MVVMパターン実装のためのツールキット。

---

### R3
- **Version**: 1.3.0
- **License**: MIT License
- **Repository**: https://github.com/Cysharp/R3
- **Copyright**: Copyright (c) 2024 Cysharp, Inc.

Reactive Extensionsの新しい実装。リアクティブプログラミングのためのライブラリ。

---

### Semi.Avalonia
- **Version**: 11.3.7.3
- **License**: MIT License
- **Repository**: https://github.com/irihitech/Semi.Avalonia
- **Copyright**: Copyright (c) 2024 Irihi

AvaloniaのためのUIテーマライブラリ。

---

### Pure.DI
- **Version**: 2.3.3
- **License**: MIT License
- **Repository**: https://github.com/DevTeam/Pure.DI
- **Copyright**: Copyright (c) 2023 Team DevTeam

コンパイル時依存性注入フレームワーク。

---

### Material.Icons.Avalonia
- **Version**: 2.4.1
- **License**: MIT License
- **Repository**: https://github.com/AvaloniaUtils/Material.Icons.Avalonia
- **Copyright**: Copyright (c) 2021 AvaloniaUtils

AvaloniaのためのMaterial Design Iconsライブラリ。

---

### ZLinq
- **Version**: 1.5.5
- **License**: MIT License
- **Repository**: https://github.com/Cysharp/ZLinq
- **Copyright**: Copyright (c) 2024 Cysharp, Inc.

ゼロアロケーションLINQ実装。

---

### HotAvalonia
- **Version**: 3.1.0
- **License**: MIT License
- **Repository**: https://github.com/Kir-Antipov/HotAvalonia
- **Copyright**: Copyright (c) 2023 Kir_Antipov

Avaloniaのホットリロード機能。

---

## Rust / ネイティブライブラリ

### nakuru_audio
- **Version**: 0.1.0
- **License**: MIT License
- **Repository**: https://github.com/your-username/NakuruTool_Avalonia_AOT/tree/main/native/nakuru_audio
- **Copyright**: Copyright (c) 2025 NakuruTool Contributors

このプロジェクトで開発されたオーディオ再生ライブラリ。rodioのC FFIラッパー。

詳細は [native/nakuru_audio/LICENSE](native/nakuru_audio/LICENSE) および [native/nakuru_audio/THIRD-PARTY-NOTICES.md](native/nakuru_audio/THIRD-PARTY-NOTICES.md) を参照してください。

---

### rodio
- **Version**: 0.21.1
- **License**: MIT License OR Apache License 2.0
- **Repository**: https://github.com/RustAudio/rodio
- **Copyright**: Copyright (c) The Rodio contributors

Rustのクロスプラットフォームオーディオ再生ライブラリ。

rodioは以下のライセンスのいずれかを選択できます：
- MIT License (http://opensource.org/licenses/MIT)
- Apache License 2.0 (http://www.apache.org/licenses/LICENSE-2.0)

---

### csbindgen
- **Version**: 1.9.7
- **License**: MIT License
- **Repository**: https://github.com/Cysharp/csbindgen
- **Copyright**: Copyright (c) 2024 Cysharp, Inc.

RustからC#へのFFIバインディング自動生成ツール。ビルド時に使用されます。

---

### parking_lot
- **Version**: 0.12.5
- **License**: MIT License OR Apache License 2.0
- **Repository**: https://github.com/Amanieu/parking_lot
- **Copyright**: Copyright (c) The parking_lot contributors

Rustの高性能な同期プリミティブライブラリ。rodioが内部的に使用しています。

parking_lotは以下のライセンスのいずれかを選択できます：
- MIT License (http://opensource.org/licenses/MIT)
- Apache License 2.0 (http://www.apache.org/licenses/LICENSE-2.0)

---

### lewton
- **Version**: 0.10.2
- **License**: MIT OR Apache-2.0
- **Repository**: https://github.com/RustAudio/lewton
- **Copyright**: Copyright (c) The lewton contributors

OGG Vorbis デコードライブラリ。rodio の `lewton` feature 経由で使用。

lewtonは以下のライセンスのいずれかを選択できます：
- MIT License (http://opensource.org/licenses/MIT)
- Apache License 2.0 (http://www.apache.org/licenses/LICENSE-2.0)

---

### ogg
- **Version**: 0.8.0
- **License**: BSD-3-Clause
- **Repository**: https://github.com/RustAudio/ogg
- **Copyright**: Copyright (c) The ogg crate contributors

OGG コンテナパーサー。lewton が内部的に使用しています。

Copyright (c) The ogg crate contributors.
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software without
   specific prior written permission.

---

### byteorder
- **Version**: 1.5.0
- **License**: Unlicense OR MIT
- **Repository**: https://github.com/BurntSushi/byteorder
- **Copyright**: Copyright (c) 2015 Andrew Gallant

バイトオーダー読み書きライブラリ。oggが内部的に使用しています。

byteorderは以下のライセンスのいずれかを選択できます：
- Unlicense (https://unlicense.org)
- MIT License (http://opensource.org/licenses/MIT)

---

### tinyvec
- **Version**: 1.11.0
- **License**: Zlib OR Apache-2.0 OR MIT
- **Repository**: https://github.com/Lokathor/tinyvec
- **Copyright**: Copyright (c) The tinyvec contributors

小規模ベクター最適化ライブラリ。lewton が内部的に使用しています。

tinyvecは以下のライセンスのいずれかを選択できます：
- Zlib License
- Apache License 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
- MIT License (http://opensource.org/licenses/MIT)

---

### tinyvec_macros
- **Version**: 0.1.1
- **License**: MIT OR Apache-2.0 OR Zlib
- **Repository**: https://github.com/Lokathor/tinyvec
- **Copyright**: Copyright (c) The tinyvec_macros contributors

tinyvec のプロシージャルマクロ補助クレート。

tinyvec_macrosは以下のライセンスのいずれかを選択できます：
- MIT License (http://opensource.org/licenses/MIT)
- Apache License 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
- Zlib License

---

### minimp3_fixed
- **Version**: 0.5.4
- **License**: CC0-1.0 OR MIT
- **Repository**: https://github.com/nicedoc/minimp3-rs
- **Copyright**: Copyright (c) minimp3 contributors

Pure RustによるMP3デコードライブラリ。rodio経由でMP3再生に使用しています。

---

### mp3-duration
- **Version**: 0.1.10
- **License**: MIT License
- **Repository**: https://github.com/agersant/mp3-duration
- **Copyright**: Copyright (c) Antoine Gersant

MP3ファイルの再生時間を計測するライブラリ。Duration取得に直接使用しています。

---

### hound
- **Version**: 3.5.1
- **License**: Apache-2.0
- **Repository**: https://github.com/ruuda/hound
- **Copyright**: Copyright (c) Ruud van Asseldonk

WAVファイルの読み書きライブラリ。rodio経由でWAV再生に使用しています。

---

## 間接的な依存関係

上記のライブラリは、さらに以下のような依存関係を持っています：

### Rustエコシステム（cpal経由 / rodio経由）
- **cpal**: Apache License 2.0 - クロスプラットフォームオーディオI/Oライブラリ（rodio経由）

### .NETエコシステム
- **SkiaSharp**: MIT License - 2Dグラフィックスライブラリ（Avalonia経由）
- **HarfBuzzSharp**: MIT License - テキストシェーピングライブラリ（Avalonia経由）

完全な依存関係ツリーについては、以下のコマンドで確認できます：

```bash
# Rust依存関係
cargo tree --manifest-path native/nakuru_audio/Cargo.toml

# .NET依存関係
dotnet list NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT.csproj package
```

---

## ライセンス全文

各ライブラリのライセンス全文については、それぞれのリポジトリを参照してください。

### MIT License

多くのライブラリで使用されているMIT Licenseの一般的な形式：

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### Apache License 2.0

Apache License 2.0の全文は以下から入手できます：
http://www.apache.org/licenses/LICENSE-2.0

---

## 謝辞

このプロジェクトは、オープンソースコミュニティの素晴らしい貢献によって成り立っています。
すべての開発者とコントリビューターに感謝いたします。
