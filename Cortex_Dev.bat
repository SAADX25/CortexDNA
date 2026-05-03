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

cd /d "e:\Code-Setup\CortexDNA"
echo [Cortex DNA] Stopping any running instance...
taskkill /F /IM CortexDNA.exe >nul 2>&1
echo [Cortex DNA] Starting in development mode...
dotnet run
pause
