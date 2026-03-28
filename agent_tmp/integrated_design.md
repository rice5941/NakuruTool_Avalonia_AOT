# 統合設計案: Raspberry Pi 4B (linux-arm64) 対応

## 概要

NakuruTool を Raspberry Pi 4B (linux-arm64, RPi OS Lite 64bit) で動作させるための統合設計。変更は大きく2つのモジュールに分かれる。**Module A（ビルドシステム）** では、csproj の Rust ビルドターゲットを RuntimeIdentifier ベースで条件分岐させ、WSL2 Ubuntu 上で linux-arm64 NativeAOT クロスコンパイルを実行する bash スクリプトと、Windows 側からの呼び出し導線を整備する。**Module B（プラットフォーム抽象化）** では、`MainWindowView` のコンテンツ部分を `MainContentView`（UserControl）に抽出し、Desktop（Window ホスト）と DRM/KMS（`ISingleViewApplicationLifetime`）の両モードで同一 UI を共有する構成へ再編する。`Avalonia.LinuxFramebuffer` パッケージを追加し、`--drm` 引数による起動分岐を `Program.cs` に実装する。Pure.DI の Root 追加、フォントフォールバック拡張も行い、NativeAOT 制約（動的コード生成禁止、Source Generator 使用）を厳守する。

---

## 変更対象ファイル一覧

| # | ファイルパス | 種別 | 概要 |
|---|------------|------|------|
| 1 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT.csproj` | 修正 | Rust ビルドの RID 対応、`Avalonia.LinuxFramebuffer` 追加、`MainContentView` DependentUpon |
| 2 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Program.cs` | 修正 | `--drm` 引数による DRM/Desktop 起動分岐 |
| 3 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/App.axaml` | 修正 | フォントスタイルセレクタの拡張（`UserControl` 向け） |
| 4 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/App.axaml.cs` | 修正 | `ISingleViewApplicationLifetime` 分岐で `composition.MainContent` を設定 |
| 5 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Composition.cs` | 修正 | `.Root<MainContentView>("MainContent")` 追加 |
| 6 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainWindowView.axaml` | 修正 | コンテンツを `MainContentView` に委譲、薄いシェル化 |
| 7 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainWindowView.axaml.cs` | 修正 | `Opened` での `StartLoadingAsync` 呼び出しを削除 |
| 8 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainContentView.axaml` | **新規** | MainWindowView から抽出した UI 本体（UserControl） |
| 9 | `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainContentView.axaml.cs` | **新規** | MainContentView コードビハインド（Loaded で StartLoadingAsync） |
| 10 | `publish.ps1` | 修正 | `$Runtime` パラメータ追加、`linux-arm64` 時 WSL2 呼び出し |
| 11 | `scripts/publish-linux-arm64.sh` | **新規** | WSL2 上の linux-arm64 NativeAOT ビルドスクリプト |
| 12 | `native/nakuru_audio/.cargo/config.toml` | **新規** | Rust aarch64 クロスコンパイル用リンカー・環境変数 |
| 13 | `docs/BUILD.md` | 修正 | WSL2 セットアップ・linux-arm64 ビルド手順を追記 |
| 14 | `docs/ARCHITECTURE.md` | 修正 | MainContentView 追加・DRM モードの記載 |

---

## Module A: ビルドシステム

### A1. csproj 変更

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT.csproj`

#### A1.1 変更不要な項目

以下は変更不要:

- `OutputType=WinExe` — Linux では `WinExe` と `Exe` は同じ ELF バイナリを生成する
- `ApplicationManifest`, `ApplicationIcon` — Linux ビルドでは MSBuild が自動的に無視する
- NativeAOT 設定全般（`PublishAot`, `IlcOptimizationPreference` 等） — linux-arm64 でもそのまま有効

#### A1.2 BuildRustLibrary ターゲットの変更

現在の Windows 固定実装を、ビルドホスト OS と `RuntimeIdentifier` に基づく条件分岐に変更する。

**変更後の全体**:

```xml
<!-- Rust build integration -->
<Target Name="BuildRustLibrary" BeforeTargets="BeforeBuild">
  <PropertyGroup>
    <RustProfile Condition="'$(Configuration)' == 'Debug'">debug</RustProfile>
    <RustProfile Condition="'$(Configuration)' == 'Release'">release</RustProfile>
    <RustBuildFlag Condition="'$(Configuration)' == 'Release'">--release</RustBuildFlag>

    <!-- RuntimeIdentifier から Rust ターゲットを決定 -->
    <RustTarget Condition="'$(RuntimeIdentifier)' == 'linux-arm64'">aarch64-unknown-linux-gnu</RustTarget>
    <RustTargetFlag Condition="'$(RustTarget)' != ''">--target $(RustTarget)</RustTargetFlag>

    <!-- Rust 出力ディレクトリ（クロスコンパイル時はターゲットサブディレクトリが付く） -->
    <RustTargetSubDir Condition="'$(RustTarget)' != ''">$(RustTarget)/</RustTargetSubDir>
    <RustOutputDir>$(ProjectDir)../../native/nakuru_audio/target/$(RustTargetSubDir)$(RustProfile)</RustOutputDir>

    <!-- ビルドホスト OS に基づく cargo コマンド -->
    <CargoCommand Condition="$([MSBuild]::IsOSPlatform('Windows'))">"$(USERPROFILE)/.cargo/bin/cargo"</CargoCommand>
    <CargoCommand Condition="$([MSBuild]::IsOSPlatform('Linux'))">cargo</CargoCommand>

    <!-- ターゲットプラットフォームに基づくネイティブライブラリ名 -->
    <_IsTargetLinux Condition="$(RuntimeIdentifier.StartsWith('linux'))">true</_IsTargetLinux>
    <_IsTargetLinux Condition="'$(RuntimeIdentifier)' == '' And $([MSBuild]::IsOSPlatform('Linux'))">true</_IsTargetLinux>
    <_IsTargetWindows Condition="$(RuntimeIdentifier.StartsWith('win'))">true</_IsTargetWindows>
    <_IsTargetWindows Condition="'$(RuntimeIdentifier)' == '' And $([MSBuild]::IsOSPlatform('Windows'))">true</_IsTargetWindows>

    <NativeLibName Condition="'$(_IsTargetWindows)' == 'true'">nakuru_audio.dll</NativeLibName>
    <NativeLibName Condition="'$(_IsTargetLinux)' == 'true'">libnakuru_audio.so</NativeLibName>
    <NativeLibPath>$(RustOutputDir)/$(NativeLibName)</NativeLibPath>
  </PropertyGroup>

  <!-- Build Rust library -->
  <Exec Command="$(CargoCommand) build $(RustBuildFlag) $(RustTargetFlag) --manifest-path &quot;$(ProjectDir)../../native/nakuru_audio/Cargo.toml&quot;" />

  <!-- Copy native library to output directory -->
  <Copy SourceFiles="$(NativeLibPath)" DestinationFolder="$(OutDir)" Condition="Exists('$(NativeLibPath)')" />
</Target>
```

#### A1.3 CopyNativeLibraryToPublish ターゲットの変更

Publish 時のネイティブライブラリ同梱も同様に条件分岐。**`RelativePath` を `$(NativeLibName)` に変数化**する。

```xml
<!-- Copy native library to publish directory for NativeAOT -->
<Target Name="CopyNativeLibraryToPublish" BeforeTargets="ComputeResolvedFilesToPublishList">
  <PropertyGroup>
    <RustProfile Condition="'$(Configuration)' == 'Debug'">debug</RustProfile>
    <RustProfile Condition="'$(Configuration)' == 'Release'">release</RustProfile>

    <RustTarget Condition="'$(RuntimeIdentifier)' == 'linux-arm64'">aarch64-unknown-linux-gnu</RustTarget>
    <RustTargetSubDir Condition="'$(RustTarget)' != ''">$(RustTarget)/</RustTargetSubDir>
    <RustOutputDir>$(ProjectDir)../../native/nakuru_audio/target/$(RustTargetSubDir)$(RustProfile)</RustOutputDir>

    <_IsTargetLinux Condition="$(RuntimeIdentifier.StartsWith('linux'))">true</_IsTargetLinux>
    <_IsTargetLinux Condition="'$(RuntimeIdentifier)' == '' And $([MSBuild]::IsOSPlatform('Linux'))">true</_IsTargetLinux>
    <_IsTargetWindows Condition="$(RuntimeIdentifier.StartsWith('win'))">true</_IsTargetWindows>
    <_IsTargetWindows Condition="'$(RuntimeIdentifier)' == '' And $([MSBuild]::IsOSPlatform('Windows'))">true</_IsTargetWindows>

    <NativeLibName Condition="'$(_IsTargetWindows)' == 'true'">nakuru_audio.dll</NativeLibName>
    <NativeLibName Condition="'$(_IsTargetLinux)' == 'true'">libnakuru_audio.so</NativeLibName>
    <NativeLibPath>$(RustOutputDir)/$(NativeLibName)</NativeLibPath>
  </PropertyGroup>

  <ItemGroup>
    <ResolvedFileToPublish Include="$(NativeLibPath)" Condition="Exists('$(NativeLibPath)')">
      <RelativePath>$(NativeLibName)</RelativePath>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </ResolvedFileToPublish>
  </ItemGroup>
</Target>
```

#### A1.4 設計判断メモ

- **プロパティ重複**: `BuildRustLibrary` と `CopyNativeLibraryToPublish` で同じプロパティ群を再定義している。MSBuild の Target 内 PropertyGroup はそのターゲットのスコープ外では見えず、`$(RuntimeIdentifier)` は `dotnet publish` 時にしか設定されない場合があるため、**各ターゲット内で個別定義を維持**する。
- **NativeMethods.g.cs**: `const string __DllName = "nakuru_audio"` のまま変更不要。.NET ランタイムが Linux 上で `libnakuru_audio.so` を自動探索する。
- **Exec コマンドのパス区切り**: 明示パスは `/` で統一。cargo は両 OS で `/` を受け付ける。`$(ProjectDir)` 末尾の `\` との混在も問題なし。
- **PackageReference 追加**: `Avalonia.LinuxFramebuffer` を A1 で追加する（B1 と統合。csproj の変更箇所を一元化）。

#### A1.5 PackageReference・ItemGroup 追加（Module B の csproj 変更を統合）

同一 csproj への変更のため、ここにまとめて記載する。

**追加する PackageReference**:
```xml
<PackageReference Include="Avalonia.LinuxFramebuffer" Version="11.3.12" />
```
`Avalonia.Desktop` と同じバージョンで追加。Windows ビルドでもコンパイルは通る（使用されないだけ）。NativeAOT バイナリサイズへの影響は軽微（約 145KB）。

**追加する ItemGroup（DependentUpon）**:
```xml
<Compile Update="Features\MainWindow\MainContentView.axaml.cs">
  <DependentUpon>MainContentView.axaml</DependentUpon>
</Compile>
```

---

### A2. WSL2 ビルドスクリプト

**新規ファイル**: `scripts/publish-linux-arm64.sh`

**配置**: リポジトリの `scripts/` ディレクトリ配下

```bash
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

# ─── ALSA クロスコンパイル用環境変数（.cargo/config.toml にも定義済みだがシェル側でも設定） ───
export PKG_CONFIG_ALLOW_CROSS=1
export PKG_CONFIG_PATH="/usr/lib/aarch64-linux-gnu/pkgconfig"

# ─── NativeAOT Publish ───
cd "$PROJECT_DIR"
echo "Publishing for linux-arm64..."
dotnet publish -c Release -r linux-arm64

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
```

---

### A3. Rust クロスコンパイル設定

**新規ファイル**: `native/nakuru_audio/.cargo/config.toml`

```toml
[target.aarch64-unknown-linux-gnu]
linker = "aarch64-linux-gnu-gcc"

[env]
PKG_CONFIG_ALLOW_CROSS = { value = "1", force = false }
PKG_CONFIG_LIBDIR = { value = "/usr/lib/aarch64-linux-gnu/pkgconfig:/usr/share/pkgconfig", force = false }
```

**設計判断**: `force = false` により、シェル側で環境変数が設定済みの場合はシェル側が優先される。通常運用では `.cargo/config.toml` だけで完結するため再現性が高い。

**変更不要なファイル**:
- `native/nakuru_audio/Cargo.toml` — `crate-type = ["cdylib"]` は Linux で自動的に `libnakuru_audio.so` を出力する
- `native/nakuru_audio/build.rs` — csbindgen は HOST 側で動作し、生成コードはプラットフォーム非依存
- `NativeMethods.g.cs` — 自動生成ファイル。`"nakuru_audio"` の論理名で Linux の `libnakuru_audio.so` も解決される

**Rust 依存クレートの linux-arm64 互換性**:

| クレート | linux-arm64 | 注意点 |
|---------|-------------|--------|
| rodio 0.21 | ○ | cpal → ALSA バックエンド。ビルド時に `libasound2-dev:arm64` が必要 |
| parking_lot 0.12 | ○ | Pure Rust |
| symphonia 0.5.4 | ○ | Pure Rust |
| csbindgen 1.9 | ○ | ビルドスクリプトのみ（HOST で実行） |

---

### A4. publish.ps1 更新

**ファイル**: `publish.ps1`

パラメータブロックと `linux-arm64` 分岐を追加する。既存の win-x64 ビルドロジックはデフォルト動作として維持。

**スクリプト先頭に追加**:

```powershell
param(
    [ValidateSet("win-x64", "linux-arm64")]
    [string]$Runtime = "win-x64"
)
```

**`linux-arm64` 分岐を既存処理の前に挿入**:

```powershell
if ($Runtime -eq "linux-arm64") {
    Write-Host "Building for Raspberry Pi (linux-arm64) via WSL2..." -ForegroundColor Cyan

    # WSL2 の存在確認
    if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
        Write-Host "Error: wsl.exe not found. WSL2 is required for linux-arm64 builds." -ForegroundColor Red
        exit 1
    }

    # Windows パスを WSL パスに変換
    $wslPath = wsl wslpath -u "$PSScriptRoot"

    # WSL2 上でビルドスクリプトを実行
    wsl bash "$wslPath/scripts/publish-linux-arm64.sh"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nWSL2 build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "`nRPi build completed!" -ForegroundColor Green
    exit 0
}

# ── 以下、既存の win-x64 ビルド処理（変更なし） ──
```

**使い方**:

```powershell
# 従来通り Windows ビルド（デフォルト）
.\publish.ps1

# RPi ビルド（WSL2 経由）
.\publish.ps1 -Runtime linux-arm64
```

**成果物フォルダ名**: RID 付きに統一する。

| Runtime | 成果物フォルダ名 |
|---------|----------------|
| win-x64 | `NakuruTool_1.2.0` （既存互換を維持） |
| linux-arm64 | `NakuruTool_1.2.0_linux-arm64` |

**既存の win-x64 成果物フォルダ名は変更しない**（既存の配布フロー・ユーザーへの影響を避けるため）。

---

## Module B: プラットフォーム抽象化

### B1. NuGet パッケージ追加

A1.5 に記載済み（csproj 変更を Module A に統合）。

```xml
<PackageReference Include="Avalonia.LinuxFramebuffer" Version="11.3.12" />
```

NativeAOT との互換性: `Avalonia.LinuxFramebuffer` は P/Invoke ベースの DRM/libinput 操作を行い、リフレクションは使用しない。完全に互換。

---

### B2. Program.cs 変更

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Program.cs`

`--drm` 引数の有無で起動ライフタイムを切り替える。`BuildAvaloniaApp()` は共通のまま維持。

**変更後の全体**:

```csharp
using System;
using Avalonia;
using Avalonia.LinuxFramebuffer;
#if DEBUG
using HotAvalonia;
#endif

namespace NakuruTool_Avalonia_AOT
{
    internal sealed class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var builder = BuildAvaloniaApp();

            if (Array.Exists(args, arg => arg == "--drm"))
            {
                return builder.StartLinuxDrm(args);
            }

            return builder.StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
#if DEBUG
                .UseHotReload()
#endif
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
```

**変更点**:
1. `Main` の戻り値を `void` → `int` に変更（`StartLinuxDrm` は `int` を返す）
2. `--drm` 引数を `Array.Exists` で判定
3. `Avalonia.LinuxFramebuffer` の `using` を追加
4. `UsePlatformDetect()` は DRM モードでは使われないが、Desktop 起動時に必要なため残す

---

### B3. App.axaml.cs 変更

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/App.axaml.cs`

`OnFrameworkInitializationCompleted` の `ISingleViewApplicationLifetime` 分岐で `composition.MainContent` を設定する。

**変更箇所のみ**（`OnFrameworkInitializationCompleted` メソッド）:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    var composition = new Composition();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = composition.MainWindow;
    }
    else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
    {
        singleViewPlatform.MainView = composition.MainContent;
    }

    base.OnFrameworkInitializationCompleted();
}
```

他のメソッド（`Initialize`, `ApplyLanguageCulture`, `UpdateSemiThemeLocale`）は変更なし。

---

### B4. View 構造変更（MainContentView 抽出）

#### 現在の MainWindowView.axaml 構造

```
<Window>
  ├── Window.Resources (CategoryTabItem, PageStackPanel, PageTextBlock)
  ├── Window.Styles (ToggleButton アイコン切替)
  └── <Panel>
       ├── <Grid> (ヘッダーバー + TabControl)
       └── <Border> (オーバーレイ: DatabaseLoadingView)
```

#### 抽出後の構造

**MainWindowView.axaml（薄いシェル）**:
```
<Window Icon=... Title=... MinWidth=800 MinHeight=500>
  └── <mainWindow:MainContentView />
```

**MainContentView.axaml（UI 本体）**:
```
<UserControl FontFamily="Meiryo, Yu Gothic UI, Hiragino Sans, Noto Sans CJK JP, sans-serif">
  ├── UserControl.Resources (CategoryTabItem, PageStackPanel, PageTextBlock)
  ├── UserControl.Styles (ToggleButton アイコン切替)
  └── <Panel>
       ├── <Grid> (ヘッダーバー + TabControl)
       └── <Border> (オーバーレイ)
```

#### B4.1 新規: `MainContentView.axaml`

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainContentView.axaml`

現在の `MainWindowView.axaml` から Window タグ内のコンテンツ全体を抽出した UserControl。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mainWindow="using:NakuruTool_Avalonia_AOT.Features.MainWindow"
             xmlns:mapList="using:NakuruTool_Avalonia_AOT.Features.MapList"
             xmlns:settings="using:NakuruTool_Avalonia_AOT.Features.Settings"
             xmlns:licenses="using:NakuruTool_Avalonia_AOT.Features.Licenses"
             xmlns:importExport="using:NakuruTool_Avalonia_AOT.Features.ImportExport"
             xmlns:osuDatabase="using:NakuruTool_Avalonia_AOT.Features.OsuDatabase"
             xmlns:mi="using:Material.Icons.Avalonia"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d" d:DesignWidth="900" d:DesignHeight="600"
             xmlns:translate="using:NakuruTool_Avalonia_AOT.Features.Translate"
             x:Class="NakuruTool_Avalonia_AOT.Features.MainWindow.MainContentView"
             x:DataType="mainWindow:MainWindowViewModel"
             FontFamily="Meiryo, Yu Gothic UI, Hiragino Sans, Noto Sans CJK JP, sans-serif">

    <UserControl.Resources>
        <!-- 現在の Window.Resources をそのまま移動 -->
        <ControlTheme x:Key="CategoryTabItem" TargetType="TabItem">
            <!-- ... 既存内容そのまま ... -->
        </ControlTheme>
        <ControlTheme x:Key="PageStackPanel" TargetType="StackPanel">
            <!-- ... 既存内容そのまま ... -->
        </ControlTheme>
        <ControlTheme x:Key="PageTextBlock" TargetType="TextBlock">
            <!-- ... 既存内容そのまま ... -->
        </ControlTheme>
    </UserControl.Resources>

    <UserControl.Styles>
        <!-- 現在の Window.Styles をそのまま移動 -->
        <Style Selector="ToggleButton#ExpandButton > mi|MaterialIcon">
            <Setter Property="Kind" Value="MenuClose"/>
        </Style>
        <Style Selector="ToggleButton#ExpandButton:checked > mi|MaterialIcon">
            <Setter Property="Kind" Value="MenuOpen"/>
        </Style>
    </UserControl.Styles>

    <!-- 現在の <Panel> 以下を全てそのまま移動 -->
    <Panel>
        <!-- メインコンテンツ -->
        <Grid RowDefinitions="Auto, *">
            <!-- ヘッダーバー ... 既存内容そのまま ... -->
            <!-- TabControl ... 既存内容そのまま ... -->
        </Grid>

        <!-- オーバーレイ ... 既存内容そのまま ... -->
        <Border IsVisible="{Binding IsLoadingOverlayVisible}" Background="#80000000"
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch">
            <Border Theme="{DynamicResource CardBorder}" HorizontalAlignment="Center" VerticalAlignment="Center"
                    MinWidth="400" MaxWidth="600" Padding="16">
                <osuDatabase:DatabaseLoadingView DataContext="{Binding DatabaseLoadingViewModel}" />
            </Border>
        </Border>
    </Panel>
</UserControl>
```

**設計ポイント**:
- `FontFamily` を UserControl ルート要素に直接設定する。これにより DRM モードでも Window セレクタに依存せずフォントが適用される
- `x:DataType="mainWindow:MainWindowViewModel"` を維持し、コンパイル済みバインディングの互換性を確保
- xmlns 宣言は `MainWindowView.axaml` と同じものを引き継ぐ

#### B4.2 新規: `MainContentView.axaml.cs`

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainContentView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainContentView : UserControl
{
    // XAML パーサー用（Desktop モード: DataContext は Window から伝播）
    public MainContentView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Pure.DI 用（DRM モード: ViewModel をコンストラクタ注入）
    public MainContentView(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Loaded は初回のみ発火するが、安全のためハンドラを解除
        Loaded -= OnLoaded;

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.StartLoadingAsync();
        }
    }
}
```

**設計判断**:
- **2つのコンストラクタ**: パラメーターなし（Desktop：XAML インスタンス化用）とViewModel 付き（DRM：Pure.DI 用）
- **`Loaded` イベント**: Desktop と DRM の両モードで同じタイミングでDB読み込みを開始。`Loaded` は `AttachedToVisualTree` より後に発火し、初回レンダリング準備完了後であることが保証される
- **ハンドラ解除**: `Loaded -= OnLoaded` により二重呼び出しを構造的に防止

#### B4.3 変更: `MainWindowView.axaml`

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainWindowView.axaml`

薄いウィンドウシェルに変更する。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mainWindow="using:NakuruTool_Avalonia_AOT.Features.MainWindow"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="900" d:DesignHeight="600"
        xmlns:translate="using:NakuruTool_Avalonia_AOT.Features.Translate"
        x:Class="NakuruTool_Avalonia_AOT.Features.MainWindow.MainWindowView"
        x:DataType="mainWindow:MainWindowViewModel"
        Icon="/Assets/Nakurage-dot.ico"
        Title="{translate:Translate 'App.Name'}"
        MinWidth="800"
        MinHeight="500">

    <mainWindow:MainContentView />
</Window>
```

**変更内容**:
- `Window.Resources` セクションを削除（`MainContentView` に移動済み）
- `Window.Styles` セクションを削除（`MainContentView` に移動済み）
- `<Panel>` 以下のコンテンツ全体を `<mainWindow:MainContentView />` 1行に置き換え
- 不要な xmlns 宣言を削除（`mapList`, `settings`, `licenses`, `importExport`, `osuDatabase`, `mi` → `MainContentView` 側に移動）
- Window 固有属性（`Icon`, `Title`, `MinWidth`, `MinHeight`, `x:DataType`）はそのまま維持

#### B4.4 変更: `MainWindowView.axaml.cs`

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Features/MainWindow/MainWindowView.axaml.cs`

`Opened` イベントでの `StartLoadingAsync` 呼び出しを削除する（責務は `MainContentView.Loaded` に移動済み）。

```csharp
using Avalonia.Controls;

namespace NakuruTool_Avalonia_AOT.Features.MainWindow;

public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();
    }

    public MainWindowView(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
```

**変更点**: `Opened += async (_, _) => { await viewModel.StartLoadingAsync(); };` を削除。DataContext を設定するだけのシンプルなホストに変更。DataContext は XAML のビジュアルツリー経由で子の `MainContentView` に伝播する。

---

### B5. Composition.cs 変更

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/Composition.cs`

`MainContentView` の Root 登録を追加する。

**変更箇所**（末尾の Root 定義）:

```csharp
// Root（エントリーポイント）の定義
.Root<MainWindowView>("MainWindow")
.Root<MainContentView>("MainContent");
```

**設計ポイント**:
- `MainContentView(MainWindowViewModel)` コンストラクタが Pure.DI により自動解決される（`MainWindowViewModel` は既に Singleton 登録済み）
- Desktop モードでは `composition.MainContent` は使われず、Pure.DI はプロパティアクセス時にのみインスタンス化するため不要なオブジェクト生成は起きない
- DRM モードでは `composition.MainContent` が `ISingleViewApplicationLifetime.MainView` に設定される

---

### B6. フォント対応

#### B6.1 MainContentView のルート要素（B4.1 で対応済み）

`MainContentView.axaml` のルート `<UserControl>` に `FontFamily` を直接設定する。

```xml
FontFamily="Meiryo, Yu Gothic UI, Hiragino Sans, Noto Sans CJK JP, sans-serif"
```

このフォールバック順序:
1. `Meiryo` — Windows 標準日本語フォント
2. `Yu Gothic UI` — Windows 標準日本語フォント（代替）
3. `Hiragino Sans` — macOS 標準日本語フォント（将来対応用）
4. `Noto Sans CJK JP` — Linux 標準日本語フォント（RPi で `fonts-noto-cjk` をインストール）
5. `sans-serif` — 最終フォールバック

#### B6.2 RPi 上でのフォントインストール

```bash
sudo apt install -y fonts-noto-cjk
```

---

### B7. App.axaml 変更

**ファイル**: `NakuruTool_Avalonia_AOT/NakuruTool_Avalonia_AOT/App.axaml`

Window セレクタのフォントスタイルは**そのまま維持**する。これはダイアログ Window など、`MainContentView` 外で表示される Window への適用のため。

```xml
<Application.Styles>
    <semi:SemiTheme Locale="ja-JP" />
    <semi:DataGridSemiTheme />
    <Mi:MaterialIconStyles />
    <Style Selector="Window">
        <Setter Property="FontFamily"
                Value="Meiryo, Yu Gothic UI, Hiragino Sans, Noto Sans CJK JP, sans-serif" />
    </Style>
</Application.Styles>
```

**変更点**: フォントリストに `Noto Sans CJK JP` を追加する（現在のリストに既に含まれている場合は変更不要）。

**現在のリスト**: `Meiryo, Yu Gothic UI, Hiragino Sans, Noto Sans CJK JP, sans-serif` — 既に含まれているため **変更なし**。

---

## Module C: ドキュメント更新

### C1. docs/BUILD.md

以下のセクションを追記する:

#### 追記内容

1. **linux-arm64 ビルド (WSL2)** セクションを新設
   - WSL2 Ubuntu での環境構築手順
   - 必要 apt パッケージ一覧
   - `rustup target add aarch64-unknown-linux-gnu`
   - publish 手順（`.\publish.ps1 -Runtime linux-arm64` または `bash scripts/publish-linux-arm64.sh`）
   - publish 出力先パス

2. **クロスプラットフォームビルド** セクションを更新
   - WSL2 環境での NativeAOT クロスコンパイルの説明
   - Rust ネイティブライブラリのファイル名が OS で変わること（`.dll` / `.so`）

### C2. docs/ARCHITECTURE.md

以下を追記する:

1. **技術スタック** テーブルに `Avalonia.LinuxFramebuffer` を追加
2. **View / ViewModel 対応表** に `MainContentView` を追加
3. **DI 構成** に `.Root<MainContentView>("MainContent")` を追記
4. **初期化フロー** に DRM モードのフローを追記

---

## RPi OS Lite セットアップ手順

RPi 4B (RPi OS Lite 64bit) 上で NakuruTool を実行するために必要な設定。

### 1. 必須パッケージのインストール

```bash
sudo apt update
sudo apt install -y \
    libasound2 \        # ALSA ランタイム（オーディオ再生）
    libfontconfig1 \    # フォント設定（Avalonia/SkiaSharp）
    fonts-noto-cjk \    # 日本語フォント
    libicu-dev \        # ICU（.NET グローバリゼーション）
    libgbm1 \           # GBM（DRM バッファ管理）
    libdrm2 \           # DRM ライブラリ
    libinput10 \        # 入力デバイス管理（タッチ、キーボード）
    libudev1            # udev（デバイス管理）
```

### 2. DRM/KMS の権限設定

```bash
# ユーザーを video グループに追加（DRM デバイスへのアクセス権限）
sudo usermod -aG video $USER
sudo usermod -aG input $USER

# 再ログインして反映
```

### 3. アプリケーションのデプロイ

```bash
# Windows 側でビルドした成果物を RPi に転送
scp -r NakuruTool_1.2.0_linux-arm64/ pi@raspberrypi:~/NakuruTool/

# RPi 上で実行権限を付与
chmod +x ~/NakuruTool/NakuruTool
```

### 4. アプリケーションの起動

```bash
# DRM モードで起動（RPi OS Lite、デスクトップ環境なし）
./NakuruTool --drm
```

---

## WSL2 環境構築手順（開発者向け）

### 1. WSL2 Ubuntu のインストール

```powershell
wsl --install -d Ubuntu
```

### 2. .NET SDK 10.0 のインストール

```bash
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0

echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> ~/.bashrc
source ~/.bashrc
```

### 3. Rust ツールチェーンのインストール

```bash
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
source "$HOME/.cargo/env"
rustup target add aarch64-unknown-linux-gnu
```

### 4. クロスコンパイル用パッケージ

```bash
# multiarch を有効化
sudo dpkg --add-architecture arm64

# arm64 リポジトリの追加（Ubuntu の場合）
sudo tee /etc/apt/sources.list.d/arm64-cross.list << 'EOF'
deb [arch=arm64] http://ports.ubuntu.com/ubuntu-ports/ noble main restricted universe multiverse
deb [arch=arm64] http://ports.ubuntu.com/ubuntu-ports/ noble-updates main restricted universe multiverse
EOF

sudo apt-get update

# 必須パッケージ
sudo apt-get install -y \
    clang \
    llvm \
    binutils-aarch64-linux-gnu \
    gcc-aarch64-linux-gnu \
    g++-aarch64-linux-gnu \
    zlib1g-dev:arm64 \
    libasound2-dev:arm64 \
    pkg-config
```

### 5. 環境確認

```bash
dotnet --version                                  # 10.0.x
rustc --version                                   # 1.92.0+
rustup target list --installed                    # aarch64-unknown-linux-gnu を含む
aarch64-linux-gnu-gcc --version                   # 動作確認
ls /usr/lib/aarch64-linux-gnu/libasound.so        # 存在確認
PKG_CONFIG_ALLOW_CROSS=1 \
PKG_CONFIG_PATH=/usr/lib/aarch64-linux-gnu/pkgconfig \
    pkg-config --libs alsa                        # -lasound が出力
```

---

## 検証手順

### Phase 1: ビルド検証

1. **Windows win-x64 ビルド**（既存動作のリグレッション確認）
   ```powershell
   .\publish.ps1
   # NakuruTool_1.2.0\NakuruTool.exe + nakuru_audio.dll が生成されること
   ```

2. **WSL2 linux-arm64 ビルド**
   ```powershell
   .\publish.ps1 -Runtime linux-arm64
   # NakuruTool_1.2.0_linux-arm64\NakuruTool + libnakuru_audio.so が生成されること
   ```

3. **ビルド成果物確認**
   ```bash
   # WSL2 上で
   file NakuruTool_1.2.0_linux-arm64/NakuruTool
   # → ELF 64-bit LSB executable, ARM aarch64 であること

   file NakuruTool_1.2.0_linux-arm64/libnakuru_audio.so
   # → ELF 64-bit LSB shared object, ARM aarch64 であること
   ```

### Phase 2: RPi 実機検証

1. **デプロイ**
   ```bash
   scp -r NakuruTool_1.2.0_linux-arm64/ pi@raspberrypi:~/NakuruTool/
   ```

2. **DRM モード起動**
   ```bash
   chmod +x ~/NakuruTool/NakuruTool
   ./NakuruTool --drm
   ```

3. **確認項目**
   - [ ] アプリが DRM/KMS モードで画面に表示される
   - [ ] 日本語フォントが正しく表示される
   - [ ] タブナビゲーションが動作する
   - [ ] DB 読み込み（`StartLoadingAsync`）が正常に完了する
   - [ ] オーディオ再生が動作する（ALSA バックエンド経由）
   - [ ] 設定の保存・読み込みが動作する

### Phase 3: Windows リグレッション確認

1. **Desktop モード起動**（引数なし）
   ```powershell
   .\NakuruTool.exe
   ```

2. **確認項目**
   - [ ] 既存の全機能が従来通り動作する
   - [ ] `MainContentView` 抽出による UI の差異がない
   - [ ] DB 読み込みタイミング（`Loaded` イベント）に問題がない

---

## DRM モードでの起動フロー（詳細）

```
[RPi起動]
  → ./NakuruTool --drm
  → Program.Main(args)
  → args に "--drm" を検出
  → BuildAvaloniaApp().StartLinuxDrm(args)
    → Avalonia.LinuxFramebuffer が DrmOutput を初期化
    → LinuxFramebufferLifetime (ISingleViewApplicationLifetime) を生成
    → App.Initialize()
      → AvaloniaXamlLoader.Load() / SemiTheme / ロケール / フォント初期化
    → App.OnFrameworkInitializationCompleted()
      → new Composition()
      → ApplicationLifetime is ISingleViewApplicationLifetime を検出
      → composition.MainContent を生成
        → Pure.DI が MainContentView(MainWindowViewModel) を呼び出し
        → MainWindowViewModel 経由で全 ViewModel/Service が注入
      → singleViewPlatform.MainView = composition.MainContent
    → EmbeddableControlRoot に MainContentView が設定される
    → MainContentView.Loaded イベント発火
      → MainWindowViewModel.StartLoadingAsync() で DB 読み込み開始
    → メインループ実行
```

### Desktop vs DRM 比較表

| 項目 | Desktop | DRM |
|------|---------|-----|
| 起動コマンド | `./NakuruTool` | `./NakuruTool --drm` |
| Lifetime | `IClassicDesktopStyleApplicationLifetime` | `ISingleViewApplicationLifetime` |
| Composition Root | `composition.MainWindow` | `composition.MainContent` |
| トップレベル | `Window` (MainWindowView) | `EmbeddableControlRoot` |
| コンテンツ | MainContentView (Window 内 XAML) | MainContentView (DI 直接生成) |
| DataContext 伝播 | Window → MainContentView (ビジュアルツリー) | コンストラクタ注入 |
| DB 読み込みトリガー | MainContentView.Loaded | MainContentView.Loaded |
| ウィンドウ装飾 | タイトルバー・アイコンあり | なし（全画面） |
| 最小サイズ制約 | MinWidth=800, MinHeight=500 | ディスプレイ解像度に依存 |

---

## 疑問点・未解決事項

### 1. SkiaSharp linux-arm64 ネイティブアセット

Avalonia 11.3.12 が依存する SkiaSharp のバージョンが linux-arm64 のネイティブアセット (`libSkiaSharp.so` arm64) を NuGet パッケージに含んでいるか確認が必要。含まれていない場合、手動でビルドまたは配置する対処が必要になる。

**検証方法**: `dotnet publish -r linux-arm64` を一度実行し、publish フォルダに `libSkiaSharp.so`（arm64）が含まれるか確認する。

### 2. .NET 10 + linux-arm64 + NativeAOT の成熟度

.NET 10 は 2025年11月 GA 予定（2026年3月時点では GA 済みのはず）。`net10.0` + `linux-arm64` + `PublishAot=true` の組み合わせが安定しているか実際のビルドで確認が必要。

### 3. `IlcOptimizationPreference` の RPi 向け調整

現在 `Speed` に設定されているが、RPi 4B はストレージ・メモリが限られる。`Size` に変更するとバイナリサイズ削減・起動速度改善の可能性がある。RID ごとに切り替えるかは検証結果を踏まえて判断する。

### 4. `Loaded` イベントの DRM モードでの動作

`MainContentView` での `StartLoadingAsync` トリガーとして `Loaded` イベントを採用したが、DRM モード（`EmbeddableControlRoot` ホスティング）で確実に発火するか実機検証が必要。問題があれば `AttachedToVisualTree` にフォールバックする。

### 5. ファイルダイアログの DRM モード互換性

ImportExport や Settings で使用しているファイルダイアログ系 API が `ISingleViewApplicationLifetime` 環境で成立するか要確認。`WindowingPlatform` 非依存ではない機能がある場合、DRM モード用の代替実装が必要になる可能性がある。

### 6. ALSA multiarch ソース設定の信頼性

WSL2 Ubuntu で `dpkg --add-architecture arm64` + `libasound2-dev:arm64` をインストールする際、Ubuntu のバージョン（24.04 LTS / 24.10 等）によっては `ports.ubuntu.com` のソースリスト追加手順が異なる場合がある。ビルドスクリプト内の `libasound2-dev:arm64` 存在チェックで早期エラーを出すことで対処する。

### 7. テストコードへの影響

`MainWindowViewScreenshotTests.cs` が `MainWindowView` を直接インスタンス化してスクリーンショットテストを行っている。`MainWindowView` のコンテンツが `MainContentView` に委譲された後も Window のスクリーンショットは撮影可能だが、`FindControl<TabControl>("MainTab")` 等の呼び出しがビジュアルツリーの階層変更により影響を受ける可能性がある。テストの修正は実装後に別タスクで対応する。

### 8. --drm をLinux以外で指定した場合の挙動

`StartLinuxDrm` は Linux 以外では DRM デバイスが存在せずクラッシュする。運用上は RPi 上でのみ `--drm` を使う想定のため、明示的なプラットフォームチェックは追加しない（クラッシュメッセージで十分判別可能）。必要に応じて将来追加。
