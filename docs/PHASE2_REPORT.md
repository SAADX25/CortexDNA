# Phase 2 — Shell + Navigation + Design System

Implemented and locally verified on October 4, 2026. Phase 3 has not started.

Phase 1 was already committed as a94a4ce (update-31). Before any Phase 2 edit, an explicit local checkpoint commit was created: **eec6717bff431a1d162f98d493852fcecde55c75**. It has the same tree as a94a4ce; the checkpoint commit is intentionally empty. Phase 2 remains unstaged and uncommitted. No push, upload, PR, fetch, pull, hosted CI dispatch or remote contact occurred. Offline NuGet restore used the existing cache. All installer output is local.

## Navigation and visible changes

MainWindow now owns chrome, sidebar, one content host, page heading, notification panel and operation-progress/footer areas. It retains tray restore/minimize, keyboard shortcuts, explicit exit, existing session-ending integration and awaited hardware/startup shutdown. Reentrant Close after already-completed shutdown is deferred through the dispatcher.

| Page | ViewModel | Existing behavior / Phase 2 presentation |
| --- | --- | --- |
| Home | HomeViewModel | Device, CPU/GPU/RAM/network summary; existing copy-specs command; links to Cleanup/Startup/Hardware. |
| Health | HealthViewModel | Existing memory/drive information, explicit scan-unavailable message. No score or animated core. |
| Cleanup | CleanupViewModel | Six original cleanup categories; existing scan, explicit category selection/confirmation, clean and results path. |
| Storage | StorageViewModel | Existing drive capacity/free-space data only. No file scan or analyzer. |
| Startup | StartupPageViewModel | Existing StartupImpactControl and shared StartupViewModel. Enable/disable/delay/remove-delay and F5 preserved. |
| Hardware | HardwarePageViewModel | Existing HardwareDashboardControl and shared HardwareViewModel; Boost/Clean/copy/specs/refresh retained. |
| Gaming | GamingViewModel | Read-only existing automatic game detection/polling-reduction state. No new optimization. |
| Security | SecurityViewModel | Honest unavailable posture view; no scanning, repair or security engine. |
| Tools | ToolsViewModel | Original eleven allowlisted Windows tools, search and unchanged elevation requirements. Ctrl+F navigates and focuses search. |
| Settings | SettingsViewModel | Existing theme/opacity persistence, uninstall/release links; persisted Reduced Motion. |
| Diagnostics | DiagnosticsViewModel | Local app/OS/privilege/status, copy diagnostics and open existing local logs. No upload. |

Each page has a separate XAML view, code-behind and ViewModel. All eleven page ViewModels are cached for one shell lifetime. PageId, INavigationAware, INavigationService, NavigationStore and NavigationService are application UI infrastructure. Repeated navigation does not reconstruct the feature services. Same-page/invalid navigation is rejected; exit disposes pages once. The existing Overview navigation alias remains valid.

## Reusable design resources

Eleven dictionaries under src/CortexDNA.App/Resources: Colors, Brushes, Typography, Spacing, Cards, Buttons, Navigation, ToggleSwitch, Dialogs, Progress and Animations. Dark/light palettes remain in Themes. CortexDNA uses navy/cyan with its own card/sidebar/chrome arrangement, with a readable white/teal light palette, keyboard focus rings, semantic accent/success/warning/error brushes and OnAccent text contrast.

ViewModels expose UiState values (BoostState, ImpactState), operation flags and actual progress instead of visual color strings. Obsolete unused UsedColor metadata was removed from StorageDrive. Constructors accept IDialogService; WpfDialogService keeps cleanup selection/results and explicit memory confirmation. NotificationCenter keeps at most 20 local entries with dismiss and consecutive-duplicate suppression. OperationPresentation observes existing tasks; it never schedules system work.

Motion provides a single 180ms fade/8px slide and finite hover/selection effects. It cancels clocks on replacement/hide/minimize or Reduced Motion. Persisted Reduced Motion also follows Windows client-area-animation and high-contrast preferences. Hardware copy feedback now cancels its delay on Unloaded; shell observers, tool filter and settings debounce resources detach/stop on disposal. There is no rendering loop or additional sensor polling timer.

## Behavior and safety preserved

Update-30 cleanup algorithms, exact path guards/locks, update-service lease/restoration, RAM process exclusions, startup migration/rollback/policies, COM disposal, logging, installer ownership/identity/privilege model, single-instance handshake and session-ending handler remain intact. Original Tests/Program.cs and domain safety sources remain hash-protected. Existing destructive action confirmations remain required; injected refusal tests prove no memory optimizer or cleanup deletion is invoked when declined.

The historical Phase 1 manifest is unchanged. Verify-Phase1 accepts an explicit ApprovedPhase2Ui switch with twelve named presentation exceptions in docs/PHASE2_UI_EXCEPTIONS.json. It verifies **35 original files unchanged**, the original cleanup implementation after only its Phase 1 interface adoption, and a normalized hash permitting only removal of obsolete Core visual metadata. Default Phase 1 verification stays strict. Verify-Local now requests the approved UI mode.

No product PackageReference or product project dependency changed; 26 resolved third-party product packages remain. The new test executable references the existing app and adds no packages. Agent Management, AI Agents, Agent Security Sandbox and sandbox deployment are permanently absent: no page, menu, placeholder, flag, dependency, code or reserved architecture. Health Core, Cleanup V3, Storage Analyzer, new Gaming/Security/Repair engines and Elevated Helper were not implemented.

## Verification results and added tests

| Check | Result |
| --- | --- |
| Offline dotnet restore CortexDNA.slnx | PASS |
| dotnet build -c Debug -warnaserror | PASS: 0 warnings, 0 errors |
| dotnet build -c Release -warnaserror | PASS: 0 warnings, 0 errors |
| dotnet test -c Release | PASS: 36 tests, 0 failed/skipped |
| Original regression / WPF smoke subprocess | PASS: 32/32 original cases |
| New isolated WPF subprocess | PASS: 94 checks; all 11 pages in both themes; zero binding errors |
| Local publish / Inno Setup compile | PASS |
| Frozen safety files / cleanup / exclusions | PASS |
| CI failure-propagation probes | PASS: 3/3 (deliberate warning/test/publish failures produce nonzero exits) |
| git diff --check, tracked and complete temporary-index snapshot | PASS |

The 36 runner tests include two subprocess wrappers; 32 original cases and 94 UI checks are reported separately rather than added to the runner count. Three new runner tests cover bounded/dismissible notifications, empty/duplicate suppression and the isolated WPF suite. The WPF suite covers exact navigation entry/exit/disposal counts, invalid/same-page navigation, selection and cached/shared VMs, legacy alias, original tool filtering, six cleanup categories, confirmation refusal, real-progress observation/detachment, theme/opacity/Reduced Motion persistence, cancellation of animation clocks, all four dialog resource loads, minimum window size, tray minimize and awaited explicit exit/close-once.

The added smoke cancels its injected hardware initialization before presentation and uses fake cleanup/startup/memory/dialog services. A separate fake fixture checks confirmation refusal. It neither performs cleanup nor writes startup configuration. Product App.xaml.cs and its single-instance guard are unchanged; the smoke collection runs serially to avoid conflicting with the original harness. The smoke clears automatic StartupUri through its WPF backing field solely inside the test process; a future framework change may require adjusting that test setup.

Evidence: artifacts/phase2-verification.log, artifacts/verification/final-tests/final-review.trx, artifacts/phase2-failure-probes.log, artifacts/phase2-ui/metrics.txt and artifacts/phase2-package-list.txt. Screenshots in artifacts/phase2-ui were inspected for dark/light Home, Hardware, Settings and minimum-size layout. A successful publish was generated under artifacts/publish/f49acda8b2bb4fe499f5559249f0f874; the installer is artifacts/installer/CortexDNA_Installer_v2.1.0.exe.

## Performance impact and known limits

The final local fake-service run measured **14.63 ms for 110 cached navigation changes**, excluding layout waits. The shell retains eleven small page ViewModels and shares its one existing hardware/startup VM pair. Notifications are bounded to twenty. The only new timer is the 250ms settings-save debounce, active only during preference edits. Page animation clocks are finite/cancelled; the existing monitor's polling behavior is unchanged.

The complete published payload including symbols is **9,135,669 bytes**, versus Phase 1's 9,027,817: **+107,852 bytes (+1.19%)**. This packaging size and fake navigation timing do not establish live startup, sensor CPU or memory performance; those were not benchmarked.

Known limits: native DPI, screen-reader and Windows high-contrast interaction were not manually exercised; 150%/200% PNG renders are scaled renders, not native per-monitor DPI verification. Physical hardware telemetry and destructive/UAC/installer VM scenarios were not expanded beyond preserved regression coverage. Health/Security scan functions intentionally remain unavailable, Storage is capacity-only and Gaming only exposes prior behavior. Existing sensor/WMI logic remains in the shared HardwareViewModel pending separately approved Phase 3. Screenshots use cancelled-monitor fixtures and therefore show initializing/calculating telemetry. No claim of production/VM certification is made.

## Files changed, git status and diff

Source changes are limited to shell/UI composition and adapters, design resources, semantic metadata, verification scripts and tests. All system/feature implementation modules remain unchanged. Full paths and git status are captured below. Raw git diff --stat covers tracked modifications; additions are listed in status and included in artifacts/phase2-full-diff-stat.txt, produced with a temporary index without touching repository staging. Final complete-diff whitespace checks include this report.

```text
 M CortexDNA.slnx
 M Tests/CortexDNA.Tests.csproj
 M Tests/Standard/CortexDNA.AutomatedTests.csproj
 M scripts/Verify-Local.ps1
 M scripts/Verify-Phase1.ps1
 M src/CortexDNA.App/AboutWindow.xaml
 M src/CortexDNA.App/App.xaml
 M src/CortexDNA.App/CleanConfirmationWindow.xaml
 M src/CortexDNA.App/CleanupResultsWindow.xaml
 M src/CortexDNA.App/Composition/AppComposition.cs
 M src/CortexDNA.App/Controls/HardwareDashboardControl.xaml
 M src/CortexDNA.App/Controls/HardwareDashboardControl.xaml.cs
 M src/CortexDNA.App/Controls/StartupImpactControl.xaml
 M src/CortexDNA.App/MainWindow.xaml
 M src/CortexDNA.App/MainWindow.xaml.cs
 M src/CortexDNA.App/Themes/DarkTheme.xaml
 M src/CortexDNA.App/Themes/LightTheme.xaml
 M src/CortexDNA.App/ViewModels/HardwareViewModel.cs
 M src/CortexDNA.App/ViewModels/MainViewModel.cs
 M src/CortexDNA.App/ViewModels/StartupViewModel.cs
 M src/CortexDNA.Core/Models/HardwareModels.cs
?? Tests/Standard/Phase2Tests.cs
?? Tests/UiSmoke/CortexDNA.UiSmoke.csproj
?? Tests/UiSmoke/Program.cs
?? docs/PHASE2_CORE_MODEL_HASH.txt
?? docs/PHASE2_MIGRATION_PLAN.md
?? docs/PHASE2_UI_EXCEPTIONS.json
?? src/CortexDNA.App/ConfirmationWindow.xaml
?? src/CortexDNA.App/ConfirmationWindow.xaml.cs
?? src/CortexDNA.App/Converters/UiStateBrushConverter.cs
?? src/CortexDNA.App/Navigation/NavigationService.cs
?? src/CortexDNA.App/Navigation/NavigationStore.cs
?? src/CortexDNA.App/Navigation/PageId.cs
?? src/CortexDNA.App/Resources/Animations.xaml
?? src/CortexDNA.App/Resources/Brushes.xaml
?? src/CortexDNA.App/Resources/Buttons.xaml
?? src/CortexDNA.App/Resources/Cards.xaml
?? src/CortexDNA.App/Resources/Colors.xaml
?? src/CortexDNA.App/Resources/Dialogs.xaml
?? src/CortexDNA.App/Resources/Navigation.xaml
?? src/CortexDNA.App/Resources/Progress.xaml
?? src/CortexDNA.App/Resources/Spacing.xaml
?? src/CortexDNA.App/Resources/ToggleSwitch.xaml
?? src/CortexDNA.App/Resources/Typography.xaml
?? src/CortexDNA.App/Services/AppearanceService.cs
?? src/CortexDNA.App/Services/IDialogService.cs
?? src/CortexDNA.App/Services/NotificationCenter.cs
?? src/CortexDNA.App/Services/OperationPresentation.cs
?? src/CortexDNA.App/Services/SystemToolService.cs
?? src/CortexDNA.App/Services/WpfDialogService.cs
?? src/CortexDNA.App/UI/Motion.cs
?? src/CortexDNA.App/UI/UiState.cs
?? src/CortexDNA.App/ViewModels/NavigationItemViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/CleanupViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/DiagnosticsViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/GamingViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/HardwarePageViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/HealthViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/HomeViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/PageViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/SecurityViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/SettingsViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/StartupPageViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/StorageViewModel.cs
?? src/CortexDNA.App/ViewModels/Pages/ToolsViewModel.cs
?? src/CortexDNA.App/Views/Pages/CleanupPage.xaml
?? src/CortexDNA.App/Views/Pages/CleanupPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/DiagnosticsPage.xaml
?? src/CortexDNA.App/Views/Pages/DiagnosticsPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/GamingPage.xaml
?? src/CortexDNA.App/Views/Pages/GamingPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/HardwarePage.xaml
?? src/CortexDNA.App/Views/Pages/HardwarePage.xaml.cs
?? src/CortexDNA.App/Views/Pages/HealthPage.xaml
?? src/CortexDNA.App/Views/Pages/HealthPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/HomePage.xaml
?? src/CortexDNA.App/Views/Pages/HomePage.xaml.cs
?? src/CortexDNA.App/Views/Pages/SecurityPage.xaml
?? src/CortexDNA.App/Views/Pages/SecurityPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/SettingsPage.xaml
?? src/CortexDNA.App/Views/Pages/SettingsPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/StartupPage.xaml
?? src/CortexDNA.App/Views/Pages/StartupPage.xaml.cs
?? src/CortexDNA.App/Views/Pages/StoragePage.xaml
?? src/CortexDNA.App/Views/Pages/StoragePage.xaml.cs
?? src/CortexDNA.App/Views/Pages/ToolsPage.xaml
?? src/CortexDNA.App/Views/Pages/ToolsPage.xaml.cs
```

Raw git diff --stat:

```text
 CortexDNA.slnx                                     |   1 +
 Tests/CortexDNA.Tests.csproj                       |   1 +
 Tests/Standard/CortexDNA.AutomatedTests.csproj     |   2 +
 scripts/Verify-Local.ps1                           |   2 +-
 scripts/Verify-Phase1.ps1                          |  14 +-
 src/CortexDNA.App/AboutWindow.xaml                 | 104 +--
 src/CortexDNA.App/App.xaml                         | 149 ++--
 src/CortexDNA.App/CleanConfirmationWindow.xaml     | 220 ++---
 src/CortexDNA.App/CleanupResultsWindow.xaml        |  75 +-
 src/CortexDNA.App/Composition/AppComposition.cs    |  25 +-
 .../Controls/HardwareDashboardControl.xaml         | 642 ++++++---------
 .../Controls/HardwareDashboardControl.xaml.cs      |  68 +-
 .../Controls/StartupImpactControl.xaml             | 315 +++----
 src/CortexDNA.App/MainWindow.xaml                  | 777 ++++--------------
 src/CortexDNA.App/MainWindow.xaml.cs               | 903 +++------------------
 src/CortexDNA.App/Themes/DarkTheme.xaml            |  38 +-
 src/CortexDNA.App/Themes/LightTheme.xaml           |  38 +-
 src/CortexDNA.App/ViewModels/HardwareViewModel.cs  | 240 +++---
 src/CortexDNA.App/ViewModels/MainViewModel.cs      | 135 ++-
 src/CortexDNA.App/ViewModels/StartupViewModel.cs   |   9 +-
 src/CortexDNA.Core/Models/HardwareModels.cs        |   7 -
 21 files changed, 1101 insertions(+), 2664 deletions(-)
```

This report is an additional untracked file beyond the status captured above. HEAD remains the Phase 1 checkpoint; Phase 2 is ready for local review and has not been committed. Nothing was pushed or contacted remotely. Stop here; do not begin Phase 3 automatically.
