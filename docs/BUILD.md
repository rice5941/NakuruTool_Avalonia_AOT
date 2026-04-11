# NakuruTool - ビルドガイド

このドキュメントでは、NakuruToolのソースコードからビルドする詳細な手順を説明します。

## 目次

1. [前提条件](#前提条件)
2. [Rustツールチェーンのセットアップ](#rustツールチェーンのセットアップ)
3. [Windows環境の準備](#windows環境の準備)
4. [ビルド手順](#ビルド手順)
5. [トラブルシューティング](#トラブルシューティング)
6. [クロスプラットフォームビルド](#クロスプラットフォームビルド)

---

## 前提条件

### 必須ソフトウェア

| ソフトウェア | バージョン | 用途 |
|------------|----------|------|
| .NET SDK | 10.0以上 | C#プロジェクトのビルド |
| Rust | 1.92.0以上 | nakuru_audio / nakuru_rate_audio ライブラリのビルド |
| Visual Studio | 2022 | NativeAOTリンカー / C++ コンパイラ (Windows) |
| CMake | 3.15以上 | nakuru_rate_audio の Bungee (C++) ビルド |
| LLVM/Clang | 18.0以上 | nakuru_rate_audio の bindgen（C→Rust FFI 自動生成）に必要 |

### 動作確認済み環境

- **OS**: Windows 11 (22H2以降)
- **Rust**: 1.92.0 (stable-x86_64-pc-windows-msvc)
- **.NET SDK**: 10.0.1
- **Visual Studio**: 2022 Community Edition (17.x)

---

## Rustツールチェーンのセットアップ

### 1. rustupのインストール

公式サイトからrustupをダウンロードしてインストールします。

**Windows:**
```powershell
# rustup-init.exeをダウンロードして実行
# https://rustup.rs/

# または、PowerShellで直接インストール
Invoke-WebRequest -Uri https://win.rustup.rs/x86_64 -OutFile rustup-init.exe
.\rustup-init.exe
```

インストール時のオプション:
- デフォルト設定を使用 (1を選択)
- Toolchain: `stable-x86_64-pc-windows-msvc`

### 2. インストールの確認

```powershell
# Rustバージョンの確認
rustc --version
# 出力例: rustc 1.92.0 (e8e6c0d1b 2025-01-01)

# Cargoバージョンの確認
cargo --version
# 出力例: cargo 1.92.0 (c8de64e29 2025-01-01)

# ツールチェーンの確認
rustup show
```

### 3. PATH設定の確認

rustupはデフォルトで以下のディレクトリをPATHに追加します:
- Windows: `%USERPROFILE%\.cargo\bin`

確認方法:
```powershell
$env:PATH -split ';' | Select-String "\.cargo"
```

---

## Windows環境の準備

### 1. Visual Studio 2022のインストール

NativeAOTビルドには、Visual Studio 2022が必要です。

**ダウンロード:**
- [Visual Studio Community 2022](https://visualstudio.microsoft.com/ja/vs/community/)

**必要なワークロード:**
1. 「Visual Studio Installerを起動」
2. 「変更」をクリック
3. 以下のワークロードを選択:

- ✅ **C++デスクトップ開発**
  - MSVC v143 - VS 2022 C++ x64/x86 ビルドツール
  - Windows 10 SDK (10.0.26100.0以上)
  - C++ CMakeツール

### 2. vswhere.exeの確認

NativeAOTビルドには、`vswhere.exe`が必要です。Visual Studioインストール時に自動的に配置されます。

**確認方法:**
```powershell
Get-Command vswhere.exe -ErrorAction SilentlyContinue
```

**場所:**
```
C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe
```

**PATHに追加** (publish.ps1で自動設定されます):
```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
```

---

## ビルド手順

### 1. リポジトリのクローン

```powershell
git clone https://github.com/your-username/NakuruTool_Avalonia_AOT.git
cd NakuruTool_Avalonia_AOT
```

### 2. Debugビルド

開発用の通常ビルド:

```powershell
dotnet build -c Debug
```

**出力先:**
```
NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/bin/Debug/net10.0/
```

**ビルドプロセス:**
1. Rustライブラリ (`nakuru_audio` と `nakuru_rate_audio`) のビルド (Debug構成)
2. BeatmapGenerator 配下の P/Invoke バインディング `.g.cs` の自動生成
3. C# プロジェクトのビルド
4. `nakuru_audio.dll` / `nakuru_rate_audio.dll` と `libmp3lame.dll`（存在する場合）のコピー

### 3. Releaseビルド

最適化されたビルド:

```powershell
dotnet build -c Release
```

### 4. NativeAOT Publishビルド

実行ファイルを生成する最終ビルド:

**PowerShellの場合:**
```powershell
.\publish.ps1
```

**手動での実行:**
```powershell
# vswhere.exeをPATHに追加
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"

# NativeAOT publish
dotnet publish -c Release -r win-x64
```

**出力先:**
```
NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/bin/Release/net10.0/win-x64/publish/
```

**出力ファイル:**
- `NakuruTool.exe` (約30MB)
- `nakuru_audio.dll` (約2.1MB)
- `nakuru_rate_audio.dll` (Rust オーディオレート変換ライブラリ)
- `libmp3lame.dll` (MP3エンコード、オプション)
- その他のDLL (Avalonia, SkiaSharp等)

### 5. Rustライブラリの個別ビルド

#### nakuru_audio

nakuru_audioライブラリのみをビルドする場合:

**Debug構成:**
```powershell
cargo build --manifest-path native/nakuru_audio/Cargo.toml
```

**Release構成:**
```powershell
cargo build --release --manifest-path native/nakuru_audio/Cargo.toml
```

**出力先:**
- Debug: `native/nakuru_audio/target/debug/nakuru_audio.dll`
- Release: `native/nakuru_audio/target/release/nakuru_audio.dll`

#### nakuru_rate_audio

オーディオレート変換ライブラリ（Bungee/Symphonia ベース）のビルド手順。

**前提条件:**
- Rust toolchain (1.92.0以上)
- CMake (Bungee の C++ ビルドに必要)
- C++ コンパイラ (MSVC — Visual Studio 2022 の「C++デスクトップ開発」ワークロード)
- LLVM/Clang (`libclang`) — bindgen による C→Rust FFI バインディング自動生成に必要。[公式サイト](https://releases.llvm.org/)からインストールするか、Visual Studio の「C++ Clang tools for Windows」コンポーネントを追加

**1. Git submodule の初期化:**
```powershell
git submodule update --init --recursive
```
これにより `native/nakuru_rate_audio/vendor/bungee/` に Bungee ソースがチェックアウトされます。

既存のクローンで `.gitmodules` をまだ取得していない場合は、先に以下も実行してください。

```powershell
git submodule sync --recursive
```

**2. Debug構成:**
```powershell
cargo build --manifest-path native/nakuru_rate_audio/Cargo.toml
```

**3. Release構成:**
```powershell
cargo build --release --manifest-path native/nakuru_rate_audio/Cargo.toml
```

**出力先:**
- Debug: `native/nakuru_rate_audio/target/debug/nakuru_rate_audio.dll`
- Release: `native/nakuru_rate_audio/target/release/nakuru_rate_audio.dll`

**ビルド時の注意:**
- `build.rs` が CMake 経由で Bungee (C++) をビルドし、csbindgen で C# バインディングを自動生成します
- 初回ビルドでは Bungee の C++ コンパイルに時間がかかります（2回目以降はキャッシュされます）
- CMake が PATH に存在しない場合は Visual Studio 付属の CMake を使用してください

### 6. libmp3lame.dll のビルド（オプション）

MP3 出力機能を有効にするには `libmp3lame.dll` が必要です。DLL が存在しなくても WAV / OGG 出力は正常に動作します。

> **ライセンス注意**: LAME は LGPL-2.0 でライセンスされています。LGPL に準拠するため、`libmp3lame.dll` は動的リンク（DLL 分離）としています。ユーザーは自分でビルドした DLL に差し替え可能です。

#### 6.1 ソースの入手

- **公式サイト**: https://lame.sourceforge.io/
- **ソースダウンロード**: https://sourceforge.net/projects/lame/files/lame/ から最新の `lame-3.100.tar.gz` を取得

#### 6.2 Windows (MSVC) でのビルド

```powershell
# 1. ソースを展開
tar -xzf lame-3.100.tar.gz
cd lame-3.100

# 2. Visual Studio Developer Command Prompt (x64) を開く
#    または "x64 Native Tools Command Prompt for VS 2022" を使用

# 3. MSVC でビルド
nmake -f Makefile.MSVC
#    → output\libmp3lame.dll が生成される
#
# 代替: CMake を使う場合
mkdir build && cd build
cmake .. -G "Visual Studio 17 2022" -A x64 -DBUILD_SHARED_LIBS=ON
cmake --build . --config Release
#    → Release\libmp3lame.dll が生成される
```

> **注意**: LAME のビルドでは一部の `.asm` ファイルに NASM が必要になる場合があります。ASM 最適化なしでビルドする場合は `HAVE_NASM` を無効にしてください。

#### 6.3 Windows (MSYS2/MinGW) でのビルド

```bash
# MSYS2 MinGW64 シェルで実行
tar -xzf lame-3.100.tar.gz
cd lame-3.100
./configure --enable-shared --disable-static --prefix=/mingw64
make -j$(nproc)
# → libmp3lame-0.dll が生成される（libmp3lame.dll にリネーム）
```

#### 6.4 DLL の配置

生成された `libmp3lame.dll` を以下に配置します:

```text
native/
└── libmp3lame.dll
```

- MSBuild ターゲットが publish / OutDir に自動コピーします
- `libmp3lame.dll` はリポジトリにはコミットしません（LGPL のため）
- `.gitignore` に `native/libmp3lame.dll` が追加されていることを確認してください

---

## トラブルシューティング

### 問題1: `vswhere.exe` が見つからない

**エラーメッセージ:**
```
'vswhere.exe' は、内部コマンドまたは外部コマンド、
操作可能なプログラムまたはバッチ ファイルとして認識されていません。
```

**原因:**
- Visual Studioがインストールされていない
- `vswhere.exe`がPATHに含まれていない

**解決策:**

1. Visual Studio 2022をインストール (上記参照)

2. PATHを手動で追加:
```powershell
# 現在のセッションのみ
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"

# 永続的に追加 (システム環境変数)
[Environment]::SetEnvironmentVariable(
    "Path",
    [Environment]::GetEnvironmentVariable("Path", "User") +
    ";${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer",
    "User"
)
```

3. `publish.ps1`を使用 (自動的にPATHを設定)

### 問題2: リンカーエラー (link.exe)

**エラーメッセージ:**
```
error MSB3073: コマンド "C:\Program Files\Microsoft Visual Studio\...\link.exe" はコード 123 で終了しました。
```

**原因:**
- Visual Studio C++ Build Toolsが不足
- Windows SDKが不足

**解決策:**

1. Visual Studio Installerを開く
2. 「変更」→「個別のコンポーネント」
3. 以下を確認してインストール:
   - `MSVC v143 - VS 2022 C++ x64/x86 ビルドツール`
   - `Windows 10 SDK (10.0.26100.0)`
   - `C++ CMake tools for Windows`

### 問題3: Rustツールチェーンが見つからない

**エラーメッセージ:**
```
cargo: command not found
```

**原因:**
- Rustがインストールされていない
- PATHが設定されていない

**解決策:**

1. Rustをインストール (上記参照)

2. 新しいターミナルを開く (PATH更新のため)

3. PATHを手動で確認:
```powershell
$env:PATH -split ';' | Select-String "\.cargo"
```

4. 手動でPATHを追加 (一時的):
```powershell
$env:PATH = "$env:USERPROFILE\.cargo\bin;$env:PATH"
```

### 問題4: `vendor/bungee` が見つからない

**エラーメッセージ:**
```
Bungee submodule is missing.
```

**原因:**
- `native/nakuru_rate_audio/vendor/bungee` が未取得
- 古いクローンで `.gitmodules` の更新が反映されていない

**解決策:**

```powershell
git submodule sync --recursive
git submodule update --init --recursive
```

### 問題5: Avalonia resourcesファイルのロック

**エラーメッセージ:**
```
System.IO.IOException: The process cannot access the file
'obj\Debug\net10.0\Avalonia\resources' because it is being used by another process.
```

**原因:**
- 前回のビルドプロセスが残っている
- VSCodeやVisual Studioでファイルがロックされている

**解決策:**

1. MSBuildプロセスを終了:
```powershell
taskkill /F /IM MSBuild.exe
```

2. obj/binディレクトリをクリーン:
```powershell
cd NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT
Remove-Item -Recurse -Force obj, bin
```

3. 再ビルド:
```powershell
dotnet build -c Debug
```

### 問題6: C# バインディング生成エラー

**エラーメッセージ:**
```
 error: BeatmapGenerator 配下の *.g.cs could not be generated
```

**原因:**
- csbindgenのビルドスクリプト (build.rs) が失敗
- `vendor/bungee` / LLVM / CMake などの前提条件が不足

**解決策:**

1. サブモジュールと前提ツールを確認:
```powershell
git submodule update --init --recursive
cmake --version
```

2. Rustライブラリを手動でビルド:
```powershell
cd native/nakuru_rate_audio
cargo clean
cargo build
```

3. 生成されたファイルを確認:
```powershell
Get-ChildItem ../../NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/BeatmapGenerator/*.g.cs
```

---

## クロスプラットフォームビルド

### Linux (将来対応予定)

**必要なパッケージ (Ubuntu/Debian):**
```bash
sudo apt-get update
sudo apt-get install -y \
    build-essential \
    libasound2-dev \
    libssl-dev \
    pkg-config
```

**ビルド:**
```bash
dotnet build -c Release -r linux-x64
```

### macOS (将来対応予定)

**必要なツール:**
```bash
xcode-select --install
```

**ビルド:**
```bash
dotnet build -c Release -r osx-x64
```

---

## ビルドの仕組み

### MSBuildターゲット

`.csproj`ファイルには、以下のカスタムターゲットが定義されています:

#### 1. BuildRustLibrary (BeforeBuild)

```xml
<Target Name="BuildRustLibrary" BeforeTargets="BeforeBuild">
  <!-- Rustライブラリをビルド -->
  <Exec Command="cargo build $(RustBuildFlag) --manifest-path ..." />

  <!-- ネイティブライブラリをコピー -->
  <Copy SourceFiles="..." DestinationFolder="$(OutDir)" />
</Target>
```

**処理:**
1. Debug/Release構成に応じてRustをビルド
2. `vendor/bungee` の有無を先に検証
3. 生成された `nakuru_audio.dll` / `nakuru_rate_audio.dll` を C# の出力ディレクトリにコピー
4. `libmp3lame.dll` が存在する場合は同梱

#### 2. CopyNativeLibraryToPublish (BeforeTargets: ComputeResolvedFilesToPublishList)

```xml
<Target Name="CopyNativeLibraryToPublish" BeforeTargets="ComputeResolvedFilesToPublishList">
  <ItemGroup>
    <ResolvedFileToPublish Include="$(NativeLibPath)">
      <RelativePath>nakuru_audio.dll</RelativePath>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </ResolvedFileToPublish>
  </ItemGroup>
</Target>
```

**処理:**
1. NativeAOT publishビルド時に、nakuru_audio.dllをpublishディレクトリに含める

### csbindgen

Rustのビルド時 (build.rs) に、C# FFIバインディングを自動生成します:

**入力:** `native/nakuru_audio/src/lib.rs`
**出力:** `Features/AudioPlayer/NativeMethods.g.cs`

**生成内容:**
- P/Invoke宣言 (`[DllImport]`)
- 構造体定義 (`AudioPlayer`, `NativeAudioPlayerState`)
- 関数ポインタ型

---

## 参考情報

### 公式ドキュメント

- [.NET 10.0 Documentation](https://docs.microsoft.com/dotnet/)
- [Rust Book](https://doc.rust-lang.org/book/)
- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [rodio - GitHub](https://github.com/RustAudio/rodio)
- [csbindgen - GitHub](https://github.com/Cysharp/csbindgen)

### ビルド時間の目安

| ビルドタイプ | 初回 | 2回目以降 |
|------------|------|----------|
| Debug (.NET) | ~30秒 | ~5秒 |
| Debug (Rust) | ~2分 | ~10秒 |
| Release (.NET) | ~1分 | ~10秒 |
| Release (Rust) | ~3分 | ~20秒 |
| NativeAOT Publish | ~5分 | ~3分 |

*Intel Core i7-12700K, 32GB RAM, NVMe SSDでの測定値

### ディスクスペース要件

- **ソースコード**: ~50MB
- **Rustビルド成果物**: ~500MB (target/ディレクトリ)
- **C#ビルド成果物**: ~200MB (bin/, obj/ディレクトリ)
- **NativeAOT publish**: ~130MB

**推奨:** 最低2GB以上の空き容量
