@echo off
title Cortex DNA - Release Builder
color 0A

:: ── UAC Elevation ──────────────────────────────────────────
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [Cortex DNA] Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)
:: ───────────────────────────────────────────────────────────

cd /d "E:\Code-Setup\CortexDNA"

echo.
echo ╔══════════════════════════════════════════╗
echo ║       Cortex DNA - Release Builder       ║
echo ╚══════════════════════════════════════════╝
echo.

:: ── Step 1: Clean old publish folder ───────────────────────
echo [1/3] Cleaning old publish folder...
if exist "bin\Release\net10.0-windows\win-x64\publish\" (
    rd /s /q "bin\Release\net10.0-windows\win-x64\publish"
    echo       Done - Old files removed.
) else (
    echo       No old publish folder found, skipping.
)
echo.

:: ── Step 2: Publish latest code ────────────────────────────
echo [2/3] Publishing latest build...
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishReadyToRun=true
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] dotnet publish FAILED! Fix build errors first.
    pause
    exit /b 1
)
echo       Done - Latest code published.
echo.

:: ── Step 3: Compile Installer ──────────────────────────────
echo [3/3] Building Installer with Inno Setup...
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if not exist %ISCC% (
    echo [ERROR] Inno Setup not found at: %ISCC%
    echo         Please install Inno Setup 6 or update the path in this script.
    pause
    exit /b 1
)

%ISCC% "E:\Code-Setup\CortexDNA\CortexDNA_Installer.iss"
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Inno Setup compilation FAILED!
    pause
    exit /b 1
)

echo.
echo ╔══════════════════════════════════════════╗
echo ║   ✅  Build Complete! Installer Ready.   ║
echo ╚══════════════════════════════════════════╝
echo.
echo Output: E:\Code-Setup\Cortex Core\app\CortexDNA\bin\Release\Installer\
echo.
pause
