#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT"

echo "=== NakuruTool linux-arm64 NativeAOT Build ==="

# ─── 前提条件チェック ───
command -v dotnet >/dev/null 2>&1 || { echo "Error: dotnet SDK not found"; exit 1; }
command -v cargo >/dev/null 2>&1  || { echo "Error: cargo not found"; exit 1; }
command -v aarch64-linux-gnu-gcc >/dev/null 2>&1 || { echo "Error: aarch64-linux-gnu-gcc not found. Run: sudo apt install gcc-aarch64-linux-gnu"; exit 1; }
command -v clang >/dev/null 2>&1 || { echo "Error: clang not found. Run: sudo apt install clang"; exit 1; }
command -v rustup >/dev/null 2>&1 || { echo "Error: rustup not found. Install from https://rustup.rs"; exit 1; }

# Rust aarch64 ターゲットがインストール済みか確認
if ! rustup target list --installed | grep -q "aarch64-unknown-linux-gnu"; then
    echo "Installing Rust target aarch64-unknown-linux-gnu..."
    rustup target add aarch64-unknown-linux-gnu
fi

# libasound2-dev:arm64 の存在チェック
if ! dpkg -s libasound2-dev:arm64 >/dev/null 2>&1; then
    echo "Error: libasound2-dev:arm64 not found. Run: sudo dpkg --add-architecture arm64 && sudo apt update && sudo apt install libasound2-dev:arm64"
    exit 1
fi

# ─── Rust クロスコンパイル用環境変数 ───
export CC_aarch64_unknown_linux_gnu=aarch64-linux-gnu-gcc
export AR_aarch64_unknown_linux_gnu=aarch64-linux-gnu-ar
export CARGO_TARGET_AARCH64_UNKNOWN_LINUX_GNU_LINKER=aarch64-linux-gnu-gcc
export CARGO_TARGET_AARCH64_UNKNOWN_LINUX_GNU_AR=aarch64-linux-gnu-ar

# ─── ALSA クロスコンパイル用環境変数 ───
export PKG_CONFIG_ALLOW_CROSS=1
export PKG_CONFIG_PATH="/usr/lib/aarch64-linux-gnu/pkgconfig"

# ─── NativeAOT Publish ───
cd "$PROJECT_DIR"
echo "Publishing for linux-arm64..."
dotnet publish -c Release -r linux-arm64

# Rust側でのビルド成功確認
RUST_SO_FILE="$REPO_ROOT/native/nakuru_audio/target/aarch64-unknown-linux-gnu/release/libnakuru_audio.so"
if [ ! -f "$RUST_SO_FILE" ]; then
    echo "Error: libnakuru_audio.so not found at $RUST_SO_FILE"
    echo "Rust build may have failed. Check above for compilation errors."
    exit 1
fi

PUBLISH_DIR="$PROJECT_DIR/bin/Release/net10.0/linux-arm64/publish"

if [ -d "$PUBLISH_DIR" ]; then
    echo ""
    echo "Publish successful!"
    echo "Output: $PUBLISH_DIR"

    # ファイルサイズ表示
    EXE_FILE="$PUBLISH_DIR/NakuruTool"
    SO_FILE="$PUBLISH_DIR/libnakuru_audio.so"

    [ -f "$EXE_FILE" ] && echo "  NakuruTool: $(du -h "$EXE_FILE" | cut -f1)"
    [ -f "$SO_FILE" ] && echo "  libnakuru_audio.so: $(du -h "$SO_FILE" | cut -f1)"

    # USER_GUIDE を同梱
    USER_GUIDE_SRC="$REPO_ROOT/USER_GUIDE.url"
    if [ -f "$USER_GUIDE_SRC" ]; then
        cp "$USER_GUIDE_SRC" "$PUBLISH_DIR/"
        echo "  Included USER_GUIDE"
    fi

    # プリセットを同梱
    PRESETS_SRC="$REPO_ROOT/presets"
    PRESETS_DST="$PUBLISH_DIR/presets"
    if [ -d "$PRESETS_SRC" ]; then
        rm -rf "$PRESETS_DST"
        cp -r "$PRESETS_SRC" "$PRESETS_DST"
        echo "  Included presets folder"
    fi

    # デバッグシンボル削除
    find "$PUBLISH_DIR" -name "*.dbg" -o -name "*.pdb" | xargs -r rm -f
    echo "  Removed debug symbols"

    # バージョン付き・RID 付きフォルダにコピー
    VERSION=$(grep -oP '<Version>\K[^<]+' "$PROJECT_DIR/NakuruTool_Avalonia_AOT.csproj" | head -1)
    if [ -n "$VERSION" ]; then
        VERSIONED_DIR="$REPO_ROOT/NakuruTool_${VERSION}_linux-arm64"
        rm -rf "$VERSIONED_DIR"
        cp -r "$PUBLISH_DIR" "$VERSIONED_DIR"
        echo "  Copied to: $VERSIONED_DIR"
    fi
else
    echo "Publish failed!"
    exit 1
fi