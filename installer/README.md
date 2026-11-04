# Staff MSI 0.2.14 — per-user pilot

The self-contained Windows x64 MSI is now built at
`artifacts/staff-installer-0.2.14/PrimEx.SmartSave-0.2.14-win-x64.msi`.
It bundles .NET 10.0.8, so staff do not need a separate .NET installation.
Default packaging does not include native messaging or require extension IDs;
ordinary Teams/browser downloads are covered by opt-in folder monitoring.

0.2.14 reduces the default post-save quiet period from 3 seconds to 1 second.
The old shipped 3-second setting upgrades in memory on load. Other custom delays
remain; once saved with PromptTimingVersion=1, a deliberate 3-second setting also
remains. No live configuration file is edited by the installer. Partial-file
filters, write-event debounce and exclusive-read checks remain in place.

0.2.13 adds Move / Copy selection to both the monitored-save and manual/browser
Files PX destination screens, without an extra dialog. Move remains the default;
Copy retains the original and verifies the server copy. The selected action is
journaled and restored when retrying an interrupted/failed operation. Old journals
retain Move semantics. Copy does not synchronize subsequent local edits.

0.2.12 stops opening/restoring the queue for monitored saves and explicit browser
downloads. Placement dialogs use the main window as owner only if it is already
visible and not minimized. Otherwise they center independently with a taskbar/
Alt+Tab entry. Company-folder and browser destination steps follow the same rule.
Open Smart Save/View Pending Operations still opens the queue explicitly.

0.2.11 bundles one-time VS Code / Visual Studio workspace links and a Connect
editors screen in Automatic guidance. Later local projects need no manual
exclusion, even without Git. See docs/editor-integrations.md for supported
versions and actual-editor acceptance limits. Restore the separate Visual Studio
SDK project before building the MSI; the builder then packages both links offline.

0.2.10 suppresses automatic placement prompts for recognized coding projects.
Git/worktrees, VS Code settings and common project manifests identify project
roots, including documents/assets underneath them. Automatic guidance includes
a Skip coding projects checkbox (on by default) and explicit excluded folders.
Existing configurations gain the default without being rewritten; custom folder
and file-type choices remain. Explicit Move a file remains available.

0.2.9 improves startup: the window opens before background history loading and
monitor initialization. The initial index prunes excluded cache/server/staging
trees and avoids repeated file/ancestor metadata reads. Overlapping recursive
folders share a watcher; idle monitoring waits for an event. Export diagnostics
now includes the actual version, process CPU/memory and initialization metrics.
See `docs/performance.md` for a controlled comparison and its limits.

0.2.8 suppresses placement prompts when existing documents are resaved. It indexes
existing eligible paths at monitoring startup and offers each newly saved path
only once. Atomic replacement saves, changed creation times and agent restarts
do not reopen the placement question. New Save As copies still receive guidance.

0.2.4 names the two choices **Files PX** (company server) and **Downloads**
(keep the current local folder). The existing location/permission/conflict and
remembered-choice behavior is unchanged. Downloads is a label for the local
choice, not a command to move a Desktop/Documents file into the Downloads folder.

0.2.7 places Files PX and Downloads in one compact row. The Albanian advisory is
hidden until Downloads is clicked. Vazhdo me Downloads confirms the local choice;
Kthehu/Escape return to the initial prompt without recording consent. The normal
manual/browser screen shows the advisory when its Downloads choice is selected.
There are no new save restrictions or automatic transfers.

The MSI installs for the signed-in user under
`%LOCALAPPDATA%\PrimEx\SmartSaveApp\Desktop`, creates Desktop and Start-menu
shortcuts, registers HKCU Run with `--startup --background`, and appears in
Windows Installed Apps. Open the desktop shortcut after installation to configure
folders. First startup of the installed build opens Automatic guidance until
setup is applied once. Existing settings and queued operations are retained.

The checkbox **Start automatically when I sign in to Windows** controls the
persisted `StartWithWindows` setting. Unticking it makes startup invocations exit;
manual opening still works. Closing the main window hides to tray. Tray Exit stops
the agent until opened again or the next enabled sign-in. It is a user-session
agent, not a Windows service, and has no crash watchdog or forced restart after
an explicit Exit. Folder monitoring stays off until the user applies setup.

Build, table checks, self-contained runtime/first-run checks, and extraction/hash
verification are documented in docs/validation.md. The restricted development
account cannot access the Windows Installer service: full ICE validation and
install/upgrade/uninstall/sign-in testing need an ordinary Windows pilot/VM.
The MSI is unsigned; enterprise certificate signing and pilot acceptance remain
required before a broad rollout. No staff PCs were installed remotely.

Windows execution policy on the development PC blocks unsigned `.ps1` files.
Scripts were syntax-checked but not executed to change registration. Sign these
scripts with an approved enterprise certificate or run them through the authorized
deployment system. No execution policy was changed or bypassed during development.

On a Windows build machine, use .NET SDK 10.0.300+, Python 3.11+, WiX 6.0.2 and
the matching WixToolset.UI.wixext extension. Keep build tools outside the installed
payload. Review WiX distribution/maintenance terms for your build environment.

```
dotnet restore extensions/visual-studio/PrimEx.SmartSave.VisualStudio.csproj
python installer/build_staff.py --wix <path-to-wix.dll> --wix-ui <path-to-WixToolset.UI.wixext.dll> --output artifacts/staff-installer-0.2.14
```

`--wix wix` uses an installed WiX CLI instead. `--offline-feed <folder>` restores
from a reviewed local NuGet feed; online audit must be completed separately.
`--runtime-version` pins the bundled runtime (default 10.0.8); update it for
servicing and rebuild the MSI. `--version` sets the three-part product version.
The builder refuses to overwrite an existing output directory. Component IDs and
GUIDs are stable for each relative payload path, and every component has an HKCU
registry key path suitable for a per-user installation. Developer state/PDBs are
rejected. The optional PowerShell Build.ps1 wrapper calls this same builder.

0.2.1 defaults to All completed file types, so images such as PNG/JPG downloaded
from Teams do not require manual extension setup. The exact extension defaults
shipped in previous builds are upgraded on load; selected folders, company roots,
startup choice and pending operations are preserved. Custom extension lists are
retained. Exit the previous version from the tray, install 0.2.14 as the same user, reopen the
installed shortcut and test with a newly downloaded file. No employee PC updates
automatically from GitHub or from the installer on this development machine.

0.2.2 replaces the custom company-folder list with the supported Windows Shell
folder picker. Move to Company Server opens familiar breadcrumb navigation,
search, This PC and New folder controls. Only configured company roots can be
confirmed. After Select Folder, review the filename, select Move or Copy and confirm the matching button. Cancel
leaves the source untouched; Keep Current Location appears only in the first prompt.

0.2.3 introduced explicit Keep Current Location choices under the user's separate
state directory. Repeated saves at that path with the same creation timestamp no
longer prompt, including after restarting. Closing/cancelling a dialog does not
persist consent. A new name/path or changed creation time is considered a new
candidate under that version. Since 0.2.8, the existing-path baseline also suppresses
resave prompts when creation time changes or no explicit Keep choice was recorded.
This does not redirect Save As or synchronize local changes to a server.

To additionally include/register the native host, pass `--extension-id <actual-id>`
once per approved Chrome/Edge extension ID. This does not install the extension
or enable browser config. No fake extension IDs are placed into the default MSI.

Review dependency advisories, update package locks and SDK servicing versions on
the build machine, run all verification, sign EXEs and MSI with your enterprise
certificate, then install in a disposable VM. Retain the UpgradeCode, increment
Version for upgrades, and test upgrade/downgrade/uninstall with an active queue.

Normal user saves need no elevation. Do not run the per-user MSI as a different
administrator account or SYSTEM; install in the employee's interactive user context.

Configuration/journals/logs live separately in `%LOCALAPPDATA%\PrimEx\SmartSave`.
Uninstall intentionally retains these and all staging documents. Review staging
and pending operations before uninstalling. Close the agent via its tray menu
before upgrades; do not force-stop it during a transfer.

For developer-only native-host registration, `Register-NativeHost.ps1` writes
only the two product HKCU keys and a manifest next to the specified executable.
Use `-WhatIf` to review and `-Unregister` to remove those product keys. This
script does not install an extension or create startup entries. Run it as the
signed-in user, not an administrator and not SYSTEM.

Intune: wrap the signed MSI with the supported Win32 content preparation tool,
select **User** install behavior, use `msiexec /i <package.msi> /qn` and the product's
MSI uninstall code, and use the MSI product/version detection rule. Do not target
the entire company until the pilot is approved. GPO: user-assigned MSI deployment
or approved user-logon deployment can install the same package. IT must manage
any browser ExtensionInstallForcelist policy through its normal approval process;
none of these scripts modify it.

Pilot checklist: close all old portable instances from the tray; install as the
employee; open the shortcut; enable the intended folders/company roots; test a
disposable download; sign out and back in; confirm the tray icon and post-save
prompt; test startup opt-out and manual open; test upgrade with retained state;
uninstall and confirm startup/shortcuts are removed while state and user files
remain. Do not force-stop an agent during a transfer. The MSI does not contain
any actual company address or copied employee configuration.

References: [Windows Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys),
[Intune Win32 deployment](https://learn.microsoft.com/en-us/mem/intune/apps/apps-win32-add),
[WiX Files schema](https://docs.firegiant.com/wix/schema/wxs/files/).
