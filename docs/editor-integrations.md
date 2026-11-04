# Connect VS Code and Visual Studio once

Smart Save 0.2.11 includes editor links for empty new projects without Git or
project manifests. Install the current MSI, then open Automatic guidance, keep
Skip coding projects automatically enabled and click Connect VS Code / Visual
Studio. Install each editor link once. Reload VS Code windows; complete the
Visual Studio extension installer and restart that editor once.

Every later local workspace/solution is shared automatically, including its
subfolders, documents and assets. Codex, terminals and builds can create files
inside it without placement prompts. Files outside those roots keep normal
guidance. The link does not enable monitoring or authorize transfers. Explicit
Move a file remains available. No files are added to projects.

Both VSIX packages also live in the installed app's EditorIntegrations folder.
VS Code supports Extensions: Install from VSIX. Its separate profiles need the
link installed once in each profile; the Connect button targets the default one.
The VS Code link uses workspace folders/change events, supports multiple roots
and windows, and shows a status-bar shield when registered. For a custom app
profile, set `primexSmartSave.dataDirectory` in User settings.

The Visual Studio package uses IVsSolution/events and
IVsFolderWorkspaceService.CurrentWorkspace. It captures solution/project
directories and the current Open Folder location. Package targets are 2022
17.14+ / 2026 x64. For custom storage, set PRIMEX_SMARTSAVE_DATA_DIR before launch.
Visual Studio multi-root Open Workspace and remote workflows need pilot testing.
Standalone files, remote/virtual folders and whole-drive/network roots are not
registered. Opening a broad folder such as Desktop suppresses that whole subtree
while linked; open the actual project folder instead.

Each editor window atomically writes a v1 JSON lease under the app data directory's
editor-workspaces folder. Fields are version, editor (VSCode/VisualStudio),
processId, updatedUtc (ISO-8601) and folders. Leases refresh every 30 seconds;
closing removes them and crashes expire them within 90 seconds. The reader checks
size/count limits, same-session live processes, path boundaries and expiry. It
watches that small registry directory and caches roots. Previously queued requests
are rechecked before prompting. Project-marker detection remains independent.
Only local paths and process/timing metadata are stored; nothing is sent over the
network. The MSI rejects editor-workspaces state from its payload.

Build the links after restoring the separate Visual Studio SDK project:

```powershell
dotnet restore extensions/visual-studio/PrimEx.SmartSave.VisualStudio.csproj
python extensions/build_editor_links.py --output artifacts/editor-links
```

SDK dependencies live in artifacts/build-tools/editor-packages. The staff builder
requires this restore first, then builds/bundles both links. No Marketplace
publication is performed.

Tests cover both publishers with the .NET reader, workspace updates/removal,
multiple windows, API event handling, stale/invalid records, a real desktop agent
with an unmarked workspace, and MSI/VSIX packaging. The VS Code VSIX installed in
an isolated profile. Its real extension-host test was blocked by the editor's
active updater (vscode-updating mutex); no lock was bypassed. No supported Visual
Studio host exists here. Actual editor activation/switching and cold-start
behavior remain pilot checks; mocks do not certify those workflows.

API references: [VS Code workspace API](https://code.visualstudio.com/api/references/vscode-api#workspace),
[VS Code VSIX installation](https://code.visualstudio.com/docs/configure/extensions/extension-marketplace#_install-from-a-vsix),
[Visual Studio workspaces](https://learn.microsoft.com/en-us/visualstudio/extensibility/workspaces?view=visualstudio),
[Visual Studio folder service](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.workspace.vsintegration.contracts.ivsfolderworkspaceservice?view=visualstudiosdk-2022).
