@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

:RUN_APP
title Any Uninstaller (Avalonia)
echo Starting Any Uninstaller (Avalonia)...

:: Close any currently running instances so build can overwrite bin files
taskkill /F /IM AnyUninstaller.exe >nul 2>&1
taskkill /F /IM AnyUninstaller.Avalonia.exe >nul 2>&1
powershell -NoProfile -Command "Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*Any Uninstaller*' } | Stop-Process -Force -ErrorAction SilentlyContinue" >nul 2>&1

echo Building latest changes...
dotnet build "source\AnyUninstaller.Avalonia\AnyUninstaller.Avalonia.csproj" --no-restore
if %errorlevel% neq 0 (
    echo [INFO] Restoring and rebuilding dependencies...
    dotnet build "source\AnyUninstaller.Avalonia\AnyUninstaller.Avalonia.csproj"
    if !errorlevel! neq 0 (
        echo.
        echo [ERROR] Build failed with exit code !errorlevel!.
        pause
        exit /b !errorlevel!
    )
)

:: Launch the updated application
echo Launching Any Uninstaller...
if exist "%~dp0bin\AnyUninstaller.Avalonia.dll" (
    start "" dotnet "%~dp0bin\AnyUninstaller.Avalonia.dll" %*
    exit /b 0
)

:: Fallback run directly with dotnet
dotnet run --project "%~dp0source\AnyUninstaller.Avalonia\AnyUninstaller.Avalonia.csproj" %*
