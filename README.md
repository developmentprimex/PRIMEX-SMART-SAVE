# PrimEx Smart Save

A working Windows .NET 10/WPF pilot candidate for guided document placement.
Files PX (company server) is recommended by default; Downloads (local saving) remains available.
There is no global Save interceptor and no cloud service or database.

**Status:** core, desktop agent, native messaging, explicit browser-link downloads,
and opt-in post-save monitoring are implemented. Office save interception is
not implemented or certified. A self-contained Windows x64 staff MSI is built;
full install/upgrade/uninstall acceptance remains pending. This
candidate does not yet meet all production acceptance criteria.

For staff PCs, use `artifacts/staff-installer-0.2.16/PrimEx.SmartSave-0.2.16-win-x64.msi`
and [the staff installation guide](installer/STAFF-INSTALL-SQ.md). It includes
.NET, creates shortcuts and registers launch at sign-in. The installed build
opens configuration on first use, with monitoring disabled until explicitly
enabled. See [deployment details and validation limits](installer/README.md).

Version 0.2.8 keeps existing documents in their current location on repeated
saves, without another placement prompt, including after restarting the agent
or an atomic replacement save. New documents still receive first-save guidance.
Use the current installer linked above. Obsolete packages and generated test/publish
copies have been removed. Its folder contains only the current MSI, checksum and staff guide.

Version 0.2.9 shows the window before loading history and preparing monitoring.
The startup index skips entire excluded cache trees, reuses enumeration metadata
and shares watchers for overlapping recursive folders. See [performance measurements](docs/performance.md).

Version 0.2.10 skips coding projects automatically, so Codex/VS Code changes stay
inside their project without placement prompts. Automatic guidance also has an
explicit excluded-folder list for projects without recognizable markers. Ordinary
documents in neighboring watched folders still receive guidance. See [project exclusions](docs/automatic-guidance.md#coding-projects-and-excluded-folders-0210).

Version 0.2.11 adds one-time VS Code and Visual Studio links for unmarked new
projects. In Automatic guidance, click Connect VS Code / Visual Studio and install
each link once. Later local workspaces/solutions register automatically. See
[setup, scope and validation limits](docs/editor-integrations.md).

Version 0.2.12 shows placement dialogs independently when the main window is in
the tray or minimized. Automatic saves/downloads do not open or restore the main
queue behind the prompt. Open the queue explicitly from the tray when needed.

Version 0.2.13 adds **Move to Files PX** and **Copy to Files PX — keep original**
on the existing destination screen. Copy preserves the source folder and creates
a verified server copy. The action is recorded in history and retained for retry;
local edits do not automatically update the server copy.

Version 0.2.14 shortens the default post-save quiet period from three seconds
to one second. Temporary downloads, continued writes and locked files still wait.
The old shipped delay upgrades automatically when loading settings; custom delays
are preserved. See [prompt timing](docs/performance.md#prompt-timing--0214).

Version 0.2.15 requires an explicit destination choice: the placement window
has no Close button and cannot be dismissed with Escape/Alt+F4. The local option
uses the actual selected folder, such as Desktop or Downloads, with its full
path shown. Back/cancelling a company-folder selection returns to the choices;
local saving remains available. Monitoring coverage still follows selected folders.

Version 0.2.16 keeps placement dialogs above ordinary application windows until
the destination choice is completed, so background downloads cannot leave only
a taskbar entry behind the browser. Dialogs open in the normal state and request
activation after rendering. Standalone prompts center on the cursor's screen;
visible owners retain centered child dialogs. The main queue stays in the tray
or minimized when an automatic prompt opens.

## Build and run

Requirements: Windows, .NET SDK 10.0.300 or a compatible later feature band,
Node.js for browser tests. Normal builds restore the pinned DI package from
NuGet.org. No administrator privileges are needed to run the agent.

```powershell
dotnet restore PrimEx.SmartSave.sln
dotnet build PrimEx.SmartSave.sln -c Release --no-restore
dotnet run --project src/PrimEx.SmartSave.Desktop -c Release -- --data-dir ".\artifacts\dev-data"
```

The first launch creates a safe configuration with empty company roots and all
automatic integrations disabled. For normal Teams/browser downloads and saved
documents, click **Automatic guidance…**, explicitly select the local folders to
watch, enable guidance, and Apply. The screen can also browse for company roots.
This immediately enables post-save suggestions for completed, unlocked eligible
files in those folders. See [automatic guidance](docs/automatic-guidance.md).

For a manual test, click **Move / Copy a file**, choose a closed test file,
then select an approved company folder under **Files PX**, or choose **Downloads**
for a local/external destination. For Files PX, choose **Move** to remove the
source only after verification, or **Copy** to retain the original in its current
folder. Local/external transfers continue to use Move, with source retention if
deletion cannot be completed safely. Existing files require Keep Both or explicit Replace confirmation.

Without `--data-dir`, state lives in `%LOCALAPPDATA%\PrimEx\SmartSave`.
`--config <absolute-json-path>` selects a configuration explicitly. A `portable.mode`
file beside the desktop EXE selects a local `data` directory, for developer testing
only. Do not include that marker or developer data in an installed deployment.

## Verification

```powershell
.\scripts\Verify.ps1
```

For an offline build, supply a local feed containing the required DI packages:
`.\scripts\Verify.ps1 -OfflineFeed <feed-directory>`. Offline restore disables
NuGet vulnerability auditing because the audit service is unavailable; perform
online advisory checking on your release build machine.

Tests are dependency-light executable C# test harnesses (nonzero exit on failure),
plus Node's built-in test runner. They are invoked with `dotnet run`, not `dotnet test`.
The desktop suite runs real WPF windows on an STA dispatcher and launches the
actual native host over framed stdio/current-user named pipes. Browser worker
tests use a mocked Chrome API; they do not certify a real Chrome/Edge installation.
Tests use disposable local directories, never company shares.

See [validation evidence](docs/validation.md), [manual integration tests](docs/manual-tests.md),
[startup troubleshooting](docs/startup-troubleshooting.md),
[configuration](docs/configuration.md), [browser integration](docs/browser.md),
[Office gate](docs/office.md), [deployment](installer/README.md),
[security and recovery](docs/security-and-recovery.md), and the exact
[source-file inventory](docs/files-created.md).

## Coverage

| Entry point | Current behavior |
|---|---|
| Agent: Move / Copy a file | User selects a completed file and destination; Files PX supports verified Move or Copy |
| Browser: explicit context-menu link download | Browser stages it first; agent prompts for final placement after completion |
| Ordinary Save / Save As in other apps | Uses that application's normal save behavior |
| Ordinary browser downloads | Normal browser behavior, plus a post-save suggestion if automatic folder guidance is enabled for the destination |
| Optional local-folder monitoring | Post-save suggestion; never establishes manual intent or moves without confirmation |
| Word / Excel | Compatibility investigation/probe only; no interception |
| PowerPoint / Outlook | Not implemented |

## Projects

Core: configuration/contracts/path rules/journals/logs/protocol. FileManager:
UNC resolution, approved folders, permission probes, safe transfers and security
metadata. Desktop: dependency-injected MVVM WPF UI and tray. NativeHost: bounded
stdio-to-agent bridge. Monitoring: opt-in local directory watchers. Office:
read-only compatibility inventory. No hooks, injected DLLs, keyboard interception,
or privileged background service are used.
