# Validation record — 2026-11-03

This is a tested implementation candidate, **not production acceptance**.

Environment: Windows NT build 26300 x64, .NET SDK 10.0.300 / runtime 10.0.8,
WPF available, Node 24.12.0. Repository was empty. Office inventory reports
Office 2024 Retail x64 16.0.17928.20148; that does not establish integration compatibility.

## Executed

| Check | Result |
|---|---|
| Eight-project Release solution build | Pass, zero warnings/errors |
| Core/filesystem/security/configuration/monitoring executable harness | 56 passed, zero failed |
| STA WPF + actual native-host process/pipe executable harness | 21 passed, zero failed |
| Node browser policy and MV3 worker tests | 8 passed, zero failed |
| PowerShell parser: Build, Register-NativeHost, Verify | Pass |
| WiX source XML well-formedness | Pass; this is not WiX schema/ICE validation |
| Framework-dependent desktop and native-host publish | Pass |
| WPF dialog rendered to PNG and visually inspected | Pass; buttons pinned, both choices available |
| Agent starts and writes startup event | Pass |
| Windows UI automation smoke | Main window and native file picker observed; full input workflow interrupted by minimized-window/user-input state |
| Developer registration script `-WhatIf` invocation | Blocked by unsigned-script execution policy; no registration changes made |

Automated checks include boundary-aware UNC approval, real local drive resolution,
injected mapped-drive resolution, malformed Windows paths, actual current-user ACL
write denial, directory creation approval, source locks, copy and move, retention
of an undeletable source, 32 MiB transfer cancellation, keep-both, explicit replace
with recovery backup, source integrity, duplicate transfers, persistent-journal
recovery, local log rotation, Zone.Identifier preservation, bounded/truncated
native framing, source containment, unapproved origins, exact completion size,
null configuration/message rejection, watcher quiet/debounce/lock/disabled behavior,
real native stdio/current-user IPC, durable native queue/status and unavailable-agent
responses, browser concurrency, background-download exclusion and worker recovery.

Browser worker tests mock the Chrome API. Native integration tests execute the
built native host, not a mock. WPF tests instantiate actual windows, real services,
and a dispatcher; the queue test substitutes destination selection to avoid
requiring interactive test input. Real file operations are used throughout.

Generated verification artifact: `artifacts/verification/smart-save.png`.
Framework-dependent published apps: `artifacts/publish/Desktop` and
`artifacts/publish/NativeHost`. Keep all dependency files together and install
the matching .NET 10 Windows Desktop Runtime before running the desktop app.
No application or extension has been installed company-wide.

The updated automatic-guidance build is published separately at
`artifacts/publish-automatic/Desktop` and `artifacts/publish-automatic/NativeHost`
so an older running pilot is not overwritten. Exit the old agent through its
tray before launching the new build, since both use the same default user state.
The new ZIP is `artifacts/PrimEx.SmartSave-automatic-guidance-pilot.zip`.

## Not verified / not implemented

Real Chrome and Edge native registration, extension lifecycle and download flows;
actual SMB transfers or disconnections; multi-gigabyte transfers; physical external
drives; unsupported alternate-stream/filesystem behavior; multiple-monitor and
high-DPI hardware; screen readers; production startup/upgrade/uninstall; signed
MSI installation, dependency provisioning and Intune/GPO rollout are unverified.

Word/Excel interception is unimplemented and configuration-gated. PowerPoint and
Outlook are unimplemented. UI localization beyond English, general graphical settings,
automatic synchronization and automatic recovery-artifact cleanup are not present.
No globally intercepted Save/Save As behavior is claimed.

Follow-up: added the graphical **Automatic guidance…** setup for folder choices,
company roots, file types, subfolders and live opt-in activation/deactivation.
Tests cover real modal setup Apply, configuration preservation and concurrent-edit
rejection, Windows known-folder lookup, scope boundaries, partial-download
exclusion and a real watcher detecting a browser-style completion rename. Rendered
`artifacts/verification/automatic-guidance.png` was visually inspected. The total
automated count is now 85. An additional test launches the actual desktop agent
with isolated opt-in monitoring and confirms that synthetic PDF/Word saves and
a completed Excel-style download automatically enter its durable queue without
clicking Move a file. Partial downloads are excluded and every source remains
untouched while the prompt is pending. Real Teams and Office workflows remain
manual gates.

Startup follow-up: differentiated instance contention from other IO failures,
added a current-user/current-session activation pipe for duplicate launches and
specific diagnostics for permission/settings/storage errors. Real duplicate
desktop-process activation passes. Legacy versions must be exited from the tray
once before updating; no running user agent was forcibly stopped. See
[startup troubleshooting](startup-troubleshooting.md).

Post-save UX follow-up: choosing Move to Company Server now opens only the
company-folder browser, with filename and Move here confirmation. It no longer
repeats the Company Server / Save Outside Server question. Three modal WPF tests
drive the real selector through move, cancelling folder selection and Keep Current
Location, assert that no second destination-type dialog is created, and verify the
source remains untouched before transfer confirmation. The rendered folder step
`artifacts/verification/company-move.png` was visually inspected. Conflict and
permission checks remain in place; manual/browser destination dialogs are unchanged.

Folder-step wording follow-up: removed Keep Current Location from the company
folder selection step. It now offers Cancel and Move here, with the keep option
only in the first post-save prompt. Error messages use the same cancellation
wording. Existing modal regression checks verify the Cancel label and that
cancelling does not move the source.

NuGet network access is blocked. For this run the two existing Microsoft DI 8.0.0
packages were copied from the user's existing package cache into an isolated
workspace feed. Restore used that feed with vulnerability auditing disabled for
offline execution only. Online dependency/advisory review remains a release gate.
No security policy was changed to run scripts, install extensions or access shares.

A developer-only portable debug UI was launched on the interactive desktop.
The helper reported a minimized window / user input during recovery, so automation
did not continue manipulating it. If still present, close it through its normal
file-picker Cancel button and tray Exit. The signed-in-user process could not be
terminated from the restricted build account. This does not affect the separately
published Release package or test results.

## Next release steps

1. Complete real browser and isolated SMB tests in manual-tests.md; expand coverage
   only through reliable explicit user actions and supported APIs.
2. Provision .NET Framework/VSTO development tooling, implement Word and Excel
   add-ins, then certify the Office matrix before enabling interception.
3. Compile/validate/sign the MSI, sign helper scripts, and test clean install,
   upgrade, uninstall, startup and user-context deployment in a disposable VM.
4. Validate DPI/accessibility, recovery and supported storage metadata on pilot
   hardware; approve a monitored-folder pilot before enabling fallback monitoring.
