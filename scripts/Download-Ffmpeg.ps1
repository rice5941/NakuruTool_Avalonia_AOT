#Requires -Version 5.1
<#
.SYNOPSIS
  ffmpeg.exe が存在しない場合、BtbN LGPL ビルドをダウンロード・展開する。

.PARAMETER DestPath
  ffmpeg.exe の配置先フルパス。

.PARAMETER Url
  ダウンロード ZIP の URL。

.PARAMETER ExpectedSha256
  ffmpeg.exe の期待 SHA-256（大文字 HEX）。空文字の場合は検証をスキップ。
#>
param(
    [Parameter(Mandatory)][string]$DestPath,
    [Parameter(Mandatory)][string]$Url,
    [string]$ExpectedSha256 = ""
)

$ErrorActionPreference = 'Stop'

if (Test-Path $DestPath) {
    Write-Host "ffmpeg.exe は既に存在します: $DestPath" -ForegroundColor DarkGray
    exit 0
}

$dir = Split-Path $DestPath -Parent
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

Write-Host "ffmpeg.exe が見つかりません。ダウンロードを開始します..." -ForegroundColor Cyan
Write-Host "  URL: $Url" -ForegroundColor DarkGray

$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) "ffmpeg_download_$(Get-Random).zip"

try {
    # ダウンロード（ファイルサイズが大きいため WebClient で進捗表示）
    $wc = New-Object System.Net.WebClient
    $wc.DownloadFile($Url, $zipPath)
    Write-Host "  ダウンロード完了。ZIP を展開中..." -ForegroundColor DarkGray

    Add-Type -Assembly System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        # ZIP 内は ffmpeg-n8.1-latest-win64-lgpl/bin/ffmpeg.exe のような構造
        $entry = $zip.Entries |
            Where-Object { $_.Name -eq 'ffmpeg.exe' } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw "ffmpeg.exe が ZIP 内に見つかりませんでした。ZIP 構造を確認してください: $Url"
        }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $DestPath)
    } finally {
        $zip.Dispose()
    }
} finally {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    }
}

# SHA-256 検証
if ($ExpectedSha256 -ne "") {
    $actual   = (Get-FileHash $DestPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $expected = $ExpectedSha256.ToUpperInvariant()
    if ($actual -ne $expected) {
        Write-Warning "ffmpeg.exe の SHA-256 が一致しません。"
        Write-Warning "  期待値 : $expected"
        Write-Warning "  実際値 : $actual"
        Write-Warning "異なるバージョンがダウンロードされた可能性があります。"
        Write-Warning "意図的なアップグレードの場合は csproj の FfmpegExpectedSha256 を更新してください。"
    } else {
        Write-Host "  SHA-256 検証 OK" -ForegroundColor Green
    }
}

Write-Host "ffmpeg.exe のダウンロードが完了しました: $DestPath" -ForegroundColor Green
