# Automatic guidance for everyday downloads and saves

In 0.2.4 the two choices are named **Files PX** (company server) and **Downloads**
(keep the current local folder). Downloads is the local-choice label: a document
already on Desktop/Documents stays there rather than being moved to Downloads.

Version 0.2.7 places Files PX and Downloads side by side in a compact prompt.
Only clicking Downloads reveals the Albanian advisory: **Kujdes: Vetëm për
dokumente provë dhe kopje jo të rëndësishme. Dokumentet e rëndësishme të punës ruajini
në Files PX.** Click **Vazhdo me Downloads** to keep the file and remember the
choice, or **Kthehu** to return. Escape first dismisses the warning; X closes
without remembering a choice. Files PX stays available. In the manual/browser
destination screen the warning appears only when Downloads is selected, before
the existing Confirm destination action. Local saving remains available.

Open the updated desktop app and click **Automatic guidance…**. Select **Enable
automatic guidance on this PC**, check Downloads and any other local folders you
want to watch, optionally include subfolders, and browse for your authorized
company root. Click **Apply automatic guidance**. Monitoring starts immediately;
there is no JSON editing or agent restart for these settings. The home screen
shows whether guidance is on and how many selected folders are active.

You can leave company roots empty while testing **Downloads**.
Company roots are needed to offer a usable company destination. The same settings
screen can disable monitoring without moving or deleting anything. Folder choices
are remembered. Opening/cancelling setup never enables monitoring or changes files.
Downloads/Desktop/Documents are suggested using the current Windows user's
registered folder paths rather than a hardcoded user directory. Suggestions are
unchecked until explicitly selected. Advanced Settings still opens JSON for the
other integration options; managed configuration may require IT to change it.

After setup, continue using applications normally:

As of 0.2.12, a save/download opens its placement prompt without showing the main
queue behind it. If the queue is hidden or minimized, the placement dialog stands
alone, centered on screen with a taskbar/Alt+Tab entry. Keeping, cancelling or
choosing a company folder does not restore the queue. A main window already opened
by the user remains available; tray Open Smart Save/View Pending Operations opens
it explicitly.

| Action | Expected behavior |
|---|---|
| Download a PDF from Teams into selected Downloads | Once complete and unlocked, offer Files PX / Downloads |
| Normal Chrome/Edge download into a selected folder | Same post-save suggestion; no browser extension required for this folder-based mode |
| Save a new Excel/Word/PDF file in a selected folder | Suggest after the file becomes quiet and unlocked; an application retaining a lock can delay it until close |
| Save an existing document again | Keep its current location without another Smart Save prompt, including after restarting the agent or an atomic replacement save |
| Save in a selected subfolder | Covered only if Include subfolders is checked |
| Save in another folder | Add that particular local folder to setup to cover it |
| Downloads | Show the advisory; Vazhdo me Downloads leaves the file untouched and persists the choice for later saves to the same path with the same creation timestamp, including after restart |
| Files PX | Opens the company-folder browser directly; select a folder, choose Move or Copy and confirm. It does not repeat the Files PX / Downloads question |

The company-folder step opens the native Windows Shell folder picker, with
breadcrumb navigation, search, This PC, mapped drives and New folder. It starts
at an approved company root and adds configured roots as shortcuts. Windows
controls its appearance and view preferences; it is a folder picker rather than
a full standalone Explorer window. You can navigate elsewhere, but confirming
a destination outside the configured company roots is rejected. Click Select
Folder, then review the filename. Since 0.2.13, select **Move to Files PX** or
**Copy to Files PX — keep original** and click **Move here** / **Copy here**.
Copy preserves the original in Downloads or its current folder and verifies the
server copy. Move removes the source only after verification. Existing filenames
still require Keep Both or explicitly confirmed Replace. **Downloads**
appears only in the initial post-save question. Cancel leaves the source
untouched. The action is recorded in history and restored for a reviewed retry.
Later edits to the original are not synchronized with the server copy.
Manual Move / Copy a file and the explicit
browser-extension workflow still offer both original destination choices.

This is **after-save guidance**, not a popup before every Save or Download. Office
save-event add-ins are still unimplemented. A file watcher cannot reliably infer
manual intent, so background changes to eligible documents in selected folders
can also be detected. Application caches, temporary downloads, Office lock files,
hidden/system files and browser staging folders are excluded. Version 0.2.1
defaults to **All completed file types**, including PNG/JPG and other images,
PDFs, Office documents, archives, media, unfamiliar extensions and files without
an extension. No entire-drive monitoring is allowed. The original two shipped
default extension lists automatically use this new default when loaded, without
rewriting the existing configuration or changing selected folders/company roots.
An explicitly customized list remains restricted. To restrict formats, uncheck
All completed file types and enter the desired extensions in Specific file types.

All-types mode does not include unfinished `.crdownload`, `.part`, `.partial`,
`.download`, `.filepart`, temporary/lock files, Office lock files, hidden/system
files, PrimEx recovery backups or cache/build subfolders such as Cache, AppData,
node_modules, .git, .vs, bin and obj inside the selected roots. It never executes
files. Choosing a broad recursive folder can produce more prompts; keep the scope
limited to the document/download folders you actually want to guide.

Open Office files are retried while locked rather than discarded after two
minutes. The pending table remains bounded. A browser-style final rename from
`.crdownload` to `.xlsx` is detected; a partial download never triggers a move.
The agent must remain running in the tray. Developer builds do not register
Windows startup; startup deployment still needs the installer/release gate.

At startup, existing eligible file paths are indexed only to suppress prompts on
later edits. No existing files are offered for placement or moved by this scan.
Since 0.2.9, history loading and indexing run in the background after the main
window is shown. Wait for **Automatic guidance ON** before testing a new save.
The index skips excluded directory trees before entering them, and overlapping
recursive roots share a watcher. Idle monitoring waits for new events.

Known limitations: no guaranteed capture
during agent downtime or the brief reconfiguration gap; no Teams cloud-only
edits or files that never reach a selected local folder; no folder or file reparse
points, links or cloud placeholders under the current strict transfer policy.
If setup rejects a redirected/cloud folder, uncheck it and select a direct local
folder such as your local Downloads. Real Teams/browser/application workflows,
SMB transfer and pilot hardware still need manual acceptance testing.

First test: enable Downloads, download a disposable PDF into that exact folder,
wait for the post-save prompt, and choose **Downloads**. To test a
move, add an approved test company folder, download a different sample file,
and choose **Files PX**. Use a new filename to test another first-save prompt.
Closing the main window hides it to tray; use tray
Exit to stop the agent. Leave automatic guidance off on other PCs until pilot
validation is complete.

Version 0.2.3 remembers an explicit Keep choice using the normalized full path
and creation timestamp, not the filename alone. Content/last-write changes do not
invalidate the choice. Closing with X, Escape or cancelling the company-folder
step does not create a remembered choice.

Version 0.2.8 also suppresses repeated-save prompts independently of explicit Keep
choices. Paths present when monitoring starts, and new paths already offered for
placement during that session, keep their current location on subsequent saves.
This includes atomic replacement saves with changed creation timestamps and
renaming a known document. Dismissing the first prompt leaves the document where
it is; another edit does not reopen the question. Restarting rebuilds the existing
path baseline. A newly created Save As copy at a different path still prompts.
Reusing an already known path for a different file also stays silent until restart;
the monitor treats the path as an existing document, not as universal file identity.
Server documents remain excluded by the approved-root policy. Ordinary Save is
performed by the original application at its current location; Smart Save does
not bypass that application's Save As dialog, silently redirect a recreated local
copy to the server, or automatically overwrite a server document. Use Move a file
to explicitly relocate a previously kept document. Remembered choices start with
new explicit Keep actions after the upgrade; prior cancellations are not migrated.

Reference: [Windows known-folder lookup](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath).

## Coding projects and excluded folders (0.2.10)

Skip coding projects automatically is enabled by default, including when loading
older settings. A project root is recognized by `.git` (folder or worktree file),
`.vscode`, `.idea`, `.hg`, `.svn`, `package.json`, `pyproject.toml`, `Cargo.toml`,
`go.mod`, `composer.json`, or a `.sln`, `.slnx`, `.csproj`, `.fsproj`, `.vbproj` or
`.code-workspace` file. Its whole subtree is skipped, including Markdown, JSON,
PDFs and image assets. Creating a project marker while monitoring runs invalidates
the detection cache; pending/previously queued monitoring requests are rechecked
before any placement prompt. Known projects are filtered before pending work.

For a folder without markers, click Exclude a folder in Automatic guidance, or
enter its absolute path under Never prompt in these folders (one per line), then
Apply. A parent Projects folder can exclude all projects beneath it. Exclusions
use path boundaries, so excluding `Project` does not exclude `Project-other`.
They persist across restarts and apply even with automatic project detection off.
Explicit Move a file and the explicit browser-extension workflow remain available.

The user's profile and standard Downloads/Desktop/Documents/Temp container roots
are not automatically classified as coding projects merely because a workspace or
manifest is present there. A project subfolder is still recognized normally. This
prevents global VS Code settings or a downloaded project attachment from silencing
all ordinary documents. Folder detection is cached for up to 30 seconds; marker
events inside the monitored scope refresh it immediately. If an ancestor outside
that scope changes, its new state can take up to 30 seconds to be recognized.
Nothing is inferred from the foreground application: an unmarked project requires
an explicit folder exclusion in 0.2.10. Monitoring enablement and selected folders do not
change automatically during upgrade.

Since 0.2.11, Connect VS Code / Visual Studio installs each link once. Open local
workspaces/solutions then register automatically, even without project markers.
Closing removes the active link; crashes expire it within 90 seconds. Both VSIX
packages are bundled with the MSI. See [editor setup and limits](editor-integrations.md).
