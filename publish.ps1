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

    $stretchDllFile = Join-Path $publishDir "nakuru_stretch.dll"

    if (Test-Path $stretchDllFile) {
        $stretchDllSize = (Get-Item $stretchDllFile).Length / 1MB
        Write-Host "  nakuru_stretch.dll: $([math]::Round($stretchDllSize, 1)) MB" -ForegroundColor White
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
    Write-Host "`nPublish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}

Pop-Location
