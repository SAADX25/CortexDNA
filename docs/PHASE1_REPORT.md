# Phase 1 — Modular Architecture verification report

October 4, 2026 (Asia/Amman). Phase 1 is implemented and locally verified. Phase 2 has not started. Nothing was committed, pushed, uploaded or published remotely. The local working tree is authoritative.

| Required item | Result |
| --- | --- |
| 1. Phase name | Phase 1 — Modular Architecture, incremental Update-30 refactor. |
| 2. Problems found | Monolithic WPF project mixed contracts, UI helpers, Windows APIs and feature implementations; ViewModels created their own services; startup approval/catalog dependencies could form cycles if split carelessly; desktop framework dependencies had been implicit. |
| 3. Root causes | A single project supplied Windows API references and service construction globally. Splitting the hardware library initially selected an uncached vendor dependency fallback. Resolved by retaining baseline System.Management 10.0.2 and existing Windows desktop framework references. The extra published-smoke fixture initially mixed a non-RID harness dependency manifest with RID-published assemblies; SDK-publishing the test host corrected that fixture. No safety code was changed to work around failures. |
| 4. Architecture changes | Eight implemented projects under src: App, Core, System, Infrastructure, Cleanup, Startup, Optimization, Hardware. Core is net10.0 without WPF/Windows/vendor dependencies. Existing Windows implementations moved into System, bounded logger into Infrastructure, cleanup/facade/RAM optimizer/visitor into feature modules. Explicit composition and constructor injection replace ViewModel-owned service construction. No DI framework/package is needed for this graph. Project graph is acyclic. Existing namespaces, CortexDNA assembly/executable name, version and resource URIs remain compatible. |
| 5. Files added | Listed below; includes seven library projects, contracts, composition, adapters, scoped friend metadata, seven standard tests and preservation checks. |
| 6. Files modified | Listed below, plus six moved files with edits: app project, MainViewModel, StartupViewModel, HardwareViewModel, StartupFeatureService and DiskCleanupService. |
| 7. Files removed | No working feature or source implementation deleted. Original source/project paths were relocated, as detailed in PHASE1_MIGRATION_PLAN.md. Old root project path becomes src/CortexDNA.App/CortexDNA.App.csproj; development/publish/test references updated accordingly. |
| 8. Behavior changes | No intended visible or system behavior changes. All original XAML, themes, code-behind, manifest and icon remain byte-identical. Existing dashboard/startup/tools/settings/cleanup/boost/tray behavior retained. No new pages or menus. Parameterless constructors remain for unchanged WPF and original tests; injected constructors accept fake services. |
| 9. Tests added | Seven: Core isolation; implementation assembly/UI boundaries; acyclic project graph/Core project restrictions; startup fake injection and migration flag forwarding; failed writes preserve displayed state; shutdown waits for outstanding load and suppresses late items; cancelled hardware initialization closes its injected session exactly once without cleanup or RAM mutation. |
| 10. Test result/count | 33/33 standard xUnit cases passed, 0 skipped/failed. Includes unchanged 25 legacy-migration cases and the existing regression wrapper; that wrapper ran all 32/32 original fixture/WPF cases. The same 32/32 cases also passed against copied production-published assemblies using an SDK-published harness. Three local CI failure-propagation probes passed. |
| 11. Debug build | PASS, warnings-as-errors, 0 warnings and 0 errors. |
| 12. Release build | PASS, warnings-as-errors, 0 warnings and 0 errors. |
| 13. Smoke result | PASS: real WPF dashboard/startup/minimize/restore/close; session-ending exit path; original cleanup fixture/junction/ancestor-lock/cancellation and fake update-service restoration tests. Production-published assemblies passed the same smoke/regression harness. Framework-dependent win-x64 ReadyToRun publish succeeded; Inno Setup compiled CortexDNA_Installer_v2.1.0.exe. No host install/uninstall performed. |
| 14. Performance impact | No new polling loop, timer or background operation. Existing polling, game-mode behavior, hardware locking and shutdown order retained. Published payload sizes measured below. Cold-start/idle CPU/memory benchmarks were not measured; no broader performance claim. |
| 15. Security impact | 47 preserved files match their original SHA-256 hashes. Cleanup algorithm matches the original after removing only its interface declaration. Native path locks, service state restoration, stable startup IDs, legacy migration, rollback, policy restrictions, COM cleanup, RAM process exclusions, bounded logger and session shutdown retained. Core contracts add no privileges. Exact same 26 resolved third-party packages/versions. Excluded features absent from product source/dependencies. Installer identity, ownership protections, privilege model and original-user launch retained. |
| 16. Known risks | HardwareViewModel still contains existing WMI/network/storage/presentation logic; vendor sensor types remain at its Hardware adapter seam pending Phase 3. Startup Windows adapters remain a cohesive unit in System pending Phase 7. Windows desktop references remain required by existing EventLog/CodeDom usage. Original public type namespaces are retained for compatibility. Tests remain in the existing Tests layout to preserve the standard runner. Hosted CI and destructive VM installer/service/startup scenarios were not run; production readiness is not claimed. |
| 17. git status | Changes remain unstaged; original staging untouched. Old paths show deleted and new src paths untracked until staged. Complete status below. |
| 18. git diff --stat | Raw diff below omits untracked additions and therefore displays moves as deletions. Complete rename-aware snapshot also below, generated with an isolated temporary index in artifacts; repository index untouched. Both normal git diff --check and complete-snapshot diff --check passed. |
| 19. Remaining work | No outstanding local Phase 1 check. Stop here for review. Later phases remain unstarted. VM installation/upgrade/uninstall and production scenarios remain future verification. The frozen Phase 1 preservation manifest must be deliberately revised when a later approved phase changes UI or behavior. |
| 20. Remote actions | Nothing committed, pushed, uploaded, released or remotely published. Offline NuGet cache used. Hosted CI was not dispatched or represented as green. |

Agent Management, AI Agents, Agent Security Sandbox and sandbox deployment are permanently excluded from this rebuild. No Agents page, placeholders, hidden menus, feature flags, unused dependencies or reserved architecture were added. Future in-scope product areas are Home, Health, Cleanup, Storage, Startup, Hardware, Gaming, Security, Tools, Settings and Diagnostics.

## Local evidence

- artifacts/phase1-baseline.log — green pre-migration baseline.
- artifacts/phase1-final.log — restore, Debug, Release, standard tests, publish, installer and preservation checks (initial 42-file manifest).
- artifacts/verification/final-tests/final-review.trx — 33 standard test results including full 32-case harness output.
- artifacts/phase1-published-smoke.log — 32/32 production-assembly regression/WPF checks.
- artifacts/phase1-published-host-build.log — SDK-published host used for the production-assembly smoke.
- artifacts/phase1-failure-propagation.log — warning-as-error, failing-test and publish-error propagation probes, 3/3.
- artifacts/phase1-publish-metrics.json — measured published payload sizes.
- docs/PHASE1_PRESERVATION.json and scripts/Verify-Phase1.ps1 — final 47-file preservation and cleanup/excluded-feature checks.
- artifacts/phase1-review.diff and artifacts/phase1-name-status.txt — complete rename-aware source review.

The failure probes intentionally produce failing child builds/tests and require those failures to propagate; those expected child failures are not product test failures. Run scripts/Verify-Local.ps1 and scripts/Verify-CIFailureModes.ps1 to repeat local verification.
## Measured publish size

Baseline: 8784032 bytes. Phase 1: 9027817 bytes. Difference: +243785 bytes (+2.78%). These are complete publish directories including symbols. Seven separate module assemblies account for additional packaging; this is not a runtime performance benchmark.

## Added files

- Tests/Standard/ArchitectureTests.cs
- docs/PHASE1_CLEANUP_HASH.txt
- docs/PHASE1_MIGRATION_PLAN.md
- docs/PHASE1_PRESERVATION.json
- scripts/Verify-Phase1.ps1
- src/CortexDNA.App/Composition/AppComposition.cs
- src/CortexDNA.Cleanup/AssemblyInfo.cs
- src/CortexDNA.Cleanup/CortexDNA.Cleanup.csproj
- src/CortexDNA.Core/Contracts/ICleanupService.cs
- src/CortexDNA.Core/Contracts/IMemoryOptimizer.cs
- src/CortexDNA.Core/Contracts/IStartupService.cs
- src/CortexDNA.Core/CortexDNA.Core.csproj
- src/CortexDNA.Hardware/CortexDNA.Hardware.csproj
- src/CortexDNA.Hardware/HardwareSession.cs
- src/CortexDNA.Infrastructure/AssemblyInfo.cs
- src/CortexDNA.Infrastructure/CortexDNA.Infrastructure.csproj
- src/CortexDNA.Optimization/CortexDNA.Optimization.csproj
- src/CortexDNA.Optimization/MemoryOptimizer.cs
- src/CortexDNA.Startup/CortexDNA.Startup.csproj
- src/CortexDNA.System/AssemblyInfo.cs
- src/CortexDNA.System/CortexDNA.System.csproj
- docs/PHASE1_REPORT.md (this report)

## Modified existing files

- CortexDNA.slnx
- CortexDNA_Installer.iss
- Cortex_Dev.bat
- README.md
- Tests/CortexDNA.Tests.csproj
- Tests/Standard/CortexDNA.AutomatedTests.csproj
- scripts/Build-Release.ps1
- scripts/Verify-Local.ps1

The six edited relocations are listed in item 6. All 50 relocations are mapped in PHASE1_MIGRATION_PLAN.md; 47 original UI/domain/safety/test files are protected by the preservation manifest.

## git status --short

```text
 D AboutWindow.xaml
 D AboutWindow.xaml.cs
 D App.xaml
 D App.xaml.cs
 D AssemblyInfo.cs
 D CleanConfirmationWindow.xaml
 D CleanConfirmationWindow.xaml.cs
 D CleanupResultsWindow.xaml
 D CleanupResultsWindow.xaml.cs
 D Controls/HardwareDashboardControl.xaml
 D Controls/HardwareDashboardControl.xaml.cs
 D Controls/StartupImpactControl.xaml
 D Controls/StartupImpactControl.xaml.cs
 D Converters/BoolToVisibilityConverter.cs
 D Converters/SemanticColorConverters.cs
 D Core/CleanupPathGuard.cs
 D Core/DiskCleanupService.cs
 D Core/Logger.cs
 D Core/NativeMethods.cs
 D Core/ObservableObject.cs
 D Core/RamOptimizer.cs
 D Core/RelayCommand.cs
 D Core/Startup/LegacyStartupDelayMigration.cs
 D Core/Startup/StartupApprovalService.cs
 D Core/Startup/StartupCatalogService.cs
 D Core/Startup/StartupCom.cs
 D Core/Startup/StartupDelayService.cs
 D Core/Startup/StartupFeatureService.cs
 D Core/Startup/StartupIconLoader.cs
 D Core/Startup/StartupImpactService.cs
 D Core/Startup/StartupPackagedCatalog.cs
 D Core/Startup/StartupPaths.cs
 D Core/Startup/StartupProcessProbe.cs
 D Core/SystemToolLauncher.cs
 D Core/UpdateServiceLease.cs
 D Core/UpdateVisitor.cs
 D CortexDNA.csproj
 M CortexDNA.slnx
 M CortexDNA_Installer.iss
 M Cortex_Dev.bat
 D MainWindow.xaml
 D MainWindow.xaml.cs
 D Models/CleanupModels.cs
 D Models/HardwareModels.cs
 D Models/StartupModels.cs
 M README.md
 M Tests/CortexDNA.Tests.csproj
 M Tests/Standard/CortexDNA.AutomatedTests.csproj
 D Themes/DarkTheme.xaml
 D Themes/LightTheme.xaml
 D ViewModels/HardwareViewModel.cs
 D ViewModels/MainViewModel.cs
 D ViewModels/StartupViewModel.cs
 D ViewModels/ViewModelBase.cs
 D app.ico
 D app.manifest
 M scripts/Build-Release.ps1
 M scripts/Verify-Local.ps1
?? Tests/Standard/ArchitectureTests.cs
?? docs/PHASE1_CLEANUP_HASH.txt
?? docs/PHASE1_MIGRATION_PLAN.md
?? docs/PHASE1_PRESERVATION.json
?? scripts/Verify-Phase1.ps1
?? src/
?? docs/PHASE1_REPORT.md
```

## Raw git diff --stat

```text
 AboutWindow.xaml                               |   75 --
 AboutWindow.xaml.cs                            |   76 --
 App.xaml                                       |   92 --
 App.xaml.cs                                    |  101 --
 AssemblyInfo.cs                                |   14 -
 CleanConfirmationWindow.xaml                   |  146 ---
 CleanConfirmationWindow.xaml.cs                |   80 --
 CleanupResultsWindow.xaml                      |   58 --
 CleanupResultsWindow.xaml.cs                   |   54 --
 Controls/HardwareDashboardControl.xaml         |  408 --------
 Controls/HardwareDashboardControl.xaml.cs      |   54 --
 Controls/StartupImpactControl.xaml             |  205 ----
 Controls/StartupImpactControl.xaml.cs          |   12 -
 Converters/BoolToVisibilityConverter.cs        |   21 -
 Converters/SemanticColorConverters.cs          |  100 --
 Core/CleanupPathGuard.cs                       |   63 --
 Core/DiskCleanupService.cs                     |  261 -----
 Core/Logger.cs                                 |   42 -
 Core/NativeMethods.cs                          |   25 -
 Core/ObservableObject.cs                       |   23 -
 Core/RamOptimizer.cs                           |  173 ----
 Core/RelayCommand.cs                           |   61 --
 Core/Startup/LegacyStartupDelayMigration.cs    |  182 ----
 Core/Startup/StartupApprovalService.cs         |  111 ---
 Core/Startup/StartupCatalogService.cs          |  174 ----
 Core/Startup/StartupCom.cs                     |   18 -
 Core/Startup/StartupDelayService.cs            |  295 ------
 Core/Startup/StartupFeatureService.cs          |   75 --
 Core/Startup/StartupIconLoader.cs              |  121 ---
 Core/Startup/StartupImpactService.cs           |  225 -----
 Core/Startup/StartupPackagedCatalog.cs         |  567 -----------
 Core/Startup/StartupPaths.cs                   |   93 --
 Core/Startup/StartupProcessProbe.cs            |   69 --
 Core/SystemToolLauncher.cs                     |   50 -
 Core/UpdateServiceLease.cs                     |  127 ---
 Core/UpdateVisitor.cs                          |   24 -
 CortexDNA.csproj                               |   38 -
 CortexDNA.slnx                                 |    9 +-
 CortexDNA_Installer.iss                        |    2 +-
 Cortex_Dev.bat                                 |    2 +-
 MainWindow.xaml                                |  636 ------------
 MainWindow.xaml.cs                             |  807 ----------------
 Models/CleanupModels.cs                        |  113 ---
 Models/HardwareModels.cs                       |  197 ----
 Models/StartupModels.cs                        |   57 --
 README.md                                      |   23 +-
 Tests/CortexDNA.Tests.csproj                   |    2 +-
 Tests/Standard/CortexDNA.AutomatedTests.csproj |    2 +-
 Themes/DarkTheme.xaml                          |   19 -
 Themes/LightTheme.xaml                         |   19 -
 ViewModels/HardwareViewModel.cs                | 1231 ------------------------
 ViewModels/MainViewModel.cs                    |   51 -
 ViewModels/StartupViewModel.cs                 |  303 ------
 ViewModels/ViewModelBase.cs                    |    8 -
 app.ico                                        |  Bin 110656 -> 0 bytes
 app.manifest                                   |   48 -
 scripts/Build-Release.ps1                      |    2 +-
 scripts/Verify-Local.ps1                       |    4 +-
 58 files changed, 37 insertions(+), 7811 deletions(-)
```

## Complete rename-aware snapshot before this report

```text
 CortexDNA.slnx                                     |   9 +-
 CortexDNA_Installer.iss                            |   2 +-
 Cortex_Dev.bat                                     |   2 +-
 README.md                                          |  23 +-
 Tests/CortexDNA.Tests.csproj                       |   2 +-
 Tests/Standard/ArchitectureTests.cs                | 200 +++++++++++++++++
 Tests/Standard/CortexDNA.AutomatedTests.csproj     |   2 +-
 docs/PHASE1_CLEANUP_HASH.txt                       |   1 +
 docs/PHASE1_MIGRATION_PLAN.md                      | 110 ++++++++++
 docs/PHASE1_PRESERVATION.json                      | 237 +++++++++++++++++++++
 scripts/Build-Release.ps1                          |   2 +-
 scripts/Verify-Local.ps1                           |   4 +-
 scripts/Verify-Phase1.ps1                          |  29 +++
 .../CortexDNA.App/AboutWindow.xaml                 |   0
 .../CortexDNA.App/AboutWindow.xaml.cs              |   0
 App.xaml => src/CortexDNA.App/App.xaml             |   0
 App.xaml.cs => src/CortexDNA.App/App.xaml.cs       |   0
 .../CortexDNA.App/AssemblyInfo.cs                  |   0
 .../CortexDNA.App/CleanConfirmationWindow.xaml     |   0
 .../CortexDNA.App/CleanConfirmationWindow.xaml.cs  |   0
 .../CortexDNA.App/CleanupResultsWindow.xaml        |   0
 .../CortexDNA.App/CleanupResultsWindow.xaml.cs     |   0
 src/CortexDNA.App/Composition/AppComposition.cs    |  25 +++
 .../Controls}/HardwareDashboardControl.xaml        |   0
 .../Controls}/HardwareDashboardControl.xaml.cs     |   0
 .../Controls}/StartupImpactControl.xaml            |   0
 .../Controls}/StartupImpactControl.xaml.cs         |   0
 .../Converters}/BoolToVisibilityConverter.cs       |   0
 .../Converters}/SemanticColorConverters.cs         |   0
 .../CortexDNA.App/CortexDNA.App.csproj             |  13 +-
 .../CortexDNA.App/MainWindow.xaml                  |   0
 .../CortexDNA.App/MainWindow.xaml.cs               |   0
 .../CortexDNA.App/Themes}/DarkTheme.xaml           |   0
 .../CortexDNA.App/Themes}/LightTheme.xaml          |   0
 {Core => src/CortexDNA.App/UI}/RelayCommand.cs     |   0
 .../CortexDNA.App/UI}/StartupIconLoader.cs         |   0
 .../CortexDNA.App/ViewModels}/HardwareViewModel.cs |  29 ++-
 .../CortexDNA.App/ViewModels}/MainViewModel.cs     |   8 +-
 .../CortexDNA.App/ViewModels}/StartupViewModel.cs  |  11 +-
 .../CortexDNA.App/ViewModels}/ViewModelBase.cs     |   0
 app.ico => src/CortexDNA.App/app.ico               | Bin
 app.manifest => src/CortexDNA.App/app.manifest     |   0
 src/CortexDNA.Cleanup/AssemblyInfo.cs              |   4 +
 src/CortexDNA.Cleanup/CortexDNA.Cleanup.csproj     |  14 ++
 .../CortexDNA.Cleanup}/DiskCleanupService.cs       |   2 +-
 src/CortexDNA.Core/Contracts/ICleanupService.cs    |  12 ++
 src/CortexDNA.Core/Contracts/IMemoryOptimizer.cs   |   8 +
 src/CortexDNA.Core/Contracts/IStartupService.cs    |  12 ++
 .../CortexDNA.Core/Core}/ObservableObject.cs       |   0
 src/CortexDNA.Core/CortexDNA.Core.csproj           |  12 ++
 .../CortexDNA.Core/Models}/CleanupModels.cs        |   0
 .../CortexDNA.Core/Models}/HardwareModels.cs       |   0
 .../CortexDNA.Core/Models}/StartupModels.cs        |   0
 src/CortexDNA.Hardware/CortexDNA.Hardware.csproj   |  16 ++
 src/CortexDNA.Hardware/HardwareSession.cs          |  35 +++
 {Core => src/CortexDNA.Hardware}/UpdateVisitor.cs  |   0
 src/CortexDNA.Infrastructure/AssemblyInfo.cs       |   4 +
 .../CortexDNA.Infrastructure.csproj                |  12 ++
 {Core => src/CortexDNA.Infrastructure}/Logger.cs   |   0
 .../CortexDNA.Optimization.csproj                  |  14 ++
 src/CortexDNA.Optimization/MemoryOptimizer.cs      |  10 +
 .../CortexDNA.Optimization}/RamOptimizer.cs        |   0
 src/CortexDNA.Startup/CortexDNA.Startup.csproj     |  13 ++
 .../CortexDNA.Startup}/StartupFeatureService.cs    |  21 +-
 src/CortexDNA.System/AssemblyInfo.cs               |   8 +
 {Core => src/CortexDNA.System}/CleanupPathGuard.cs |   0
 src/CortexDNA.System/CortexDNA.System.csproj       |  15 ++
 {Core => src/CortexDNA.System}/NativeMethods.cs    |   0
 .../Startup/LegacyStartupDelayMigration.cs         |   0
 .../Startup/StartupApprovalService.cs              |   0
 .../Startup/StartupCatalogService.cs               |   0
 .../CortexDNA.System}/Startup/StartupCom.cs        |   0
 .../Startup/StartupDelayService.cs                 |   0
 .../Startup/StartupImpactService.cs                |   0
 .../Startup/StartupPackagedCatalog.cs              |   0
 .../CortexDNA.System}/Startup/StartupPaths.cs      |   0
 .../Startup/StartupProcessProbe.cs                 |   0
 .../CortexDNA.System}/SystemToolLauncher.cs        |   0
 .../CortexDNA.System}/UpdateServiceLease.cs        |   0
 79 files changed, 880 insertions(+), 41 deletions(-)
```

This report is one additional file beyond that snapshot. No feature implementation was removed.
