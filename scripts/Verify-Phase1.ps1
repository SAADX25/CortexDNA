param([switch]$ApprovedPhase2Ui)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $workspace 'docs/PHASE1_PRESERVATION.json') -Raw | ConvertFrom-Json
$uiExceptions = @()
if ($ApprovedPhase2Ui) { $uiExceptions = @(Get-Content -LiteralPath (Join-Path $workspace 'docs/PHASE2_UI_EXCEPTIONS.json') -Raw | ConvertFrom-Json) }
foreach ($entry in $manifest) {
    if ($entry.Destination -in $uiExceptions) { continue }
    $path = Join-Path $workspace $entry.Destination
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Hash) {
        throw "Update-30 preservation failed: $($entry.Original) -> $($entry.Destination)"
    }
}
Write-Output "PASS $($manifest.Count - $uiExceptions.Count) unchanged Update-30 domain, safety and test files ($($uiExceptions.Count) approved Phase 2 presentation exceptions)"
# Check the cleanup source changed only by adopting its service interface.
$cleanup = [IO.File]::ReadAllText((Join-Path $workspace 'src/CortexDNA.Cleanup/DiskCleanupService.cs'))
$cleanup = $cleanup.Replace('public sealed class DiskCleanupService : ICleanupService','public sealed class DiskCleanupService')
$hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($cleanup)))
$expected = (Get-Content -LiteralPath (Join-Path $workspace 'docs/PHASE1_CLEANUP_HASH.txt') -Raw).Trim()
if ($hash -ne $expected) { throw 'Cleanup algorithm differs from Update-30 beyond interface adoption.' }
Write-Output 'PASS Update-30 cleanup implementation preserved beyond interface adoption'
# Refuse feature scaffolding as well as reachable product implementations.
$productFiles = Get-ChildItem -LiteralPath (Join-Path $workspace 'src') -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$forbidden = '(?i)AgentManagement|AgentsPage|AgentSandbox|AgentSecuritySandbox|SecuritySandbox|SandboxDeployment|Microsoft\.SemanticKernel|Microsoft\.Agents|AutoGen|AppContainer|HyperV'
foreach ($file in $productFiles) {
    if ($file.Name -match '(?i)agent|sandbox' -or
        ($file.Extension -in @('.cs','.xaml','.csproj','.json') -and
        [IO.File]::ReadAllText($file.FullName) -match $forbidden)) {
        throw "Excluded feature found in product source: $($file.FullName)"
    }
}
Write-Output 'PASS no excluded product feature, placeholder or dependency'

if ($ApprovedPhase2Ui) {
    $model = [IO.File]::ReadAllText((Join-Path $workspace 'src/CortexDNA.Core/Models/HardwareModels.cs')).Replace("`r`n", "`n")
    $modelHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($model)))
    $expectedModel = (Get-Content -LiteralPath (Join-Path $workspace 'docs/PHASE2_CORE_MODEL_HASH.txt') -Raw).Trim()
    if ($modelHash -ne $expectedModel) { throw 'Core hardware model changed beyond removal of obsolete visual color metadata.' }
    Write-Output 'PASS Core model only removes obsolete visual color metadata'
}
