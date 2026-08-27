@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

:: Check for Administrator privileges
net session >nul 2>&1
if %errorlevel% equ 0 (
    goto :RUN_APP
)

:: If already passed 'elevated' or '--no-elevation', don't loop
if /i "%1"=="elevated" goto :RUN_APP
if /i "%1"=="--no-elevation" goto :RUN_APP
if /i "%1"=="--user" goto :RUN_APP

echo Requesting Administrator privileges...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/k cd /d """%~dp0.""" && """%~f0""" elevated' -WorkingDirectory '%~dp0.' -Verb RunAs" >nul 2>&1
if %errorlevel% equ 0 (
    exit /b 0
)

echo.
echo [INFO] Administrator elevation was not granted or cancelled.
echo Launching in standard user mode...
echo.

:RUN_APP
title Any Uninstaller (Avalonia)
echo Starting Any Uninstaller (Avalonia)...

if /i "%1"=="--build" (
    echo Building latest changes...
    dotnet build "source\AnyUninstaller.Avalonia\AnyUninstaller.Avalonia.csproj"
)

:: If precompiled binary exists in bin\, launch it directly
if exist "bin\AnyUninstaller.Avalonia.exe" (
    echo Launching prebuilt application...
    start "" "bin\AnyUninstaller.Avalonia.exe" %*
    exit /b 0
)

:: Otherwise build and run with dotnet
dotnet run --project "source\AnyUninstaller.Avalonia\AnyUninstaller.Avalonia.csproj" %*
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Application failed with exit code %errorlevel%.
    pause
)
