# NakuruTool アーキテクチャ概要

> **対象読者**: 新規参画者・LLMエージェント  
> **目的**: プロジェクト全体像の迅速な把握

---

## 1. プロジェクト概要

osu!mania の beatmap を管理し、各種データベースファイルを読み込み、譜面をフィルタリングしてカスタムコレクションを生成する Avalonia デスクトップアプリケーション。

### 主要機能

- **DB読み込み**: `osu!.db`、`collection.db`、`scores.db` の3種を並列パース
- **譜面一覧表示**: ページング付きリスト表示、スコアデータの統合表示
- **高度なフィルタリング**: 最大8条件の複合フィルタ、プリセット保存/復元
- **コレクション生成**: フィルタ結果から `collection.db` を生成・書き込み
- **エクスポート/インポート**: コレクションを JSON 形式でファイルに書き出し・読み込み
- **レート変更譜面生成**: beatmapの倍速・減速版を自動生成（.osuタイミング変換 + オーディオレート変換）。一括生成では生成済み譜面のMD5・メタ情報を集約した `CollectionExchangeData` 互換 JSON を `imports/rate-generation/` へサイドカー出力し、ImportExport 画面からそのまま取り込み可能
- **オーディオプレビュー**: 譜面選択時に音声を自動再生（Rust ネイティブライブラリ）
- **7K 譜面解析**: Mania Tracker の計算で、全 7K 譜面（等速）の Prism 基準Overall（Reg / LN、精度 96.5%）・5ジャンルのジャンル濃度と基準スキル値・段位推定（Reg / LN とジャンル）を求め、beatmap一覧の Prism 列・段位推定列・フィルタ・ソートに使う（仕様: [ANALYZE_7K_SPEC.md](ANALYZE_7K_SPEC.md)）
- **多言語対応**: 日本語/英語の動的切り替え
- **テーマ切替**: ダーク/ライトテーマ
- **表示倍率**: WQHD で開発した画面を、メインモニターの論理解像度に合わせて自動で縮小（FHD = 100%、開発時の大きさ = 125%、下限 95%）。設定画面で固定倍率（75 / 100 / 125 / 150%）にも変更可能

---

## 2. 技術スタック

### NuGet パッケージ

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| Avalonia | 12.1.6 | UIフレームワーク |
| Avalonia.Desktop | 12.1.6 | デスクトップランタイム |
| Avalonia.Fonts.Inter | 12.1.6 | Inter フォント |
| AvaloniaUI.DiagnosticsSupport | 2.2.3 | デバッグ用DevTools（Debug構成のみ、`AttachDeveloperTools()`） |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM Source Generator（`[ObservableProperty]`、`[RelayCommand]`） |
| HotAvalonia | 3.1.5 | XAML ホットリロード（Debug構成のみ） |
| Material.Icons.Avalonia | 3.0.2 | Material Design アイコン（3.x の既定サイズ変更は `App.axaml` の互換スタイルで吸収） |
| Pure.DI | 2.5.7 | コンパイル時DI（Source Generator） |
| R3 | 1.3.1 | リアクティブプログラミング |
| R3Extensions.Avalonia | 1.3.1 | R3 の Avalonia バインディング |
| Semi.Avalonia | 12.1.0.1 | Semi Design テーマ |
| Semi.Avalonia.DataGrid | 12.1.0.1 | DataGrid テーマ |
| ZLinq | 1.6.1 | ゼロアロケーション LINQ |

### ネイティブライブラリ

| ライブラリ | 言語 | 用途 |
|-----------|------|------|
| nakuru_audio | Rust | オーディオ再生エンジン（P/Invoke経由） |
| nakuru_7k_analyze（analyze_7k.dll） | Rust | 7K 譜面解析（Mania Tracker の Prism / LN スキル / 段位推定を P/Invoke 経由で呼ぶ。MinaCalc は同梱しない） |
| FFmpeg | C | オーディオレート変換・エンコード（n8.1 LGPL static build, subprocess 経由, LGPL-2.1-or-later。同梱 `LICENSE.txt` は LGPL v3 本文） |

---

## 3. ディレクトリ構造

### ソリューション構成

`NakuruTool_Avalonia_AOT.slnx` に以下2プロジェクトを含む:

- **NakuruTool_Avalonia_AOT** — メインアプリケーション
- **NakuruTool_Avalonia_AOT.Tests** — スクリーンショットテスト

### Features/ 配下のモジュール構成

```
Features/
├── AudioPlayer/       # オーディオ再生（Rust FFI連携）
├── BeatmapGenerator/  # レート変更譜面生成（FFmpeg subprocess 連携、.osu 時間軸変換、DT/NCモード、MP3/OGG/WAV出力。.osb はレート変換せず raw コピーで .osz に同梱。一括生成は生成 .osu の MD5/メタを集めて ImportExport 互換 JSON を sidecar 出力）
├── ImportExport/      # コレクションJSON形式エクスポート/インポート
│   └── Models/        #   CollectionExchangeData, ExportCollectionItem, ImportFileItem, ImportExportBeatmapItem
├── Licenses/          # ライセンス表示画面
├── MainWindow/        # メインウィンドウ（DrawerPage サイドバー + NavigationPage、読み込みオーバーレイ）
├── Analyze7k/         # 7K 譜面解析（Rust FFI・バックグラウンド解析・キャッシュ・段位ジャンル判定・Prism のジャンル表示）
├── MapList/           # 譜面一覧・フィルタ・コレクション生成
│   └── Models/        #   FilterCondition, FilterPreset
├── OsuDatabase/       # osu! DB パーサー群・データモデル
├── Settings/          # 設定画面・設定永続化
├── Shared/            # 共通基盤
│   ├── Converters/    #   値コンバーター（13種）
│   ├── Extensions/    #   R3Extensions
│   └── ViewModels/    #   ViewModelBase
└── Translate/         # 多言語対応（LanguageService, TranslateExtension）
    └── Resources/     #   言語JSON
```

### その他のディレクトリ

```
native/
├── nakuru_audio/      # Rust オーディオライブラリ
│   └── src/lib.rs
├── nakuru_7k_analyze/ # Rust 7K 譜面解析ライブラリ → analyze_7k.dll（Mania Tracker の FFI アダプタ。Rust 1.96.0 固定）
│   ├── src/lib.rs, src/analyze.rs
│   ├── vendor/leoblack-parser/  # 時刻順修正を当てたパーサー（[patch]）
│   ├── vendor/leoblack-ett/     # MinaCalc を外す代替クレート（[patch]）
│   └── UPSTREAM.md, THIRD-PARTY-NOTICES.md, licenses/
└── ffmpeg/win-x64/    # FFmpeg LGPL static ビルド（subprocess 実行用）
    ├── ffmpeg.exe
    ├── LICENSE.txt    # LGPL v3 全文（BtbN 同梱。FFmpeg の LGPL-2.1-or-later における "or later" 節により v3 を選択）
    └── NOTICE.txt     # 採用バージョン / SHA256 / 同梱コーデック

docs/                  # ドキュメント群
requirement/           # 要件定義
```

---

## 4. アーキテクチャパターン

### 4.1 MVVM

#### View / ViewModel 対応表

| View | ViewModel |
|------|-----------|
| `MainWindowView` | `MainWindowViewModel` |
| `DatabaseLoadingView` | `DatabaseLoadingViewModel` |
| `MapListPageView` | `MapListPageViewModel` |
| `MapListView` | `MapListViewModel` |
| `MapFilterView` | `MapFilterViewModel` |
| `ImportExportPageView` | `ImportExportPageViewModel` |
| `ExportView` | `ExportViewModel` |
| `ImportView` | `ImportViewModel` |
| `ImportExportBeatmapListView` | `ImportExportBeatmapListViewModel` |
| `SettingsPage` | `SettingsViewModel` |
| `LicensesPage` | `LicensesViewModel` |
| `BeatmapGenerationPageView` | `BeatmapGenerationPageViewModel` |
| `CollectionSelectorView` | `CollectionSelectorViewModel` |
| `RateGenerationView` | `RateGenerationViewModel` |
| `SingleBeatmapGenerationOverlayView` | `SingleBeatmapGenerationViewModel` |

#### ViewModelBase の責務

- `ObservableObject`（CommunityToolkit.Mvvm）を継承
- `CompositeDisposable` を保持し、R3 購読のライフサイクルを管理
- `Dispose()` で全購読を一括解放
- `LanguageService.Instance` への参照を提供（`LangServiceInstance`）

#### ViewModel 階層図

譜面一覧系 ViewModel は `BeatmapListViewModelBase`（abstract）に共通責務（ページング・ContextMenu コマンド・Mod / ScoreSystem 切替・`PreferUnicode` 監視）を集約し、`MapListViewModel` と `BeatmapGenerationPageViewModel` がこれを継承する。

```
ViewModelBase
└── BeatmapListViewModelBase  (abstract, IBeatmapListViewModel 実装)
    ├── MapListViewModel              (フィルタ / AudioPlayer ナビゲート)
    └── BeatmapGenerationPageViewModel(コレクション解決 / バッチレート生成)
```

DI バインドは `IMapListViewModel → MapListViewModel` のまま不変（`IMapListViewModel` は `IBeatmapListViewModel` を継承する形に内部仕様で縮小済み）。

#### コンパイル済みバインディング

- csproj で `AvaloniaUseCompiledBindingsByDefault` = `true` を設定
- 各 View の XAML に `x:DataType` を指定して型安全なバインディングを実現
- NativeAOT 環境でのリフレクションベースバインディングを排除

### 4.2 DI 構成（Pure.DI）

#### Composition.cs の登録一覧

**ViewModel（全て Singleton）**

- `MainWindowViewModel`
- `ISettingsViewModel` → `SettingsViewModel`
- `IDatabaseLoadingViewModel` → `DatabaseLoadingViewModel`
- `IMapListViewModel` → `MapListViewModel`
- `MapListPageViewModel`
- `AudioPlayerViewModel`
- `ILicensesViewModel` → `LicensesViewModel`
- `ImportExportPageViewModel`

**Service（全て Singleton）**

- `ISettingsService` → `SettingsService`
- `IDatabaseService` → `DatabaseService`
- `IGenerateCollectionService` → `GenerateCollectionService`
- `IFilterPresetService` → `FilterPresetService`
- `IAudioPlayerService` → `AudioPlayerService`
- `IImportExportService` → `ImportExportService`

**Root（エントリポイント）**

- `MainWindowView` — `.Root<MainWindowView>("MainWindow")` として登録

#### 初期化フロー

```
Program.Main()
  → AppBuilder.Configure<App>()
    → App.Initialize()
      → AvaloniaXamlLoader.Load() / SemiTheme ロケール設定
    → App.OnFrameworkInitializationCompleted()
      → new Composition()
      → desktop.MainWindow = composition.MainWindow
        → MainWindowView + 全依存 ViewModel/Service がコンストラクタ注入で解決
```

### 4.3 リアクティブプログラミング（R3）

#### 主な使用パターン

| パターン | 概要 |
|---------|------|
| `Subject<T>` | イベントの手動発行（進捗通知など） |
| `ObserveProperty()` | 特定プロパティの変更を監視 |
| `ObservePropertyChanged()` | 全プロパティの変更を監視 |
| `Subscribe().AddTo(Disposables)` | 購読をライフサイクルに紐付け |
| `Throttle()` / `Debounce()` | 高頻度イベントの間引き |
| `Observable.FromEvent()` | .NET イベントを Observable に変換 |

#### R3Extensions カスタムメソッド一覧

| メソッド | 引数 | 用途 |
|---------|------|------|
| `ObserveProperty<T>()` | `source: T`, `propertyName: string` | 特定プロパティの `PropertyChanged` を `Observable` に変換 |
| `ObservePropertyChanged<T>()` | `source: T` | 全プロパティの `PropertyChanged` を `Observable` に変換 |
| `ObservePropertyAndSubscribe<T>()` | `source: T`, `propertyName: string`, `action: Action`, `disposables: CompositeDisposable` | プロパティ変更の監視＋購読＋ライフサイクル管理を一括実行 |
| `ObserveCollectionChanged<T>()` | `source: AvaloniaList<T>` | `AvaloniaList` の `CollectionChanged` を `Observable` に変換 |
| `ObserveElementPropertyChanged<T>()` | `source: AvaloniaList<T>` | `AvaloniaList` 内の各要素の `PropertyChanged` を監視（要素の追加・削除に自動追従） |

---

## 5. モジュール依存関係図

```mermaid
graph TD
    MW[MainWindow] --> Settings
    MW --> OsuDatabase
    MW --> MapList
    MW --> ImportExport
    MW --> Licenses
    MW --> BeatmapGenerator
    BeatmapGenerator --> OsuDatabase
    BeatmapGenerator --> Settings
    BeatmapGenerator -->|ISettingsService| Settings
    MapList --> OsuDatabase
    MapList --> AudioPlayer
    MapList --> Shared
    ImportExport --> OsuDatabase
    ImportExport --> Translate
    AudioPlayer --> Settings
    AudioPlayer --> NativeLib["nakuru_audio (Rust)"]
    Analyze7k --> OsuDatabase
    Analyze7k --> Settings
    Analyze7k --> Analyze7kNative["analyze_7k.dll (Rust)"]
    MapList --> Analyze7k
    BeatmapGenerator --> FFmpeg["FFmpeg (subprocess, LGPL build)"]
    OsuDatabase --> Settings
    Settings --> Translate
    Translate -.->|参照| MW
    Translate -.->|参照| MapList
    Translate -.->|参照| OsuDatabase
    Translate -.->|参照| ImportExport
    Shared -.->|参照| MW
    Shared -.->|参照| MapList
    Shared -.->|参照| ImportExport
```

---

## 6. UI テーマ・スタイリング

| 項目 | 設定 |
|------|------|
| テーマ | Semi.Avalonia（`SemiTheme`） |
| DataGrid テーマ | Semi.Avalonia.DataGrid（`DataGridSemiTheme`） |
| アイコン | Material.Icons.Avalonia（`MaterialIconStyles`） |
| フォントファミリー | `Meiryo, Yu Gothic UI, Hiragino Sans, Noto Sans CJK JP, sans-serif` |
| デフォルトロケール | `ja-JP` |
| テーマバリアント | `Default`（システム追従）、`Dark` / `Light` 切替可能 |
| UI 表示倍率 | WQHD（2560×1440・拡大率 100%）で開発し、各 .axaml に数値を直接記述。倍率の表記は FHD = 100%、開発時の大きさ = 125%。各ウィンドウのルートの `UiScaleHost`（`LayoutTransformControl`）で一括拡大・縮小し、ポップアップは `Popup.InheritsTransform` で倍率を引き継ぐ（詳細: [MODULES.md](MODULES.md) の Shared / Scaling） |

---

## 関連ドキュメント

| ドキュメント | 内容 |
|-------------|------|
| [MODULES.md](MODULES.md) | Feature モジュールの詳細仕様 |
| [DATA_FLOW.md](DATA_FLOW.md) | データフローと状態管理 |
| [NATIVE_AOT.md](NATIVE_AOT.md) | NativeAOT 対応ガイドライン |
| [TESTING.md](TESTING.md) | テスト戦略・実行方法 |
| [BUILD.md](BUILD.md) | ビルド・環境構築ガイド |
