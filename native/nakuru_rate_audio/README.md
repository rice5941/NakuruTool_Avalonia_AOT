# nakuru_rate_audio

NakuruTool 用のオフラインオーディオレート変換 DLL (cdylib)。

## 概要

osu! 譜面の DT/NC レート変換に使用するオーディオ処理ライブラリ。
Symphonia でデコード → Bungee でタイムストレッチ → WAV/OGG/MP3 でエンコードのパイプラインを提供する。

## ビルド

```bash
# 事前に vendor/bungee を git submodule で取得
git submodule update --init --recursive

# ビルド
cargo build --release
```

## 依存関係

- **Bungee** — タイムストレッチエンジン (vendor/bungee, cmake ビルド)
- **Symphonia** — 純 Rust オーディオデコーダー
- **hound** — WAV エンコーダー (feature: wav)
- **vorbis_rs** — OGG Vorbis エンコーダー (feature: ogg)
- **libmp3lame** — MP3 エンコーダー、動的ロード (feature: mp3)

## ライセンス

MIT License
