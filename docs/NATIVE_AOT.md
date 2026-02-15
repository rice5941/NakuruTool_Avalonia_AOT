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
