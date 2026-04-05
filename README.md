# NakuruTool

osu!mania beatmap管理ツール

## 概要

NakuruToolは、osu!のbeatmapを管理するためのデスクトップアプリケーションです。collection.dbファイルの読み込み・編集・書き込みに対応しています。

使用方法は [USER_GUIDE.md](USER_GUIDE.md) を参照してください。

## 技術スタック

- **UI Framework**: Avalonia UI 11.3.12
- **Runtime**: .NET 10.0 with NativeAOT
- **Audio Playback**: nakuru_audio (Rust + rodio)
- **Reactive Programming**: R3
- **Dependency Injection**: Pure.DI
- **UI Theme**: Semi.Avalonia

## ビルド要件

### 前提条件

1. **.NET 10.0 SDK**
   - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)

2. **Rust Toolchain**
   - [Download rustup](https://rustup.rs/)
   - インストール後、`cargo --version`で確認

3. **Visual Studio 2026** (Windows)
   - Community Edition以上
   - 必要なコンポーネント:
     - C++デスクトップ開発
     - Windows 10/11 SDK
     - MSVC v143 ビルドツール

### クイックスタート

```powershell
# Debugビルド
dotnet build -c Debug

# NativeAOT Publishビルド (Windows)
.\publish.ps1
# または
publish.bat
```

### 詳細なビルド手順

詳細なセットアップとトラブルシューティングについては、[docs/BUILD.md](docs/BUILD.md)を参照してください。

## プロジェクト構成

```
NakuruTool_Avalonia_AOT/
├── native/
│   └── nakuru_audio/          # Rust製オーディオライブラリ
│       ├── Cargo.toml
│       ├── build.rs
│       └── src/lib.rs
├── NakuruTool_Avalonia_AOT/
│   └── NakuruTool_Avalonia_AOT/
│       ├── Features/          # 機能別モジュール
│       │   ├── AudioPlayer/
│       │   ├── MainWindow/
│       │   └── ...
│       └── NakuruTool_Avalonia_AOT.csproj
├── publish.ps1              # NativeAOT publishスクリプト
└── publish.bat              # NativeAOT publishスクリプト (cmd)
```

## ライセンス

このリポジトリのオリジナルソースコード（C#、Rust 自作部分、AXAML 等）は [MIT License](LICENSE) でライセンスされています。

配布バイナリ（アプリケーションパッケージ）には、それぞれ固有のライセンスを持つ第三者コンポーネントが含まれます。
アプリ全体が MIT のみでライセンスされているわけではありません。

- 第三者コンポーネントの一覧と各ライセンス: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
- nakuru_audio (Rust) の依存関係: [native/nakuru_audio/THIRD-PARTY-NOTICES.md](native/nakuru_audio/THIRD-PARTY-NOTICES.md)
- アプリケーション内ライセンスページ: 設定 → ライセンス
