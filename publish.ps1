# NakuruTool NativeAOT Publish Script
# このスクリプトはvswhere.exeをPATHに追加してNativeAOTビルドを実行します

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$projectDir = Join-Path $repoRoot "NakuruTool_Avalonia_AOT\NakuruTool_Avalonia_AOT"
$bungeeDir = Join-Path $repoRoot "native\nakuru_rate_audio\vendor\bungee"
$cargoExe = Join-Path $env:USERPROFILE ".cargo\bin\cargo.exe"

# vswhere.exeのパスを追加
$vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path $vsWherePath) {
    $env:PATH = "$vsWherePath;$env:PATH"
    Write-Host "vswhere.exe added to PATH" -ForegroundColor Green
} else {
    Write-Host "Warning: vswhere.exe not found at $vsWherePath" -ForegroundColor Yellow
}

if (-not (Test-Path (Join-Path $bungeeDir "CMakeLists.txt"))) {
    Write-Host "Error: native\nakuru_rate_audio\vendor\bungee is missing." -ForegroundColor Red
    Write-Host "Run 'git submodule update --init --recursive' from the repository root and retry." -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $cargoExe) -and -not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Host "Error: cargo was not found. Install Rust via rustup and ensure cargo is available." -ForegroundColor Red
    exit 1
}

# libmp3lame.dll を vcpkg でビルドして native/ に配置する関数
function Ensure-LibMp3Lame {
    param([string]$TargetPath)

    if (Test-Path $TargetPath) {
        Write-Host "libmp3lame.dll: exists at native\libmp3lame.dll (skipping build)" -ForegroundColor DarkGray
        return
    }

    # vcpkg を探す（VCPKG_ROOT 環境変数 → よくある場所 → PATH の順）
    $vcpkgExe = $null
    $vcpkgSearchPaths = @(
        $env:VCPKG_ROOT,
        "D:\work\vcpkg",
        "C:\vcpkg",
        "$env:USERPROFILE\vcpkg",
        "C:\src\vcpkg"
    )
    foreach ($p in $vcpkgSearchPaths) {
        if ($p -and (Test-Path (Join-Path $p "vcpkg.exe"))) {
            $vcpkgExe = Join-Path $p "vcpkg.exe"
            break
        }
    }
    if (-not $vcpkgExe) {
        $found = Get-Command vcpkg -ErrorAction SilentlyContinue
        if ($found) { $vcpkgExe = $found.Source }
    }

    if (-not $vcpkgExe) {
        Write-Host "Warning: vcpkg not found. MP3 output will be disabled." -ForegroundColor Yellow
        Write-Host "  To enable MP3: install vcpkg, run 'vcpkg install mp3lame:x64-windows'," -ForegroundColor Yellow
        Write-Host "  then copy libmp3lame.dll to native\libmp3lame.dll" -ForegroundColor Yellow
        return
    }

    Write-Host "Building libmp3lame via vcpkg ($vcpkgExe)..." -ForegroundColor Cyan
    & $vcpkgExe install "mp3lame:x64-windows"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Warning: vcpkg install mp3lame:x64-windows failed. MP3 output will be disabled." -ForegroundColor Yellow
        return
    }

    $vcpkgRoot = Split-Path $vcpkgExe -Parent
    $vcpkgDll = Join-Path $vcpkgRoot "installed\x64-windows\bin\libmp3lame.dll"
    if (Test-Path $vcpkgDll) {
        Copy-Item -Path $vcpkgDll -Destination $TargetPath -Force
        $sz = [math]::Round((Get-Item $TargetPath).Length / 1KB, 1)
        Write-Host "libmp3lame.dll: copied from vcpkg ($sz KB)" -ForegroundColor Green
    } else {
        Write-Host "Warning: libmp3lame.dll not found at expected vcpkg path: $vcpkgDll" -ForegroundColor Yellow
    }
}

Push-Location $projectDir

try {

# libmp3lame.dll を用意（vcpkg ビルド、既存なら再使用）
Ensure-LibMp3Lame -TargetPath (Join-Path $repoRoot "native\libmp3lame.dll")

Write-Host "Publishing NakuruTool with NativeAOT..." -ForegroundColor Cyan

# NativeAOT publish
dotnet publish -c Release -r win-x64
$publishExitCode = $LASTEXITCODE

if ($publishExitCode -eq 0) {
    Write-Host "`nPublish successful!" -ForegroundColor Green
    Write-Host "Output: $projectDir\bin\Release\net10.0\win-x64\publish\" -ForegroundColor Green

    # ファイルサイズを表示
    $publishDir = Join-Path $projectDir "bin\Release\net10.0\win-x64\publish"
    $exeFile = Join-Path $publishDir "NakuruTool.exe"
    $dllFile = Join-Path $publishDir "nakuru_audio.dll"
    $rateAudioDllFile = Join-Path $publishDir "nakuru_rate_audio.dll"
    $lameDllFile = Join-Path $publishDir "libmp3lame.dll"
    $userGuideSource = Join-Path $PSScriptRoot "USER_GUIDE.url"
    $userGuideDestination = Join-Path $publishDir "USER_GUIDE.url"
    $presetsSource = Join-Path $PSScriptRoot "presets"
    $presetsDestination = Join-Path $publishDir "presets"

    if (Test-Path $exeFile) {
        $exeSize = (Get-Item $exeFile).Length / 1MB
        Write-Host "  NakuruTool.exe: $([math]::Round($exeSize, 1)) MB" -ForegroundColor White
    }

    if (Test-Path $dllFile) {
        $dllSize = (Get-Item $dllFile).Length / 1MB
        Write-Host "  nakuru_audio.dll: $([math]::Round($dllSize, 1)) MB" -ForegroundColor White
    }

    if (Test-Path $rateAudioDllFile) {
        $rateAudioDllSize = (Get-Item $rateAudioDllFile).Length / 1MB
        Write-Host "  nakuru_rate_audio.dll: $([math]::Round($rateAudioDllSize, 1)) MB" -ForegroundColor White
    }

    # libmp3lame.dll を配置（オプション依存）
    $lameSource = Join-Path $repoRoot "native\libmp3lame.dll"
    if (Test-Path $lameSource) {
        Copy-Item -Path $lameSource -Destination $lameDllFile -Force
        $lameDllSize = (Get-Item $lameDllFile).Length / 1MB
        Write-Host "  libmp3lame.dll: $([math]::Round($lameDllSize, 1)) MB" -ForegroundColor White
    } else {
        Write-Host "  libmp3lame.dll: not found at native\libmp3lame.dll (MP3 output disabled)" -ForegroundColor Yellow
    }
    # 配布用のユーザーガイドを同梱
    if (Test-Path $userGuideSource) {
        Copy-Item -Path $userGuideSource -Destination $userGuideDestination -Force
        Write-Host "  Included USER_GUIDE" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: USER_GUIDE not found, skipping." -ForegroundColor Yellow
    }

    # 配布用のプリセットを同梱
    if (Test-Path $presetsSource) {
        if (Test-Path $presetsDestination) {
            Remove-Item -Path $presetsDestination -Recurse -Force
        }

        Copy-Item -LiteralPath $presetsSource -Destination $presetsDestination -Recurse -Force
        Write-Host "  Included presets folder" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: presets folder not found, skipping." -ForegroundColor Yellow
    }

    # pdbファイルを削除
    $pdbFiles = @(Get-ChildItem -Path $publishDir -Recurse -Filter "*.pdb" -ErrorAction SilentlyContinue)
    if ($pdbFiles.Count -gt 0) {
        $pdbFiles | Remove-Item -Force
        Write-Host "  Removed $($pdbFiles.Count) .pdb file(s)" -ForegroundColor DarkGray
    } else {
        Write-Host "  No .pdb files found" -ForegroundColor DarkGray
    }

    # バージョン番号を .csproj から取得してバージョン付きフォルダにコピー
    $csprojPath = Join-Path $projectDir "NakuruTool_Avalonia_AOT.csproj"
    $xml = [xml](Get-Content $csprojPath -Encoding UTF8)
    $version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ($version) {
        $versionedDir = Join-Path $PSScriptRoot "NakuruTool_$version"
        if (Test-Path $versionedDir) {
            Remove-Item -Path $versionedDir -Recurse -Force
        }
        Copy-Item -Path $publishDir -Destination $versionedDir -Recurse -Force
        Write-Host "  Copied to: $versionedDir" -ForegroundColor Cyan
    } else {
        Write-Host "  Warning: Version not found in .csproj, skipping versioned folder creation." -ForegroundColor Yellow
    }
} else {
    Write-Host "`nPublish failed with exit code $publishExitCode" -ForegroundColor Red
    exit $publishExitCode
}
}
finally {
    Pop-Location
}
