# Third-Party Notices

## ライセンスについて

このリポジトリのオリジナルソースコード（C#、Rust 自作部分、AXAML レイアウト等）は
[MIT License](LICENSE) でライセンスされています。

ただし、配布バイナリ（アプリケーションパッケージ）には以下に列挙する第三者コンポーネントが含まれます。
これらのコンポーネントはそれぞれ固有のライセンス条件に従います。
**アプリ全体が MIT のみでライセンスされているわけではありません。**

---

## .NET / C# ライブラリ（コード依存）

### Avalonia UI
- **Version**: 11.3.12
- **License**: MIT License
- **Repository**: https://github.com/AvaloniaUI/Avalonia
- **Copyright**: Copyright (c) The Avalonia Project
- **備考**: Avalonia.Desktop も同梱

クロスプラットフォームUIフレームワーク。

---

### Avalonia.Fonts.Inter
- **Version**: 11.3.12
- **License**: MIT License（パッケージ自体）/ **SIL Open Font License 1.1**（Inter フォント）
- **Repository**: https://github.com/AvaloniaUI/Avalonia
- **Font Source**: https://github.com/rsms/inter
- **Font License**: https://github.com/rsms/inter/blob/master/LICENSE.txt
- **Copyright**: Copyright (c) The Avalonia Project（パッケージ）/ Copyright (c) 2016 The Inter Project Authors（フォント）
- **確認済み**: Inter フォントは SIL Open Font License 1.1 でライセンスされています（公式リポジトリの LICENSE.txt で確認）

Avalonia 向け Inter フォントバンドル。配布物にフォントデータが含まれます。

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

### R3Extensions.Avalonia
- **Version**: 1.3.0
- **License**: MIT License
- **Repository**: https://github.com/Cysharp/R3
- **Copyright**: Copyright (c) 2024 Cysharp, Inc.

R3 の Avalonia 向け拡張。

---

### Semi.Avalonia
- **Version**: 11.3.7.3
- **License**: MIT License
- **Repository**: https://github.com/irihitech/Semi.Avalonia
- **Copyright**: Copyright (c) 2024 Irihi

AvaloniaのためのUIテーマライブラリ。

---

### Semi.Avalonia.DataGrid
- **Version**: 11.3.7.3
- **License**: MIT License
- **Repository**: https://github.com/irihitech/Semi.Avalonia
- **Copyright**: Copyright (c) 2024 Irihi

Semi.Avalonia の DataGrid コンポーネント。

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
- **License**: MIT License（パッケージコード）/ **Apache-2.0**（同梱アイコンデータ）
- **Repository**: https://github.com/SKProCH/Material.Icons
- **Icon Source**: https://pictogrammers.com/ (Material Design Icons)
- **Copyright**: Copyright (c) 2021 SKProCH（パッケージ）/ Pictogrammers（アイコンデータ）
- **備考**: パッケージ自体は MIT ですが、同梱されるアイコン SVG パスデータは [Pictogrammers Free License](https://pictogrammers.com/docs/general/license/) に基づき Apache-2.0 でライセンスされています

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
- **備考**: Debug ビルドのみで使用。Release / 配布バイナリには含まれません。

Avaloniaのホットリロード機能。

---

## Rust / ネイティブライブラリ（コード依存）

### nakuru_audio
- **Version**: 0.1.0
- **License**: MIT License
- **Repository**: 本リポジトリ (`native/nakuru_audio/`)
- **Copyright**: Copyright (c) 2025 NakuruTool Contributors

このプロジェクトで開発されたオーディオ再生ライブラリ。rodioのC FFIラッパー。

詳細は [native/nakuru_audio/LICENSE](native/nakuru_audio/LICENSE) および [native/nakuru_audio/THIRD-PARTY-NOTICES.md](native/nakuru_audio/THIRD-PARTY-NOTICES.md) を参照してください。

---

### nakuru_stretch
- **Version**: 0.1.0
- **License**: MIT License
- **Repository**: 本リポジトリ (`native/nakuru_stretch/`)
- **Copyright**: Copyright (c) 2025 NakuruTool Contributors

このプロジェクトで開発されたタイムストレッチライブラリ。SignalsmithStretchのRust FFIラッパー。

詳細は [native/nakuru_stretch/THIRD-PARTY-NOTICES.md](native/nakuru_stretch/THIRD-PARTY-NOTICES.md) を参照してください。

---

### SignalsmithStretch (C++ Library)
- **License**: MIT License
- **Repository**: https://github.com/SignalsmithAudio/signalsmith-stretch
- **Copyright**: Copyright (c) Signalsmith Audio Ltd.

高品質なタイムストレッチ／ピッチシフト C++ ライブラリ。nakuru_stretch が内部的に使用。

---

### rodio
- **Version**: 0.21.1
- **License**: MIT License OR Apache License 2.0
- **Repository**: https://github.com/RustAudio/rodio
- **Copyright**: Copyright (c) The Rodio contributors

Rustのクロスプラットフォームオーディオ再生ライブラリ。

---

### csbindgen
- **Version**: 1.9.7
- **License**: MIT License
- **Repository**: https://github.com/Cysharp/csbindgen
- **Copyright**: Copyright (c) 2024 Cysharp, Inc.
- **備考**: ビルド時依存。配布バイナリには直接含まれません。

RustからC#へのFFIバインディング自動生成ツール。

---

### parking_lot
- **Version**: 0.12.5
- **License**: MIT License OR Apache License 2.0
- **Repository**: https://github.com/Amanieu/parking_lot
- **Copyright**: Copyright (c) The parking_lot contributors

Rustの高性能な同期プリミティブライブラリ。

---

### mp3-duration
- **Version**: 0.1.10
- **License**: MIT License
- **Repository**: https://github.com/agersant/mp3-duration
- **Copyright**: Copyright (c) Antoine Gersant

MP3ファイルの再生時間を計測するライブラリ。

---

### minimp3_fixed
- **Version**: 0.5.4
- **License**: CC0-1.0 OR MIT
- **Repository**: https://github.com/BOB450/minimp3-rs
- **Copyright**: Copyright (c) minimp3 contributors

Pure RustによるMP3デコードライブラリ。rodio経由で使用。

---

### hound
- **Version**: 3.5.1
- **License**: Apache-2.0
- **Repository**: https://github.com/ruuda/hound
- **Copyright**: Copyright (c) Ruud van Asseldonk

WAVファイルの読み書きライブラリ。rodio経由で使用。

---

### lewton
- **Version**: 0.10.2
- **License**: MIT OR Apache-2.0
- **Repository**: https://github.com/RustAudio/lewton
- **Copyright**: Copyright (c) The lewton contributors

OGG Vorbis デコードライブラリ。rodio 経由で使用。

---

### ogg
- **Version**: 0.8.0
- **License**: BSD-3-Clause
- **Repository**: https://github.com/RustAudio/ogg
- **Copyright**: Copyright (c) The ogg crate contributors

OGG コンテナパーサー。lewton が内部的に使用。

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

バイトオーダー読み書きライブラリ。

---

### tinyvec
- **Version**: 1.11.0
- **License**: Zlib OR Apache-2.0 OR MIT
- **Repository**: https://github.com/Lokathor/tinyvec
- **Copyright**: Copyright (c) The tinyvec contributors

小規模ベクター最適化ライブラリ。

---

### tinyvec_macros
- **Version**: 0.1.1
- **License**: MIT OR Apache-2.0 OR Zlib
- **Repository**: https://github.com/Lokathor/tinyvec
- **Copyright**: Copyright (c) The tinyvec_macros contributors

tinyvec のプロシージャルマクロ補助クレート。

---

## 間接的な依存関係

上記のライブラリは、さらに以下のような推移的依存関係を持っています。
これらは直接参照していませんが、配布バイナリに含まれる可能性があります。

### Rust エコシステム（rodio / cpal 経由）
- **cpal** 0.16.0: Apache License 2.0 - クロスプラットフォームオーディオ I/O
- **dasp_sample** 0.11.0: MIT OR Apache-2.0 - オーディオサンプル型
- **num-rational** 0.4.2: MIT OR Apache-2.0 - 有理数型

### .NET エコシステム（Avalonia 経由）
- **SkiaSharp**: MIT License - 2Dグラフィックスライブラリ（Copyright (c) 2015-2016 Xamarin, Inc. / Copyright (c) 2017-2018 Microsoft Corporation. [GitHub](https://github.com/mono/SkiaSharp)）
- **HarfBuzzSharp**: MIT License - テキストシェーピングライブラリ（同リポジトリ、[NuGet](https://www.nuget.org/packages/HarfBuzzSharp)）

完全な依存関係ツリーについては、以下のコマンドで確認できます：

```bash
# Rust 依存関係
cargo tree --manifest-path native/nakuru_audio/Cargo.toml

# .NET 依存関係
dotnet list NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT.csproj package --include-transitive
```

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

### BSD 3-Clause License

ogg クレートで使用。全文は上記 ogg セクションに記載。

---

## 謝辞

このプロジェクトは、オープンソースコミュニティの素晴らしい貢献によって成り立っています。
すべての開発者とコントリビューターに感謝いたします。
