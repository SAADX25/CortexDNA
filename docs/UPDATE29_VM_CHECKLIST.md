# Update-29: disposable VM verification

Do not execute this plan on the user's host. No disposable VM was accessible through the
local Hyper-V/VirtualBox/VMware tools during this review. Installer compilation and mocked
integration tests passed; real install/uninstall/service/startup checks remain release gates.

## Prepare

1. Use a disposable Windows 10 x64 VM and a Windows 11 x64 VM, with snapshots, .NET 10
   Desktop Runtime, a standard user and an administrator. Copy local installers into the VM.
2. Snapshot before each group. Record the original BITS/wuauserv states and export only VM
   fixture registry values/tasks. Disable automatic game-mode interference for measurement.
3. Keep all test files in a distinct `CortexDNA_VM_Test` fixture folder. Use a harmless
   .NET console probe that appends UTC time, arguments and user SID to a VM fixture log and
   exits. Do not select existing personal startup applications as test targets.
4. Treat a failure as a release blocker: preserve VM-only logs, restore the snapshot and
   repeat after a fix. Do not use force-kill as the successful shutdown path.

## Installer matrix

| Case | Steps | Expected evidence |
|---|---|---|
| Clean install | Start with no CortexDNA, run the new local installer as a standard user and approve installation UAC; choose Desktop shortcut | One app/uninstall entry, correct version and install path, correct Start Menu shortcut, Desktop shortcut created only when selected; app launch uses original user token |
| Desktop opt-out | Restore clean snapshot, install with Desktop unchecked | Start Menu works; no Desktop shortcut created |
| Different path | Install into a VM path with spaces | App and shortcuts work; files resolve from the selected install directory |
| Upgrade | Install previous release, create theme/cache/log data, upgrade using the same AppId | One uninstall entry, new version/files, retained appearance/data; no downgrade to an old binary |
| Running upgrade | Run app normally and minimized to tray; also test during a VM-only cleanup; start upgrade and select close applications | Restart Manager detects the mapped app; app exits gracefully, cleanup cancellation restores services before exit; no file-in-use replacement or forced termination counted as success |
| Cancel upgrade | Cancel before changes are committed | Existing app and data remain usable |
| Ownership | Add `keep-user.txt` and `CustomUserFolder/keep.txt` inside the install folder after install, with hashes recorded | Upgrade and uninstall leave both sentinel files unchanged |
| Normal uninstall | Uninstall from Programs/Settings, app closed and then separately app running | Installer-owned executables/dependencies/shortcuts are removed; no other app processes or personal files are deleted |
| Profile retention | Put distinguishable sentinels into `%LocalAppData%/CortexDNA` and `%AppData%/CortexDNA`, record hashes, uninstall | Both profile folders and their data remain. This version intentionally preserves logs/cache/theme; it does not perform a profile purge |
| Older unsafe uninstaller | Install a release containing old recursive `[UninstallDelete]` rules, add install/profile sentinels, upgrade then uninstall with the new installer | Historical recursive deletions are not inherited; sentinels survive. Verify old-version-only dependencies and shortcuts explicitly: they may remain because the old ownership log is not merged |

The AppId is intentionally unchanged. `CloseApplications=yes` uses Windows Restart Manager,
not arbitrary process termination. Its effectiveness with WPF shutdown/cancellation is not
established by compiling an installer: the running-upgrade case above is required.
[Inno CloseApplications documentation](https://jrsoftware.org/ishelp/topic_setup_closeapplications.htm).

The app now intercepts WPF `SessionEnding` and defers the end-session request while using
its existing asynchronous exit path. This prevents session shutdown bypassing cleanup;
the installer/Windows may need to retry closing once the app has exited. The automated test
invokes the callback in its own WPF harness, not a real host shutdown. Verify native Restart
Manager behavior, logoff and normal shutdown in the VM.
[WPF SessionEnding documentation](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.sessionending?view=windowsdesktop-10.0),
[Window.Closing semantics](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.closing?view=windowsdesktop-10.0).

`UninstallLogMode=overwrite` deliberately avoids inheriting older recursive deletion entries.
Inno normally appends old ownership records and does not generally recommend overwriting,
because records for obsolete files can be lost. Here user-file preservation takes priority;
obsolete owned files and shortcuts must be checked in the upgrade VM, without broad cleanup.
[Inno uninstall-log documentation](https://jrsoftware.org/ishelp/topic_setup_uninstalllogmode.htm),
[appending existing uninstall logs](https://jrsoftware.org/ishelp/topic_appendnotes.htm).

## Windows Update integration — VM only

Record service states immediately before and after every case. Use real `wuauserv` and BITS
only inside the disposable VM. Run CortexDNA elevated for the selected Windows Update Cache
category. Use VM-created fixture downloads; do not test during an actual OS update installation.

| Initial state / event | Action | Expected result |
|---|---|---|
| wuauserv running, BITS running | Confirm Update Cache cleanup | Both reach stopped before deletion; both return to running after normal completion |
| wuauserv running, BITS stopped | Confirm cleanup | wuauserv stops then returns to running; BITS remains stopped |
| wuauserv stopped, BITS running | Confirm cleanup | BITS stops then returns to running; wuauserv remains stopped |
| Both stopped | Confirm cleanup | Neither is started by CortexDNA afterwards |
| Stop denied or times out | Deny stop on one fixture service configuration / apply appropriate VM ACL | No cache deletion after failure; any previously stopped initially-running service is restored; error visible/logged |
| External service restart | Restart BITS or wuauserv from a separate VM administrator session while a sufficiently large fixture cleanup runs | Next per-file state check stops cleanup; initially-running services are restored; partial result/error is reported |
| Cancellation | Start sufficiently large cleanup then use the app's explicit Exit command | Cancellation stops further file deletion; normal exit waits for service restoration; initial running/stopped states preserved |
| Restoration failure | Prevent start of one service after stopping it in the fixture scenario | Failure reported; restoration of the other service is still attempted; recover through VM snapshot afterwards |

Service state can change between a check and deletion; this is not a Windows service lock.
Power loss/forced termination cannot guarantee restoration and is not a passing cancellation test.

## Real Startup / Delay — VM only

1. Add one uniquely named HKCU Run fixture `CortexDNA_VM_Probe`, pointing to the harmless
   probe's absolute executable path and fixed arguments. Record the original registry data.
2. Load Startup Impact: enable/disable the fixture in CortexDNA and compare Task Manager and
   the specific StartupApproved value. Log off/reboot to verify actual launch/no-launch.
3. Apply Delay. Export the task XML from exactly `\CortexDNA\StartupDelay`; check description,
   executable, arguments, working directory, principal SID, interactive token, least privilege,
   logon trigger for the same user and 30-second delay. Original immediate entry is disabled.
4. Reboot Windows. Verify one probe invocation per logon at the expected delay, and that the
   new process of CortexDNA still recognizes the same stable SHA-256 task as delayed.
5. Remove Delay. Confirm only the verified task was removed and the original approval state
   now enables immediate startup. Reboot and verify one immediate probe launch.
6. Repeat with a Startup-folder shortcut using a working directory containing spaces and
   quoted arguments; verify they are preserved. Test invalid/unquoted ambiguous commands:
   no new task and no approval change should result.
7. Test cancelling/denying task registration, then task removal. Verify rollback retains a
   working startup path and reports failures instead of falsely indicating success.

## Legacy migration — VM only

1. Snapshot the VM. Create only a fixture task inside `\CortexDNA\StartupDelay` using the
   previous format `Delay_<first-24-alphanumeric-item-id>_<8-hex-GetHashCode>`. Use exactly
   `Cortex DNA delayed start for <fixture StartupItem.Name>` as description, the probe's
   matching executable/arguments/working directory, same user identity (including a
   DOMAIN/user-name alias case), least privilege, one logon trigger, one Exec action and 30s.
2. Keep the immediate fixture startup entry disabled; let any pending/running legacy trigger
   finish before migration. Refresh CortexDNA Startup Impact.
3. Verify the new SHA-256 name is registered from the complete definition. The new task is
   initially disabled, verified, activated and verified before the verified old task is deleted.
   Only the URI/name and temporary enabled state should change; registry approval is unchanged.
4. Reboot: delayed state remains recognized and the probe runs once. Remove Delay and reboot
   again to verify immediate startup.
5. Repeat separately with wrong description/executable/arguments/SID/working directory,
   elevated principal, extra action, different delay, disabled or malformed task, same task
   name in another folder, running task, duplicate legacy tasks, ambiguous StartupItems and
   an existing target-name collision. Such tasks remain; no foreign task is overwritten/deleted.
   Review information appears in existing DiagnosticsNote and the local log.
6. Deny creation/update/deletion via VM task ACLs or simulate task changes between reads.
   Verify the original remains after failed migration. Only a verified newly created task may
   be rolled back. Any unverifiable task is left for explicit review, without automatic deletion.

Two registrations may briefly coexist during activation/deletion. Running/queued tasks are
deferred, but Task Scheduler does not provide a cross-registration atomic rename or compare-
and-delete operation. Include a logon-timing stress case; do not describe mock tests as proof
that this native boundary is atomic. A failed read/rollback leaves a review note and may leave
a disabled new task; inspect it explicitly rather than deleting arbitrary tasks.

## CI and release evidence

- Run `scripts/Verify-Local.ps1`: Debug/Release with warnaserror, `dotnet test`, publish and
  installer compile must all succeed. Cached packages are sufficient for offline verification.
- Run `scripts/Verify-CIFailureModes.ps1`: all three expected-failure probes must pass; their
  deliberately failing fixture test is separate from the production regression suite.
- The workflow must retain no `continue-on-error`, artifact-upload action, Release step or
  repository write permission. TRX remains a local runner output; no upload/publishing is added.
- CI was reviewed/verified locally, not dispatched to GitHub. Release readiness remains
  conditional on completing the VM matrix above.
