# Cortex DNA

Cortex DNA is a local Windows desktop utility built with C#/.NET 10 and WPF.
The local source is authoritative. This pass does not publish anything, create a release,
contact Git remotes, or upload build artifacts.

## Implemented features

- CPU/GPU sensors through LibreHardwareMonitor, RAM usage, network rate, storage and WMI system details.
- Startup catalog, per-user approval changes, best-effort boot impact and optional 30-second delayed startup.
- Selectable disk cleanup with confirmation, scan totals and cleanup results.
- Optional RAM working-set trimming, game detection, dark/light themes and system tray.
- Local logs and hardware cache. Windows privacy toggles are **not implemented in this local version**.

## Requirements and privileges

Windows 10 or newer, x64, and .NET 10 Desktop Runtime for the framework-dependent executable.
Building requires the .NET 10 SDK. Installer compilation requires Inno Setup 6.

The app and build scripts run as the current user. Some hardware sensors and system cleanup
locations require explicitly launching the application as Administrator. Access failures are
skipped or reported; the app does not silently elevate. System tools are launched from the
Windows system directory. Administrative tools, including Registry Editor, MMC consoles, Command Prompt and PowerShell, request elevation through UAC when clicked. Declining UAC cancels the launch. Registry Editor is resolved from the Windows directory; MMC snap-ins use the system MMC executable.
The installer requires elevation to install into Program Files, then launches the app as the
original user.

## Local development and verification

```powershell
dotnet restore CortexDNA.slnx --configfile Tests/NuGet.Offline.config -p:NuGetAudit=false
dotnet build CortexDNA.csproj --no-restore -c Debug -warnaserror
dotnet test -c Release --no-restore -warnaserror
powershell -NoProfile -File scripts/Verify-Local.ps1
```

The verification script uses cached NuGet packages only. If a required package is missing,
offline restore fails instead of contacting a package source. The console regression harness
has no test-framework package dependency and remains intact. The standard xUnit project
adds migration tests and runs the complete harness as an isolated child process. `dotnet test`
from the repository root discovers these tests through CortexDNA.slnx and produces native
test results. The xUnit/Test SDK versions are pinned; TRX outputs stay local.

The harness creates fixtures only under its build output and tests command parsing, stable
IDs across separate processes, cleanup path restrictions, junction handling, ancestor locking,
cancellation, exact totals, service failure recovery through fakes, and hardware shutdown.
Its WPF smoke test opens the dashboard, scans startup entries read-only, minimizes/restores,
and exits through the real shutdown path. It does not clean real Windows caches, change
startup entries, stop real Windows services, or trim real processes.

## Publishing and installer

```powershell
# Restore using the local cache for the Windows runtime identifier first:
dotnet restore CortexDNA.csproj -r win-x64 --configfile Tests/NuGet.Offline.config -p:NuGetAudit=false
./scripts/Build-Release.ps1 -NoRestore
# Publish without compiling an installer:
./scripts/Build-Release.ps1 -NoRestore -SkipInstaller
```

`Build_Release.bat` wraps the PowerShell build script. The project path comes from the script
location; no fixed drive/path or elevation is required. Each publish uses a fresh directory
under `artifacts/publish/` to avoid packaging stale files. Set `ISCC_PATH` if Inno Setup is
outside the standard installation path. The installer is written to
`artifacts/installer/CortexDNA_Installer_v2.1.0.exe`. Artifacts are local and gitignored.
The installer removes only installer-owned files; it does not recursively erase the install
folder or user profile data and does not force-kill unrelated processes. It overwrites the
uninstall ownership log to avoid inheriting recursive deletion rules from old releases; obsolete
old-version-only files/shortcuts can remain and need verification in a disposable VM. Both
LocalAppData/CortexDNA and AppData/CortexDNA data are intentionally retained on uninstall.

## Cleanup safety

- Only built-in category/root combinations are accepted. User temp is the canonical
  `%LocalAppData%/Temp` location, not an arbitrary `TEMP` environment override.
- Drive roots, directory junctions/reparse points and file links are refused.
- Ancestor directory handles remain open without write/delete sharing while enumerating and
  deleting files; if a safe lease cannot be acquired, that directory is skipped.
- Recent Items is top-level only in both scan and clean. Roots/subdirectories are retained.
  Read-only files are preserved; their attributes are not rewritten.
- Windows Update cleanup is opt-in. Both BITS and Windows Update must be stopped successfully;
  only services initially running are restarted, including after failure/cancellation.
  State is checked before cache deletion, and restoration failures are reported.
- Cleanup operations are serialized. Scan results are detached from UI-bound models.

Cleanup permanently deletes selected files. Applications may have active temporary files;
review the selection and close affected applications. Freed-byte totals describe logical file
sizes, not guaranteed additional physical space (compression/hardlinks can differ).

## Startup and RAM safety

Delay task IDs use a deterministic SHA-256 digest and the user's SID. Scheduled tasks run
with the user's interactive token, without elevation. Delay accepts existing absolute `.exe`
paths and preserves argument text. Unquoted paths containing spaces and malformed quotes
are refused. Policy Run entries are excluded because StartupApproved does not control them.
Packaged-app changes require an existing exact task key and cannot override policy states or
create speculative registry keys. Delay registration/removal failures are surfaced, with
rollback where possible.

Old 8-hex randomized Delay IDs are recognized and migrated only within the exact StartupDelay
folder after unique description/action/arguments/working-directory/user/trigger verification.
The new task starts disabled, is verified and activated before the old verified definition is
deleted. Conflicts, running tasks and uncertain definitions remain for review in DiagnosticsNote
and the local log. The regression smoke explicitly disables migration and never changes host tasks.
Valid shortcut working directories are preserved in the scheduled action; unavailable
directories are refused. Registry approval behavior and packaged startup
state are Windows-dependent and still require testing on the target Windows versions.

RAM Boost requests confirmation and trims only accessible background processes owned by the
same user in the same session, excluding the foreground app and known critical/game/security
processes. It does not force garbage collection, disable the pagefile or promise permanent
memory reclamation. The reported available-memory change is an observation, not proof of
which process freed memory. Performance can regress when trimmed pages are needed again.

## Local data

- Logs: `%LocalAppData%/CortexDNA/Logs/log.txt` (1 MiB rotation, one backup).
- Hardware cache: `%LocalAppData%/CortexDNA/specs.json`.
- Theme: `%AppData%/CortexDNA/theme_settings.json` (built-in themes only).

Logs may contain exception details and local paths. They remain on this device. The CI file
under `.github/workflows/` is prepared locally; no workflow was dispatched or uploaded.
Dependencies remain LibreHardwareMonitorLib 0.9.5 and System.Management 10.0.2. An online
vulnerability-feed audit was not performed in this local-only pass.

See `LOCAL_ENGINEERING_REPORT.md` for findings, changed/deleted files and verification results.
No LICENSE file exists in the local repository; no license was invented as part of this pass.
See [the Update-29 VM checklist](docs/UPDATE29_VM_CHECKLIST.md) for installer, real services and startup integration release gates. CI failure propagation can be checked locally with scripts/Verify-CIFailureModes.ps1.
