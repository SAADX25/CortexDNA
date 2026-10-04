$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $workspace 'docs/PHASE3_PRESERVATION.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest) {
    $text=[IO.File]::ReadAllText((Join-Path $workspace $entry.Path)).Replace("`r`n","`n")
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
    if($entry.Binary){$hash=(Get-FileHash -LiteralPath (Join-Path $workspace $entry.Path) -Algorithm SHA256).Hash}
    if($hash -ne $entry.Hash){throw "Update-32 preservation failed: $($entry.Path)"}
}
Write-Output "PASS $($manifest.Count) unchanged Update-32 baseline files"
$vm=[IO.File]::ReadAllText((Join-Path $workspace 'src/CortexDNA.App/ViewModels/HardwareViewModel.cs'))
if($vm -match 'LibreHardwareMonitor|IHardwareSession|ManagementObject|PerformanceCounter|NetworkInterface|GlobalMemoryStatusEx|DispatcherTimer|DriveInfo|Process\.GetProcesses') {throw 'Low-level hardware leaked back into the UI.'}
Write-Output 'PASS snapshot-only HardwareViewModel, no low-level sensor/vendor objects'
& (Join-Path $PSScriptRoot 'Verify-Phase1.ps1') -ApprovedPhase2Ui
