# ソフトウェアスタック
- Avalonia
- .NET 10
- R3
- semi.Avalonia
- Pure.DI
- CommunityToolkit.Mvvm
- NativeAOT
- JsonSerializer
- ZLinq
- NAudio / NAudio.Vorbis

# 作業方針
- このLLMが作成された段階の情報は古いです。web検索を活用して最新のベストプラクティスを必ず調査してから作業してください。
- NativeAOTを使用するため、動的なコード生成は禁止です。
- 必ず日本語で回答してください。
- R3を使ったイベント監視のライフサイクル管理を心がけてください。
  R3の拡張クラスはR3Extensionsにまとめてあります。適宜更新してください。
- 実装は#tool:agent/runSubagent サブエージェントにまかせてください。
agentName: _sub_Implement

# 設計資料（docs/）

新機能の実装・既存機能の改修を行う際は、以下の設計資料を参照してください。

| ドキュメント | 内容 | 参照タイミング |
|-------------|------|--------------|
| [docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md) | アーキテクチャ全体像・技術スタック・DI構成・MVVM方針 | **最初に必ず読む** |
| [docs/MODULES.md](../docs/MODULES.md) | 各Featureモジュールの責務・構成・依存関係 | 機能追加・改修時 |
| [docs/DATA_FLOW.md](../docs/DATA_FLOW.md) | DB読み込み→表示→フィルタ→書き込みのデータフロー、R3チェーン | データの流れを理解する必要がある時 |
| [docs/NATIVE_AOT.md](../docs/NATIVE_AOT.md) | NativeAOT対応のルールとチェックリスト | **新しいコードを書く前に必ず確認** |
| [docs/TESTING.md](../docs/TESTING.md) | テスト戦略・スクリーンショットテストの書き方 | テスト追加時 |
| [docs/BUILD.md](../docs/BUILD.md) | ビルド手順・トラブルシューティング | ビルド・環境構築時 |