@echo off
title Cortex DNA - Dev Launcher

:: ── UAC Elevation ──────────────────────────────────────────
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [Cortex DNA] Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)
:: ───────────────────────────────────────────────────────────

cd /d "%~dp0"

echo [Cortex DNA] Stopping any running instance...
taskkill /F /IM CortexDNA.exe >nul 2>&1

echo [Cortex DNA] Building project...
dotnet build "%~dp0src\CortexDNA.App\CortexDNA.App.csproj" -c Debug --nologo
if %errorlevel% neq 0 (
    echo.
    echo [Cortex DNA] Build failed! Check errors above.
    pause
    exit /b %errorlevel%
)

echo [Cortex DNA] Launching Cortex DNA...
start "" "%~dp0src\CortexDNA.App\bin\Debug\net10.0-windows\CortexDNA.exe"

exit /b 0