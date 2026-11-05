# Validation record — 2026-11-05

This is a tested implementation candidate, **not production acceptance**.

Environment: Windows NT build 26300 x64, .NET SDK 10.0.300 / runtime 10.0.8,
WPF available, Node 24.12.0. Repository was empty. Office inventory reports
Office 2024 Retail x64 16.0.17928.20148; that does not establish integration compatibility.

## Executed

Whole local drive selection (0.2.17, 2026-11-05): explicitly selected local drive
roots now validate, save and reopen in Automatic guidance. A previously empty
drive checkbox label now displays `C:\`; the existing recursive-scope checkbox
still controls subfolders. UNC shares (including slash spelling) and mapped
network watch scopes remain rejected. No drive is selected automatically.
The shared SystemDirectoryPolicy protects standard drive-root OS/application,
recovery and temporary trees and actual Windows/program/common-data locations.
Startup enumeration prunes them; live events and queued automatic prompts use
the same policy. Ordinary nested folders named Windows/Program Files remain
eligible. Existing cache/project/company/user/reparse exclusions remain active.

The initial red run had 2 expected new core failures and 1 WPF validation
failure. Updated the obsolete test expecting drive rejection to assert continued
network rejection. A fresh reviewer identified a queued-directory deletion race
that could prevent an entire drive watcher from starting. A disposable fixture
reproduced DirectoryNotFoundException; descendant-only enumeration recovery now
retains neighboring baseline files while selected-root failures still propagate.
The reviewer checked the fix, iterator disposal and cancellation with no further
findings. Custom TEMP locations outside AppData and the anchored Temp/tmp trees
are not automatically protected just because they are the TEMP environment path.

Release build: zero warnings/errors. All 176 checks passed: 87 core/filesystem,
57 WPF/native, 11 JavaScript, 9 self-contained runtime, 10 MSI and 2 editor-package
checks. Setup drive acceptance/persistence was tested without starting a real
whole-drive watcher. Protected paths, case/slash variants, component boundaries,
relocated-tree baseline pruning, custom exclusions and nonrecursive scope were
checked. The setup render with the labeled drive and new hint was visually
inspected. Background prompt latency was 1.327 seconds in the packaged test.
All 481 MSI embedded files were extracted and hash-compared with the payload.

Staff MSI: `artifacts/staff-installer-0.2.17/PrimEx.SmartSave-0.2.17-win-x64.msi`,
SHA-256 `3d31de4304972cd7ed8053bed9e9a55d7696eaeede1031be3978a16537a99c0f`.
The distribution copy's hash was rechecked. The package remains unsigned; no
installed application, employee configuration or live C-drive monitoring scope
was changed. Whole-drive startup time, sustained event volume/overflow handling
and actual MSI installation/upgrade on employee machines remain manual gates.

Visible placement prompts (0.2.16, 2026-11-05): background-triggered destination
dialogs now use Topmost for their lifetime, open in Normal state with ShowActivated,
and request activation after rendering. The main queue retains its hidden or
minimized state. Standalone dialogs retain WPF CenterScreen (cursor monitor);
visible owners retain CenterOwner. This uses the normal topmost z-order rather
than input injection or repeated focus stealing. Other topmost windows and the
Windows secure desktop are outside this guarantee.

Before the fix, the strengthened real-WPF suite failed 18 visibility scenarios
(39 passed); after the fix all 57 passed. These include native WS_EX_TOPMOST and
IsIconic checks for monitoring, Files PX Move/Copy and explicit browser dialogs
with hidden/minimized main windows, plus topmost checks with a visible owner.
The self-contained background-process suite passed all 9 tests: its actual
download prompt was visible, not iconic, and had WS_EX_TOPMOST. Latency was
1.342 seconds in this local run. Explicit local confirmation closed it, kept
the source, and left the main queue hidden. Release build passed with zero
warnings/errors; 83 core and 11 JavaScript checks passed, as did 4 installer
source checks and 2 packaged editor-link checks. Read-only code review found no
important issue. Real native picker z-order, competing topmost applications,
multiple monitors/mixed DPI and full MSI install/upgrade remain manual acceptance.

The 0.2.16 MSI passed all 10 package checks, including extraction and SHA-256
comparison of all 481 embedded files. Together with the suites above, all 172
checks passed. Staff package:
`artifacts/staff-installer-0.2.16/PrimEx.SmartSave-0.2.16-win-x64.msi`, SHA-256
`b5efa46cdc72c108695c9d94e69fdc665720c61973da6301fd6a3648eccd86af`.
The package, checksum and current staff guide were copied into the distribution
folder and the MSI hash rechecked. No installed application or user settings
were changed; existing machines need the new installer.

Required destination choice and actual local-folder labels (0.2.15, 2026-11-04):
placement windows hide the title-bar Close control and block dismissal without
explicit local keeping or a valid transfer destination. Escape can return from
the local advisory to the choices. Company-folder Back/native-picker cancellation
returns to the initial question without recording local consent. Desktop,
Downloads and arbitrary folder labels use the actual selected folder, with its
full path shown and literal underscores preserved. Manual/browser local targets
update after browsing/editing; staged browser downloads start at the configured
Downloads folder. Keeping the original path/name performs no transfer or
self-conflict. Interrupted decisions stay Pending; Windows session shutdown
remains possible. Monitoring coverage, resave suppression and timing are unchanged.

Release build passed with zero warnings/errors. All 172 automated checks passed:
83 core/filesystem, 57 WPF/native, 11 browser/editor JavaScript, 9 self-contained
runtime, 10 MSI and 2 editor-package checks. New WPF scenarios cover Close,
Escape and SC_CLOSE/Alt+F4, Desktop/Downloads/custom names and underscores,
dynamic manual local targets, local confirmation without transfer, pending-state
preservation, native-picker cancellation/Back returning to choices and actual
staged-browser transfer to its configured local target. Hidden/minimized main
window and Move/Copy tests remain green. The Desktop advisory was rendered and
visually inspected. Read-only review found no critical/important defect; its
minor stale manual-test instruction was corrected.

The packaged-agent test checks the native WS_SYSMENU bit is absent, sends
WM_CLOSE and verifies the prompt/journal remain pending with no remembered local
choice, then uses Windows UI Automation scoped to that isolated prompt to
confirm Desktop keeping. Only after confirmation does the prompt close; the
source remains and the main queue stays hidden. Popup latency was 1.407 seconds
in this single local test, preserving the prior timing improvement. All 481 MSI
embedded files were extracted and SHA-256 compared with the payload. Actual
install/upgrade/uninstall, session shutdown, DPI/long-path/accessibility and
network pilot acceptance remain manual.

MSI: `artifacts/staff-installer-0.2.15/PrimEx.SmartSave-0.2.15-win-x64.msi`
(58,192,488 bytes), SHA-256
`559f2095b363d6cb3a762498058fc75387f83456409105e383a724f879b35436`.
The package remains unsigned. After hash verification, removed the previous
0.2.14 distribution and temporary build/extraction copies (976 generated files
checked). Targets were resolved inside artifacts and checked for links/private
state before removal. Only the current MSI remains, with its checksum, staff
guide and verification evidence retained. No installed
application, employee configuration or Windows setting was changed.

Faster placement prompts (0.2.14, 2026-11-04): traced the delay to the default
3-second quiet period before the existing 250 ms pending-file/exclusive-read
check. Lowered the standard quiet period to 1 second. The watcher engine,
partial-download filters, continued-write debounce, locks, resave suppression,
confirmed Move/Copy and hidden/minimized main-window behavior remain intact.
Old JSON without PromptTimingVersion upgrades a shipped 3-second value in
memory, without rewriting the file. Other delays remain; a later explicit
3-second value with PromptTimingVersion=1 survives normal save/reload.

The packaged-agent native-window test measured 3.517 seconds before the change
and 1.408 seconds after it, using the same legacy configuration and completed
synthetic local save (one sample per build). This measures file-close-to-popup,
not download duration or application lock time. See docs/performance.md and
`artifacts/verification/prompt-timing-0.2.14.json` for scope and raw evidence.

Release build passed with zero warnings/errors. All 161 automated checks passed:
83 core/filesystem, 46 WPF/native, 11 browser/editor JavaScript, 9 self-contained
runtime, 10 MSI and 2 editor-package checks. Four new core cases cover faster
default latency, continued writes, legacy migration without config writes and
custom/case-insensitive timing preservation. The packaged hidden-window test
now exercises a real legacy delay upgrade and asserts popup latency below
2.5 seconds on this PC. Read-only code review found no critical/important defect;
its minor pending-documentation link was resolved. All 481 embedded files were
extracted and SHA-256 compared with the published payload. Actual MSI install/
upgrade/uninstall and pilot hardware/network/DPI acceptance remain manual.

MSI: `artifacts/staff-installer-0.2.14/PrimEx.SmartSave-0.2.14-win-x64.msi`
(58,212,968 bytes), SHA-256
`191a73a6996c8b7b0901dcebc7f365a730bdf5aa6923e66c6bd7942de27c312d`.
The package remains unsigned. After verifying the distributed hash, removed the
previous 0.2.13 distribution and temporary build/extraction copies (976 generated
files checked). Targets were resolved beneath artifacts and checked for links
and private application state before removal. Only the current application MSI
remains, alongside its checksum, staff guide and verification evidence.
No installed application, employee configuration or Windows setting was changed.

Files PX Move / Copy (0.2.13, 2026-11-04): both monitored-save and
manual/browser destination screens offer Move or Copy on the existing screen.
Copy retains the original folder/file and publishes a verified server copy;
Move removes the source only after verification. The selected mode is persisted
before transfer, displayed in history and restored for a reviewed retry after
failure/restart. Legacy journal JSON without Mode retains Move semantics.
Intentional Copy completion has a distinct source-retained success message.

Release build passed with zero warnings/errors. All 157 automated release checks
passed: 79 core/filesystem, 46 WPF/native, 11 browser/editor JavaScript, 9
self-contained runtime, 10 MSI and 2 editor-package checks. New coverage includes
Copy source retention and persisted completion, duplicate processing after reload,
unavailable-destination/restart/retry, legacy journals, both selection screens,
and monitoring/browser Copy dialogs with hidden and minimized main windows.
The Copy screen was rendered and visually inspected, including a long folder
path. A read-only code review found no critical, important or minor defects.
All 481 embedded files were extracted and SHA-256 compared with the payload.
Actual MSI install/upgrade/uninstall and SMB/DPI/accessibility pilot gates remain
manual; no employee installation or configuration was changed.

MSI: `artifacts/staff-installer-0.2.13/PrimEx.SmartSave-0.2.13-win-x64.msi`
(58,204,776 bytes), SHA-256
`5a2cb895f4e5415c3d724df59d6614cae1b625ca8865e81524ce1c6812c8d878`.
The package remains unsigned. After verifying the distributed MSI hash, removed
0.2.12 and temporary build/extraction copies (976 generated files checked),
retaining the current MSI, checksum, staff guide and verification reports.
Only the 0.2.13 application MSI remains. Cleanup targets were resolved beneath
artifacts and checked for links/private application state before removal.

Final version cleanup (2026-11-04): confirmed the only remaining MSI is 0.2.12
and its checksum matches the release record. Removed the two obsolete isolated
VS Code host-smoke profiles left from 0.2.11 (52 files, 174,233 bytes), after
verifying their workspace directories were empty, their settings pointed into
the isolated test roots and no paths/ancestors were links. The updater-block log
was retained as `artifacts/verification/editor-host-updater-block.log`. Source,
configuration, build tools, measurement reports and current distribution remain.
No application tests were rerun for this artifact-only cleanup.

Standalone placement prompts (0.2.12, 2026-11-04): monitoring and browser-download
callbacks no longer open the main queue. Placement, company-folder and browser
destination dialogs only use the main window as owner if it is already visible
and not minimized. Otherwise they center independently with a taskbar/Alt+Tab
entry. Dismissal, local Keep and company selection leave the queue hidden or
minimized. Explicit tray/main-window actions remain available.

Release build passed with zero warnings/errors. All 59 relevant release checks
passed: 38 WPF/native, 9 self-contained runtime, 10 MSI and 2 editor package checks.
Six new WPF scenarios exercise Keep, company selection and browser destination
dialogs against hidden/minimized owners; existing visible-owner flows also pass.
The standalone prompt was rendered and visually inspected. A new packaged-agent
test uses native window enumeration to verify that a background save shows the
placement prompt only, never the main queue; dismissing it returns to no visible
app windows, retains the source and persists cancellation. All 481 embedded files
were extracted and SHA-256 compared with the published payload.

Core/filesystem and browser/editor JavaScript behavior is unchanged and was not
rerun; its previous evidence remains below. Editor VSIX manifests were checked
again after versioning. The actual-editor activation limits from 0.2.11 remain.
MSI install/upgrade and pilot DPI/accessibility/network acceptance remain manual.

MSI: `artifacts/staff-installer-0.2.12/PrimEx.SmartSave-0.2.12-win-x64.msi`
(58,188,392 bytes), SHA-256
`b60a0d121d7813b57f386aba7fd4f7568674ee5e6a40d51d67a22f04a0147db5`.
The package remains unsigned. Following the user's current-version-only cleanup
preference, obsolete 0.2.11 and generated build/extraction copies were removed
after verification; current MSI, checksum and staff guide remain with their hash
verified. No installed application/configuration was changed.

One-time editor workspace links (0.2.11, 2026-11-04): added VS Code and Visual
Studio VSIX packages, a Connect editors screen in Automatic guidance and a local
current-user workspace lease registry. New open folders/solutions can now be
suppressed without Git/project markers or per-project configuration. Both editor
publishers interoperate with the .NET reader. Leases update on workspace changes,
heartbeat every 30 seconds and expire within 90 seconds; closing unregisters them.
The agent rechecks queued requests and reports only a workspace count in diagnostics.
Visual Studio's .vs directory is now an independent project marker too.

Desktop solution and Visual Studio SDK package builds passed with zero warnings/
errors. All 141 automated checks passed: 78 core/filesystem, 32 WPF/native,
11 browser/editor Node, 2 editor VSIX checks, 10 MSI and 8 self-contained runtime.
The packaged-agent regression verified that four new files in an empty linked
workspace do not enter the operation journal, then that removing the lease restores
guidance for a new ordinary PDF. The editor connection screen was rendered and
visually inspected. All 481 MSI payload files (including both links) were extracted
and SHA-256 checked against the published payload. Runtime tests used that payload.

The VS Code VSIX successfully installed into an isolated profile/extensions folder.
The real extension-host smoke could not run because VS Code's updater held the
vscode-updating mutex; the launcher logged that condition and exited. No updater
or lock was bypassed. No supported Visual Studio host is installed here. Therefore
actual extension loading, editor project switching and Visual Studio multi-root
Open Workspace remain pilot acceptance checks; publisher/API mocks do not certify
them. No installed Smart Save configuration or normal editor profile was changed.
See docs/editor-integrations.md for scope, installation and primary API references.

MSI: `artifacts/staff-installer-0.2.11/PrimEx.SmartSave-0.2.11-win-x64.msi`
(58,204,776 bytes), SHA-256
`467acc5054a410fbfc29a9f558a9e75f0dc98e1892fb58b331ad9b2082ea9ba4`.
The MSI/VSIX packages remain unsigned. Following the user's latest-version-only
cleanup preference, obsolete 0.2.10 and generated build/extraction/editor package
copies were removed after validation. Current distribution contains MSI, checksum
and staff guide; editor SDK/build tools, source, validation reports and the blocked
host smoke profiles/logs remain.

Coding-project exclusions (0.2.10, 2026-11-04): recognized Git/worktree, VS Code
and common coding-project roots now suppress monitoring prompts for their entire
subtree, including documents/assets. Explicit excluded folders and the default-on
project-detection switch are editable in Automatic guidance and persisted. Old
settings gain protection without rewriting the file or changing monitoring
enablement. Standard profile/document-container roots are never auto-classified.
Marker events invalidate cached detection; ignored dependency manifests do not
invalidate project caches. Old queued requests are rechecked before opening a
dialog. Explicit manual moves and explicit browser downloads remain available.

Release build: zero warnings/errors. All 130 checks passed: 74 core, 31 desktop/
native integration, 8 browser, 10 installer and 7 self-contained runtime. Coverage
includes multiple marker types, project ancestors above the selected folder,
worktrees, assets, sibling-folder boundaries, live Git/manifest initialization,
old-config defaults, explicit settings persistence and stale queued requests.
The packaged-app regression generated eight project/excluded-folder assets and
an ordinary neighboring PDF; only the PDF entered the operation journal. The
updated WPF setup screen was rendered and visually inspected; controls are
readable, the long form scrolls, and Apply/Cancel remain pinned. All 479 embedded
MSI files were extracted and hash-verified against the published payload.

The same startup corpus measured 81 ms median index preparation with project
detection enabled (three warmed runs), preserving the prior startup improvement.
Actual installed Codex/VS Code workflows and cold launch remain manual pilot
checks. No installed app or live user configuration was changed by this work.

MSI: `artifacts/staff-installer-0.2.10/PrimEx.SmartSave-0.2.10-win-x64.msi`
(58,188,328 bytes), SHA-256
`a6140131ce20c884b7c3dbd4fb08a0d3facba170b3eda06ddb21f1b6740dbd2b`.
The MSI remains unsigned. Following the earlier latest-version-only cleanup,
0.2.9 and newly generated build/extraction/benchmark copies were removed after
verification. Only the 0.2.10 MSI, checksum and copied staff guide remain in the
current release folder; measurements, screenshots, source, tools and configuration
were retained. The distribution MSI hash was checked unchanged after cleanup.

Version cleanup (2026-11-04, after 0.2.9 validation): at the user's request,
removed 14,642 obsolete/generated files totaling 5,984,736,174 bytes (5.57 GiB).
Targets included 0.2.7/0.2.8 installers, the old-version archive, extracted MSI
images, benchmark binaries and the current release's duplicate publish payload
and build intermediates. Every target and ancestor was resolved inside the
workspace artifacts directory and checked for links and private app state before
deletion. Code, repositories, build tools/feeds, configuration, preview state,
screenshots and measurement reports were retained. The only remaining MSI is
0.2.9, alongside its checksum and copied staff guide. Its SHA-256 was verified
unchanged before and after cleanup. No application tests were rerun for artifact
removal. Historical release/publish/extraction paths below may no longer exist;
their validation records and hashes remain as evidence.

Startup optimization (0.2.9, 2026-11-04): window creation/showing now precedes
background journal loading and monitor initialization. Existing-file indexing
prunes excluded directory trees and uses enumeration metadata; overlapping
recursive scopes share one watcher. Ignored events do not enter the pending queue,
and idle monitoring has no periodic timer. Export diagnostics includes actual
version and process/startup metrics. See `docs/performance.md` for methodology.

Against the same 2,000-document/12,000-excluded-file local corpus, three warmed
runs had median preparation times of 4,455 ms (0.2.8) and 83 ms (0.2.9).
Managed allocations fell from 117.4 MiB to 22.0 MiB. These measurements cover
monitor initialization on the development PC, not total launch or another laptop.

Release build passed with zero warnings/errors. All 123 checks passed: 70 core,
29 desktop/native integration, 8 browser, 10 installer and 6 self-contained runtime.
New regressions cover startup scope/exclusions/cancellation, overlapping scopes
and forward-slash paths, discarded temporary events, asynchronous initialization
with concurrent disposal, and deferred history/interrupted-transfer recovery.
All 479 embedded MSI files were extracted and SHA-256 compared with the published
payload; runtime checks used that matching payload. Actual MSI install/upgrade,
cold-start timing on the colleague's laptop and real large-history UI acceptance
were not performed. No existing installed app or user settings were changed.

MSI: `artifacts/staff-installer-0.2.9/PrimEx.SmartSave-0.2.9-win-x64.msi`
(58,176,040 bytes), SHA-256
`192e1efb810f2f811b96a1276ba878ba23a6d737c4ff1adb009f5f1bf63653bf`.
The MSI remains unsigned. Previous packages remain intact.

Repeated-save correction (0.2.8, 2026-11-04): the monitor indexes existing eligible
paths at startup and marks each newly offered path as existing. Subsequent saves
stay at the current location without placement prompts, including atomic file
replacement with a changed creation time, renaming a known document, and monitor
restart without any explicit Keep choice. New Save As copies and completed
browser download renames still receive their first placement prompt.

Release build: zero warnings/errors. All 119 checks passed: 67 core/filesystem,
28 WPF/native integration, 8 browser, 10 installer and 6 self-contained runtime.
Two new filesystem regressions exercise existing files before startup, repeated
writes, atomic replacement, rename, restart and Save As at a new path. All 479
embedded MSI files were extracted and SHA-256 verified against the published
payload. Runtime checks ran against that matching published payload. No existing
installation, user settings or staff PC was changed. Actual MSI upgrade and real
Word/Excel/PDF-editor acceptance remain manual checks.

MSI: `artifacts/staff-installer-0.2.8/PrimEx.SmartSave-0.2.8-win-x64.msi`
(58,159,656 bytes), SHA-256
`d23a5f79fc8dfdfc8ad09fda1b73a6300c283ad1bb29117ecf8ca98e2efa3f77`.
Earlier release packages remain intact. The current MSI is unsigned.

Artifact housekeeping (2026-11-03): 46 obsolete release/publish/test-payload targets
were moved into `artifacts/_ARKIV-VERSIONE-TE-VJETRA`. Recursive deletion was denied
by the environment's automatic approval policy, so nothing was permanently deleted.
Every source/destination was resolved and checked inside the workspace artifacts
directory before moving. Preview user state, normal app state, source repositories,
build tools and package feeds were retained. The current 0.2.6 folder now contains
only the MSI, checksum and copied staff instructions. The MSI's SHA-256 remains
`ed70c4731293a3577ca01c62e69ad7a1391b42b9647cf7a106288792065f8730`.
Historical extracted/published verification paths below now live under that archive.
This was artifact organization only; application tests were not rerun.

| Check | Result |
|---|---|
| Eight-project Release solution build | Pass, zero warnings/errors |
| Core/filesystem/security/configuration/monitoring executable harness | 78 passed, zero failed |
| STA WPF + actual native-host process/pipe executable harness | 38 passed, zero failed (0.2.12) |
| Node browser policy, MV3 worker and editor extension tests | 11 passed, zero failed |
| VS Code / Visual Studio VSIX structure checks | 2 passed, zero failed |
| PowerShell parser: Build, Register-NativeHost, Verify | Pass |
| WiX source XML well-formedness | Pass; this is not WiX schema/ICE validation |
| Framework-dependent desktop and native-host publish | Pass |
| Self-contained staff MSI 0.2.12, x64, WiX 6.0.2 build | Pass, 481 embedded payload files |
| Installer/source/MSI table and embedded-file hash checks | 10 passed, zero failed |
| Self-contained published staff app runtime/sign-in/first-run checks | 9 passed, zero failed; extracted MSI payload hashes match |
| Full Windows Installer ICE validation | Blocked: Windows Installer Service could not be accessed in restricted account |
| Actual MSI install/upgrade/uninstall and real sign-in | Not executed; requires Windows pilot/VM |
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
automated count is now 109. An additional test launches the actual desktop agent
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

Staff deployment follow-up (0.2.0): built the per-user staff MSI using WiX 6.0.2
downloaded into workspace-local build tools. The package includes .NET 10.0.8
and creates current-user Desktop/Start-menu shortcuts and an HKCU sign-in Run
entry. Browser/native messaging is optional, and is absent from the default staff
MSI. All 479 embedded files were extracted using WiX decompile and SHA-256 checked
against the published payload. The self-contained runtime suite ran against that
extracted image with an invalid global DOTNET_ROOT, confirming first-run setup,
later background sign-in, startup opt-out and manual opening. Setup remains
opt-in; the new setup-completed flag and startup preference are persisted.

MSI: `artifacts/staff-installer/PrimEx.SmartSave-0.2.0-win-x64.msi` (58,167,848 bytes).
SHA-256: `3a06950a1ca33212a4f97a7c40e21877c027bd0921be79002b35dddfb0dc960e`.
Authenticode status: NotSigned. No enterprise certificate was provided. Windows
Installer service access failed during ICE validation in the restricted account;
this is an environment failure, not a successful validation. Actual MSI install,
repair, upgrade, uninstall and Windows sign-out/sign-in acceptance must be tested
in a disposable ordinary Windows account/VM. No staff PC or system policy was
modified. The app starts at sign-in but has no crash watchdog. Close/Exit behavior
remains under the signed-in user's control. See installer/README.md and the
Albanian staff guide for installation and pilot instructions.

All-file-types follow-up (0.2.1): the default extension setting is now `["*"]`,
with an All completed file types checkbox. PNG/JPG images, unfamiliar extensions
and extensionless files are covered in the explicitly selected local folders.
Partial downloads, temporary/lock files, caches/build subfolders and recovery
backups are still excluded. Cache matching is scoped to the selected root rather
than all of its ancestors, so legitimate redirected folders under AppData are
not accidentally discarded. Existing reparse/system/hidden checks remain.

The exact two previously shipped default lists upgrade to all-types in memory;
custom lists are preserved. No configuration file is rewritten at startup, and
monitoring/folder/server/startup settings do not change. Tests cover legacy-default
loading, byte-for-byte configuration retention, custom restrictions, images and
unknown types, cache/partial exclusion and actual desktop PNG queuing. The staff
runtime suite additionally launches the self-contained app with an old default
configuration and confirms PNG/JPG downloads queue automatically, while the
partial file is untouched and settings stay byte-for-byte unchanged. No staff
PC has been upgraded remotely. Install 0.2.1 as the same user after exiting 0.2.0;
normal install/upgrade/uninstall and real sign-in still need pilot acceptance.

0.2.1 MSI: `artifacts/staff-installer-0.2.1/PrimEx.SmartSave-0.2.1-win-x64.msi`
(58,171,944 bytes), SHA-256
`118740ea908dd5eb238ab81ed8c0236ea6fb1df2592bc3149d3d5e323e19df79`.
All 479 files were extracted and verified against the release payload. The
six-test runtime suite passed on the reconstructed extracted MSI image. The
package remains unsigned and retains the previous Windows Installer service
validation limitations.

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

## Native company folder picker release

Native folder browser follow-up (0.2.2): replaced the custom company tree with
Microsoft.Win32.OpenFolderDialog. Both the monitoring move flow and Browse server
folders use Windows Shell navigation. Only approved roots can be committed after
mapped-drive resolution. The post-save flow opens the shell picker first, then
shows a compact filename/folder confirmation with Cancel and Move here. Source
files remain untouched until confirmation; permission probes and explicit conflict
decisions still run at confirmation. No writes are made just by opening the picker.

The 24 desktop checks include picker settings/root approval and three real WPF
modal paths with an injected picker result (move, cancel and keep). The compact
confirmation was rendered and visually inspected. Interactive preview input did
not capture the native modal reliably; native Shell navigation, mapped SMB drives
and keyboard/DPI behavior remain manual pilot checks. Automated modal checks do
not certify a real Shell interaction or company share.

0.2.2 MSI: `artifacts/staff-installer-0.2.2/PrimEx.SmartSave-0.2.2-win-x64.msi`
(58,163,752 bytes), SHA-256
`ce3c02b67e29bdc29d3ced66afa4b62da411e6743fd153c03a00dc220a462ed4`.
All 479 embedded files were extracted and hash-verified; all six runtime checks
passed against the reconstructed extracted image. Core 61, desktop 24, browser 8,
installer 10 and staff runtime 6 checks passed (109 total). Signing and real MSI
installation/upgrade/uninstall/sign-in remain subject to the limits above.

## Next release steps

Compact placement follow-up (0.2.7): Files PX and Downloads now occupy one row in
a 480-DIP prompt with tighter spacing. The warning is initially collapsed. Clicking
Downloads reveals the advisory and Vazhdo me Downloads / Kthehu in the same window.
Only confirmation sets KeepChosen and persists the local choice. Kthehu/Escape
collapse the warning; Files PX remains available and X cancels without consent.
In the manual/browser screen the warning follows the Downloads radio selection.
Both initial/expanded prompts were rendered and visually inspected. Release build
passed without warnings/errors; 28 desktop checks passed, including two new modal
paths for cancelling the warning and switching back to the company folder picker.
Existing modal coverage also verifies that the local choice remains unrecorded
before confirmation, survives a later save/reopened selector after confirmation,
and that the manual screen's warning follows its selected destination.

0.2.7 MSI: `artifacts/staff-installer-0.2.7/PrimEx.SmartSave-0.2.7-win-x64.msi`
(58,159,656 bytes), SHA-256
`84ee3eb79409f6f498bd4a7b75c51eb1935f0a6b667ed41ec3850f9026624cad`.
All 479 embedded files were extracted and hash-verified; 10 installer and six
reconstructed-image runtime checks passed, alongside the 28 desktop checks
(44 relevant checks). Core/browser suites were unchanged and not rerun. The
previous 0.2.6 distribution and generated 0.2.7 test/build payloads are archived,
leaving the active installer folder with only MSI, checksum and staff instructions.
The package remains unsigned; actual MSI upgrade/sign-in and pilot hardware
accessibility/Office/SMB acceptance remain manual gates.

Warning wording follow-up (0.2.6): the Downloads advisory now reads "Kujdes:
Vetëm për dokumente provë dhe kopje jo të rëndësishme. Dokumentet e rëndësishme të
punës ruajini në Files PX." Both placement screens and current staff guidance use
the updated wording. This is a text-only change; transfer and selection behavior
are unchanged.

For 0.2.6 the Release build completed with zero warnings/errors, both rendered
placement screens were visually inspected, and 26 desktop plus 10 installer
checks passed (36 repeated checks). All 479 embedded files were extracted and
hash-verified. Core/browser/runtime suites from the prior release were not rerun
for this wording-only update. MSI:
`artifacts/staff-installer-0.2.6/PrimEx.SmartSave-0.2.6-win-x64.msi`
(58,159,656 bytes), SHA-256
`ed70c4731293a3577ca01c62e69ad7a1391b42b9647cf7a106288792065f8730`.
Signing and actual MSI upgrade/sign-in remain manual acceptance gates.

Downloads advisory follow-up (0.2.5): added the Albanian warning inside the
Downloads button and local destination choice, with wrapping amber text/background
and accessible help text. Important work documents are directed toward Files PX;
local saving remains available and no transfer/permission behavior changes. Both
WPF placement screens were rendered and visually inspected with the warning fully
visible. During verification the live-agent test encountered a log-reader/writer
sharing race. Its polling reader now permits concurrent append/rotation and keeps
the original bounded event assertions. This only changes test reading behavior.

The rerun passed all 26 desktop checks. Installer checks passed 10/10, including
all 479 embedded file hashes; the reconstructed-image runtime checks passed 6/6
(42 relevant checks). Core/browser behavior was unchanged and was not rerun.
0.2.5 MSI: `artifacts/staff-installer-0.2.5/PrimEx.SmartSave-0.2.5-win-x64.msi`
(58,180,136 bytes), SHA-256
`7953a41243887d2ab0a8a13cfcd22ce0ddad6871b8b6f8ee2329c8ffb852af8f`.
The package remains unsigned; real MSI upgrade/sign-in acceptance remains manual.

Button naming follow-up (0.2.4): renamed the placement choices to Files PX and
Downloads across the post-save prompt, destination choices and relevant browsing
titles. Files PX retains the company-server action; Downloads retains the current
local folder. Supporting text and tooltips explicitly explain that a Desktop or
Documents file stays there. The company choice remains recommended. Save/transfer
and remembered-location behavior are unchanged. The post-save dialog was rendered
to `artifacts/verification/post-save.png` and visually inspected; all 26 desktop
checks passed. Core/browser behavior was unchanged and was not rerun for this
wording-only release.

0.2.4 MSI: `artifacts/staff-installer-0.2.4/PrimEx.SmartSave-0.2.4-win-x64.msi`
(58,155,560 bytes), SHA-256
`e64d5ca59c89791987be28d42c170bf7fad3744be812c428b0094fb81fb5f4cb`.
All 479 embedded files were extracted and hash-verified. The 10 installer checks
and six reconstructed-image runtime checks passed, alongside 26 desktop checks
(42 repeated checks). The package remains unsigned and actual MSI upgrade/sign-in
acceptance remains a manual gate.

Repeated-save follow-up (0.2.3): explicit Keep Current Location choices persist
using a hashed normalized path plus creation timestamp. Content edits no longer
prompt again, even across monitor/agent restarts. Different paths and changed
creation times remain eligible. This is a filesystem heuristic, not guaranteed
cross-application document identity. X, Escape and company-folder cancellation do
not persist a Keep choice. Old queue cancellations are not migrated. There is no
automatic server synchronization or interception of the original Save As dialog.

Four additional core checks cover repeated writes/store reopening, different
paths/recreated files, damaged/unavailable state and a real restarted watcher
that ignores remembered edits while still reporting a new file. WPF tests now
exercise explicit Keep followed by another save through a newly constructed
selector/store; X and Escape paths verify no remembered consent. The full suite
is 65 core + 26 desktop + 8 browser + 10 installer + 6 staff runtime = 115 passed.
Real Word/Excel/PDF-editor replacement saves remain a manual pilot matrix.

0.2.3 MSI: `artifacts/staff-installer-0.2.3/PrimEx.SmartSave-0.2.3-win-x64.msi`
(58,176,040 bytes), SHA-256
`9b95f27c5ca6f0d99680b7f12df0c334aaff0b52a7de4570f9adbf4dc06e5fcc`.
All 479 files were extracted and verified against the published payload, and the
six runtime checks passed against that reconstructed extracted image. The MSI
remains unsigned; actual install/upgrade/uninstall and sign-in acceptance gates
remain unchanged.

1. Complete real browser and isolated SMB tests in manual-tests.md; expand coverage
   only through reliable explicit user actions and supported APIs.
2. Provision .NET Framework/VSTO development tooling, implement Word and Excel
   add-ins, then certify the Office matrix before enabling interception.
3. Compile/validate/sign the MSI, sign helper scripts, and test clean install,
   upgrade, uninstall, startup and user-context deployment in a disposable VM.
4. Validate DPI/accessibility, recovery and supported storage metadata on pilot
   hardware; approve a monitored-folder pilot before enabling fallback monitoring.
