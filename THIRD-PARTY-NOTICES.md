# Third-Party Notices

## ライセンスについて

NakuruTool のオリジナルソースコードは
MIT License (licenses/LICENSE) でライセンスされています。

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

## Rust / ネイティブライブラリ（コード依存）

### nakuru_audio
- **Version**: 0.1.0
- **License**: MIT License
- **Repository**: https://github.com/rice5941/NakuruTool_Avalonia_AOT
- **Copyright**: Copyright (c) 2025 NakuruTool Contributors

このプロジェクトで開発されたオーディオ再生ライブラリ。rodioのC FFIラッパー。

---

### FFmpeg (LGPL build)
- **Version**: n8.1 (BtbN LGPL build, win64)
- **License**: **GNU Lesser General Public License v2.1 or later (LGPL-2.1-or-later)**（`native/ffmpeg/win-x64/LICENSE.txt` に同梱されるライセンス全文は LGPL version 3 の本文で、FFmpeg の LGPL-2.1-or-later における "or later" 節に基づき v3 を選択した形になっています）
- **Website**: https://ffmpeg.org/
- **Source (upstream)**: https://ffmpeg.org/download.html / https://git.ffmpeg.org/ffmpeg.git / https://github.com/FFmpeg/FFmpeg
- **Downloaded build**: https://github.com/BtbN/FFmpeg-Builds/releases （asset: `ffmpeg-n8.1-latest-win64-lgpl-8.1.zip`）
- **Copyright**: Copyright (c) 2000-2026 the FFmpeg developers
- **ライセンスファイル**: `native/ffmpeg/win-x64/LICENSE.txt`（LGPL 全文）/ `native/ffmpeg/win-x64/NOTICE.txt`（メタデータ）/ 配布アーカイブの `FFmpeg_LICENSE.txt`
- **SHA-256**:
  - `ffmpeg.exe`  : `4C2891E5DCC1F9A206D43C42CE730163AB947CBC97A447700402136D69095458`
  - `LICENSE.txt` : `DA7EABB7BAFDF7D3AE5E9F223AA5BDC1EECE45AC569DC21B3B037520B4464768`
- **備考**: `ffmpeg.exe` を配布物に同梱し、アプリケーションからは subprocess（別プロセス）としてのみ呼び出します。C# / Rust 側のコードに対して静的・動的リンクは一切行いません。この LGPL ビルドには GPL-only コンポーネントは含まれません。`libmp3lame`（MP3 エンコード）および `libvorbis` / `libogg`（Ogg Vorbis エンコード／コンテナ）が含まれます。LGPL-2.1-or-later の要件に従い、ユーザーは同梱バイナリを LGPL 互換の自前ビルドに差し替えて利用できます。

FFmpeg プロジェクトのライブラリ／ツール群。オーディオのデコード・レート変換・エンコード（MP3 / OGG / WAV）に使用。

> **Trademarks**: FFmpeg is a trademark of Fabrice Bellard, originator of the FFmpeg project.

---

### libmp3lame (bundled inside FFmpeg LGPL build)
- **License**: **GNU Lesser General Public License v2.0 or later (LGPL-2.0-or-later)**
- **Website / Source**: https://lame.sourceforge.io/
- **Copyright**: Copyright (C) 1999-2017 The LAME Project
- **備考**: 上記 FFmpeg LGPL ビルドに MP3 エンコーダとしてリンク済みで同梱されます。独立した DLL としては配布していません。

---

### libvorbis (bundled inside FFmpeg LGPL build)
- **License**: **BSD 3-Clause License**（Xiph.Org Foundation）
- **Website / Source**: https://xiph.org/vorbis/ / https://gitlab.xiph.org/xiph/vorbis
- **Copyright**: Copyright (c) 2002-2020 Xiph.Org Foundation
- **備考**: 上記 FFmpeg LGPL ビルドに Vorbis エンコーダ／デコーダとしてリンク済みで同梱されます。関連する `libogg` も同じく Xiph.Org の BSD ライセンスで同梱されています。

---

### BtbN/FFmpeg-Builds (build scripts)
- **License**: **MIT License**
- **Repository**: https://github.com/BtbN/FFmpeg-Builds
- **Copyright**: Copyright (c) BtbN and contributors
- **備考**: 同梱 FFmpeg バイナリを生成するために使用されたビルドスクリプトのライセンス。FFmpeg 本体・同梱コーデックのライセンス（LGPL-2.1-or-later 等）とは別物です。

---

### rodio
- **Version**: 0.21.1
- **License**: MIT License OR Apache License 2.0
- **Repository**: https://github.com/RustAudio/rodio
- **Copyright**: Copyright (c) The Rodio contributors

Rustのクロスプラットフォームオーディオ再生ライブラリ。

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

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
OF THE POSSIBILITY OF SUCH DAMAGE.

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

完全な依存関係ツリーについては、ソースリポジトリ (https://github.com/rice5941/NakuruTool_Avalonia_AOT) を参照してください。

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
