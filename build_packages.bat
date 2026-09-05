@echo off
cd /d "%~dp0"
title Building Any Uninstaller Packages...

echo ========================================================================
echo         Any Uninstaller - Packaging Pipeline (v1.4.2)
echo ========================================================================
echo Target Packages:
echo   1. App:        Release application folder
echo   2. EXE:        Standalone executable
echo   3. Portable:   Portable ZIP distribution with local settings
echo   4. MSIX:       Windows Store package (Saayan.AnyUninstaller)
echo ========================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build_packages.ps1"
set BUILD_STATUS=%errorlevel%

if %BUILD_STATUS% neq 0 (
    echo.
    echo [ERROR] Build pipeline failed with error code %BUILD_STATUS%.
    pause
    exit /b %BUILD_STATUS%
)

echo.
echo All requested packages were built successfully in the dist\ directory!
pause
