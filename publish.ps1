# NakuruTool NativeAOT Publish Script
# このスクリプトはvswhere.exeをPATHに追加してNativeAOTビルドを実行します

$ErrorActionPreference = "Stop"

# vswhere.exeのパスを追加
$vsWherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
if (Test-Path $vsWherePath) {
    $env:PATH = "$vsWherePath;$env:PATH"
    Write-Host "vswhere.exe added to PATH" -ForegroundColor Green
} else {
    Write-Host "Warning: vswhere.exe not found at $vsWherePath" -ForegroundColor Yellow
}

# プロジェクトディレクトリに移動
$projectDir = Join-Path $PSScriptRoot "NakuruTool_Avalonia_AOT\NakuruTool_Avalonia_AOT"
Push-Location $projectDir

Write-Host "Publishing NakuruTool with NativeAOT..." -ForegroundColor Cyan

# NativeAOT publish
dotnet publish -c Release -r win-x64

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish successful!" -ForegroundColor Green
    Write-Host "Output: $projectDir\bin\Release\net10.0\win-x64\publish\" -ForegroundColor Green

    # ファイルサイズを表示
    $publishDir = Join-Path $projectDir "bin\Release\net10.0\win-x64\publish"
    $exeFile = Join-Path $publishDir "NakuruTool.exe"
    $dllFile = Join-Path $publishDir "nakuru_audio.dll"
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

    $soundTouchDllFile = Join-Path $publishDir "SoundTouch.dll"

    if (Test-Path $soundTouchDllFile) {
        $soundTouchDllSize = (Get-Item $soundTouchDllFile).Length / 1MB
        Write-Host "  SoundTouch.dll: $([math]::Round($soundTouchDllSize, 1)) MB" -ForegroundColor White
    }
    # ライセンスファイルを licenses/ フォルダに集約して同梱
    $licensesDir = Join-Path $publishDir "licenses"
    if (-not (Test-Path $licensesDir)) {
        New-Item -ItemType Directory -Path $licensesDir -Force | Out-Null
    }

    # プロジェクト LICENSE (MIT)
    $licenseSource = Join-Path $PSScriptRoot "LICENSE"
    if (Test-Path $licenseSource) {
        Copy-Item -Path $licenseSource -Destination (Join-Path $licensesDir "LICENSE") -Force
        Write-Host "  Included LICENSE" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: LICENSE not found, skipping." -ForegroundColor Yellow
    }

    # THIRD-PARTY-NOTICES.md
    $thirdPartySource = Join-Path $PSScriptRoot "THIRD-PARTY-NOTICES.md"
    if (Test-Path $thirdPartySource) {
        Copy-Item -Path $thirdPartySource -Destination (Join-Path $licensesDir "THIRD-PARTY-NOTICES.md") -Force
        Write-Host "  Included THIRD-PARTY-NOTICES.md" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: THIRD-PARTY-NOTICES.md not found, skipping." -ForegroundColor Yellow
    }

    # SoundTouch LGPL ライセンスファイル
    $soundTouchLicenseSource = Join-Path $PSScriptRoot "SoundTouch\COPYING.TXT"
    if (Test-Path $soundTouchLicenseSource) {
        Copy-Item -Path $soundTouchLicenseSource -Destination (Join-Path $licensesDir "SoundTouch_COPYING.TXT") -Force
        Write-Host "  Included SoundTouch COPYING.TXT" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: SoundTouch COPYING.TXT not found, skipping." -ForegroundColor Yellow
    }

    # LAME LGPL ライセンスファイル
    $lameLicenseSource = Join-Path $PSScriptRoot "Lame\COPYING.TXT"
    if (Test-Path $lameLicenseSource) {
        Copy-Item -Path $lameLicenseSource -Destination (Join-Path $licensesDir "LAME_COPYING.TXT") -Force
        Write-Host "  Included LAME COPYING.TXT" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: LAME COPYING.TXT not found, skipping." -ForegroundColor Yellow
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
            Remove-Item -Path $versionedDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Copy-Item -Path $publishDir -Destination $versionedDir -Recurse -Force
        Write-Host "  Copied to: $versionedDir" -ForegroundColor Cyan
    } else {
        Write-Host "  Warning: Version not found in .csproj, skipping versioned folder creation." -ForegroundColor Yellow
    }
} else {
    Write-Host "`nPublish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}

Pop-Location
