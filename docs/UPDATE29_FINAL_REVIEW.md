# CortexDNA — final review after Update-29

2026-10-04, Asia/Amman. Review performed against local source only. Existing uncommitted
changes were preserved. No broad refactor, UI redesign, deletion of existing tests, actual
service stop/start or host startup/task modification was performed for verification.

## Confirmed remaining problems

- Old 8-hex GetHashCode task names were not recognized by the stable-name implementation.
  Name-only recognition also lacked verification of the stored task's identity/definition.
- The installer retained Inno's default append behavior for uninstall logs. Upgrading an old
  version with recursive `[UninstallDelete]` records could retain those unsafe records.
  Inno confirms old installation records normally remain effective after upgrade:
  [append semantics](https://jrsoftware.org/ishelp/topic_appendnotes.htm).
- WPF session-ending shutdown was not connected to the asynchronous exit/resource cleanup
  path. WPF can shut down without the normal cancellable Closing path:
  [SessionEnding](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.sessionending?view=windowsdesktop-10.0),
  [Closing](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.closing?view=windowsdesktop-10.0).

## Implemented closure

- Added a focused legacy-delay matcher/migration and a Task Scheduler adapter. Scope is
  exactly `\CortexDNA\StartupDelay`, never arbitrary tasks or nested folders. Verification
  requires one uniquely matching current user StartupItem, legacy/modern naming pattern,
  exact description, absolute executable path, exact arguments/working directory, matching
  principal and trigger SID (account aliases resolved), least privilege, interactive token,
  a single Exec/logon trigger and 30-second delay. Disabled/unknown/unverifiable tasks stay.
- The migration creates the target with TASK_CREATE only, initially disabled, preserving
  the complete definition except its URI/temporary enabled state. It verifies, activates,
  verifies again, then rechecks/deletes only the original definition. Creation/activation/
  deletion failure preserves the original and attempts rollback of only the verified new
  task. Target collisions, duplicate/ambiguous/running tasks are not overwritten/deleted.
- Uncertain tasks are logged and shown via existing DiagnosticsNote; no extra UI was added.
  Read-only loading recognizes verified legacy tasks without migrating. The WPF smoke
  explicitly uses this mode so it cannot mutate the host's startup tasks.
- Added a standard xUnit project and root solution. `dotnet test -c Release` now discovers
  the native tests; one native suite test runs all existing harness cases in an isolated
  process. Pinned test-only dependencies were available locally. The harness remains intact,
  with one additional SessionEnding case; no existing coverage was reduced.
- CI restores/builds the solution with warnaserror, runs native tests and publishes with
  warnaserror. Publish errors are propagated by the existing script. The workflow has read-
  only permissions and no continue-on-error, artifact upload, Release or repository write.
  No GitHub workflow was dispatched; changes exist locally only.
- Installer retains its AppId, file manifest, shortcuts, graceful CloseApplications setting
  and original-user postinstall launch. UninstallLogMode=overwrite prevents inherited old
  recursive deletion records. Both CortexDNA profile folders intentionally survive uninstall.
  Tradeoff: obsolete older-version-only files/shortcuts may remain because historical
  ownership records are not merged; no broad deletion was introduced.
- SessionEnding defers the immediate shutdown request and calls the existing explicit exit
  path to allow resource release/service restoration. The regular tray Exit uses that same
  path. Native Windows/Restart Manager behavior still requires VM verification.

Changed in this round: Core/Startup/StartupDelayService.cs,
Core/Startup/StartupFeatureService.cs, Core/Startup/LegacyStartupDelayMigration.cs (new),
ViewModels/StartupViewModel.cs, App.xaml.cs, MainWindow.xaml.cs, AssemblyInfo.cs,
CortexDNA_Installer.iss, CortexDNA.slnx (new), Tests/CortexDNA.Tests.csproj, Tests/Program.cs,
Tests/Standard/* (new standard project and tests), .github/workflows/windows.yml,
scripts/Build-Release.ps1, scripts/Verify-Local.ps1,
scripts/Verify-CIFailureModes.ps1 (new), README.md and this review/checklist documentation.
No source files were deleted. No local commit was created.

## New automated checks

- 25 native migration cases: verified migration and subsequent recognition; description,
  path, args, SID, elevation, delay, working-directory, extra-action, disabled/name/XML
  mismatch; exact folder/nested-folder restrictions; read-only mode; SID serialization;
  duplicates, distinct-ID prefix ambiguity, target collision, running task; create,
  activation, deletion, verification and changed-source failure handling.
- Existing 31 harness cases retained, plus one callback-only WPF SessionEnding test:
  **32/32 passed**. The callback does not end the real host session.
- **26/26 native xUnit tests passed, zero failed/skipped**: 25 migration cases plus the
  complete harness suite. These counts are not additive: the suite includes the 32 cases.
- Three isolated negative CI probes passed: a deliberate compile warning fails under
  warnaserror; a built/discovered deliberate failing test returns nonzero; a substituted
  publish failure makes Build-Release.ps1 fail. The intentional fixture failure is separate
  from the regression suite. No production source is changed by these probes.

## Final verification

- Offline solution restore: successful from cached dependencies.
- Debug and Release solution builds with warnaserror: **0 compiler warnings, 0 errors**.
- Native tests: **26 passed / 0 failed / 0 skipped**, with **32/32** harness output in TRX.
- CI failure propagation probes: **3/3 passed**.
- Release win-x64 ReadyToRun publish and Inno Setup compilation: successful.
- No regression observed in the executed tests. No install/uninstall was run on the host.
- Results: `artifacts/verification/update29-final.txt`, `update29-debug-final.txt`,
  `update29-test-final.txt`, `ci-failure-probes.txt`, and
  `artifacts/verification/final-tests/final-review.trx` (all local/gitignored).

## Remaining risk and readiness

No disposable VM was accessible for this session. The explicit release gates are in
[UPDATE29_VM_CHECKLIST.md](UPDATE29_VM_CHECKLIST.md): clean install, upgrade/running app,
shortcuts/ownership/uninstall sentinels/profile retention, all BITS/wuauserv state and
cancellation/restart cases, real approval toggles, delay/reboot/remove and legacy migration.

Native Task Scheduler registration is not an atomic rename. Source/target may briefly
coexist; running/queued tasks are deferred, and definition checks narrow concurrent-change
risk, but external edits between read and mutation are not an atomic CAS. Failed native
reads or rollback can leave a disabled new task or require review. Conservative whole-XML
verification can defer a task if Scheduler changes fields beyond identity aliases.

The installer may leave obsolete older-owned files/shortcuts under the safer replacement
ownership log. Restart Manager/Windows may need to retry the cancelled session-ending
request after the app exits. Force-kill/power loss cannot guarantee service restoration.
These native behaviors are not proven by fake tests or successful installer compilation.
The prior limitations of permanent cleanup, temporary RAM trimming and unaudited online
dependency vulnerability feeds remain. No dependency version upgrade was attempted here.

Production-readiness assessment: **8/10**, conditional on completing the disposable VM
release gates. This is an engineering judgment, not a claim that native integration has
been validated on every Windows/device configuration.

**Nothing was pushed or uploaded to GitHub or any remote service.**
