# Phase 3 — Hardware V3 verification report

Implemented and locally verified against **045926b (Update-32)** on October 4, 2026. Phase 3 only. No Phase 4 work was started. HEAD remains Update-32; all Phase 3 changes are unstaged/uncommitted. No push, upload, PR, fetch, pull, GitHub contact or hosted CI dispatch occurred. NuGet restore used Tests/NuGet.Offline.config and the local cache with auditing disabled. The installer/publish outputs are local files only.

## Services and architecture

| Service | Responsibility |
| --- | --- |
| HardwareMonitorService | One initialization task, one loop, serialized automatic/manual refresh admissions, visibility cadence, snapshot publication, bounded history and tracked shutdown. |
| CpuMonitor | CPU load, nullable temperature, clock reading/previous performance-counter estimate; owns/disposes its performance counter. |
| GpuMonitor | Independent stable IDs for all GPUs; nullable core load and all available temperatures, including hotspot/memory sensors. |
| MemoryMonitor | Read-only physical total/available memory through GlobalMemoryStatusEx; no process or working-set optimization. |
| StorageMonitor | Drive capacity/free-space metadata only; per-drive failures are unavailable, no file/directory scan. |
| NetworkMonitor | Per-interface counter deltas over real monotonic elapsed time, resets/adapter changes without artificial spikes. |
| HardwareInfoService | Background WMI OS/BIOS/board/CPU/preferred-GPU/RAM specifications; retains legacy specification-cache location/property names. |
| GameDetectionService | Existing game-process allowlist and this application's prior priority reduction, with priority restoration on disposal. No Gaming Center. |

WindowsHardwareSnapshotSource composes these collectors; HardwareSession and SensorProjection confine all LibreHardwareMonitor types to the Hardware assembly. A failed device update is projected with unavailable readings rather than exposing its old values. Core defines IHardwareMonitorService and MonitoringMode without Windows/vendor/presentation references. Hardware adds one project reference to the existing Infrastructure logger; it adds no package. All eight original projects remain; the acyclic dependency tests pass. The same **26 resolved product packages/versions** remain.

## Immutable models and presentation

Core record models: HardwareSnapshot, CpuSnapshot, GpuSnapshot, MemorySnapshot, StorageSnapshot, NetworkSnapshot, HardwareInfoSnapshot, TemperatureSnapshot and HardwareHistorySample. Collections are ImmutableArray values, readings are nullable numeric values, and stable GPU identity is separate from display name. No vendor hardware/sensor object crosses the UI contract.

HardwareViewModel now consumes snapshots only for monitoring. It no longer owns WMI, DriveInfo, NetworkInterface, GlobalMemoryStatusEx, performance counters, vendor Computer/IHardware/ISensor instances, base-clock lookup, process enumeration, game detection/priority manipulation, specification cache IO or a DispatcherTimer. It is 457 lines versus 1235 in Update-32, retaining formatting/bindable adapters and the existing copy/refresh/Boost/Cleanup commands. RAM optimization and cleanup services remain separate, injected action dependencies with their existing confirmations, mutual exclusion, cancellation and awaited cleanup behavior.

The existing Hardware page and all Phase 2 XAML/design dictionaries are unchanged. Snapshot adapters drive its existing bindings; CPU/GPU/storage items retain stable UI identities rather than rebuilding their view trees every tick. Multiple GPUs with identical display names remain separate, temperature rows are retained, removed/invalid readings are cleared to Unavailable, and unknown RAM/network values are labeled unavailable. Home/Health/Storage/Gaming summaries continue using the same shared VM. No new charts, scans, Health Core or UI redesign was added.

## Polling and lifecycle

- StartAsync is idempotent and tracks the existing 1.5-second initialization delay, vendor open, counters and background specification load. Cancellation stops between collection stages; cancellation of initialization prevents polling.
- One awaited loop reads sensors on workers. Manual requests use the same SemaphoreSlim admission gate. Queued requests are cancellation-aware and counted, so shutdown drains them as well as active native calls before disposing the gate/token resources. Reads cannot overlap.
- Foreground targets about 1 second after a completed read, visible minimized 5 seconds and game mode 10 seconds where applicable. Reading duration adds to the interval; this is deliberately serialized rather than an overlapping fixed timer. Hidden mode waits for visibility changes with no polling timer. The existing minimize-to-tray flow hides the window, so it pauses rather than performing visible-minimized polling. A visibility wake resumes the same loop promptly.
- Game detection is sampled at most once per five seconds, preserving the existing allowlist. All sensor classes are refreshed at the reduced ten-second cadence during a game; no additional gaming optimization is introduced.
- One monitor is constructed per shell. Navigation only changes page activity; it never constructs a new monitor or polling loop. Repeated initialization calls share one task.
- Shutdown marks stopping, cancels, clears subscribers, awaits initialization/loop/active-and-queued refresh admissions, then closes the session/counters and restores owned priority once. ShutdownAsync returns the same task; Dispose/DisposeAsync converge on it. The application continues awaiting existing Boost/Cleanup tasks and Startup shutdown before closing.
- Snapshot callbacks enqueue dispatcher work. HardwareViewModel unsubscribes before shutdown and rejects queued/late presentation updates after disposal. Existing operation cleanup completes without publishing new presentation state after disposal.

HardwareHistory uses a thread-safe **90-slot circular buffer**, retaining CPU usage/temperature, RAM usage, network download/upload and each GPU's usage/temperatures. Returned histories are immutable copies, ordered by acquisition and bounded to 90. No sensor history is written to disk or database. The existing static specs.json metadata cache remains; the optional performance JSON contains only aggregate resource/timing/sample counts.

## Preservation and verification

Verify-Phase3 protects **132 unchanged Update-32 baseline files** (normalized text hashes, raw icon hash). It explicitly permits only the eight existing files migrated at this hardware seam. Phase 1's historical manifest and Phase 2's presentation exception list are unchanged. Original cleanup source, 35 frozen Update-30 safety/domain/test files and the no-excluded-feature check remain green. Installer, Startup, Optimization and Cleanup implementations are unchanged. The original Tests/Program.cs and existing assertions remain intact; ArchitectureTests only replaces its injected constructor argument with the new monitor seam. The existing WPF smoke adds snapshot coverage but retains all prior checks.

| Required verification | Result |
| --- | --- |
| dotnet restore (offline configuration) | PASS |
| Debug build -warnaserror | PASS, 0 warnings/errors |
| Release build -warnaserror | PASS, 0 warnings/errors |
| dotnet test -c Release | **62 passed**, 0 failed/skipped |
| Original regression/WPF subprocess | **32/32** original cases pass |
| Extended Phase 2 WPF subprocess | **99 checks** pass, all 11 pages/both themes, no binding errors |
| Verify-Local / Verify-Phase3 / historical safety checks | PASS |
| Local publish / Inno Setup compile | PASS |
| Local CI failure-propagation script | **3/3** intended failure probes pass |
| git diff --check, tracked and complete temporary-index snapshot | PASS |

Twenty-six new standard test cases cover initialization cancellation, shutdown during non-interruptible initialization/read, refresh serialization, canceled queued requests, no automatic/manual overlap, foreground/minimized/game cadence, hidden pause/wake, one initialization through repeated starts, close/dispose idempotence, worker-thread reads, missing/NaN sensors, multiple identically named GPUs, extra GPU temperatures, 1/5/10-second network deltas, reset/adapter changes, bounded chronological immutable history, invalid history capacity, partial availability, temporary collector failure/recovery, draining 25 queued requests and the vendor-free UI/Core boundary.

Five additional WPF checks cover snapshot rendering with multiple GPUs, clearing stale values while reusing GPU adapter objects, invalid zero-capacity RAM, repeated navigation with an active fake monitor without duplicate starts/refresh, and rejecting queued/late UI updates after disposal. The existing navigation/tray/confirmation-refusal tests remain. Runner totals include subprocess wrappers once; the 32 original and 99 WPF checks are separate subprocess counts, not added to 62.

Evidence: artifacts/phase3-verification.log; artifacts/verification/final-tests/final-review.trx; artifacts/phase3-monitor-tests.log (intermediate focused run); artifacts/phase3-failure-probes.log; artifacts/phase3-performance.log; artifacts/phase3-hardware-metrics.json; artifacts/phase3-package-list.txt; scripts/Verify-Phase3.ps1 and docs/PHASE3_PRESERVATION.json. Existing UI smoke screenshots remain under artifacts/phase2-ui. Local final publish: artifacts/publish/23387c11d0934e27a2a03d0605b50b8f; installer: artifacts/installer/CortexDNA_Installer_v2.1.0.exe.

## Measured CPU/memory and packaging

A separate owned read-only WPF process initialized the real collectors, warmed them for 1.5 seconds, observed foreground polling for 5.00 seconds and hidden behavior for 2.02 seconds. No action command was executed and no sensor values/history were exported.

- Foreground process CPU time: **140.63 ms**, about **2.81% of one logical core** averaged over this short interval (not normalized to all cores).
- Process working set: **95.84 -> 96.67 MiB**; managed memory before collection: **6.04 -> 6.94 MiB**. This includes WPF/runtime/vendor allocation and warm-up; it is not a retained-memory/leak benchmark.
- Hidden process CPU: **0.00 ms**; **0 new snapshots**. History remained bounded (6 samples in this short run).
- Final published directory, including symbols: **9,257,573 bytes**, compared with Phase 2's 9,135,669 bytes: +121,904 bytes (+1.33%).

There is no controlled Update-32 comparative CPU/memory run, so no improvement/regression percentage is claimed. These short measurements do not certify long-running sensor overhead or full-window production performance. Deterministic tests establish the ring bound and single polling owner.

## Sensor limitations and lifecycle/resource risks

Some CPU/GPU load, temperature or clock sensors require driver support/privilege and may be null. CPU clock can use the prior performance-counter/base-clock estimate; it is not a precise per-core frequency measurement. Physical multiple-GPU hardware was not available for certification; stable IDs, duplicate names and multiple GPU values are covered with fixtures. WMI metadata can fall back to the prior static cache and be stale; its GPU preference retains the baseline VRAM/name heuristic. Network rates sum active non-loopback/non-tunnel interfaces, so virtual/VPN plus physical adapters can double-count forwarded traffic. Game detection retains the limited prior process-name allowlist rather than providing a new gaming engine.

Vendor open/update/close, WMI and drive-capacity APIs are synchronous and cannot be forcibly interrupted mid-call safely. Cancellation is checked around them, WMI queries request five-second timeouts, and shutdown waits before closing resources. A blocked driver/provider or inaccessible mapped drive can delay shutdown; this phase does not kill threads, add an elevated helper or force unsafe resource release. Close errors are logged, with a single close attempt. Dispose begins asynchronous shutdown; callers requiring release completion must await ShutdownAsync or DisposeAsync (the application does). No pending snapshot is permitted to update a disposed UI.

Native DPI/accessibility/high-contrast and destructive/UAC/installer VM scenarios were not expanded beyond retained checks. No production/driver/VM certification is claimed. No Health Engine/Core, Cleanup V3, Storage Analyzer, Gaming/Security/Repair Center, Elevated Helper, Agent Management, AI Agent or sandbox feature/dependency/placeholder/reserved architecture was implemented. Later phases remain out of scope.

## Files changed and local Git state

Existing files migrated: HardwareViewModel, AppComposition, MainWindow visibility handling, HardwareSession and its project reference, the two injected test fixtures, and Verify-Local. Added source/services, tests and preservation/report files are listed in the complete status below. Repository staging was not changed. Raw diff stats omit untracked additions; artifacts/phase3-full-diff-stat.txt includes all additions through an isolated temporary index.

```text
 M Tests/Standard/ArchitectureTests.cs
 M Tests/UiSmoke/Program.cs
 M scripts/Verify-Local.ps1
 M src/CortexDNA.App/Composition/AppComposition.cs
 M src/CortexDNA.App/MainWindow.xaml.cs
 M src/CortexDNA.App/ViewModels/HardwareViewModel.cs
 M src/CortexDNA.Hardware/CortexDNA.Hardware.csproj
 M src/CortexDNA.Hardware/HardwareSession.cs
?? Tests/Standard/HardwareMonitorTests.cs
?? docs/PHASE3_APPROVED_MIGRATIONS.json
?? docs/PHASE3_MIGRATION_PLAN.md
?? docs/PHASE3_PRESERVATION.json
?? scripts/Verify-Phase3.ps1
?? src/CortexDNA.Core/Contracts/IHardwareMonitorService.cs
?? src/CortexDNA.Core/Models/HardwareSnapshots.cs
?? src/CortexDNA.Hardware/CpuMonitor.cs
?? src/CortexDNA.Hardware/GameDetectionService.cs
?? src/CortexDNA.Hardware/GpuMonitor.cs
?? src/CortexDNA.Hardware/HardwareHistory.cs
?? src/CortexDNA.Hardware/HardwareInfoService.cs
?? src/CortexDNA.Hardware/HardwareMonitorService.cs
?? src/CortexDNA.Hardware/MemoryMonitor.cs
?? src/CortexDNA.Hardware/NetworkMonitor.cs
?? src/CortexDNA.Hardware/SensorProjection.cs
?? src/CortexDNA.Hardware/StorageMonitor.cs
?? src/CortexDNA.Hardware/WindowsHardwareSnapshotSource.cs
```

Raw git diff --stat:

```text
 Tests/Standard/ArchitectureTests.cs               |   2 +-
 Tests/UiSmoke/Program.cs                          |  58 +-
 scripts/Verify-Local.ps1                          |   2 +-
 src/CortexDNA.App/Composition/AppComposition.cs   |   6 +-
 src/CortexDNA.App/MainWindow.xaml.cs              |   5 +-
 src/CortexDNA.App/ViewModels/HardwareViewModel.cs | 990 +++-------------------
 src/CortexDNA.Hardware/CortexDNA.Hardware.csproj  |   3 +-
 src/CortexDNA.Hardware/HardwareSession.cs         |  17 +-
 8 files changed, 187 insertions(+), 896 deletions(-)
```

This report is an additional untracked file beyond the captured status. HEAD stays 045926b. No new commit or remote operation occurred. Phase 3 is ready for local review; stop here and do not begin Phase 4 automatically.
