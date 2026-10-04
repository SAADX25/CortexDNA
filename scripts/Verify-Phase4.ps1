$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content -LiteralPath (Join-Path $workspace 'docs/PHASE4_PRESERVATION.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest) {
    $hash=(Get-FileHash -LiteralPath (Join-Path $workspace $entry.Path) -Algorithm SHA256).Hash
    if($hash -ne $entry.Hash){throw "Update-33 preservation failed: $($entry.Path)"}
}
Write-Output "PASS $($manifest.Count) unchanged Phase 3 baseline files, including all hardware polling and resource lifecycle implementations"
& (Join-Path $PSScriptRoot 'Verify-Phase3.ps1') -ApprovedPhase4
# Keep assessment sources isolated from existing mutation contracts and any second monitoring pipeline.
$healthFiles=@(
    Get-ChildItem -LiteralPath (Join-Path $workspace 'src/CortexDNA.Core/Health') -Filter '*.cs'
    Get-ChildItem -LiteralPath (Join-Path $workspace 'src/CortexDNA.System/Health') -Filter '*.cs'
    Get-Item -LiteralPath (Join-Path $workspace 'src/CortexDNA.App/ViewModels/Pages/HealthViewModel.cs')
)
$forbidden='HardwareMonitorService\s*\(|CreateHardwareMonitor|\.RefreshAsync\s*\(|\.StartAsync\s*\(|DispatcherTimer|CompositionTarget\.Rendering|SetValue\s*\(|CreateSubKey|\.CleanAsync\s*\(|OptimizeMemory|\.SetEnabled\s*\(|\.Delay\s*\(|Process\.Start|\.InvokeMethod\s*\(|Registry\.SetValue|File\.Write|Directory\.Create|\.LoadAsync\s*\('
foreach($file in $healthFiles) {
    $text=[IO.File]::ReadAllText($file.FullName)
    # Only this finite, cancellation-aware presentation interval is permitted; Startup.Delay remains forbidden.
    $text=$text -replace 'Task\.Delay\(\s*140,\s*token\s*\)',''
    if($text -match $forbidden) {throw "Health assessment acquired a forbidden side effect or polling source: $($file.Name)"}
}
Write-Output 'PASS Health assessment has no mutation, cleanup, startup migration, online scan, disk persistence or monitoring calls'
$models=[IO.File]::ReadAllText((Join-Path $workspace 'src/CortexDNA.Core/Health/HealthModels.cs'))
if($models -match 'System\.Windows|System\.Management|LibreHardwareMonitor|Microsoft\.Win32') {throw 'Platform or presentation objects leaked into health contracts.'}
Write-Output 'PASS immutable health contracts remain platform-neutral'
