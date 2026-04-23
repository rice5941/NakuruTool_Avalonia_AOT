# Featureモジュール詳細

本ドキュメントでは `Features/` 配下の各モジュールの責務・構成ファイル・依存関係・主要挙動を記述する。アーキテクチャ全体像は [ARCHITECTURE.md](ARCHITECTURE.md) を参照。

---

## 1. MainWindow

### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `MainWindowViewModel.cs` | ViewModel | タブナビゲーション制御、読み込みオーバーレイ制御 |
| `MainWindowView.axaml` | View | 左サイドバータブ＋オーバーレイのレイアウト定義 |
| `MainWindowView.axaml.cs` | CodeBehind | ウィンドウのOpened イベントから `StartLoadingAsync` を呼び出し |

### 責務

- アプリケーションのルートウィンドウとして機能
- 左サイドバーのタブナビゲーション（MapList, Import/Export, Settings, Licenses）
- データベース読み込み中のオーバーレイ表示制御（`IsLoadingOverlayVisible`）
- 読み込みエラー発生時の設定タブへの自動遷移

### 依存モジュール

- **OsuDatabase** — `IDatabaseLoadingViewModel` を通じてDB読み込み進捗を表示
- **MapList** — `MapListPageViewModel` をMapListタブに配置
- **Settings** — `ISettingsViewModel` を設定タブに配置、`ISettingsService` でフォルダパス変更を監視
- **Licenses** — `ILicensesViewModel` をライセンスタブに配置
- **Shared** — `ViewModelBase` を継承
- **Translate** — `TranslateExtension` でUI文字列を多言語化

### UI構成

- **ヘッダーバー** — アプリ名表示、サイドバー折りたたみ用 `ToggleButton`
- **左サイドバー** — `TabControl`（`TabStripPlacement="Left"`）によるページ切り替え。カテゴリラベル（メイン/システム）と各ページタブで構成
- **オーバーレイ** — 半透明背景の `Border` + 中央配置のカード内に `DatabaseLoadingView` を表示

### 主要挙動

1. **起動時読み込み** — `Window.Opened` → `StartLoadingAsync()` → `DatabaseLoadingViewModel.InitialLoadAsync()` → `MapListPageViewModel.Initialize()` → オーバーレイ非表示
2. **エラー時の設定タブ遷移** — `DatabaseLoadingViewModel.HasError` が `true` の場合、`SelectedTabIndex` を設定タブ（インデックス 4）に切り替え
3. **フォルダパス変更監視** — `ISettingsData.OsuFolderPath` をR3の `ObserveProperty` で監視し、変更時に `ReloadDatabaseAsync()` を実行

### タブインデックス定数

| 定数 | 値 | 対応タブ |
|------|---|---------|
| `TabIndexMapList` | 1 | 譜面一覧 |
| `TabIndexSettings` | 4 | 設定 |

---

## 2. MapList

### 概要

譜面一覧の表示・フィルタリング・コレクション書き込みを担うモジュール。3つのViewModelと2つのModelクラス、1つのサービスクラスで構成される。

### 2.1 MapListPageViewModel（統合コントローラ）

#### 構成ファイル

| ファイル | 種別 |
|---------|------|
| `MapListPageViewModel.cs` | ViewModel |
| `MapListPageView.axaml` | View |
| `MapListPageView.axaml.cs` | CodeBehind |

#### 責務

- `MapFilterViewModel`、`MapListViewModel`、`PresetEditorViewModel` を統合し、MapListページ全体を制御
- コレクション名の入力管理
- `AddToCollectionAsync` によるcollection.db書き込みの起動
- コレクション生成時のプリセット自動保存
- 生成進捗の表示管理
- `TogglePresetEditorCommand` によるプリセット編集画面の表示切替と `MapFilterViewModel` への中継

#### 依存モジュール

- **OsuDatabase** — `IDatabaseService`, `IGenerateCollectionService`
- **AudioPlayer** — `AudioPlayerViewModel`, `AudioPlayerPanelViewModel`（高機能再生パネルとしてMapListViewModelに渡す）
- **Shared** — `ViewModelBase`
- **Translate** — コレクション生成結果メッセージの多言語化

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `FilterViewModel` | `MapFilterViewModel` | フィルタ条件管理 |
| `ListViewModel` | `MapListViewModel` | 譜面一覧管理 |
| `PresetEditorViewModel` | `PresetEditorViewModel` | プリセット編集画面VM（手動new） |
| `IsPresetEditorVisible` | `bool` | プリセット編集画面の表示切替フラグ |
| `CollectionName` | `string` | 書き込み先コレクション名 |
| `GenerationStatusMessage` | `string` | 生成進捗/結果メッセージ |
| `GenerationProgressValue` | `int` | 生成進捗値（0–100） |
| `IsGenerating` | `bool` | 生成中フラグ |
| `IsLargeCollectionConfirmVisible` | `bool` | 大量件数確認オーバーレイの表示制御 |
| `LargeCollectionConfirmMessage` | `string` | 確認メッセージ（フィルタ後件数を含む動的テキスト） |

#### 主要挙動

1. **`Initialize()`** — `FilterViewModel.RefreshCollectionNames()`, `ListViewModel.Initialize()`, `PresetEditorViewModel.RefreshCollectionNames()` を呼び出し、全譜面のフィルタ適用と表示を初期化
2. **`AddToCollectionAsync()`** — フィルタ後件数が 10,000件を超える場合はインラインオーバーレイ（`IsLargeCollectionConfirmVisible`）を表示してユーザの確認を待つ。確認後に `ExecuteAddToCollectionAsync()` を実行し `IGenerateCollectionService.GenerateCollection()` でcollection.dbに書き込み。成功時は `SavePresetIfNeeded()` でプリセットを自動保存
3. **大量件数確認コマンド** — `ConfirmLargeCollectionCommand`（生成を続行）と `CancelLargeCollectionCommand`（キャンセル）でユーザの判断を受け付ける
4. **プリセット選択時のコレクション名反映** — `FilterViewModel.SelectedPreset` の変更をR3で監視し、`CollectionName` に反映
5. **進捗監視** — `IGenerateCollectionService.GenerationProgressObservable` をR3で購読し、UI更新
6. **`TogglePresetEditorCommand`** — `IsPresetEditorVisible` を反転させ、`MapListPageView` 上でプリセット編集画面（`PresetEditorView`）と譜面一覧（`MapListView`）を排他表示。`MapFilterViewModel.TogglePresetEditorCommand` に中継される
7. **`OnIsPresetEditorVisibleChanged()`** — 編集画面を開いたときに `PresetEditorViewModel.RefreshCollectionNames()` を実行。閉じたときに `GenerationStatusMessage`/`GenerationProgressValue` をリセット

---

### 2.2 MapListViewModel（譜面一覧）

#### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `MapListViewModel.cs` | ViewModel | フィルタ済み譜面一覧・ページング・オーディオ連携・コンテキストメニュー制御 |
| `MapListView.axaml` | View | DataGrid・ページング・オーディオパネルのレイアウト定義 |
| `MapListView.axaml.cs` | CodeBehind | DataGrid右クリック時のコンテキストメニュー表示制御（行ヒットテスト・メニュー生成） |

#### 責務

- フィルタ済み譜面配列（`FilteredBeatmapsArray`）の管理
- ページング処理（表示用部分配列 `ShowBeatmaps` の管理）
- 譜面選択時のオーディオ自動再生
- オーディオパネルモードの管理と設定永続化
- フィルタ済み譜面配列のナビゲーションコンテキスト提供（AudioPlayerPanelViewModelへ）
- 前後トラック移動時のページ遷移とスクロール

#### データ構造

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `ShowBeatmaps` | `IAvaloniaReadOnlyList<Beatmap>` | 現在ページに表示中の譜面リスト |
| `FilteredBeatmapsArray` | `Beatmap[]` | フィルタ適用後の全譜面配列 |
| `TotalCount` | `int` | DB内の全譜面数 |
| `FilteredCount` | `int` | フィルタ後の譜面数 |
| `CurrentPage` | `int` | 現在ページ（1始まり） |
| `FilteredPages` | `int` | フィルタ後の総ページ数 |
| `PageSize` | `int` | 1ページあたりの表示件数（デフォルト: 20） |
| `SelectedBeatmap` | `Beatmap?` | 選択中の譜面 |
| `AudioPlayer` | `AudioPlayerViewModel` | オーディオ再生コントロール |
| `AudioPlayerPanel` | `AudioPlayerPanelViewModel` | 高機能オーディオパネル |
| `IsAudioPanelMode` | `bool` | オーディオパネル表示モード（設定に永続化） |

#### ページング仕様

- 選択可能なページサイズ: **10 / 20 / 50 / 100**（`PageSizes` プロパティ）
- デフォルトページサイズ: **20**
- ページ変更時は `UpdateShowBeatmaps()` で `FilteredBeatmapsArray` から `Span` でスライスした部分を `AvaloniaList` に転写

#### フィルタ連携

- `MapFilterViewModel.FilterChanged`（R3 `Observable<Unit>`）を購読
- 発火時に `ApplyFilter()` → `UpdateFilteredBeatmapsArray()` + `UpdateFilteredPages()` + `UpdateShowBeatmaps()`
- フィルタ実行はZLinqの `AsValueEnumerable().Where().ToArray()` で実施

#### オーディオ連携

- `SelectedBeatmap` プロパティの変更をR3の `ObserveProperty` で監視
- 変更時に `AudioPlayer.PlayBeatmapAudio(SelectedBeatmap)` を呼び出し、自動的にプレビュー再生
- `IsAudioPanelMode` が `true` の場合、選択変更時に `AudioPlayerPanel.PlayBeatmap()` を呼び出し
- `AudioPlayerPanel.NavigateToFilteredIndex` コールバック経由でページ遷移を実行
- `ApplyFilter()` 呼び出し時に `AudioPlayerPanel.SetNavigationContext()` でフィルタ結果を連携

#### コンテキストメニュー

- **コンテキストメニュー** — DataGrid行の右クリックで「ダウンロードURLをコピー」コンテキストメニューを表示。`SelectBeatmapForContextMenu()` で右クリック行を選択（`_isNavigating` で音声再生を抑制）、`TryPrepareContextMenu()` でメニュー表示判定、`CopyDownloadUrlAsync()` で `{BeatmapMirrorUrl}{BeatmapSetId}` 形式のURLをクリップボードにコピー

---

### 2.3 MapFilterViewModel（フィルタ条件）

#### 構成ファイル

| ファイル | 種別 |
|---------|------|
| `MapFilterViewModel.cs` | ViewModel |
| `MapFilterView.axaml` | View |
| `MapFilterView.axaml.cs` | CodeBehind |

#### 責務

- フィルタ条件のCRUD管理（追加・削除・クリア）
- 条件変更時の通知（R3 `Subject<Unit>`）
- 譜面に対するフィルタマッチング判定
- プリセットの読み込み・保存
- コレクション名一覧の管理（`CollectionNames`）

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Conditions` | `AvaloniaList<FilterCondition>` | 現在のフィルタ条件リスト |
| `FilterChanged` | `Observable<Unit>` | フィルタ条件変更通知 |
| `SelectedPreset` | `FilterPreset?` | 選択中のプリセット |
| `PresetsWithNone` | `AvaloniaList<FilterPreset?>` | プリセット一覧（先頭にnull=「なし」を含む） |
| `CollectionNames` | `ObservableCollection<string>` | 利用可能なコレクション名一覧（Collectionフィルタ用） |

#### 依存モジュール

- **OsuDatabase** — `IFilterPresetService` を通じてプリセットを管理、`IDatabaseService.OsuCollections` を参照してコレクション名一覧・Collectionフィルタマッチングに使用
- **Shared** — `ViewModelBase` を継承

#### 最大条件数

- **8** 条件まで追加可能（`MaxConditions = 8`）

#### R3による条件変更監視チェーン

1. `Conditions.ObserveCollectionChanged()` → 条件の追加/削除を検知 → `NotifyFilterChanged()`
2. `Conditions.ObserveElementPropertyChanged()` → 各条件のプロパティ変更を検知 → `NotifyFilterChanged()`
3. `_presetService.Presets.ObserveCollectionChanged()` → プリセット一覧の変更を検知 → `UpdatePresetsWithNone()`

#### マッチング

- `Matches(Beatmap)` — 全条件がAND結合。条件が0件の場合は全譜面にマッチ
- 各 `FilterCondition.Matches(Beatmap)` に委譲

---

### 2.4 Models

#### FilterCondition

フィルタの単一条件を表すクラス。`ObservableObject` を継承し、プロパティ変更を通知する。

##### 比較タイプ（`ComparisonType` enum）

| 値 | 概要 |
|---|------|
| `Equals` | 完全一致（文字列の場合は部分一致） |
| `Range` | 範囲指定（最小値–最大値） |

##### フィルタ対象（`FilterTarget` enum）

| 値 | データ型 | Equals対応 | Range対応 | 概要 |
|---|---------|-----------|----------|------|
| `KeyCount` | 数値（int） | ✓ | ✓ | キー数 |
| `Status` | Enum | ✓ | ✗ | ランクステータス |
| `Title` | 文字列 | ✓ | ✗ | 曲名（部分一致） |
| `Version` | 文字列 | ✓ | ✗ | 難易度名（部分一致） |
| `Artist` | 文字列 | ✓ | ✗ | アーティスト名（部分一致） |
| `Creator` | 文字列 | ✓ | ✗ | 譜面作成者（部分一致） |
| `BPM` | 数値（double） | ✓ | ✓ | BPM |
| `OD` | 数値（double） | ✓ | ✓ | Overall Difficulty |
| `HP` | 数値（double） | ✓ | ✓ | HP Drain Rate |
| `DrainTime` | 数値（int） | ✓ | ✓ | ドレイン時間（秒、`mm:ss` 形式での入力も可） |
| `Difficulty` | 数値（double） | ✗ | ✓ | 難易度（StarRating） |
| `LongNoteRate` | 数値（double） | ✗ | ✓ | LN率（入力は0–100、内部は0.0–1.0） |
| `BestAccuracy` | 数値（double） | ✓ | ✓ | 最高精度 |
| `BestScore` | 数値（int） | ✓ | ✓ | 最高スコア |
| `IsPlayed` | Bool | ✓ | ✗ | プレイ済みか |
| `LastPlayed` | 日付 | ✗ | ✓ | 最終プレイ日 |
| `LastModifiedTime` | 日付 | ✗ | ✓ | 最終更新日 |
| `PlayCount` | 数値（int） | ✓ | ✓ | プレイ回数 |
| `Collection` | 文字列 | ✓ | ✗ | コレクション名（部分一致ではなく完全一致でコレクション内の譜面に一致） |

##### `FilterTargetInfo` ヘルパークラス

`FilterTarget` に対してデータ型や対応比較タイプを判定する静的メソッド群:

- `SupportsRange(FilterTarget)` — 範囲指定をサポートするか
- `SupportsEquals(FilterTarget)` — 等価比較をサポートするか
- `IsDateType(FilterTarget)` — 日付型か
- `IsCollectionType(FilterTarget)` — コレクション型か（`Collection` のみ）
- `IsNumericType(FilterTarget)` — 数値型か
- `IsStringType(FilterTarget)` — 文字列型か
- `IsBoolType(FilterTarget)` — Bool型か
- `IsEnumType(FilterTarget)` — Enum型か

##### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Id` | `string` | 一意のID（`Guid`、RadioButtonの `GroupName` 用） |
| `Target` | `FilterTarget` | フィルタ対象 |
| `ComparisonType` | `ComparisonType` | 比較タイプ |
| `Value` | `string` | 等価比較値、または範囲の最小値 |
| `ValueMax` | `string` | 範囲の最大値 |
| `StatusValue` | `BeatmapStatus` | Status用の選択値 |
| `BoolValue` | `bool` | IsPlayed用の選択値 |
| `CollectionValue` | `string` | Collection用の選択コレクション名 |
| `DateValue` / `DateValueMax` | `DateTime?` | 日付型の値（CalendarDatePicker連携用） |

##### `Matches(Beatmap)` メソッド

`Target` に応じて以下のマッチングメソッドに分岐:

- `MatchesString` — 大文字小文字無視の `Contains` による部分一致
- `MatchesNumeric` — int値の完全一致または範囲判定
- `MatchesDouble` — double値の近似一致（誤差 < 0.001）または範囲判定
- `MatchesLongNoteRate` — 入力値を100で割って内部値と比較
- `MatchesDateTime` — 日付部分のみで一致または範囲判定

#### FilterPreset

フィルタプリセット（フィルタ条件のセット + コレクション名）を表すモデルクラス。

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Name` | `string` | プリセット名（ファイル名としても使用） |
| `CollectionName` | `string` | コレクション名 |
| `Conditions` | `List<FilterConditionData>` | フィルタ条件のシリアライズ用データリスト |

#### FilterConditionData

`FilterCondition` のシリアライズ用データクラス。Enum値は文字列で保存する。

- `FromFilterCondition(FilterCondition)` — `FilterCondition` からデータを作成
- `ToFilterCondition()` — `FilterCondition` に復元。Enumパース失敗時はデフォルト値を使用

#### FilterPresetJsonContext

NativeAOT対応のJSON Source Generatorコンテキスト。`FilterPreset`, `List<FilterPreset>`, `FilterConditionData` のシリアライズに対応。

---

### 2.5 FilterPresetService

#### 構成ファイル

| ファイル | 種別 |
|---------|------|
| `FilterPresetService.cs` | サービス |

#### 責務

- フィルタプリセットの永続化（JSON形式での保存・読み込み・削除）
- プリセット一覧の管理

#### インターフェース: `IFilterPresetService`

| メソッド | 概要 |
|---------|------|
| `Presets` | 利用可能なプリセット一覧（`AvaloniaList<FilterPreset>`） |
| `SavePreset(FilterPreset)` | プリセットをJSON保存。同名は上書き |
| `DeletePreset(string)` | プリセットを削除 |
| `LoadPresets()` | プリセットフォルダから全プリセットを読み込み |

#### 保存先

- `{AppDirectory}/presets/*.json`
- ファイル名 = プリセット名（不正文字は `_` に置換）
- シリアライズには `FilterPresetJsonContext` を使用

#### 読み書き仕様

- コンストラクタで `presets/` フォルダを確認・作成し、全プリセットを読み込み
- `SavePreset` — 既存の同名プリセットはリスト内で置換、新規は追加
- `LoadPresets` — フォルダ内の全 `.json` ファイルをデシリアライズ
- `RenamePreset(oldName, newPreset)` — 新名が既存プリセットと重複する場合は `false` を返して中止。成功時は旧ファイルを削除し新ファイルを作成、リスト内のエントリを置換

---

### 2.6 PresetEditorViewModel（プリセット編集）

#### 構成ファイル

| ファイル | 種別 |
|---------|------|
| `PresetEditorViewModel.cs` | ViewModel |
| `PresetEditorView.axaml` | View |
| `PresetEditorView.axaml.cs` | CodeBehind |

#### 責務

- プリセット一覧の表示・選択
- 選択プリセットのフィルタ条件読み込みと編集（`EditingConditions`）
- 編集内容の保存（名前変更時は重複チェック付き `RenamePreset` 経由）
- プリセット削除
- 全プリセットに対する一括コレクション生成（`BatchGenerateCollectionsAsync`）

> **DI登録なし**: `MapListPageViewModel` のコンストラクタ内で `new PresetEditorViewModel(...)` される

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Presets` | `AvaloniaList<FilterPreset>` | `FilterPresetService.Presets` への参照 |
| `SelectedPreset` | `FilterPreset?` | 選択中プリセット。変更時に `EditingConditions` を自動更新 |
| `EditingPresetName` | `string` | 編集中のプリセット名 |
| `EditingCollectionName` | `string` | 編集中のコレクション名 |
| `EditingConditions` | `AvaloniaList<FilterCondition>` | 編集中のフィルタ条件一覧（`MapFilterViewModel` とは独立） |
| `IsBatchGenerating` | `bool` | 一括生成中フラグ |
| `BatchGenerationProgress` | `int` | 一括生成進捗値（0–100） |
| `BatchGenerationStatusMessage` | `string` | 一括生成進捗/結果メッセージ |
| `EditorStatusMessage` | `string` | 保存・削除などの操作結果メッセージ |
| `CollectionNames` | `ObservableCollection<string>` | DB内コレクション名一覧（Collection条件用） |

#### 主要コマンド

| コマンド | CanExecute | 概要 |
|---------|-----------|------|
| `SavePresetCommand` | `SelectedPreset != null` かつ名前・コレクション名・条件が1件以上 | 現在の編集内容でプリセットを保存。名前変更時は `RenamePreset` 経由 |
| `DeletePresetCommand` | `SelectedPreset != null` | 選択プリセットを削除し `SelectedPreset = null` |
| `AddConditionCommand` | `EditingConditions.Count < 8` | `EditingConditions` に新規条件を追加 |
| `RemoveConditionCommand` | 常時 | 指定条件を `EditingConditions` から削除 |
| `ClearConditionsCommand` | 常時 | `EditingConditions` を全消去 |
| `BatchGenerateCollectionsCommand` | `!IsBatchGenerating && Presets.Count > 0` | 全プリセットのコレクションを順次生成 |

#### 一括生成フロー

`BatchGenerateCollectionsAsync()` は以下の手順で全プリセットを処理:

1. `_presetService.Presets` をループ
2. 各プリセットの条件で `_databaseService.Beatmaps` をZLinqでフィルタ（`FilterBeatmapsByPreset()`）
3. フィルタ結果が1件以上 かつ `CollectionName` が非空の場合、`IGenerateCollectionService.GenerateCollection()` を呼び出し
4. 進捗を `BatchGenerationProgress` / `BatchGenerationStatusMessage` で随時更新

#### R3による条件変更監視

| 発行元 | トリガー | アクション |
|-------|---------|----------|
| `EditingConditions.ObserveCollectionChanged()` | 条件の追加/削除 | `AddConditionCommand` / `SavePresetCommand` のCanExecute更新 |
| `_presetService.Presets.ObserveCollectionChanged()` | プリセットリスト変更 | `BatchGenerateCollectionsCommand` のCanExecute更新 |

---

## 3. OsuDatabase

### 概要

osu!のDBファイル（`osu!.db`, `collection.db`, `scores.db`）の読み込み・書き込み・データ管理を担うモジュール。高性能なバイナリパース（アンマネージドメモリ + 並列処理）を特徴とする。

### 構成ファイル一覧

| ファイル | 種別 | 概要 |
|---------|------|------|
| `DatabaseService.cs` | サービス | 3DBの並列読み込み統合・スコアデータ適用 |
| `DatabaseLoadingViewModel.cs` | ViewModel | 読み込み進捗UI管理 |
| `DatabaseLoadingView.axaml` | View | 読み込み進捗の表示 |
| `DatabaseLoadingView.axaml.cs` | CodeBehind | |
| `OsuDbParser.cs` | パーサー | osu!.dbの高性能パース |
| `CollectionDbParser.cs` | パーサー | collection.dbのパース |
| `ScoresDbParser.cs` | パーサー | scores.dbのパース |
| `GenerateCollectionService.cs` | サービス | collection.dbへの書き込み（MapListPageからのコレクション生成） |
| `CollectionDbWriter.cs` | ユーティリティ | collection.db バイナリ書き込み（GenerateCollectionService・ImportExportServiceから共用） |
| `BinaryReaderHelper.cs` | ヘルパー | バイナリ読み取り共通処理 |
| `UnmanagedBuffer.cs` | メモリ管理 | アンマネージドバッファ管理 |
| `Beatmap.cs` | モデル | 譜面データ（record型） |
| `OsuCollection.cs` | モデル | コレクションデータ |
| `ScoreData.cs` | モデル | スコアデータ |

### 依存モジュール

- **Settings** — `ISettingsService` からosu!フォルダパスを取得
- **Translate** — 読み込み進捗メッセージの多言語化

---

### 3.1 DatabaseService

#### インターフェース: `IDatabaseService`

| メンバ | 型 | 概要 |
|-------|---|------|
| `CollectionDbProgress` | `Observable<DatabaseLoadProgress>` | collection.db読み込み進捗 |
| `OsuDbProgress` | `Observable<DatabaseLoadProgress>` | osu!.db読み込み進捗 |
| `ScoresDbProgress` | `Observable<DatabaseLoadProgress>` | scores.db読み込み進捗 |
| `OsuCollections` | `List<OsuCollection>` | コレクション一覧 |
| `Beatmaps` | `Beatmap[]` | 譜面配列 |
| `LoadDatabasesAsync()` | `Task` | 3DBの並列読み込み |
| `TryGetBeatmapByMd5(string, out Beatmap?)` | `bool` | MD5ハッシュで譜面を検索（ImportExportServiceも使用） |

#### 3DBファイルの並列読み込みフロー

1. osu!フォルダパスの検証
2. `collection.db` のバックアップ作成（`backups/collection_startup_{timestamp}.db`）
3. `Task.WhenAll` で3ファイルを並列読み込み:
   - `ReadCollectionDbAsync` → `CollectionDbParser`
   - `ReadOsuDbAsync` → `OsuDbParser`
   - `ReadScoresDbFileAsync` → `ScoresDbParser`
4. 読み込み完了後、`ApplyScoresToBeatmaps` でスコアデータをBeatmapに統合

#### スコアデータ統合（MD5マッチング）

- `BuildBeatmapIndex()` でMD5Hash→配列インデックスの辞書を構築
- `ApplyScoresToBeatmaps()` でスコアDBの各エントリをMD5で照合
- マッチしたBeatmapに `BestScore`, `BestAccuracy`, `PlayCount` を `with` 式で適用
- 精度計算は `CalculateAccuracy()` で行い、Maniaモードとその他モードで計算式が異なる

#### 進捗通知

- 3系統のR3 `Subject<DatabaseLoadProgress>` で進捗を通知
- `DatabaseLoadProgress` は `record(string Message, int Progress)` 型
- UIスレッドへの配信は `Dispatcher.UIThread.Post()` で実施

---

### 3.2 パーサー群

#### 共通特徴

- **`UnmanagedBuffer`** — `NativeMemory.Alloc/Free` によるアンマネージドメモリ管理。LOH（Large Object Heap）回避。`IDisposable` で確実に解放
- **`Span<byte>` ベースのパース** — ゼロコピーでバイナリデータにアクセス
- **`BinaryReaderHelper`** — osu!形式の文字列読み込み（0x00=空文字列、0x0b=ULEB128長+UTF-8データ）と `DateTime` 読み込みの共通実装。全メソッドに `AggressiveInlining` を適用

#### OsuDbParser

osu!.dbファイルのパーサー。最も大規模なファイル（数百MB）を扱うため、高性能な実装となっている。

##### パース手順

1. **ファイル全体をアンマネージドバッファに一括読み込み**（1MBバッファ、`FileOptions.None`）
2. **ヘッダー読み込み** — `OsuVersion`, `FolderCount`, `AccountUnlocked`, `UnlockDate`, `PlayerName`, `BeatmapCount`
3. **シングルスレッドでオフセットスキャン** — 各譜面エントリの開始/終了オフセットを `RawBeatmapData` 構造体に記録。`TrySkipBeatmapEntry()` でバイナリデータを高速にスキップ
4. **`Parallel.For` による並列インスタンス展開** — `ParseBeatmapFromBuffer()` でunsafeポインタから `Beatmap` レコードを生成。Maniaモード（Ruleset=3）の譜面のみを抽出
5. **MD5Hashでソート → 隣接比較で重複排除** — `Span.Sort` + 線形スキャンで重複を除去

##### Mania抽出

- Rulesetフィールドの値が3（Mania）の譜面のみを `Beatmap` として返却
- KeyCountはCS（Circle Size）値から取得

#### CollectionDbParser

collection.dbファイルのパーサー。

- ファイル全体をアンマネージドバッファに一括読み込み
- ヘッダー（バージョン + コレクション数）を読み取り
- 各コレクション: 名前（osu!形式文字列） + MD5ハッシュ配列を読み込み
- 結果を `List<OsuCollection>` として返却

#### ScoresDbParser

scores.dbファイルのパーサー。

- ファイル全体をアンマネージドバッファに一括読み込み
- ヘッダー（osuVersion + ビートマップ数）を読み取り
- 各ビートマップ: MD5ハッシュ + スコアリストを読み込み
- 各スコア: Ruleset, 各種ヒットカウント, スコア, コンボ, Mods, タイムスタンプ等を読み取り
- 結果を `ScoresDatabase`（`Dictionary<string, List<ScoreData>>`）として返却

---

### 3.3 GenerateCollectionService

#### インターフェース: `IGenerateCollectionService`

| メンバ | 型 | 概要 |
|-------|---|------|
| `GenerationProgressObservable` | `Observable<GenerationProgress>` | 生成進捗通知 |
| `GenerateCollection(string, Beatmap[])` | `Task<bool>` | コレクション生成・書き込み |

#### 書き込みフォーマット仕様

collection.dbのバイナリフォーマットに準拠して書き込み（`CollectionDbWriter.WriteAsync()` に委譲）:

1. バージョン情報（int32: `20210528`）
2. コレクション数（int32）
3. 各コレクション:
   - コレクション名（osu!形式文字列: 0x0b + ULEB128長 + UTF-8バイト列）
   - MD5ハッシュ数（int32）
   - 各MD5ハッシュ（osu!形式文字列）

#### 同名コレクション置換ロジック

1. 既存コレクションリストから同名コレクションを `RemoveAll` で削除
2. 新しいコレクションを作成してリストに追加
3. リスト全体をcollection.dbに書き込み

---

### 3.4 データモデル

#### Beatmap（sealed record型）

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `MD5Hash` | `string` (required) | 譜面のMD5ハッシュ |
| `KeyCount` | `int` | キー数 |
| `Status` | `BeatmapStatus` | ランクステータス |
| `Title` | `string` (required) | 曲名 |
| `Artist` | `string` (required) | アーティスト名 |
| `Version` | `string` (required) | 難易度名 |
| `Creator` | `string` (required) | 譜面作成者 |
| `BPM` | `double` | BPM |
| `OD` | `double` | Overall Difficulty |
| `HP` | `double` | HP Drain Rate |
| `DrainTimeSeconds` | `int` | ドレイン時間（秒） |
| `Difficulty` | `double` | 難易度（StarRating） |
| `LongNoteRate` | `double` | LN率（0.0–1.0） |
| `IsPlayed` | `bool` | プレイ済みか |
| `LastPlayed` | `DateTime?` | 最終プレイ日時 |
| `LastModifiedTime` | `DateTime?` | 最終更新日時 |
| `FolderName` | `string` (required) | 曲フォルダ名 |
| `AudioFilename` | `string` (required) | オーディオファイル名 |
| `BeatmapSetId` | `int` | ビートマップセットID |
| `BeatmapId` | `int` | ビートマップID |
| `BestScore` | `int` | 最高スコア（scores.dbから適用） |
| `BestAccuracy` | `double` | 最高精度（scores.dbから適用） |
| `PlayCount` | `int` | プレイ回数（scores.dbから適用） |
| `Grade` | `string` (required) | グレード |

#### BeatmapStatus（enum）

| 値 |
|---|
| `None` |
| `Ranked` |
| `Loved` |
| `Approved` |
| `Qualified` |
| `Pending` |

#### OsuCollection

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Name` | `string` | コレクション名 |
| `BeatmapMd5s` | `string[]` | 含まれる譜面のMD5ハッシュ配列 |

#### ScoreData（record型）

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Ruleset` | `byte` | ルールセット（0=Standard, 1=Taiko, 2=Catch, 3=Mania） |
| `OsuVersion` | `int` | osu!バージョン |
| `BeatmapMD5Hash` | `string` | 譜面のMD5ハッシュ |
| `PlayerName` | `string` | プレイヤー名 |
| `ReplayMD5Hash` | `string` | リプレイのMD5ハッシュ |
| `Count300` | `ushort` | 300判定数 |
| `Count100` | `ushort` | 100判定数 |
| `Count50` | `ushort` | 50判定数 |
| `CountGeki` | `ushort` | 激判定数 |
| `CountKatu` | `ushort` | 可判定数 |
| `CountMiss` | `ushort` | ミス数 |
| `ReplayScore` | `int` | スコア |
| `Combo` | `ushort` | 最大コンボ |
| `PerfectCombo` | `bool` | フルコンボか |
| `Mods` | `int` | Modsビットフラグ |
| `ScoreTimestamp` | `DateTime` | スコア記録日時 |
| `ScoreId` | `long` | スコアID |

#### ScoresDatabase

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `OsuVersion` | `int` | osu!バージョン |
| `Scores` | `Dictionary<string, List<ScoreData>>` | MD5ハッシュをキーとしたスコア辞書 |

#### DatabaseLoadingViewModel

DB読み込み進捗表示専用のViewModel。

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `IsLoading` | `bool` | 読み込み中フラグ |
| `HasError` | `bool` | エラー発生フラグ |
| `ErrorMessage` | `string` | エラーメッセージ |
| `CollectionDbProgress` / `CollectionDbMessage` | `int` / `string` | collection.db進捗 |
| `OsuDbProgress` / `OsuDbMessage` | `int` / `string` | osu!.db進捗 |
| `ScoresDbProgress` / `ScoresDbMessage` | `int` / `string` | scores.db進捗 |

- `IDatabaseService` の3つの進捗Observableを購読し、各進捗プロパティを更新
- `InitialLoadAsync()` が外部から呼ばれるエントリポイント

---

## 4. AudioPlayer

### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `IAudioPlayerService.cs` | インターフェース | オーディオ再生サービスの契約定義 |
| `AudioPlayerService.cs` | サービス | Rust FFIを介した再生実装 |
| `AudioPlayerViewModel.cs` | ViewModel | 再生コントロールUI管理 |
| `NativeMethods.g.cs` | 自動生成コード | csbindgenが生成したP/Invokeバインディング |
| `AudioPlayerPanelViewModel.cs` | ViewModel | 高機能再生パネルのUI管理（シーク、前後トラック、シャッフル、リピート） |
| `AudioPlayerPanelView.axaml` | View | 高機能再生パネルのレイアウト |
| `AudioPlayerPanelView.axaml.cs` | CodeBehind | シークバーのドラッグ操作ハンドリング |
| `OsuFileParser.cs` | ユーティリティ | .osuファイルの[Events]セクションから背景画像ファイル名を取得 |

### 概要

- osu!の譜面オーディオファイルのプレビュー再生機能を提供するモジュール
- Rustネイティブライブラリ（rodio）とC#をFFI（P/Invoke）で橋渡しし、低レベルなオーディオ再生を実現
- 再生・一時停止・停止・音量制御の操作と、再生状態の変更通知をR3で管理
- 簡易版（AudioPlayerViewModel）と高機能版（AudioPlayerPanelViewModel）の2つの再生UIモードを提供
- 高機能版はシークバー、前後トラック移動、シャッフル再生、リピートモード（None/All/One）、背景画像表示を含む
- モード切替は設定に永続化され、MapListViewの上部パネルとして表示

### 依存モジュール

- **Settings** — `ISettingsService` からosu!フォルダパスを取得（オーディオファイルパス構築用）
- **OsuDatabase** — `Beatmap` 型を参照（`FolderName`, `AudioFilename`）
- **Shared** — `ViewModelBase` を継承
- **nakuru_audio**（Rustネイティブライブラリ） — `NativeMethods` 経由でFFI呼び出し

### アーキテクチャ（C# ↔ Rust FFI）

```
AudioPlayerViewModel → IAudioPlayerService → AudioPlayerService → NativeMethods (P/Invoke) → nakuru_audio.dll (Rust/rodio)
```

### IAudioPlayerService

オーディオ再生の抽象インターフェース。

| メンバ | 型 | 概要 |
|-------|---|------|
| `StateChanged` | `Observable<AudioPlayerState>` | 再生状態変更通知 |
| `CurrentState` | `AudioPlayerState` | 現在の再生状態 |
| `Volume` | `int` | 音量（0–100） |
| `Play(string)` | `void` | ファイルパス指定で再生 |
| `Pause()` | `void` | 一時停止 |
| `Resume()` | `void` | 再開 |
| `Stop()` | `void` | 停止 |
| `TogglePlayPause()` | `void` | 再生/一時停止トグル |
| `PlaybackCompleted` | `Observable<Unit>` | 再生完了通知 |
| `GetPosition()` | `double` | 現在の再生位置（秒） |
| `GetDuration()` | `double` | 楽曲の総再生時間（秒） |
| `Seek(double)` | `void` | 指定位置へシーク |

### AudioPlayerState（enum）

| 値 | 概要 |
|---|------|
| `Stopped` | 停止中 |
| `Playing` | 再生中 |
| `Paused` | 一時停止中 |
| `Error` | エラー |

### AudioPlayerService

- `NativeMethods.nakuru_audio_create()` でネイティブプレイヤーを作成（`AudioPlayer*` ハンドル管理）
- 音量はC#側で0–100 ↔ Rust側で0.0–1.0に変換
- `Play()` ではファイルパスをUTF-8バイト列に変換し、fixedポインタでRustに渡す
- R3 `Subject<AudioPlayerState>` で状態変更を通知
- `Dispose()` で `nakuru_audio_destroy()` を呼びネイティブリソースを解放
- 200msポーリングで再生完了を検知し、`_playbackCompletedSubject` を通じてUIスレッドに通知
- `_isManualStop` フラグで手動停止と自然完了を区別
- Rust側ではSinkを再生成することで新規再生を実現（`Sink::connect_new`）
- 新規Sinkに既存の音量設定を自動継承

### AudioPlayerViewModel

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `IsPlaying` | `bool` | 再生中かどうか |
| `Volume` | `int` | 音量（変更時にサービスに反映） |

- `IAudioPlayerService.StateChanged` を購読し、`IsPlaying` を更新
- `PlayBeatmapAudio(Beatmap?)` — osu!フォルダパス + `Songs/{FolderName}/{AudioFilename}` のパスを構築して再生
- `TogglePlayPauseCommand`, `StopAudioCommand` コマンドを提供

### AudioPlayerPanelViewModel

高機能オーディオ再生パネルのViewModel。シークバー、前後トラック、シャッフル、リピートモードを管理する。

#### RepeatMode（enum）

| 値 | 概要 |
|---|------|
| `None` | リピートなし |
| `All` | 全曲リピート |
| `One` | 1曲リピート |

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Title` | `string` | 再生中の曲名 |
| `Artist` | `string` | アーティスト名 |
| `Position` | `double` | 現在の再生位置（秒） |
| `Duration` | `double` | 楽曲の総再生時間（秒） |
| `PositionText` | `string` | 位置テキスト（mm:ss / mm:ss 形式） |
| `IsPlaying` | `bool` | 再生中フラグ |
| `IsSeeking` | `bool` | シーク操作中フラグ（ポーリング位置更新を一時停止） |
| `IsShuffleEnabled` | `bool` | シャッフルON/OFF |
| `CurrentRepeatMode` | `RepeatMode` | 現在のリピートモード |
| `BackgroundImage` | `Bitmap?` | 背景画像 |

#### 主要コマンド

| コマンド | 概要 |
|---------|---------|
| `TogglePlayPauseCommand` | 再生/一時停止の切り替え |
| `NextTrackCommand` | 次のトラックへ移動（シャッフル時はランダム選択） |
| `PreviousTrackCommand` | 3秒以上再生時は先頭に戻る、3秒未満は前のトラックへ |
| `ToggleShuffleCommand` | シャッフルのON/OFF切り替え |
| `CycleRepeatModeCommand` | リピートモードのサイクル（None → All → One → None） |
| `SeekCommand` | 指定位置へシーク |

#### 外部連携メソッド

| メソッド | 概要 |
|---------|---------|
| `PlayBeatmap(Beatmap)` | 指定譜面のオーディオを再生し、タイトル・背景画像を更新 |
| `SetNavigationContext(Beatmap[], int)` | フィルタ済み譜面配列と現在のページサイズを設定 |
| `SetPanelActive(bool)` | パネルの有効/無効を切り替え（無効時は再生停止） |

#### R3チェーン

- `IAudioPlayerService.StateChanged` → `IsPlaying` 更新 + 位置ポーリングの開始/停止
- `IAudioPlayerService.PlaybackCompleted` → リピート/シャッフルに応じた次トラック処理
- 位置ポーリング: 再生中は100msごとに `GetPosition()` で位置更新（`IsSeeking` 時はスキップ）
- Duration取得は再生開始後300ms遅延（Rust側のsource.total_duration()確定待ち）

#### ナビゲーション動作

- `NextTrack`: シャッフル時はRandom、通常時は番号+1。RepeatAll時は末尾から先頭に循環
- `PreviousTrack`: 再生位置3秒以上なら先頭に戻る、3秒未満なら前トラック。RepeatAll時は先頭から末尾に循環
- `NavigateToFilteredIndex`: MapListViewModelに曲IDのインデックスからページを計算してジャンプ要求

### OsuFileParser

.osuファイルの[Events]セクションを解析し、背景画像のファイル名を取得する静的ユーティリティクラス。

| メソッド | 概要 |
|---------|---------|
| `GetBackgroundFilename(string osuFilePath)` | 指定.osuファイルの[Events]セクションから `0,0,"filename"` 行を検索し、背景画像ファイル名を返す。見つからない場合はnull |

- ファイルは `File.ReadLines` で1行ずつ遅延読み込み（メモリ効率）
- `[Events]` セクション内の `0,0,"..."` パターンのみ処理
- 他の `[` セクションに到達したら探索を終了

### nakuru_audio（Rust側）

- **言語**: Rust
- **主要クレート**: rodio（オーディオ再生）
- **ビルド**: csbindgenでC#バインディングを自動生成
- **公開API**: `nakuru_audio_create`, `nakuru_audio_destroy`, `nakuru_audio_play`, `nakuru_audio_pause`, `nakuru_audio_resume`, `nakuru_audio_stop`, `nakuru_audio_set_volume`, `nakuru_audio_get_volume`, `nakuru_audio_get_state`, `nakuru_audio_set_state_callback`, `nakuru_audio_get_position`, `nakuru_audio_get_duration`, `nakuru_audio_seek`
- `AudioPlayer` 構造体に `_stream` (OutputStream) と `duration` (f64) フィールドを追加
- `nakuru_audio_play` はSinkを `Sink::connect_new` で再生成し、旧Sinkの音量を新Sinkに自動継承
- `duration` は `source.total_duration()` から取得（取得不可の場合は0.0）
- `nakuru_audio_seek` は不正値（NaN, Infinity, 負値）をガード

---

## 5. Settings

### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `SettingsData.cs` | モデル | 設定データ + JSON Source Generator |
| `SettingsService.cs` | サービス | 設定の永続化・読み込み |
| `SettingsViewModel.cs` | ViewModel | 設定画面のUI管理 |
| `SettingsPage.axaml` | View | 設定画面のレイアウト |
| `SettingsPage.axaml.cs` | CodeBehind | |

### 概要

- アプリケーション設定（osu!フォルダパス、言語、テーマ）の管理と永続化を担うモジュール
- 設定データをJSON形式で保存・読み込みし、NativeAOT対応のSource Generatorを使用
- 設定変更をR3で監視し、言語切替やデータベース再読み込みなどの副作用を自動的にトリガー

### 依存モジュール

- **Translate** — `LanguageService` で言語切替を実行
- **OsuDatabase** — `IDatabaseService` を参照（SettingsViewModelで使用）
- **Shared** — `ViewModelBase` を継承

### SettingsService

#### インターフェース: `ISettingsService`

| メンバ | 型 | 概要 |
|-------|---|------|
| `SettingsData` | `ISettingsData` | 現在の設定データ |
| `SaveSettings(SettingsData)` | `bool` | 設定を保存 |
| `CheckSettingsPath()` | `bool` | 設定ファイルの存在確認 |
| `GetSettingsPath()` | `string` | 設定ファイルのパス |

#### 設定ファイルパス

- `{AppDirectory}/settings/settings.json`

#### 永続化方式

- `JsonSerializer` + `SettingsJsonContext`（Source Generator）でJSON読み書き
- コンストラクタで設定ファイルを読み込み、`LanguageService` で言語を初期化
- `SettingsData.LanguageKey` の変更をR3の `ObservePropertyAndSubscribe` で監視し、言語変更を自動適用

### SettingsData

`ObservableObject` を継承した設定データクラス。

| プロパティ | 型 | デフォルト値 | 概要 |
|-----------|---|------------|------|
| `OsuFolderPath` | `string` | `""` | osu!フォルダパス |
| `LanguageKey` | `string` | `"ja-JP"` | 言語コード |
| `PreferUnicode` | `bool` | `false` | Unicode表示優先フラグ |
| `IsAudioPanelMode` | `bool` | `false` | オーディオパネルモードの永続化フラグ |

- JSON Source Generatorとの互換性のため、`[ObservableProperty]` ではなく手動の `SetProperty` パターンを使用
- `Update(SettingsData)` メソッドで既存インスタンスのプロパティを一括更新

#### SettingsJsonContext

NativeAOT対応のJSON Source Generatorコンテキスト。`SettingsData` のシリアライズに対応。

### SettingsViewModel

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `LanguageKeys` | `IAvaloniaReadOnlyList<string>` | 利用可能な言語コード一覧 |
| `SelectedLanguageKey` | `string` | 選択中の言語コード |
| `SelectedFolderPath` | `string` | 選択中のosu!フォルダパス |
| `OsuPathErrorMessage` | `string` | osu!パスエラーメッセージ |
| `HasOsuPathError` | `bool` | パスエラー有無 |
| `AutoPlayOnSelect` | `bool` | 選択時自動再生 |
| `PreferUnicode` | `bool` | Unicode表示優先設定 |

- 言語・フォルダパスの変更時に `UpdateSettingData()` で設定を保存
- `ToggleThemeCommand` — ダーク/ライトテーマの切替（`Semi.Avalonia` の `UnregisterFollowSystemTheme` を使用）

---

## 6. Translate

### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `LanguageService.cs` | サービス（シングルトン） | 言語リソースの管理・翻訳値の取得 |
| `TranslateExtension.cs` | マークアップ拡張 | XAMLでの多言語バインディング |
| `LanguageJsonContext.cs` | Source Generator | 言語JSONのデシリアライズ用 |
| `Resources/Languages/ja-JP.json` | リソース | 日本語翻訳データ |
| `Resources/Languages/en-US.json` | リソース | 英語翻訳データ |
| `Resources/Languages/zh-CN.json` | リソース | 中国語（簡体字）翻訳データ |

### 概要

- アプリケーション全体の多言語化（i18n）基盤を提供するモジュール
- JSONベースの言語リソースファイルを管理し、XAMLマークアップ拡張によるバインディングで動的な言語切替を実現
- WeakReferenceによるメモリリーク防止と、NativeAOT対応のSource Generatorによるリフレクション回避を特徴とする

### 依存モジュール

- なし（他モジュールから参照される基盤モジュール）

### LanguageService（シングルトン）

#### 対応言語

| 言語コード | 言語 |
|-----------|------|
| `ja-JP` | 日本語（デフォルト） |
| `en-US` | 英語 |
| `zh-CN` | 中国語（簡体字） |

#### 主要機能

| メンバ | 概要 |
|-------|------|
| `Instance` | `Lazy<LanguageService>` によるシングルトンインスタンス |
| `CurrentLanguage` | 現在の言語コード |
| `AvailableLanguages` | 対応言語コードのリスト |
| `ChangeLanguage(string)` | 言語を切り替え、`LanguageChanged` イベントを発火 |
| `GetString(string)` | キーに対応する翻訳文字列を取得。未発見時は `[key]` を返す |

#### JSONロード

- `avares://` URI でAvaloniaのアセットローダーからリソースファイルを読み込み
- `LanguageJsonContext.Default.DictionaryStringJsonElement` でデシリアライズ
- `FlattenDictionary()` でネストされたJSONをドット区切りのフラットキーに変換（例: `Menu.MapList`）

#### AOT対応

- JSONデシリアライズに `LanguageJsonContext`（Source Generator）を使用し、リフレクションを回避
- `Dictionary<string, JsonElement>` 型でデシリアライズ後、再帰的にフラット化

### TranslateExtension

XAMLマークアップ拡張（`MarkupExtension` 継承）。

#### 使用方法

XAML内で `{translate:Translate 'Key.Name'}` として使用。

#### WeakReference管理

- `TranslateWeakEventManager` を通じて、ターゲットの `AvaloniaObject` と `AvaloniaProperty` を `WeakReference` で登録
- `LanguageService.LanguageChanged` イベント発火時に、生存中の全登録先を更新
- ターゲットが破棄されると `WeakReference.TryGetTarget()` が失敗し、自動的にリストから削除
- 全サブスクリプションが削除されるとイベント購読自体も解除

---

## 7. Shared

### 構成ファイル

| フォルダ/ファイル | 概要 |
|----------------|------|
| `ViewModels/ViewModelBase.cs` | ViewModel基底クラス |
| `Extensions/R3Extensions.cs` | R3関連の拡張メソッド |
| `Converters/` | XAML用の値コンバーター（16クラス） |

### 概要

- 全モジュールが共通利用する基盤コンポーネントを提供するモジュール
- ViewModel基底クラス（`ViewModelBase`）によるR3ライフサイクル管理の統一、R3拡張メソッド、XAML値コンバーター群を集約
- NativeAOT対応のため、リフレクションを使用しないプロパティ監視パターンを提供

### 依存モジュール

- **Translate** — `ViewModelBase` が `LanguageService.Instance` を保持

---

### ViewModelBase

全ViewModelの基底クラス（`ObservableObject` 継承）。

| メンバ | 型 | 概要 |
|-------|---|------|
| `LangServiceInstance` | `LanguageService` | 翻訳サービスへの参照（`LanguageService.Instance`） |
| `Disposables` | `CompositeDisposable` | R3購読のライフサイクル管理 |
| `Dispose()` | `virtual void` | `Disposables.Dispose()` を呼び出し |

---

### R3Extensions

R3関連のカスタム拡張メソッド。NativeAOT対応のため、リフレクションを使用せず `string` でプロパティ名を指定する。

| メソッド | 引数 | 概要 |
|---------|------|------|
| `ObserveProperty<T>(this T, string)` | `source`: `INotifyPropertyChanged` 実装オブジェクト, `propertyName`: 監視プロパティ名 | 特定プロパティの変更を監視する `Observable<PropertyChangedEventArgs>` を生成 |
| `ObservePropertyChanged<T>(this T)` | `source`: `INotifyPropertyChanged` 実装オブジェクト | 全プロパティの変更を監視する `Observable<PropertyChangedEventArgs>` を生成 |
| `ObservePropertyAndSubscribe<T>(this T, string, Action, CompositeDisposable)` | `source`, `propertyName`, `action`, `disposables` | プロパティ変更時にアクションを実行し、ライフサイクル管理を行うヘルパー |
| `ObserveCollectionChanged<T>(this AvaloniaList<T>)` | `source`: 監視対象の `AvaloniaList` | コレクション変更を監視する `Observable<NotifyCollectionChangedEventArgs>` を生成 |
| `ObserveElementPropertyChanged<T>(this AvaloniaList<T>)` | `source`: `INotifyPropertyChanged` 要素を持つ `AvaloniaList` | 要素のPropertyChanged + コレクション変更を統合監視する `Observable<(T, PropertyChangedEventArgs)>` を生成。要素追加時は自動購読、Reset時は全再構築 |

---

### Converters

XAML用の値コンバーター一覧（全16クラス、14ファイル）。

| クラス名 | ファイル | 概要 |
|---------|---------|------|
| `AccuracyConverter` | `AccuracyConverter.cs` | 精度値（double）を `"XX.XX%"` 形式に変換。0の場合は `"-"` |
| `BoolToBrushConverter` | `BoolToBrushConverter.cs` | bool値に基づいて `TrueBrush` / `FalseBrush` を返す |
| `BoolToOpacityConverter` | `BoolToHiddenConverter.cs` | bool値をOpacity値に変換（Hidden的な非表示を実現） |
| `BoolToHitTestVisibleConverter` | `BoolToHiddenConverter.cs` | bool値を `IsHitTestVisible` 値に変換 |
| `InverseBooleanConverter` | `BoolToHiddenConverter.cs` | bool値を反転 |
| `BoolToIconKindConverter` | `BoolToIconKindConverter.cs` | bool値に基づいて `MaterialIconKind` を返す |
| `DateTimeOffsetConverter` | `DateTimeOffsetConverter.cs` | `string` ↔ `DateTimeOffset?` 間の変換（CalendarDatePicker用） |
| `EmptyStringConverter` | `EmptyStringConverter.cs` | 空文字列を `"-"` に変換 |
| `EnumToBoolConverter` | `EnumToBoolConverter.cs` | Enum値をboolに変換（RadioButton用） |
| `FilterTargetToStringConverter` | `FilterTargetToStringConverter.cs` | `FilterTarget` を多言語対応した文字列に変換 |
| `GradeToBrushConverter` | `GradeToBrushConverter.cs` | グレード文字列に基づいて色を返す（S=オレンジ, A=緑, B=青, C=ピンク, D=茶） |
| `NullableDateTimeConverter` | `NullableDateTimeConverter.cs` | `DateTime?` をフォーマット文字列で変換。`null`/`MinValue` の場合は `"-"` |
| `PresetNameConverter` | `PresetNameConverter.cs` | `FilterPreset?` を名前に変換。`null` の場合は翻訳された「(なし)」 |
| `ScoreConverter` | `ScoreConverter.cs` | スコア値（int）をカンマ区切り表示。0の場合は `"-"` |
| `DrainTimeConverter` | `DrainTimeConverter.cs` | `mm:ss` 形式 ⇔ 秒数の変換ユーティリティ |
| `ZeroToStringConverter` | `ZeroToStringConverter.cs` | 0値（int/double）を `"-"` に変換 |
| `UnicodeDisplayConverter` | `UnicodeDisplayConverter.cs` | `PreferUnicode`設定に基づいて`Beatmap`/`ImportExportBeatmapItem`のTitle/ArtistのUnicode版/ASCII版を切り替え。`SettingsService.Current`から設定値を参照。Unicodeが空の場合はASCII版にフォールバック |
| `RepeatModeToIconConverter` | `RepeatModeToIconConverter.cs` | `AudioPlayerPanelViewModel.RepeatMode` を `MaterialIconKind` に変換。None→RepeatOff、All→Repeat、One→RepeatOnce |
| `DownloadStateConverters` | `DownloadStateConverters.cs` | `BeatmapDownloadState` をUI要素の可視性（`IsVisible`）に変換するコンバーター群 |

#### RepeatModeToIconConverter

`AudioPlayerPanelViewModel.RepeatMode` を `MaterialIconKind` に変換するIValueConverter。

| 入力値 | 出力 |
|-------|------|
| `RepeatMode.None` | `MaterialIconKind.RepeatOff` |
| `RepeatMode.All` | `MaterialIconKind.Repeat` |
| `RepeatMode.One` | `MaterialIconKind.RepeatOnce` |

---

## 8. Licenses

### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `LicenseItem.cs` | モデル | ライセンス情報を保持するレコード |
| `LicensesViewModel.cs` | ViewModel | ライセンス一覧の管理 |
| `LicensesPage.axaml` | View | ライセンス一覧の表示 |
| `LicensesPage.axaml.cs` | CodeBehind | |

### 概要

- アプリケーションが使用するサードパーティライブラリのライセンス情報を一覧表示するモジュール
- ライセンス情報はハードコードで管理し、各パッケージのURL外部ブラウザ起動機能を提供

### 依存モジュール

- **Shared** — `ViewModelBase` を継承

### LicenseItem

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `PackageName` | `string` (required) | パッケージ名 |
| `Version` | `string` (required) | バージョン |
| `LicenseType` | `string` (required) | ライセンス種別 |
| `Url` | `string?` | プロジェクトURL |
| `Copyright` | `string?` | 著作権表示 |

### 固定リスト管理方式

- `LicensesViewModel` のコンストラクタで `AvaloniaList<LicenseItem>` に全ライセンス情報をハードコードで登録
- 動的な読み込みは行わない

### 登録済みライセンス

| パッケージ名 | バージョン | ライセンス |
|-------------|-----------|-----------|
| Avalonia | 11.3.10 | MIT |
| CommunityToolkit.Mvvm | 8.4.0 | MIT |
| R3 | 1.3.0 | MIT |
| Semi.Avalonia | 11.3.7.1 | MIT |
| Pure.DI | 2.2.15 | MIT |
| Material.Icons.Avalonia | 2.4.1 | MIT |
| ZLinq | 1.5.4 | MIT |
| HotAvalonia | 3.0.2 | MIT |
| nakuru_audio | 0.1.0 | MIT |
| rodio | 0.21.1 | MIT OR Apache-2.0 |
| csbindgen | 1.9.7 | MIT |
| parking_lot | 0.12.5 | MIT OR Apache-2.0 |

### URL外部ブラウザ起動

- ライセンス一覧の各項目にURLリンクを設置し、クリック時に外部ブラウザで開く

---

## 9. ImportExport

### 概要

コレクションをJSON形式のファイルにエクスポート・インポートするモジュール。`exports/` フォルダへのJSON出力と `imports/` フォルダからのJSON入力を担う。MapListモジュールと同様の親子View/ViewModel構成（3子ViewModel）を採用する。

- **ドラッグ&ドロップ**: ImportViewにJSON/フォルダのDnDを受け付け、`imports/` フォルダにコピー後リロード
- **未所持フィルタ**: プレビューDataGridで未所持（`Exists=false`）の譜面のみ表示するチェックボックスフィルタ
- **再帰探索**: `imports/` フォルダ内のサブフォルダも含めてJSONファイルを探索

### 構成ファイル一覧

| ファイル | 種別 | 概要 |
|---------|------|------|
| `IImportExportService.cs` | インターフェース | エクスポート/インポートサービスの契約定義 |
| `ImportExportService.cs` | サービス | エクスポート・インポートのビジネスロジック |
| `ImportExportPageViewModel.cs` | ViewModel | 親VM: 子VM統合・進捗管理・排他選択・処理中相互排他 |
| `ImportExportPageView.axaml` | View | 親View: 3パネルUIレイアウト（子Viewを参照） |
| `ImportExportPageView.axaml.cs` | CodeBehind | `InitializeComponent()` のみ |
| `ExportViewModel.cs` | ViewModel | 子VM: エクスポートリスト管理・実行 |
| `ExportView.axaml` | View | 子View: エクスポートリストUI |
| `ExportView.axaml.cs` | CodeBehind | `InitializeComponent()` のみ |
| `ImportViewModel.cs` | ViewModel | 子VM: インポートリスト管理・実行 |
| `ImportView.axaml` | View | 子View: インポートリストUI |
| `ImportView.axaml.cs` | CodeBehind | DnDイベントハンドリング（パス抽出→ViewModel委譲） |
| `ImportExportBeatmapListViewModel.cs` | ViewModel | 子VM: プレビュー表示・ページング |
| `ImportExportBeatmapListView.axaml` | View | 子View: Beatmapプレビュー DataGrid + ページングUI |
| `ImportExportBeatmapListView.axaml.cs` | CodeBehind | Exists列の `IsVisible` 制御 |
| `Models/CollectionExchangeData.cs` | モデル | JSON DTOクラス + NativeAOT Source Generator（`ImportExportJsonContext`） |
| `Models/ExportCollectionItem.cs` | モデル | エクスポートリスト行モデル（コレクション名、MD5配列） |
| `Models/ImportFileItem.cs` | モデル | インポートリスト行モデル（ファイルパス、パース済みデータ） |
| `Models/ImportExportBeatmapItem.cs` | モデル | Beatmap表示行モデル（プレビューペイン用） |
| `IBeatmapDownloadService.cs` | インターフェース | ダウンロードサービスの契約定義 |
| `BeatmapDownloadService.cs` | サービス | nerinyan.moe APIを使用した非同期ダウンロード実装 |
| `Models/BeatmapDownloadState.cs` | モデル | ダウンロード状態enum（6段階） |

### 依存モジュール

- **OsuDatabase** — `IDatabaseService`（コレクション読み書き・MD5ルックアップ）、`CollectionDbWriter`（collection.db書き込み）
- **Settings** — `ISettingsService`（osu!フォルダパス取得、collection.dbパス構築）
- **Shared** — `ViewModelBase` を継承

### 9.1 IImportExportService / ImportExportService

#### インターフェース: `IImportExportService`

| メンバ | 型 | 概要 |
|-------|---|------|
| `ProgressObservable` | `Observable<ImportExportProgress>` | エクスポート/インポート進捗通知 |
| `GetImportFiles()` | `List<ImportFileItem>` | `imports/` フォルダのJSONファイルを再帰的に走査（`SearchOption.AllDirectories`）・パースして返す |
| `ExportAsync(IReadOnlyList<string>)` | `Task<int>` | 指定コレクション名をJSONファイルに書き出す。戻り値: 成功件数 |
| `ImportAsync(IReadOnlyList<string>)` | `Task<bool>` | 指定パスのJSONをインポートしcollection.dbに反映。戻り値: 全成功時true |

#### エクスポート先/インポート元フォルダ

| フォルダ | パス |
|---------|------|
| エクスポート先 | `{AppDirectory}/exports/` |
| インポート元 | `{AppDirectory}/imports/` |

フォルダが存在しない場合は自動作成する。

#### ファイル名規則

エクスポート時のファイル名 = `{コレクション名}.json`（ファイル名に使用できない文字は `_` に置換）

#### インポート時の同名コレクション処理

- `_databaseService.OsuCollections` から同名コレクションを `RemoveAll` で削除し、新規コレクションを追加（サイレント上書き）
- 最後に `CollectionDbWriter.WriteAsync()` でcollection.db全体を書き直す

### 9.2 CollectionExchangeData（JSON DTO）

JSONのルートオブジェクト。1コレクション = 1ファイル。

#### `CollectionExchangeData`

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Name` | `string` | コレクション名 |
| `Beatmaps` | `List<CollectionExchangeBeatmap>` | 所属Beatmap一覧 |

#### `CollectionExchangeBeatmap`

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `Title` | `string` | 曲名 |
| `Artist` | `string` | アーティスト名 |
| `Version` | `string` | 難易度名 |
| `Creator` | `string` | 譜面作成者 |
| `Cs` | `double` | キー数（CS値） |
| `BeatmapsetId` | `int` | ビートマップセットID（JSON: `beatmapset_id`） |
| `Md5` | `string` | MD5ハッシュ |

#### `ImportExportJsonContext`

NativeAOT対応のJSON Source Generatorコンテキスト。`CollectionExchangeData`, `List<CollectionExchangeData>`, `CollectionExchangeBeatmap` のシリアライズに対応。シリアライズ設定: `WriteIndented=true`, `SnakeCaseLower` 命名規則。

### 9.3 ImportExportPageViewModel（親ViewModel）

#### 責務

- 子VM（`ExportViewModel` / `ImportViewModel` / `ImportExportBeatmapListViewModel`）を手動生成して統合
- `IImportExportService.ProgressObservable` を購読し `StatusMessage` / `ProgressValue` を更新
- 子VMの `PreviewRequested` Subject を購読し `BeatmapListViewModel.SetPreviewRows()` を呼び出す（親仲介パターン）
- 排他選択（Export選択時にImport選択をクリア、逆も同様）をR3 `ObserveProperty` で制御
- Export/Import各VMの `IsProcessing` のOR統合を `IsAnyProcessing` として子VMに逆流させ、双方のコマンドを連動させる

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `ExportViewModel` | `ExportViewModel` | 子VM: エクスポート（get-only） |
| `ImportViewModel` | `ImportViewModel` | 子VM: インポート（get-only） |
| `BeatmapListViewModel` | `ImportExportBeatmapListViewModel` | 子VM: プレビュー（get-only） |
| `StatusMessage` | `string` | 進捗メッセージ（初期値 `" "`） |
| `ProgressValue` | `int` | 進捗値（0–100） |
| `IsProcessing` | `bool` | Export / Import いずれかが処理中の統合フラグ |

#### 主要挙動

1. **`Initialize()`** — 子VMの `Initialize()` を呼び出し + `BeatmapListViewModel.Reset()` でプレビューをリセット
2. **プレビュー更新** — `ExportViewModel.PreviewRequested` / `ImportViewModel.PreviewRequested` を購読し、`BeatmapListViewModel.SetPreviewRows(rows, isImport)` を呼び出す
3. **ステータス更新** — `ExportViewModel.StatusMessageRequested` / `ImportViewModel.StatusMessageRequested` を購読し `StatusMessage` に反映
4. **Import成功後の再初期化** — `ImportViewModel.ImportCompleted` を購読し `Initialize()` を再実行
5. **IsProcessing統合** — Export/Import各VMの `IsProcessing` を `Merge` で統合し、`IsAnyProcessing` として両子VMに逆流
6. **排他選択** — `ExportViewModel.SelectedExportCollection` が非nullになるとImport選択をnullクリア（逆も同様）

#### コンストラクタ内のR3購読チェーン

| # | 発行元 | アクション |
|---|-------|-----------|
| 1 | `IImportExportService.ProgressObservable` | `StatusMessage` / `ProgressValue` 更新 |
| 2 | `ExportViewModel.PreviewRequested` | `BeatmapListVM.SetPreviewRows(rows, isImport: false)` |
| 3 | `ImportViewModel.PreviewRequested` | `BeatmapListVM.SetPreviewRows(rows, isImport: true)` |
| 4 | `ExportViewModel.StatusMessageRequested` | `StatusMessage` 更新 |
| 5 | `ImportViewModel.StatusMessageRequested` | `StatusMessage` 更新 |
| 6 | `ImportViewModel.ImportCompleted` | `Initialize()` 再実行 |
| 7 | `ExportViewModel.IsProcessing` + `ImportViewModel.IsProcessing`（Merge） | `IsProcessing` 統合 + `IsAnyProcessing` を両子VMに逆流 |
| 8 | `ExportViewModel.SelectedExportCollection` | 非null時に `ImportViewModel.SelectedImportFile = null` |
| 9 | `ImportViewModel.SelectedImportFile` | 非null時に `ExportViewModel.SelectedExportCollection = null` |

---

### 9.4 ExportViewModel（子ViewModel）

#### 責務

- DBのコレクション一覧（`ExportCollections`）の管理・チェックボックス操作
- 選択コレクション変更時にプレビュー行を構築し `PreviewRequested` Subjectで通知
- エクスポート実行（`ExportCommand`）、全選択/全解除/リロード操作
- `IsAnyProcessing`（親VMから設定）を参照して `ExportCommand` のCanExecuteを制御

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `ExportCollections` | `AvaloniaList<ExportCollectionItem>` | エクスポート対象コレクション一覧 |
| `SelectedExportCollection` | `ExportCollectionItem?` | 選択中のコレクション |
| `IsProcessing` | `bool` | 自身の処理中フラグ（親VMが統合） |
| `IsAnyProcessing` | `bool` | 親VMが設定するグローバル処理中フラグ（CanExecute制御用） |

#### 公開Observable（Subject経由）

| プロパティ | 発行タイミング | 内容 |
|-----------|-------------|------|
| `PreviewRequested` | `SelectedExportCollection` 変更時（null時は空配列） | `ImportExportBeatmapItem[]` |
| `StatusMessageRequested` | エクスポート完了/失敗時 | ステータス文字列 |

> **スレッド安全性**: Subject.OnNext は `Dispatcher.UIThread.InvokeAsync` で包み、UIスレッドで実行することを保証する。

#### 主要挙動

1. **`Initialize()`** — `IDatabaseService.OsuCollections` から `ExportCollections` を再構築（プレビューSubjectは発行しない）
2. **`OnSelectedExportCollectionChanged()`** — 選択変更時に `BuildPreviewRows()` でMD5→DB照合し `PreviewRequested` を発行。null時は空配列を発行してプレビューをクリア
3. **`OnIsAnyProcessingChanged()`** — `ExportCommand.NotifyCanExecuteChanged()` を呼び出し
4. **`ReloadExportCommand`** — `Initialize()` + 空配列Subject発行（プレビューのリセット）
5. **`ExportAsync(CanExecute = !IsAnyProcessing)`** — チェックされたコレクション名を `IImportExportService.ExportAsync()` に渡す

---

### 9.5 ImportViewModel（子ViewModel）

#### 責務

- `imports/` フォルダのJSONファイル一覧（`ImportFiles`）の管理
- 選択ファイル変更時にプレビュー行を構築し `PreviewRequested` Subjectで通知
- インポート実行（`ImportCommand`）、全選択/全解除/リロード操作
- インポート成功時に `ImportCompleted` Subjectを発行し、親VMに再初期化を依頼

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `ImportFiles` | `AvaloniaList<ImportFileItem>` | インポート対象ファイル一覧 |
| `SelectedImportFile` | `ImportFileItem?` | 選択中のファイル |
| `IsProcessing` | `bool` | 自身の処理中フラグ（親VMが統合） |
| `IsAnyProcessing` | `bool` | 親VMが設定するグローバル処理中フラグ（CanExecute制御用） |

#### 公開Observable（Subject経由）

| プロパティ | 発行タイミング | 内容 |
|-----------|-------------|------|
| `PreviewRequested` | `SelectedImportFile` 変更時（null時は空配列） | `ImportExportBeatmapItem[]` |
| `StatusMessageRequested` | インポート完了/失敗時 | ステータス文字列 |
| `ImportCompleted` | インポート成功時 | `Unit` |

#### 主要挙動

1. **`Initialize()`** — `IImportExportService.GetImportFiles()` から `ImportFiles` を再構築（プレビューSubjectは発行しない）
2. **`OnSelectedImportFileChanged()`** — 選択変更時に `BuildPreviewRows()` でDB照合し `PreviewRequested` を発行。null時は空配列を発行
3. **`ReloadImportCommand`** — `Initialize()` + 空配列Subject発行
4. **`ImportAsync(CanExecute = !IsAnyProcessing)`** — チェックされたファイルパスを `IImportExportService.ImportAsync()` に渡す。成功時に `ImportCompleted.OnNext(Unit.Default)` を発行
5. **`HandleDroppedPathsAsync(string[])`** — DnDで受け取ったパス（JSONファイル/フォルダ）を `imports/` フォルダにコピーし、`Initialize()` でリストをリロード。フォルダの場合は内部の `.json` ファイルを再帰的にコピー

#### DnD対応（View側）

`ImportView.axaml.cs` のcode-behindで `DragOver` / `Drop` イベントをハンドリングする。ドロップされたデータからファイル/フォルダパスを抽出し、`ImportViewModel.HandleDroppedPathsAsync()` に委譲する。

---

### 9.6 ImportExportBeatmapListViewModel（子ViewModel）

#### 責務

- 親VMから `SetPreviewRows(rows, isImport)` で渡されたBeatmapプレビュー行のページング表示
- `IsImportPreview` フラグで「所持」列の表示を切り替え（Import時: 表示、Export時: 非表示）
- `ShowOnlyMissing` フラグで未所持譜面のみ表示するフィルタリング
- ページング操作（`NextPageCommand` / `PreviousPageCommand`）

#### 主要プロパティ

| プロパティ | 型 | 概要 |
|-----------|---|------|
| `ShowBeatmaps` | `IAvaloniaReadOnlyList<ImportExportBeatmapItem>` | 現在ページの表示データ |
| `TotalPreviewCount` | `int` | プレビュー総件数 |
| `IsImportPreview` | `bool` | Import時 true（「所持」列表示制御用） |
| `ShowOnlyMissing` | `bool` | 未所持（`Exists=false`）のみ表示するフィルタフラグ |
| `CurrentPage` | `int` | 現在ページ（1始まり） |
| `FilteredPages` | `int` | 総ページ数 |
| `PageSize` | `int` | 1ページ表示件数（初期値20） |
| `PageSizes` | `IAvaloniaReadOnlyList<int>` | `{10, 20, 50, 100}` |

#### 主要API

| メソッド | 概要 |
|---------|------|
| `SetPreviewRows(ImportExportBeatmapItem[], bool isImport)` | 外部（親VM経由）からプレビュー行を設定してページング初期化 |
| `Reset()` | プレビューを初期状態（空）にリセット |
| `ApplyMissingFilter()` | `ShowOnlyMissing` の状態に応じて未所持譜面のみにフィルタし、ページングを再初期化 |

#### 未所持フィルタ

`ShowOnlyMissing` チェックボックスの変更時に `ApplyMissingFilter()` を呼び出し、`Exists=false` の行のみに絞り込む。フィルタOFF時は全行を表示する。列ヘッダーは「所持」として表示される。

#### 引数なしコンストラクタ

外部サービスへの依存を持たない。データは親VMが `SetPreviewRows()` を通じて渡す（テスタビリティ向上・循環依存回避のための設計）。

---

### 9.7 親子間通信パターン

ImportExportモジュールは **親仲介パターン** を採用する。MapListモジュール（子→子の直接購読も許容）とは異なり、Export/Import子VM間に双方向の依存を持たせず、親VMがオーケストレーションを担う。

---

### 9.8 BeatmapDownloadService（ダウンロード機能）

#### 構成ファイル

| ファイル | 種別 | 概要 |
|---------|------|------|
| `IBeatmapDownloadService.cs` | インターフェース | ダウンロードサービスの契約定義 |
| `BeatmapDownloadService.cs` | サービス | ダウンロード実装 |
| `Models/BeatmapDownloadState.cs` | モデル | ダウンロード状態enum |
| `Shared/Converters/DownloadStateConverters.cs` | コンバーター | ダウンロード状態をUIの可視性に変換 |

#### 責務

- nerinyan.moe APIから未所持の beatmapset を `.osz` 形式でダウンロード
- `System.Threading.Channels` による非同期キュー処理
- `ConcurrentDictionary` によるBeatmapSetIdごとの重複排除
- 一括ダウンロード・キャンセル機能

#### BeatmapDownloadState（enum）

| 値 | 概要 |
|---|------|
| `Exists` | 既にローカルに存在 |
| `NotExists` | 未所持（ダウンロード可能） |
| `Queued` | ダウンロードキュー待機中 |
| `Downloading` | ダウンロード中 |
| `Downloaded` | ダウンロード完了 |
| `Error` | ダウンロード失敗 |

#### UI状態遷移

```
NotExists → Queued → Downloading → Downloaded
                                  → Error
```

#### インターフェース: `IBeatmapDownloadService`

| メンバ | 型 | 概要 |
|-------|---|------|
| `GetDownloadState(int beatmapSetId)` | `BeatmapDownloadState` | 指定BeatmapSetIdの現在の状態を返す |
| `DownloadStateChanged` | `Observable<(int, BeatmapDownloadState)>` | 状態変化通知（BeatmapSetId + 新状態） |
| `EnqueueAsync(int beatmapSetId)` | `Task` | ダウンロードキューに追加 |
| `CancelAll()` | `void` | 全キューのキャンセル |

```
ExportViewModel  ──PreviewRequested──▶  ImportExportPageViewModel  ──SetPreviewRows()──▶  BeatmapListViewModel
ImportViewModel  ──PreviewRequested──▶  (親仲介)                   ──SetPreviewRows()──▶
ExportViewModel  ──StatusMessageRequested──▶  ImportExportPageViewModel.StatusMessage
ImportViewModel  ──StatusMessageRequested──▶  ImportExportPageViewModel.StatusMessage
ImportViewModel  ──ImportCompleted──▶   ImportExportPageViewModel.Initialize()
ImportExportPageViewModel  ──IsAnyProcessing──▶  ExportViewModel（逆流）
ImportExportPageViewModel  ──IsAnyProcessing──▶  ImportViewModel（逆流）
```

#### 排他選択の仕組み

親VMがR3 `ObserveProperty` で双方向の選択プロパティを監視し、片方が選ばれたらもう片方をnullクリアする。これにより「ExportとImportの同時プレビュー」を防ぐ。無限ループはせず（null設定→空配列発行→停止）、1往復のみで収束する。

#### 処理中の相互排他（IsAnyProcessing）

親VMが `ExportViewModel.IsProcessing` と `ImportViewModel.IsProcessing` を `Observable.Merge` で統合し、ORをとった値を `IsAnyProcessing` として両子VMに設定する。各子VMは `OnIsAnyProcessingChanged()` で `Command.NotifyCanExecuteChanged()` を呼び出す。これにより、Export中はImportボタンも無効化される（逆も同様）。

---

## 10. BeatmapGenerator

### 概要

osu!mania beatmapのレート変更版（倍速・減速）を自動生成するモジュール。指定レート範囲で.osuファイルのタイミング変換とオーディオファイルのレート変換を行い、`.osz`（ZIP形式）ファイルとしてSongsフォルダに出力する。DT（Double Time）モードではピッチを保持したタイムストレッチ、NC（NightCore）モードではピッチ変更を伴うレート変換を選択可能。

.osuファイルおよび関連.osbファイルから参照されるすべてのアセット（背景画像、動画、スキン、ストーリーボード、ヒットサウンド等）を`OsuFileAssetParser`で解析・収集し、音声ファイルにはレート変換を適用、非音声ファイルはそのままコピーして、一つの.oszにパッケージする。ヒットサウンド等のサンプル音声はメインオーディオと同じレート・モードで変換し、リネーム後のファイル名で.oszに格納する（例: `F5S_s.wav` → `F5S_s_1.25x_dt.wav`）。変換元の原音も.oszに保持する。

オーディオ出力形式は入力ファイルの拡張子に応じて自動選択される（MP3→MP3, OGG→OGG, WAV→WAV）。オーディオレート変換・エンコードは FFmpeg（n8.1 LGPL static build）を subprocess として起動して行い、MP3 は `libmp3lame`、OGG は `libvorbis`、WAV は `pcm_s16le` で出力する。MP3 出力時、純 C# パーサー（`AudioInputMetadataReader`）で事前にチャンネル数を取得し、3ch 以上の場合は OGG にフォールバックする。

### 構成ファイル一覧

| ファイル | 種別 | 概要 |
|---------|------|------|
| `RateGenerationOptions.cs` | モデル | レート生成オプション（レート範囲・ステップ・出力先など） |
| `RateGenerationResult.cs` | モデル | 単一レート生成の結果 |
| `BatchGenerationResult.cs` | モデル | バッチ生成の集計結果 |
| `RateGenerationProgress.cs` | モデル | 生成進捗データ |
| `OsuFileConvertOptions.cs` | モデル | .osuファイル変換オプション |
| `IAudioRateChanger.cs` | インターフェース | オーディオレート変換の契約定義 |
| `IOsuFileRateConverter.cs` | インターフェース | .osuファイルレート変換の契約定義 |
| `IBeatmapRateGenerator.cs` | インターフェース | レート生成オーケストレータの契約定義 |
| `AudioRateChanger.cs` | サービス | （公開契約のみ保持）旧 NAudio 実装は FFmpeg 移行に伴い削除済み |
| `FfmpegAudioRateChanger.cs` | サービス | `IAudioRateChanger` 実装。FFmpeg subprocess 経由でレート変換を実行（DT/NC モード、MP3/OGG/WAV 出力、MP3 出力時は `AudioInputMetadataReader` でチャンネル数を取得して3ch以上は OGG にフォールバック） |
| `FfmpegArgumentsBuilder.cs` | internal static (純関数) | ffmpeg コマンドラインの組立（DTの `atempo` チェーン分解、NCの `asetrate+aresample`、出力コーデック選択、VBR 品質） |
| `FfmpegBinaryLocator.cs` | internal static | ffmpeg.exe の実行パス解決（アプリ同梱 `native/ffmpeg/win-x64/` を基準に探索） |
| `FfmpegProcessRunner.cs` | internal static | `Process.Start` / `ProcessStartInfo.ArgumentList` でプロセスを起動し、stderr を回収し、`CancellationToken` でキャンセル・終了を処理 |
| `FfmpegExecutionException.cs` | internal sealed | FFmpeg 実行失敗時にスローされる例外（exit code / stderr 末尾を保持） |
| `AudioInputMetadata.cs` | internal readonly record struct | 入力オーディオのチャンネル数 / サンプルレートを保持する値オブジェクト |
| `AudioInputMetadataReader.cs` | internal static | 純 C# 実装の WAV / MP3 / OGG（Vorbis / Opus）メタデータパーサー。`BinaryPrimitives` ベースでリフレクション・動的コード生成を用いず NativeAOT 安全 |
| `OsuFileRateConverter.cs` | サービス | .osuファイルのタイミング・メタデータ変換（StreamReaderによる逐次読み込み、SampleFilenameMap適用） |
| `OsuFileAssetParser.cs` | サービス | .osuファイルおよび関連.osbファイルから参照アセットを解析・抽出 |
| `OsuReferencedAssets.cs` | モデル | .osuファイルが参照する外部アセットの分類済みデータ（MainAudio / SampleAudioFiles / NonAudioFiles） |
| `BeatmapRateGenerator.cs` | サービス | レート生成のオーケストレータ（アセット収集 + オーディオ変換 + .osu変換 → .osz生成） |
| `BeatmapGenerationPageViewModel.cs` | ViewModel | 生成ページ全体の制御（タブ切替） |
| `BeatmapGenerationPageView.axaml` | View | 生成ページのレイアウト |
| `BeatmapGenerationPageView.axaml.cs` | CodeBehind | |
| `CollectionSelectorViewModel.cs` | ViewModel | コレクション選択・一括レート生成 |
| `CollectionSelectorView.axaml` | View | コレクション選択UI |
| `CollectionSelectorView.axaml.cs` | CodeBehind | |
| `RateGenerationViewModel.cs` | ViewModel | レート範囲・オプション設定UI |
| `RateGenerationView.axaml` | View | レート設定UI |
| `RateGenerationView.axaml.cs` | CodeBehind | |
| `SingleBeatmapGenerationViewModel.cs` | ViewModel | 単一譜面指定のレート生成 |
| `SingleBeatmapGenerationWindow.axaml` | View | 単一譜面生成ウィンドウ |
| `SingleBeatmapGenerationWindow.axaml.cs` | CodeBehind | |

### 依存モジュール

- **OsuDatabase** — `IDatabaseService`（コレクション・譜面データの参照）
- **Settings** — `ISettingsService`（osu!フォルダパス取得、`PreferUnicode` 監視）
- **Shared** — `ViewModelBase` を継承
- **Translate** — UIの多言語化
- **FFmpeg (subprocess)** — `native/ffmpeg/win-x64/ffmpeg.exe` を `Process.Start` で起動し、オーディオのデコード・レート変換・エンコードを実施（n8.1 LGPL static build, DTモードは `atempo` チェーン、NCモードは `asetrate+aresample`、MP3 は `libmp3lame` / VBR、OGG は `libvorbis`、WAV は `pcm_s16le`、LGPL-2.1-or-later）

### ビートマップリスト表示機能

`BeatmapGenerationPageView` は5カラム構成で、中央カラムにコレクション内譜面のDataGridを表示する。

#### DataGrid列構成

| 列 | 概要 |
|---|------|
| 曲情報 | Title・Artist・Version（`UnicodeDisplayConverter` 対応） |
| BPM | BPM値 |
| LN率 | LongNoteRate（%表示） |
| スコア情報 | BestScore・BestAccuracy・Grade（Mod/ScoreSystem依存） |

#### Mod / ScoreSystem切替

- `SelectedModCategory`（NoMod / HT / DT）と `SelectedScoreSystemCategory`（Default / v1 / v2）でスコア表示を切替
- 切替時は `UpdateShowBeatmaps()` でDataGrid行を再構築し、`Beatmap.GetBestScore()` / `GetBestAccuracy()` / `GetGrade()` で対応する値を表示

#### ページネーション

- ページサイズ: 10 / 20 / 50 / 100（デフォルト: 20）
- `RefreshCollectionBeatmaps()` でコレクション選択時にMD5マッチングで譜面を解決し `_resolvedBeatmaps` に格納
- `UpdateShowBeatmaps()` で `Span` スライスによりページ分のデータを `ShowBeatmaps`（`AvaloniaList`）に転写

### NativeAOT対応

- ffmpeg は `Process.Start` + `ProcessStartInfo.ArgumentList` による subprocess 実行のため、P/Invoke は不要で AOT 互換
- 入力オーディオのメタデータ抽出は純 C# パーサー `AudioInputMetadataReader`（`BinaryPrimitives` ベース）で行い、`JsonSerializer` / `JsonSerializerContext` や subprocess を追加せずに処理する
- `FfmpegArgumentsBuilder` は純関数 static クラスで、引数組立にリフレクションを使用しない
- ReflectionBindingは不使用（全Viewに `x:DataType` 指定）

---

## 関連ドキュメント

- [ARCHITECTURE.md](ARCHITECTURE.md) — アーキテクチャ全体像・技術スタック・DI構成
- [DATA_FLOW.md](DATA_FLOW.md) — データフローと状態管理・R3チェーン
- [NATIVE_AOT.md](NATIVE_AOT.md) — NativeAOT対応ガイドライン
- [TESTING.md](TESTING.md) — テスト戦略・実行方法
- [BUILD.md](BUILD.md) — ビルド手順
