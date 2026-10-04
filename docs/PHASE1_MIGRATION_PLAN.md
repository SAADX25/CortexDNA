# Phase 1 migration plan — Update-30 preservation

Scope: architecture only. Preserve current UI and all working behavior. Product scope is Home, Health, Cleanup, Storage, Startup, Hardware, Gaming, Security, Tools, Settings, Diagnostics. Agent Management, AI Agents, Agent Security Sandbox and sandbox deployment are permanently excluded from this rebuild. No pages, menu entries, flags, dependencies, reserved interfaces or implementation for those features. Phase 2 requires review after a verified Phase 1 report.

## Baseline

Clean local working tree. Existing Verify-Local.ps1 passed on October 4, 2026: restore, Debug/Release warnings-as-errors, 26 standard tests (including the original 32-case regression/WPF harness), runtime restore, publish and Inno Setup compilation. Evidence: artifacts/phase1-baseline.log and artifacts/verification/final-tests/final-review.trx. No remote operations.

## Dependency findings and references

Core currently mixes domain objects, WPF commands/icon loading, P/Invoke, filesystem safety, Windows services and startup adapters. ViewModels construct services. Startup approval and packaged catalog call each other; keep this cohesive Windows safety unit together rather than inventing a cyclic split. Cleanup path locks and update service leases are shared Windows implementations and must retain identical source. The hardware ViewModel mixes sensors, WMI, presentation and lifetime; preserve its tested lifetime and introduce a session seam now, leaving the snapshot redesign for Phase 3.

References (all downward, never back to App):

- Core: net10.0; models, ObservableObject, cleanup/startup/memory service contracts; no packages, WPF or Windows implementations.
- Infrastructure -> Core: existing bounded local logger.
- System -> Core, Infrastructure: Win32, safe filesystem locks, service lease, approved system tool launch, cohesive Windows startup implementations.
- Cleanup -> Core, System, Infrastructure: existing cleanup algorithm, now implementing its service contract. No provider redesign.
- Startup -> Core, System: existing facade, injected catalog/approval/delay/impact dependencies. No startup behavior changes.
- Optimization -> Core, System, Infrastructure: existing RAM optimizer and injectable adapter.
- Hardware -> Core: existing visitor and vendor session adapter; LibreHardwareMonitor remains the same version.
- App -> Core and all implemented modules: identical WPF resources/views, existing ViewModels, UI-only commands/icons, explicit composition root and constructor injection. Assembly/executable remain CortexDNA; namespaces remain stable for XAML and tests.

No empty Elevated project or future feature/test module scaffolding. Existing Tests layout and standard runner remain; test source files stay untouched. Add architectural and fake-injection checks to the standard test project. Friend assembly access preserves internal safety test seams without making them public.

## Safest migration order

1. Capture original hashes and complete this file map before moving source.
2. Move domain code to Core and logger to Infrastructure.
3. Move cohesive Windows implementations to System, retaining safety algorithms byte-for-byte.
4. Move cleanup, startup facade, RAM optimizer and hardware visitor to feature modules; add narrowly scoped contracts/adapters.
5. Move WPF app/resources intact and introduce explicit per-window composition; retain parameterless WPF/test constructors, existing cancellation and shutdown ownership.
6. Update solution, test project references and local/CI build paths; preserve installer identity, privileges and installed executable.
7. Verify original test sources and UI hashes, graph boundaries, fake injection, Debug/Release builds, all original regressions, WPF smoke, publish, installer compile and failure propagation. Run git diff --check. Report results and stop.

## Every tracked file mapped

Files mapped to their original path stay in place. Tests/Program.cs, Tests/Standard/LegacyMigrationTests.cs and Tests/Standard/RegressionHarnessTests.cs must remain byte-identical. Historical engineering reports/checklists remain unchanged.

| Original file | Phase 1 destination |
| --- | --- |
| .github/workflows/windows.yml | .github/workflows/windows.yml |
| .gitignore | .gitignore |
| AboutWindow.xaml | src/CortexDNA.App/AboutWindow.xaml |
| AboutWindow.xaml.cs | src/CortexDNA.App/AboutWindow.xaml.cs |
| app.ico | src/CortexDNA.App/app.ico |
| app.manifest | src/CortexDNA.App/app.manifest |
| App.xaml | src/CortexDNA.App/App.xaml |
| App.xaml.cs | src/CortexDNA.App/App.xaml.cs |
| AssemblyInfo.cs | src/CortexDNA.App/AssemblyInfo.cs |
| Build_Release.bat | Build_Release.bat |
| CleanConfirmationWindow.xaml | src/CortexDNA.App/CleanConfirmationWindow.xaml |
| CleanConfirmationWindow.xaml.cs | src/CortexDNA.App/CleanConfirmationWindow.xaml.cs |
| CleanupResultsWindow.xaml | src/CortexDNA.App/CleanupResultsWindow.xaml |
| CleanupResultsWindow.xaml.cs | src/CortexDNA.App/CleanupResultsWindow.xaml.cs |
| Controls/HardwareDashboardControl.xaml | src/CortexDNA.App/Controls/HardwareDashboardControl.xaml |
| Controls/HardwareDashboardControl.xaml.cs | src/CortexDNA.App/Controls/HardwareDashboardControl.xaml.cs |
| Controls/StartupImpactControl.xaml | src/CortexDNA.App/Controls/StartupImpactControl.xaml |
| Controls/StartupImpactControl.xaml.cs | src/CortexDNA.App/Controls/StartupImpactControl.xaml.cs |
| Converters/BoolToVisibilityConverter.cs | src/CortexDNA.App/Converters/BoolToVisibilityConverter.cs |
| Converters/SemanticColorConverters.cs | src/CortexDNA.App/Converters/SemanticColorConverters.cs |
| Core/CleanupPathGuard.cs | src/CortexDNA.System/CleanupPathGuard.cs |
| Core/DiskCleanupService.cs | src/CortexDNA.Cleanup/DiskCleanupService.cs |
| Core/Logger.cs | src/CortexDNA.Infrastructure/Logger.cs |
| Core/NativeMethods.cs | src/CortexDNA.System/NativeMethods.cs |
| Core/ObservableObject.cs | src/CortexDNA.Core/Core/ObservableObject.cs |
| Core/RamOptimizer.cs | src/CortexDNA.Optimization/RamOptimizer.cs |
| Core/RelayCommand.cs | src/CortexDNA.App/UI/RelayCommand.cs |
| Core/Startup/LegacyStartupDelayMigration.cs | src/CortexDNA.System/Startup/LegacyStartupDelayMigration.cs |
| Core/Startup/StartupApprovalService.cs | src/CortexDNA.System/Startup/StartupApprovalService.cs |
| Core/Startup/StartupCatalogService.cs | src/CortexDNA.System/Startup/StartupCatalogService.cs |
| Core/Startup/StartupCom.cs | src/CortexDNA.System/Startup/StartupCom.cs |
| Core/Startup/StartupDelayService.cs | src/CortexDNA.System/Startup/StartupDelayService.cs |
| Core/Startup/StartupFeatureService.cs | src/CortexDNA.Startup/StartupFeatureService.cs |
| Core/Startup/StartupIconLoader.cs | src/CortexDNA.App/UI/StartupIconLoader.cs |
| Core/Startup/StartupImpactService.cs | src/CortexDNA.System/Startup/StartupImpactService.cs |
| Core/Startup/StartupPackagedCatalog.cs | src/CortexDNA.System/Startup/StartupPackagedCatalog.cs |
| Core/Startup/StartupPaths.cs | src/CortexDNA.System/Startup/StartupPaths.cs |
| Core/Startup/StartupProcessProbe.cs | src/CortexDNA.System/Startup/StartupProcessProbe.cs |
| Core/SystemToolLauncher.cs | src/CortexDNA.System/SystemToolLauncher.cs |
| Core/UpdateServiceLease.cs | src/CortexDNA.System/UpdateServiceLease.cs |
| Core/UpdateVisitor.cs | src/CortexDNA.Hardware/UpdateVisitor.cs |
| Cortex_Dev.bat | Cortex_Dev.bat |
| CortexDNA_Installer.iss | CortexDNA_Installer.iss |
| CortexDNA.csproj | src/CortexDNA.App/CortexDNA.App.csproj |
| CortexDNA.slnx | CortexDNA.slnx |
| docs/UPDATE29_FINAL_REVIEW.md | docs/UPDATE29_FINAL_REVIEW.md |
| docs/UPDATE29_VM_CHECKLIST.md | docs/UPDATE29_VM_CHECKLIST.md |
| LOCAL_ENGINEERING_REPORT.md | LOCAL_ENGINEERING_REPORT.md |
| MainWindow.xaml | src/CortexDNA.App/MainWindow.xaml |
| MainWindow.xaml.cs | src/CortexDNA.App/MainWindow.xaml.cs |
| Models/CleanupModels.cs | src/CortexDNA.Core/Models/CleanupModels.cs |
| Models/HardwareModels.cs | src/CortexDNA.Core/Models/HardwareModels.cs |
| Models/StartupModels.cs | src/CortexDNA.Core/Models/StartupModels.cs |
| README.md | README.md |
| scripts/Build-Release.ps1 | scripts/Build-Release.ps1 |
| scripts/Verify-CIFailureModes.ps1 | scripts/Verify-CIFailureModes.ps1 |
| scripts/Verify-Local.ps1 | scripts/Verify-Local.ps1 |
| Tests/CortexDNA.Tests.csproj | Tests/CortexDNA.Tests.csproj |
| Tests/NuGet.Offline.config | Tests/NuGet.Offline.config |
| Tests/Program.cs | Tests/Program.cs |
| Tests/Standard/CortexDNA.AutomatedTests.csproj | Tests/Standard/CortexDNA.AutomatedTests.csproj |
| Tests/Standard/LegacyMigrationTests.cs | Tests/Standard/LegacyMigrationTests.cs |
| Tests/Standard/RegressionHarnessTests.cs | Tests/Standard/RegressionHarnessTests.cs |
| Themes/DarkTheme.xaml | src/CortexDNA.App/Themes/DarkTheme.xaml |
| Themes/LightTheme.xaml | src/CortexDNA.App/Themes/LightTheme.xaml |
| ViewModels/HardwareViewModel.cs | src/CortexDNA.App/ViewModels/HardwareViewModel.cs |
| ViewModels/MainViewModel.cs | src/CortexDNA.App/ViewModels/MainViewModel.cs |
| ViewModels/StartupViewModel.cs | src/CortexDNA.App/ViewModels/StartupViewModel.cs |
| ViewModels/ViewModelBase.cs | src/CortexDNA.App/ViewModels/ViewModelBase.cs |
