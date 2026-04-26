# NativeAOT対応ガイドライン

NativeAOT（Ahead-of-Time コンパイル）に対応するため、本プロジェクトでは動的コード生成やリフレクションに依存しない実装を徹底している。本ドキュメントは新しいコードを追加する際に必ず参照すべきガイドラインである。

> **関連ドキュメント**: [ARCHITECTURE.md](ARCHITECTURE.md) · [MODULES.md](MODULES.md) · [BUILD.md](BUILD.md)

---

## 1. 基本原則

- **動的コード生成の完全禁止** — `System.Reflection.Emit`、`Expression.Compile()`、動的プロキシ生成などは使用不可
- **リフレクションの使用禁止** — `Type.GetType()`、`Activator.CreateInstance()`、`MethodInfo.Invoke()` 等は使用不可
- **Source Generatorの活用が必須** — DI、MVVM、JSONシリアライズ、XAMLバインディングの全てでSource Generatorベースのライブラリを採用

---

## 2. 必須チェックリスト

新しいコードを追加する際に、以下の項目を確認すること。

### DI・サービス登録

- [ ] DI登録は `Composition.cs` にPure.DIで記述しているか
- [ ] `Microsoft.Extensions.DependencyInjection` 等のリフレクションベースDIを使用していないか

### ViewModel・データバインディング

- [ ] `[ObservableProperty]` / `[RelayCommand]` はCommunityToolkit.Mvvm（Source Generator）を使用しているか
- [ ] XAMLバインディングは `x:DataType` 指定のCompiled Bindingか
- [ ] `{Binding}` にパス文字列を使う場合、`x:DataType` がViewのルートに設定されているか

### シリアライズ

- [ ] JSONシリアライズには `JsonSerializerContext` 派生クラス（Source Generator）を使用しているか
- [ ] `JsonSerializer.Serialize/Deserialize` 呼び出しでSource Generatorのコンテキストを渡しているか

### 禁止パターン

- [ ] `Type.GetType()`、`Activator.CreateInstance()` を使用していないか
- [ ] `Assembly.Load()` / `Assembly.GetTypes()` を使用していないか
- [ ] `dynamic` キーワードを使用していないか
- [ ] `System.Reflection.Emit` を使用していないか

---

## 3. 技術別の対応方法

### 3.1 DI — Pure.DI

Pure.DIはSource Generatorベースのデータモデルを解析しコンパイル時にDIコンテナを生成するため、NativeAOTと完全に互換性がある。

**登録ルール**:

- 全てのViewModel・Serviceは `Composition.cs` の `Setup()` メソッド内に登録する
- ライフタイムは原則 `Singleton` を使用（本アプリケーションは単一ウィンドウ構成のため）
- インターフェースバインドを使用し、テスタビリティを確保する
- `Root<T>()` でエントリポイント（`MainWindowView`）を定義する

**現在の登録一覧**:

| カテゴリ | バインド | ライフタイム |
|---------|---------|------------|
| ViewModel | `MainWindowViewModel` | Singleton |
| ViewModel | `ISettingsViewModel` → `SettingsViewModel` | Singleton |
| ViewModel | `IDatabaseLoadingViewModel` → `DatabaseLoadingViewModel` | Singleton |
| ViewModel | `IMapListViewModel` → `MapListViewModel` | Singleton |
| ViewModel | `MapListPageViewModel` | Singleton |
| ViewModel | `AudioPlayerViewModel` | Singleton |
| ViewModel | `ILicensesViewModel` → `LicensesViewModel` | Singleton |
| Service | `ISettingsService` → `SettingsService` | Singleton |
| Service | `IDatabaseService` → `DatabaseService` | Singleton |
| Service | `IGenerateCollectionService` → `GenerateCollectionService` | Singleton |
| Service | `IFilterPresetService` → `FilterPresetService` | Singleton |
| Service | `IAudioPlayerService` → `AudioPlayerService` | Singleton |

### 3.2 MVVM — CommunityToolkit.Mvvm

CommunityToolkit.MvvmはSource Generatorにより `[ObservableProperty]`、`[RelayCommand]` 等の属性からボイラープレートコードをコンパイル時に生成する。

**注意事項**:

- `[ObservableProperty]` とJSONシリアライズを同一クラスで併用する場合、Source Generator間の競合に注意が必要
- `SettingsData` では `[ObservableProperty]` を使用せず手動で `SetProperty` を呼び出すことでJSONシリアライズとの共存を実現している
- ViewModelの基底クラス `ViewModelBase` は `ObservableObject` を継承し、`CompositeDisposable` でR3のライフサイクル管理を行う

#### CommunityToolkit.Mvvm の継承パターン

譜面一覧系 ViewModel（`MapListViewModel` / `BeatmapGenerationPageViewModel`）は共通基底 `BeatmapListViewModelBase` を介してページング・ContextMenu などの責務を共有している。CTM の Source Generator を継承境界をまたいで使う際に踏むべき注意点を以下に整理する。本パターンはリフレクション・IL Emit を一切導入せず、AOT 安全である。

- **`[ObservableProperty]` 由来の partial メソッドは継承境界を越えられない**: CTM が生成する `partial void On<Name>Changed(...)` / `On<Name>Changing(...)` は C# 仕様上 **暗黙 `private`** であり、`virtual` / `override` / `new` / `sealed` 化できない。そのため `[ObservableProperty]` の宣言と `OnXxxChanged` partial 実装は **基底クラス内に閉じる** こと。派生で差分を入れたい場合は、別途 `protected virtual` フックを基底に用意して partial から呼び出す形にする。
- **`OnXxxChanged` partial の発火順序**: 生成された setter は「`field = value;` → `OnXxxChanged(...)` → `OnPropertyChanged()`」の順に実行する。`PropertyChanged` 後の順序が必要な処理（例: `SelectedBeatmap` を引き金にする AudioPlayer 起動）を partial に書くと R3 の `ObserveProperty` 経路と発火順がずれるため、**順序要件のある処理は R3 購読のまま維持する**。
- **`[RelayCommand]` は基底に置けば派生から再利用可能**: 生成される `XxxCommand` プロパティは `public` で、派生クラスから `XxxCommand.NotifyCanExecuteChanged()` を呼べる。`CanExecute` 判定の派生差分は `protected virtual bool` フックで吸収する（基底が partial や private メソッドを介して評価する形にしない）。
- **派生の leaf 状態は派生で `[ObservableProperty]` を継続使用してよい**: `BeatmapGenerationPageViewModel.IsGenerating` のように、継承境界を越えて partial を共有する必要がない leaf 状態は派生クラス側で `[ObservableProperty]` を使ってよい。基底と派生で同名プロパティが衝突しない限り、混在しても AOT 互換性は損なわれない。
- **リフレクション・IL Emit は不要**: CTM の Source Generator は C# コードを生成するだけで `System.Reflection.Emit` / `MakeGenericType` 等は呼び出さない。本継承パターンも `protected virtual` フックと `[RelayCommand]` 継承のみで実現しており、リフレクション・動的型生成・`dynamic` を一切導入しない。

### 3.3 JSONシリアライズ

`System.Text.Json` のSource Generator（`JsonSerializerContext`）を使用し、リフレクションベースのシリアライズを完全に排除する。

**JsonSerializerContext一覧**:

| コンテキストクラス | 対象型 | 配置場所 | 用途 |
|-------------------|--------|---------|------|
| `SettingsJsonContext` | `SettingsData` | `Features/Settings/SettingsData.cs` | アプリ設定の永続化 |
| `FilterPresetJsonContext` | `FilterPreset`, `List<FilterPreset>`, `FilterConditionData` | `Features/MapList/Models/FilterPreset.cs` | フィルタプリセットの保存・読み込み |
| `LanguageJsonContext` | `Dictionary<string, JsonElement>` | `Features/Translate/LanguageJsonContext.cs` | 言語リソースJSONの読み込み |

**新しいJSON型を追加する場合**:

- [ ] `JsonSerializerContext` を継承した `partial class` を作成する
- [ ] `[JsonSerializable(typeof(対象型))]` 属性でシリアライズ対象を指定する
- [ ] `[JsonSourceGenerationOptions]` で命名ポリシー等を設定する
- [ ] `JsonSerializer.Serialize/Deserialize` 呼び出し時に `.Default` プロパティ経由でコンテキストを渡す

### 3.4 XAMLバインディング

csprojで `AvaloniaUseCompiledBindingsByDefault` を `true` に設定しており、全てのバインディングがコンパイル時に解決される。

**必須ルール**:

- [ ] 全てのView（`.axaml`）に `x:DataType` を指定する
- [ ] コンパイル済みバインディングが解決できないパスを使用しない
- [ ] `ReflectionBinding` は使用しない

### 3.5 翻訳（TranslateExtension）

`TranslateExtension` はAvaloniaの `MarkupExtension` を継承したXAMLマークアップ拡張で、リフレクションを一切使用せずに翻訳を実現している。

**NativeAOT対応のポイント**:

- `ProvideValue` で `LanguageService.Instance.GetString(Key)` を直接呼び出し、文字列値を返す
- 言語変更時の動的更新は `WeakReference<AvaloniaObject>` を使った弱参照サブスクリプションで実現
- `AvaloniaProperty.SetValue` による直接的な値設定でリフレクションを回避
- `TranslateWeakEventManager` がターゲットオブジェクトの破棄を自動検出し、メモリリークを防止

---

## 4. メモリ管理

NativeAOTの最大限のパフォーマンスを引き出すため、メモリ管理にも注意を払っている。

### UnmanagedBuffer（NativeMemory.Alloc/Free）

DBファイル（osu!.db等、数百MB規模）の読み込みにはアンマネージドメモリを使用し、Large Object Heap（LOH）への配置を回避する。

- `NativeMemory.Alloc` でアンマネージドメモリを確保
- `IDisposable` パターンで `NativeMemory.Free` による確実な解放を保証
- `using` ステートメントでスコープベースのライフサイクル管理

### Span\<byte\>ベースのゼロコピーパース

DBファイルのパースでは `Span<byte>` / `ReadOnlySpan<byte>` を活用し、ヒープアロケーションを最小化している。

- `UnmanagedBuffer.GetBufferSpan()` でアンマネージドメモリをSpanとして取得
- ULEB128デコード等のバイナリ解析をSpan上で直接実行
- 文字列はオフセット情報のみ保持し、必要時にのみ `Encoding.UTF8.GetString()` で変換

### ZLinqによるアロケーション削減

コレクション操作には `ZLinq` を使用し、LINQ操作時の中間アロケーションを削減している。

- `AsValueEnumerable().Where().ToArray()` パターンでフィルタリング実行
- 通常のLINQと比較して中間イテレータのヒープアロケーションを回避

### Beatmap record型のイミュータビリティ

`Beatmap` は `sealed record` として定義され、`init` プロパティにより構築後の変更を禁止している。

- パース完了後のデータは不変であることが型レベルで保証される
- スレッドセーフ性と参照透明性を確保
- `sealed` 修飾子によりJIT/AOTコンパイラの最適化（仮想呼び出しの除去等）を促進

---

## 5. csproj NativeAOT設定

Release構成でのみ NativeAOT が有効になる。以下は `NakuruTool_Avalonia_AOT.csproj` の設定一覧。

### 共通設定

| プロパティ | 値 | 説明 |
|-----------|-----|------|
| `TargetFramework` | `net10.0` | .NET 10をターゲット |
| `AllowUnsafeBlocks` | `true` | ポインタ操作（UnmanagedBuffer、P/Invoke）を許可 |
| `AvaloniaUseCompiledBindingsByDefault` | `true` | 全バインディングをコンパイル時解決に強制 |

### Release構成専用設定（NativeAOT）

| プロパティ | 値 | 説明 |
|-----------|-----|------|
| `PublishAot` | `true` | NativeAOTパブリッシュを有効化 |
| `InvariantGlobalization` | `false` | カルチャ依存の動作を維持（多言語対応のため） |
| `StripSymbols` | `true` | デバッグシンボルを除去してバイナリサイズを削減 |
| `IlcOptimizationPreference` | `Speed` | 実行速度を優先した最適化 |
| `IlcGenerateStackTraceData` | `false` | スタックトレースデータの生成を無効化（バイナリサイズ削減） |
| `TieredCompilation` | `false` | 段階的コンパイルを無効化（AOTでは不要） |
| `TieredCompilationQuickJit` | `false` | Quick JITを無効化（AOTでは不要） |
| `EventSourceSupport` | `false` | EventSourceサポートを無効化（バイナリサイズ削減） |
| `UseSystemResourceKeys` | `true` | 例外メッセージをリソースキーのみにしてバイナリサイズを削減 |
| `IlcTrimMetadata` | `true` | 不要なメタデータをトリムしてバイナリサイズを削減 |

> ビルド手順の詳細は [BUILD.md](BUILD.md) を参照。

---

## 6. ネイティブライブラリ統合

本プロジェクトではオーディオ再生機能にRust製ネイティブライブラリ（`nakuru_audio`）を使用している。

### MSBuildターゲットによるRustビルド統合

csprojに定義された2つのMSBuildカスタムターゲットにより、C#ビルドプロセスにRustビルドを統合している。

| ターゲット名 | 実行タイミング | 役割 |
|-------------|-------------|------|
| `BuildRustLibrary` | `BeforeBuild` の前 | `cargo build` を実行し、生成された `nakuru_audio.dll` を出力ディレクトリにコピー |
| `CopyNativeLibraryToPublish` | `ComputeResolvedFilesToPublishList` の前 | `dotnet publish` 時に `nakuru_audio.dll` をパブリッシュディレクトリに同梱 |

- `Configuration` に応じて `debug` / `release` プロファイルが自動選択される
- Rustライブラリのソースは `native/nakuru_audio/` に配置

### LameMp3Encoder — LibraryImportパターン

本プロジェクトでは MP3 / OGG / WAV のエンコードを含むオーディオレート変換を FFmpeg subprocess に委譲しており、LAME を P/Invoke で in-process 呼び出しする実装は採用していない。旧 `LameMp3Encoder`（`[LibraryImport]` ラッパー）および `SoundTouch` の `[DllImport]` ラッパーは FFmpeg 移行に伴い削除済み。

### FFmpeg — subprocess 呼び出しパターン

`FfmpegAudioRateChanger` はオーディオレート変換・エンコードを FFmpeg の subprocess 実行で行う。P/Invoke による in-process リンクは行わないため、本アプローチ自体が AOT 互換性を大きく向上させる。

**NativeAOTとの統合ポイント**:

- `System.Diagnostics.Process.Start` + `ProcessStartInfo.ArgumentList` は NativeAOT で完全サポートされる（リフレクション非依存）
- 引数リストは `ArgumentList.Add(...)` で個別に渡すため、シェルエスケープ問題と AOT 非互換なパーサー経路を同時に回避
- stdin / stdout / stderr のパイプは `StreamReader` / `StreamWriter` の同期・非同期 API を通じて扱い、`CancellationToken` で安全に中断可能
- `ffmpeg.exe` は `native/ffmpeg/win-x64/` に同梱され、`FfmpegBinaryLocator` がアプリディレクトリ基準で実行パスを解決する
- 入力オーディオのメタデータ（チャンネル数・サンプルレート）は純 C# パーサー `AudioInputMetadataReader`（`BinaryPrimitives` ベース）で抽出しており、P/Invoke も subprocess も使わない。リフレクション・動的コード生成を使わないため AOT 安全
- `FfmpegArgumentsBuilder` は純関数 static クラスであり、引数組立にリフレクション・動的コード生成・dynamic を一切用いない

| 設定項目 | 値 |
|---------|-----|
| 実行バイナリ | `native/ffmpeg/win-x64/ffmpeg.exe` |
| ビルド | BtbN/FFmpeg-Builds n8.1 LGPL static（win64, LGPL-2.1-or-later。同梱 `LICENSE.txt` は LGPL v3 本文） |
| 呼び出し方式 | `Process.Start` + `ProcessStartInfo.ArgumentList` |
| AOT互換性 | 完全互換（P/Invoke 不要） |
| 引数組立 | `FfmpegArgumentsBuilder`（純関数 static） |
| 実行パス解決 | `FfmpegBinaryLocator` |
| プロセス制御 | `FfmpegProcessRunner`（stderr 回収・`CancellationToken` 対応） |
| エラー型 | `FfmpegExecutionException` |
| 入力メタデータ抽出 | 純 C# パーサー `AudioInputMetadataReader`（`BinaryPrimitives` ベース、JSON 非使用） |

### csbindgenによるP/Invokeバインディング生成

Rust側の `build.rs` で [csbindgen](https://github.com/Cysharp/csbindgen) を使用し、`extern "C"` 関数からC#のP/Invokeバインディングを自動生成する。

| 設定項目 | 値 |
|---------|-----|
| 入力ファイル | `src/lib.rs` |
| DLL名 | `nakuru_audio` |
| C#名前空間 | `NakuruTool_Avalonia_AOT.Features.AudioPlayer` |
| C#クラス名 | `NativeMethods` |
| アクセシビリティ | `internal` |
| 関数ポインタ | `unmanaged[Cdecl]`（`csharp_use_function_pointer(true)`） |
| 出力先 | `Features/AudioPlayer/NativeMethods.g.cs` |

### Rustライブラリの構成

| 項目 | 値 |
|------|-----|
| クレートタイプ | `cdylib`（動的ライブラリ） |
| 主要依存 | `rodio`（オーディオ再生）、`parking_lot`（同期プリミティブ） |
| ビルドツール | `csbindgen`（C#バインディング生成） |

**NativeAOTとの統合ポイント**:

- `DllImport` 属性による静的なP/Invoke宣言はNativeAOTで完全サポートされる
- `CallingConvention.Cdecl` と `ExactSpelling = true` を指定し、ランタイムのマーシャリングオーバーヘッドを最小化
- コールバックには `delegate* unmanaged[Cdecl]`（関数ポインタ）を使用し、マネージドデリゲートのマーシャリングを回避
