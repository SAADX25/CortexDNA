$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Set-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" }
}
Invoke-Dotnet @('restore', 'CortexDNA.slnx', '--configfile', 'Tests/NuGet.Offline.config', '-p:NuGetAudit=false')
Invoke-Dotnet @('build', 'CortexDNA.slnx', '--no-restore', '-c', 'Debug', '-warnaserror')
Invoke-Dotnet @('build', 'CortexDNA.slnx', '--no-restore', '-c', 'Release', '-warnaserror')
Invoke-Dotnet @('test', 'CortexDNA.slnx', '--no-restore', '-c', 'Release', '-warnaserror', '--logger', 'trx;LogFileName=final-review.trx', '--results-directory', 'artifacts/verification/final-tests')
Invoke-Dotnet @('restore', 'src/CortexDNA.App/CortexDNA.App.csproj', '-r', 'win-x64', '--configfile', 'Tests/NuGet.Offline.config', '-p:NuGetAudit=false')
& "$PSScriptRoot/Build-Release.ps1" -NoRestore
if ($LASTEXITCODE -ne 0) { throw 'Release verification failed.' }

& "$PSScriptRoot/Verify-Phase1.ps1" -ApprovedPhase2Ui
