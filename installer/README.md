# Staff MSI 0.2.2 — per-user pilot

The self-contained Windows x64 MSI is now built at
`artifacts/staff-installer-0.2.2/PrimEx.SmartSave-0.2.2-win-x64.msi`.
It bundles .NET 10.0.8, so staff do not need a separate .NET installation.
Default packaging does not include native messaging or require extension IDs;
ordinary Teams/browser downloads are covered by opt-in folder monitoring.

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
python installer/build_staff.py --wix <path-to-wix.dll> --wix-ui <path-to-WixToolset.UI.wixext.dll> --output artifacts/staff-installer-0.2.2
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
retained. Exit the previous version from the tray, install 0.2.2 as the same user, reopen the
installed shortcut and test with a newly downloaded file. No employee PC updates
automatically from GitHub or from the installer on this development machine.

0.2.2 replaces the custom company-folder list with the supported Windows Shell
folder picker. Move to Company Server opens familiar breadcrumb navigation,
search, This PC and New folder controls. Only configured company roots can be
confirmed. After Select Folder, review the filename and click Move here. Cancel
leaves the source untouched; Keep Current Location appears only in the first prompt.

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
