@echo off
REM NakuruTool NativeAOT Publish Script
REM このスクリプトはvswhere.exeをPATHに追加してNativeAOTビルドを実行します

setlocal

REM vswhere.exeのパスを追加
set "VSWHERE_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"
if exist "%VSWHERE_PATH%\vswhere.exe" (
    echo vswhere.exe added to PATH
    set "PATH=%VSWHERE_PATH%;%PATH%"
) else (
    echo Warning: vswhere.exe not found
)

REM プロジェクトディレクトリに移動
cd /d "%~dp0NakuruTool_Avalonia_AOT\NakuruTool_Avalonia_AOT"

echo Publishing NakuruTool with NativeAOT...
echo.

REM NativeAOT publish
dotnet publish -c Release -r win-x64

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Publish successful!
    echo Output: %CD%\bin\Release\net10.0\win-x64\publish\
) else (
    echo.
    echo Publish failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

endlocal
