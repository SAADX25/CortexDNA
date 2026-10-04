# Phase 3 migration plan

Baseline: local Update-32 045926b. Phase 3 only; no remote operations or Phase 4.

Keep the Phase 2 shell, eleven views, commands, cleanup confirmation, RAM optimizer, startup services, tray behavior, settings and design resources. Move sensor ownership, WMI/specification cache, performance counters, physical memory, capacity-only drives, network deltas and game-process detection into CortexDNA.Hardware. Core holds immutable records and an IHardwareMonitorService contract without Windows/vendor references.

HardwareMonitorService owns one tracked initialization and polling loop, serialized manual/automatic refresh, cancellation, exactly-once awaited shutdown and a bounded 90-sample memory-only history. Synchronous vendor/WMI/drive calls cannot be interrupted mid-call; cancellation is checked around them, and shutdown waits before releasing their resources. Foreground targets 1 second, visible minimized 5 seconds, hidden pauses, game 10 seconds. Navigation never creates a monitoring service. UI receives immutable snapshots via its dispatcher and rejects queued work after disposal.

CPU/GPU monitors project nullable numeric values and stable identities; multiple GPUs stay separate. Network rates use monotonic elapsed time and reset on adapter/counter changes. Missing readings are unavailable rather than zero. HardwareInfoService retains the existing specification-cache path/schema, but no telemetry history is persisted. RAM optimization remains a separate existing command/service.

Adapt injected hardware test fixtures to the snapshot service seam while preserving their close-once/cancellation assertions. Retain the original regression harness unchanged. Add deterministic lifecycle, serialization, polling, history, sensor, GPU, network and disposed-UI tests, then run the existing full local verification. All excluded product areas remain unimplemented.
