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

# ffmpeg.exe が存在しなければ事前にダウンロード
$ffmpegNativePath = Join-Path $PSScriptRoot "native\ffmpeg\win-x64\ffmpeg.exe"
if (-not (Test-Path $ffmpegNativePath)) {
    Write-Host "ffmpeg.exe が見つかりません。ダウンロードします..." -ForegroundColor Yellow
    $downloadScript = Join-Path $PSScriptRoot "scripts\Download-Ffmpeg.ps1"
    & $downloadScript `
        -DestPath $ffmpegNativePath `
        -Url "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n8.1-latest-win64-lgpl-8.1.zip" `
        -ExpectedSha256 "4C2891E5DCC1F9A206D43C42CE730163AB947CBC97A447700402136D69095458"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ffmpeg.exe のダウンロードに失敗しました。" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} else {
    Write-Host "ffmpeg.exe 確認済み: $ffmpegNativePath" -ForegroundColor DarkGray
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

    $ffmpegExeFile = Join-Path $publishDir "ffmpeg.exe"
    if (Test-Path $ffmpegExeFile) {
        $ffmpegExeSize = (Get-Item $ffmpegExeFile).Length / 1MB
        Write-Host "  ffmpeg.exe: $([math]::Round($ffmpegExeSize, 1)) MB" -ForegroundColor White
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
    # （FFmpeg subprocess 方式への移行により SoundTouch は同梱していません）

    # LAME LGPL ライセンスファイル
    # （FFmpeg subprocess 方式への移行により LAME DLL は同梱していません。
    #   MP3 エンコードは FFmpeg 同梱の libmp3lame で行われます。
    #   該当ライセンスは FFmpeg_LICENSE.txt に含まれます。）

    # FFmpeg LGPL ライセンス (同梱 ffmpeg.exe 用)
    $ffmpegLicenseSource = Join-Path $PSScriptRoot "native\ffmpeg\win-x64\LICENSE.txt"
    if (Test-Path $ffmpegLicenseSource) {
        Copy-Item -Path $ffmpegLicenseSource -Destination (Join-Path $licensesDir "FFmpeg_LICENSE.txt") -Force
        Write-Host "  Included FFmpeg LICENSE.txt" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: FFmpeg LICENSE.txt not found, skipping." -ForegroundColor Yellow
    }

    # FFmpeg 同梱バイナリの NOTICE
    $ffmpegNoticeSource = Join-Path $PSScriptRoot "native\ffmpeg\win-x64\NOTICE.txt"
    if (Test-Path $ffmpegNoticeSource) {
        Copy-Item -Path $ffmpegNoticeSource -Destination (Join-Path $licensesDir "FFmpeg_NOTICE.txt") -Force
        Write-Host "  Included FFmpeg NOTICE.txt" -ForegroundColor DarkGray
    } else {
        Write-Host "  Warning: FFmpeg NOTICE.txt not found, skipping." -ForegroundColor Yellow
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
