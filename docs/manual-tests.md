# Manual release gates

Use disposable local test files and an isolated **test** SMB share. Do not run
fault-injection, replacement, permission or destructive tests on company shares.
Record OS/browser/Office version, bitness, configuration, test outcome and evidence.
All manual gates below remain unexecuted unless explicitly listed in validation.md.

## Windows and local files

Start as a normal user on Windows 11. Confirm one instance/data directory, tray
commands, closing to tray, startup enabled/disabled, correct focus, Tab/Shift+Tab,
Alt access keys, Enter and Escape, screen-reader names, keyboard folder navigation,
125/150/200% DPI, multiple monitors and small work areas. Confirm both destination
choices remain available and Company Server starts selected. Test empty roots,
approved roots, access denied, creating a folder, cancellation and read-only media.
Verify Replace requires two explicit choices and keeps a backup; Keep Both never
alters an old file. Review hashes and source retention. Open/locked documents must
fail safely. Try a multi-gigabyte file and cancel mid-copy.

## SMB and offline

On a dedicated test share, verify UNC and mapped-drive equivalence, per-user
permissions, offline/unavailable server and reconnect, unavailable mapped drive,
share-root boundaries and DFS/reparse rejection. Disconnect only the isolated
test network during transfer. The UI should stay responsive, source remain
recoverable and retry require destination review. Test ADS support and downloaded
security-zone preservation on the server filesystem. Do not grant broader ACLs
to make a test pass. Validate directory access with an ordinary employee token.

## Chrome and Edge (each independently)

Use clean test profiles, register the exact ID, enable browser config and match
the actual Downloads folder. Right-click a direct link with the extension command.

| Scenario | Expected |
|---|---|
| PDF, XLSX, DOCX, ZIP | Complete in staging; one correctly associated desktop prompt; verified final file |
| Several simultaneous downloads | Unique operation folders; serialized prompts; no crossed destinations |
| Same filename twice | Distinct staged sources; Keep Both or explicitly confirmed replacement |
| Multi-GB download | Streaming transfer; no UI freeze or whole-file memory buffering |
| Interrupted/cancelled browser download | No transfer of partial data; browser resume remains available |
| Browser restart mid-download | Job recovered; only complete safe file delivered |
| Service worker restart | Storage-backed state reused; no duplicate move |
| Agent absent before download | Normal browser destination workflow; no data loss |
| Agent disappears after staging | File stays in staging; delivery retries after agent returns |
| Server disappears | Outside-server option remains; source retained on transfer failure |
| Cancel destination | Completed staging file retained; desktop can retry after review |
| Browser Ask where to save / another filename extension | If staging overridden, native host refuses path; manual move available |
| Ordinary/background page download | Normal browser behavior, no Smart Save prompt |
| Browser flags dangerous download | Browser safety controls remain authoritative; no transfer |
| Forged origin/traversal/unknown fields/oversized messages | Request rejected, no transfer |

## Monitoring pilot

The new **Automatic guidance…** screen can perform this setup without JSON editing.
Verify cancel leaves guidance disabled, selections/root paths persist, Apply
starts/stops watchers without restarting, and active-folder counts are accurate.
Test Teams downloads, normal browser downloads and Excel/PDF saves in selected
folders; locked Office documents may prompt only after closing. Test optional
subfolders and ensure siblings outside selected roots are not observed. Verify
an unavailable/linked/cloud-placeholder folder gives a clear error rather than
silently enabling unsupported coverage.

Leave disabled until pilot approval. Enable only one disposable local folder.
Create/modify eligible files, rename into it, create temp/Office lock files, leave
a file locked, generate duplicate events and exercise a watcher overflow. Verify
one suggestion per path within 30 minutes; Downloads leaves the file
in place; moving requires confirmation and selection. Verify recovery from agent
restart does not automatically move files. Existing files are not scanned at startup.

## Office, installer, enterprise

Office interception is not present: the full matrix in office.md must be completed
after implementing the separate add-ins. Do not label Word/Excel as working now.
Compile/sign the MSI on a WiX-equipped build machine, validate ICE diagnostics,
and test clean install, dependencies, repair, version upgrade, downgrade prevention,
startup opt-out and uninstall. Confirm uninstall retains config/journal/staging
documents and removes only product registration. Pilot Intune/GPO user-context
deployment before broader assignment. No policy changes were made in development.
# Native company folder picker (0.2.2)

Use disposable local test roots first. Enable monitoring for a separate temporary
download folder, configure two approved test company roots, then save a new sample.
Choose Files PX. Confirm Windows Shell breadcrumb navigation,
search, This PC, New folder and company-root shortcuts work. Select a child folder,
click Select Folder, review the filename, then click Move here. Verify exact bytes
at the destination and source removal only after success. Repeat with a filename
conflict, Cancel in the shell picker, Cancel in final confirmation, and a selection
outside approved roots (must be rejected). Confirm Downloads appears
only in the initial prompt. Test Alt/Tab/Escape, screen reader labels and DPI.
Only afterward use an IT-approved disposable SMB test directory to check mapped
drive/UNC equivalence and server outage. Never use existing company documents.

## Repeated saves (0.2.3)

In an isolated watched test folder, create a new TXT, choose Downloads,
then edit and Save it twice. Expect no further Smart Save prompt. Exit/reopen the
agent and edit/Save again; still no prompt. Save As under a new name and expect a
new post-save prompt. Repeat with Word, Excel and PDF editors, including atomic
replacement save behavior; changed creation timestamps may cause another prompt.
Test X/Escape and cancellation of company selection: these must not persist a Keep
choice. Files already in an approved server root must not prompt. Save As remains
the original application's dialog and choosing a different path creates a new
candidate. Confirm no local copy is automatically copied onto a server version.
