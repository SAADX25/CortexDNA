@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "%~dp0src\CortexDNA.App\CortexDNA.App.csproj"
exit /b %errorlevel%