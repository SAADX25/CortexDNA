$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
# Disposable sources stay in a test obj directory, excluded from every product/test compile glob.
$fixture = Join-Path $projectRoot ('Tests\Standard\obj\ci-fail-fixtures\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$config = Join-Path $projectRoot 'Tests\NuGet.Offline.config'
$project = Join-Path $fixture 'Failure.csproj'
$source = Join-Path $fixture 'Failure.cs'
[IO.File]::WriteAllText($project, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>')
[IO.File]::WriteAllText($source, '#warning Deliberate CI warning probe')
& dotnet restore $project --configfile $config
if ($LASTEXITCODE -ne 0) { throw 'Warning fixture restore failed.' }
$warningOutput = & dotnet build $project --no-restore -c Release -warnaserror 2>&1
$warningExit = $LASTEXITCODE
$warningOutput | Write-Output
if ($warningExit -eq 0 -or ($warningOutput -join "`n") -notmatch 'error CS1030') { throw 'CI warning probe did not produce the intended warning-as-error failure.' }
Write-Output 'PASS compile warning returns nonzero with warnaserror'
[IO.File]::WriteAllText($project, @'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1"/><PackageReference Include="xunit" Version="2.9.3"/><PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" PrivateAssets="all"/></ItemGroup></Project>
'@)
[IO.File]::WriteAllText($source, 'public class Probe { [Xunit.Fact] public void DeliberateFailure() { Xunit.Assert.Fail("Deliberate CI failure probe"); } }')
& dotnet restore $project --configfile $config
if ($LASTEXITCODE -ne 0) { throw 'Test failure fixture restore failed.' }
# Prove the nonzero test exit comes from execution, not a compile/restore failure.
& dotnet build $project --no-restore -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Test failure fixture build failed.' }
$testOutput = & dotnet test $project --no-build --no-restore -c Release 2>&1
$testExit = $LASTEXITCODE
$testOutput | Write-Output
if ($testExit -eq 0 -or ($testOutput -join "`n") -notmatch 'Failed:\s+1') { throw 'CI failing-test probe did not execute its expected failed test.' }
Write-Output 'PASS test failure returns nonzero'
$probe = Join-Path $fixture 'PublishFailure.ps1'
$builder = Join-Path $projectRoot 'scripts\Build-Release.ps1'
# Substitute only the native publish command in an owned child process; no production source is changed.
[IO.File]::WriteAllText($probe, "function dotnet { `$global:LASTEXITCODE = 19 }`r`n& '" + $builder.Replace("'", "''") + "' -SkipInstaller`r`n")
$hostShell = (Get-Process -Id $PID).Path
$publishOutput = & $hostShell -NoProfile -File $probe 2>&1
$publishExit = $LASTEXITCODE
$publishOutput | Write-Output
if ($publishExit -eq 0 -or ($publishOutput -join "`n") -notmatch 'Publish failed') { throw 'CI publish probe did not propagate the intended publish failure.' }
Write-Output 'PASS publish failure propagates out of Build-Release.ps1'
Write-Output 'TOTAL 3; PASSED 3; FAILED 0'
exit 0
