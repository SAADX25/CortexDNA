@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "%~dp0CortexDNA.csproj"
exit /b %errorlevel%