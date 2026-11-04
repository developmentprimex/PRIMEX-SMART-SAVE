# PrimEx Smart Save Workspace Link

Install once in VS Code using **Extensions: Install from VSIX**. Every open local
workspace folder is then shared automatically with Smart Save 0.2.11 or later,
including new folders without Git or project files. Multi-root workspaces and
opening/switching projects are handled automatically. No files are added to projects.

Smart Save must have **Skip coding projects automatically** enabled. This link
does not enable folder monitoring or move any file. Ordinary files outside open
workspaces still receive normal guidance. Standalone files without an open folder
and remote/virtual workspaces are not registered.

Close/remove a workspace to stop registering it. If the editor crashes, its lease
expires within 90 seconds. Recognized Git/project roots may remain excluded by
Smart Save's independent project detection. The status bar shield indicates the
workspace link is registered; the desktop agent must also be installed/running.

Only local workspace paths, editor process ID and a timestamp are written to the
current user's Smart Save data directory. Nothing is sent over the network.
For a custom `--data-dir`, set `primexSmartSave.dataDirectory` in User settings.
