# テスト戦略

## 1. テストフレームワーク

xUnit をベースに、Avalonia のヘッドレステスト機能を組み合わせたスクリーンショットテスト環境を構築している。

### NuGetパッケージ一覧

| パッケージ | バージョン | 用途 |
|---|---|---|
| Microsoft.NET.Test.Sdk | 17.14.1 | テストSDK基盤 |
| xunit | 2.9.3 | テストフレームワーク本体 |
| xunit.runner.visualstudio | 3.1.0 | Visual Studio / dotnet test 連携 |
| coverlet.collector | 6.0.4 | コードカバレッジ収集 |
| Avalonia.Headless.XUnit | 11.3.10 | Avalonia ヘッドレステスト統合 |
| Avalonia.Skia | 11.3.10 | Skia レンダラー（スクリーンショット撮影に必要） |
| Semi.Avalonia | 11.3.7.1 | テーマ（ヘッドレス環境でも必要） |
| Semi.Avalonia.DataGrid | 11.3.7.1 | DataGrid テーマ |
| Irihi.Ursa.Themes.Semi | 1.14.0 | Ursa Semi テーマ |
| Material.Icons.Avalonia | 2.4.1 | アイコン |

テストプロジェクトはメインプロジェクトを `ProjectReference` で参照している。

---

## 2. テスト環境構成

### TestAppBuilder

`TestAppBuilder` クラスで Avalonia の `AppBuilder` を構成する。

- **UseSkia**: Skia レンダラーを有効化し、`CaptureRenderedFrame()` によるスクリーンショット撮影を可能にする
- **UseHeadless**: ヘッドレスプラットフォームを使用。`UseHeadlessDrawing = false` を指定し、実際のレンダリングを有効化している

### TestApp

`TestApp` はテスト専用の `Application` クラス。XAML で以下のテーマ・スタイルを適用している。

- `SemiTheme`（Locale: ja-JP）
- `DataGridSemiTheme`
- Ursa `SemiTheme`（Locale: ja-JP）
- `MaterialIconStyles`
- Window のフォントファミリー設定（Meiryo, Yu Gothic UI 等）

これにより、本番アプリケーションと同等の見た目でスクリーンショットを撮影できる。

### アセンブリ属性

`TestAppBuilder.cs` にて `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` を宣言し、テストプロジェクト全体で `TestApp` を使用するよう設定している。

---

## 3. スクリーンショットテスト

### 3.1 書き方

**テスト属性**

- 単一テストケース: `[AvaloniaFact]` 属性を使用
- パラメータ化テスト: `[AvaloniaTheory]` + `[InlineData]` 属性を使用

**基本的な手順**

1. モック ViewModel / Service を作成し、テストデータを設定する
2. 対象の View を生成し、`DataContext` にモック ViewModel を設定する
3. `Window` に View を配置して `Show()` で表示する
4. `Dispatcher.UIThread.RunJobs()` と `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` でレンダリングを完了させる
5. `window.CaptureRenderedFrame()` でフレームをキャプチャする
6. `Screenshots/` ディレクトリに PNG として保存する
7. `Assert.True(File.Exists(...))` でファイル生成を検証する
8. `window.Close()` でクリーンアップする

**DataGrid バインディングの注意点**

ヘッドレス環境ではコンパイル済みバインディングが正しく動作しない場合がある。その際は `dataGrid.ItemsSource` を直接設定し、再度レンダリングを実行する。

### 3.2 既存テスト一覧

#### MainWindowViewScreenshotTests

| テストメソッド | 属性 | 検証内容 |
|---|---|---|
| `CaptureMainWindowView_WithMapList` | `[AvaloniaFact]` | MapList タブを選択した状態の MainWindowView |
| `CaptureMainWindowView_WithMapList_MenuCollapsed` | `[AvaloniaFact]` | サイドメニューを閉じた状態の MainWindowView |
| `CaptureMainWindowView_LoadingOverlay` | `[AvaloniaFact]` | 読み込みオーバーレイ表示状態（各 DB の進捗表示あり） |
| `CaptureMainWindowView_DifferentSizes` | `[AvaloniaTheory]` | 異なるウィンドウサイズ（800×600, 1280×720, 1920×1080） |

#### MapListViewScreenshotTests

| テストメソッド | 属性 | 検証内容 |
|---|---|---|
| `CaptureMapListView_Empty` | `[AvaloniaFact]` | データなしの空状態 |
| `CaptureMapListView_WithData` | `[AvaloniaFact]` | テストデータ表示状態（DataGrid バインディングの検証含む） |
| `CaptureMapListView_DifferentSizes` | `[AvaloniaTheory]` | 異なるウィンドウサイズ（800×600, 1280×720, 1920×1080） |
| `CaptureMapListView_Pagination_Page2` | `[AvaloniaFact]` | ページネーション 2ページ目（PageSize=5, 全15件） |
| `CaptureMapListView_Pagination_Pages` | `[AvaloniaTheory]` | ページネーション各ページ（Page1, Page2, Page3） |

---

## 4. モック戦略

### モックインターフェース一覧

テストファイル内で以下のインターフェースに対するモック実装が定義されている。

| モッククラス | 実装インターフェース | 概要 |
|---|---|---|
| `MockSettingsViewModel` | `ISettingsViewModel` | 言語キーリスト・フォルダパスの固定値を返す |
| `MockDatabaseLoadingViewModel` | `IDatabaseLoadingViewModel` | 読み込み進捗のプロパティを直接設定可能 |
| `MockMapListViewModel` | `IMapListViewModel` | `SetTestData()` でテストデータを注入、ページング処理を自前実装 |
| `MockMapListPageViewModel` | `MapListPageViewModel`（継承） | 各種モック Service をコンストラクタに渡して生成 |
| `MockLicensesViewModel` | `ILicensesViewModel` | 空のライセンスリストを返す |
| `MockSettingsService` | `ISettingsService` | 固定の設定データを返す |
| `MockSettingsData` | `ISettingsData` | `OsuFolderPath` と `LanguageKey` の固定値 |
| `MockDatabaseService` | `IDatabaseService` | 空の Beatmaps / OsuCollections を返す |
| `MockGenerateCollectionService` | `IGenerateCollectionService` | 常に成功を返す |
| `MockFilterPresetService` | `IFilterPresetService` | 操作を受け付けるが何もしない |
| `MockAudioPlayerViewModel` | `AudioPlayerViewModel`（継承） | モック AudioPlayerService を使用 |
| `MockAudioPlayerService` | `IAudioPlayerService` | `ReactiveProperty` で状態管理する最小実装 |

### モック実装パターン

- **インターフェースベース**: 各 ViewModel / Service はインターフェース（`IMapListViewModel`, `IDatabaseService` 等）を持つため、テスト用の軽量実装に差し替えられる
- **継承ベース**: `MapListPageViewModel` や `AudioPlayerViewModel` はインターフェースを持たないため、クラスを継承しモック Service をコンストラクタに渡す
- **DI を介さない直接注入**: Pure.DI の `Composition` クラスは使用せず、テストコード内で直接モックインスタンスを生成し、View の `DataContext` に設定する

---

## 5. テスト実行方法

### コマンド

テストプロジェクトのディレクトリで以下を実行する。

```
dotnet test
```

ソリューションルートから実行する場合:

```
dotnet test NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT.Tests
```

### 出力ファイルの確認

スクリーンショットは以下のディレクトリに保存される。

```
NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT.Tests/bin/Debug/net10.0/Screenshots/
```

出力ファイル名の例:
- `MainWindowView_MapList.png`
- `MainWindowView_MapList_MenuCollapsed.png`
- `MainWindowView_LoadingOverlay.png`
- `MainWindowView_HD_1280x720.png`
- `MapListView_Empty.png`
- `MapListView_WithData.png`
- `MapListView_Small_800x600.png`
- `MapListView_Pagination_Page2.png`

---

## 6. 今後の拡張方針

### ドメインロジック単体テストの追加余地

現在のテストはスクリーンショット（ビジュアル）テストのみで構成されている。以下の領域にロジック単体テストを追加することで、品質を向上できる。

- **FilterCondition.Matches()**: 各フィルタ対象・比較タイプの組み合わせに対する正確なマッチング検証
- **ページング処理**: `MapListViewModel` のページ計算・境界値テスト

### DBパーサー・GenerateCollectionService の検証

- **OsuDbParser / CollectionDbParser / ScoresDbParser**: テスト用バイナリファイルを用意し、パース結果の正確性を検証
- **GenerateCollectionService**: collection.db への書き込み結果を読み戻して検証（同名コレクション置換ロジック等）
- **DatabaseService**: バックアップ作成やスコアデータ統合の検証

---

## 関連ドキュメント

- [アーキテクチャ概要](ARCHITECTURE.md)
- [モジュール詳細](MODULES.md)
- [データフローと状態管理](DATA_FLOW.md)
- [NativeAOT対応ガイドライン](NATIVE_AOT.md)
- [ビルドガイド](BUILD.md)
