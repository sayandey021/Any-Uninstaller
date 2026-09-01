@echo off
cd /d "%~dp0"
title Any Uninstaller - Change Version

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\set_version.ps1" -NewVersion "%~1"
set EXIT_CODE=%errorlevel%

exit /b %EXIT_CODE%
