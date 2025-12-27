# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 会話ガイドライン

- トークン節約のため思考は英語で行うが回答は日本語で
- いい質問ですね！等の私に対する共感は不要です

## プロジェクト概要
NakuruTool - osu!のbeatmapコレクション編集ツール。各DBファイルを読み込みリスト表示、collection.dbに書き込みを行うAvaloniaアプリケーション

## ソフトウェアスタック
- Avalonia
- .NET 10
- R3
- semi.Avalonia
- Pure.DI
- CommunityToolkit.Mvvm
- NativeAOT
- JsonSerializer
- OsuParsers
- ZLinq

# 作業方針
- このLLMが作成された段階の情報は古いです。web検索を活用して最新のベストプラクティスを必ず調査してから作業してください。
- NativeAOTを使用するため、動的なコード生成は禁止です。
- 必ず日本語で回答してください。
- R3を使ったイベント監視のライフサイクル管理を心がけてください。
  R3の拡張クラスはR3Extensionsにまとめてあります。適宜更新してください。