# Deployment preparation (not installation certification)

MSI source targets WiX 6 and a per-user Windows Installer package. WiX is not
available in this development sandbox, so the MSI has not been compiled or
installed. Treat packaging as a release gate. Do not deploy this candidate widely.

Windows execution policy on the development PC blocks unsigned `.ps1` files.
Scripts were syntax-checked but not executed to change registration. Sign these
scripts with an approved enterprise certificate or run them through the authorized
deployment system. No execution policy was changed or bypassed during development.

On a network-enabled Windows build machine, use .NET SDK 10.0.300+ and WiX 6.
Obtain stable approved Chrome/Edge extension IDs from the approved store or your
managed developer package. Do not use placeholder IDs for deployment.

```
.\installer\Build.ps1 -ChromeExtensionId <32-letter-id> -EdgeExtensionId <32-letter-id>
```

The default publishes self-contained executables and includes the .NET runtime.
`-FrameworkDependent` instead requires the matching architecture .NET 10 Windows
Desktop Runtime to be installed as an Intune dependency before the MSI. `-SkipMsi`
prepares payloads only. Use a clean payload directory for repeatable releases;
the script refuses to recursively replace an existing payload.

Review dependency advisories, update package locks and SDK servicing versions on
the build machine, run all verification, sign EXEs and MSI with your enterprise
certificate, then install in a disposable VM. Retain the UpgradeCode, increment
Version for upgrades, and test upgrade/downgrade/uninstall with an active queue.

The MSI installs in `%LOCALAPPDATA%\PrimEx\SmartSaveApp`, registers Native Messaging
in HKCU for Chrome and Edge, adds a Start-menu shortcut, and uses HKCU Run to
start the agent with `--startup --background`. Runtime `StartWithWindows=false`
causes startup invocations to exit. Normal user saves need no elevation.

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

References: [Windows Run keys](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys),
[Intune Win32 deployment](https://learn.microsoft.com/en-us/mem/intune/apps/apps-win32-add),
[WiX Files schema](https://docs.firegiant.com/wix/schema/wxs/files/).
