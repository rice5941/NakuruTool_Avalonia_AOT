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
Set-Location $projectDir

Write-Host "Publishing NakuruTool with NativeAOT..." -ForegroundColor Cyan

# NativeAOT publish
dotnet publish -c Release -r win-x64

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish successful!" -ForegroundColor Green
    Write-Host "Output: $projectDir\bin\Release\net10.0\win-x64\publish\" -ForegroundColor Green

    # ファイルサイズを表示
    $publishDir = Join-Path $projectDir "bin\Release\net10.0\win-x64\publish"
    $exeFile = Join-Path $publishDir "NakuruTool_Avalonia_AOT.exe"
    $dllFile = Join-Path $publishDir "nakuru_audio.dll"

    if (Test-Path $exeFile) {
        $exeSize = (Get-Item $exeFile).Length / 1MB
        Write-Host "  NakuruTool_Avalonia_AOT.exe: $([math]::Round($exeSize, 1)) MB" -ForegroundColor White
    }

    if (Test-Path $dllFile) {
        $dllSize = (Get-Item $dllFile).Length / 1MB
        Write-Host "  nakuru_audio.dll: $([math]::Round($dllSize, 1)) MB" -ForegroundColor White
    }
} else {
    Write-Host "`nPublish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
