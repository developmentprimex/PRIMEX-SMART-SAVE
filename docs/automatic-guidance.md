# Automatic guidance for everyday downloads and saves

Open the updated desktop app and click **Automatic guidance…**. Select **Enable
automatic guidance on this PC**, check Downloads and any other local folders you
want to watch, optionally include subfolders, and browse for your authorized
company root. Click **Apply automatic guidance**. Monitoring starts immediately;
there is no JSON editing or agent restart for these settings. The home screen
shows whether guidance is on and how many selected folders are active.

You can leave company roots empty while testing **Keep Current Location**.
Company roots are needed to offer a usable company destination. The same settings
screen can disable monitoring without moving or deleting anything. Folder choices
are remembered. Opening/cancelling setup never enables monitoring or changes files.
Downloads/Desktop/Documents are suggested using the current Windows user's
registered folder paths rather than a hardcoded user directory. Suggestions are
unchecked until explicitly selected. Advanced Settings still opens JSON for the
other integration options; managed configuration may require IT to change it.

After setup, continue using applications normally:

| Action | Expected behavior |
|---|---|
| Download a PDF from Teams into selected Downloads | Once complete and unlocked, offer Move to Company Server / Keep Current Location |
| Normal Chrome/Edge download into a selected folder | Same post-save suggestion; no browser extension required for this folder-based mode |
| Save an Excel/Word/PDF file in a selected folder | Suggest after the file becomes quiet and unlocked; an application retaining a lock can delay it until close |
| Save in a selected subfolder | Covered only if Include subfolders is checked |
| Save in another folder | Add that particular local folder to setup to cover it |
| Keep Current Location | Leave the file untouched; suppress further prompts for that path for 30 minutes in this agent session |
| Move to Company Server | Opens the company-folder browser directly; select a folder and click Move here. It does not ask Company Server / Save Outside Server again |

The company-folder step shows the filename, approved company roots and their
accessible subfolders. Existing filenames still require Keep Both or explicitly
confirmed Replace. **Keep Current Location** remains available during folder
selection and leaves the source untouched. Manual Move a file and the explicit
browser-extension workflow still offer both original destination choices.

This is **after-save guidance**, not a popup before every Save or Download. Office
save-event add-ins are still unimplemented. A file watcher cannot reliably infer
manual intent, so background changes to eligible documents in selected folders
can also be detected. Application caches, temporary downloads, Office lock files,
hidden/system files and browser staging folders are excluded. Defaults cover
PDF, Word/Excel/PowerPoint formats, CSV, RTF, text and ZIP; the visible file-types
box controls the exact extensions. No entire-drive monitoring is allowed.

Open Office files are retried while locked rather than discarded after two
minutes. The pending table remains bounded. A browser-style final rename from
`.crdownload` to `.xlsx` is detected; a partial download never triggers a move.
The agent must remain running in the tray. Developer builds do not register
Windows startup; startup deployment still needs the installer/release gate.

Known limitations: no initial scan of existing files; no guaranteed capture
during agent downtime or the brief reconfiguration gap; no Teams cloud-only
edits or files that never reach a selected local folder; no folder or file reparse
points, links or cloud placeholders under the current strict transfer policy.
If setup rejects a redirected/cloud folder, uncheck it and select a direct local
folder such as your local Downloads. Real Teams/browser/application workflows,
SMB transfer and pilot hardware still need manual acceptance testing.

First test: enable Downloads, download a disposable PDF into that exact folder,
wait for the post-save prompt, and choose **Keep Current Location**. To test a
move, add an approved test company folder, download a different sample file,
and choose **Move to Company Server**. Use a new filename to avoid the 30-minute
repeat-prompt suppression. Closing the main window hides it to tray; use tray
Exit to stop the agent. Leave automatic guidance off on other PCs until pilot
validation is complete.

Reference: [Windows known-folder lookup](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath).
