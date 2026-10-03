$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" }
}
Invoke-Dotnet @('restore', 'Tests/CortexDNA.Tests.csproj', '--configfile', 'Tests/NuGet.Offline.config', '-p:NuGetAudit=false')
Invoke-Dotnet @('build', 'CortexDNA.csproj', '--no-restore', '-c', 'Debug', '-warnaserror')
Invoke-Dotnet @('build', 'CortexDNA.csproj', '--no-restore', '-c', 'Release', '-warnaserror')
Invoke-Dotnet @('run', '--project', 'Tests/CortexDNA.Tests.csproj', '--no-restore', '-c', 'Release')
Invoke-Dotnet @('restore', 'CortexDNA.csproj', '-r', 'win-x64', '--configfile', 'Tests/NuGet.Offline.config', '-p:NuGetAudit=false')
& "$PSScriptRoot/Build-Release.ps1" -NoRestore
if ($LASTEXITCODE -ne 0) { throw 'Release verification failed.' }
