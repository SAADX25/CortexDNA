param([switch]$NoRestore, [switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
# A fresh directory prevents stale files from entering the installer; no recursive deletion.
$publish = Join-Path $projectRoot ('artifacts\publish\' + [guid]::NewGuid().ToString('N'))
$arguments = @('publish', 'src/CortexDNA.App/CortexDNA.App.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-p:PublishReadyToRun=true', '-p:NuGetAudit=false', '-warnaserror', '-o', $publish)
if ($NoRestore) { $arguments += '--no-restore' }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if ($SkipInstaller) { Write-Output "Published locally: $publish"; exit 0 }
$compiler = $env:ISCC_PATH
if (!$compiler) { $compiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (!$compiler) { $compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
if (!(Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Set ISCC_PATH to the Inno Setup compiler.' }
& $compiler "/DPublishDir=$publish" (Join-Path $projectRoot 'CortexDNA_Installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Write-Output "Installer created locally in $projectRoot\artifacts\installer"
