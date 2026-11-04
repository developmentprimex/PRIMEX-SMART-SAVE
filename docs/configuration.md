# Configuration workflow

For everyday downloads/saves, use **Automatic guidance…** in the desktop window
or tray. It suggests the actual Downloads/Desktop/Documents paths and lets you
select folders, enable subfolders, choose company roots, and enable/disable
post-save guidance without editing JSON. Apply saves and starts/stops watchers
immediately. Folder selections are opt-in; existing files are not scanned. See
[automatic guidance](automatic-guidance.md). Other settings still use the JSON
workflow below. Managed files remain subject to the existing write permissions.

1. Start the agent once. Open **Settings** from the window or tray. Settings opens
   the actual JSON file in Notepad; there is no graphical settings editor yet.
2. IT supplies actual approved UNC roots under `CompanyServer.ApprovedRoots`.
   Prefer UNC paths to mapped letters. Local roots are accepted for isolated test
   environments. No sample server address is installed.
3. Confirm access with an ordinary user account. The app honors share/NTFS ACLs
   and does not solicit, store, or substitute credentials.
4. To enable browser integration, first install the extension through an approved
   mechanism, obtain its exact ID, register the native host, set `Browser.Enabled`
   true, set `Browser.DownloadsFolder` to that browser profile's actual default
   Downloads folder, and add the exact ID to `AllowedExtensionIds`. Both the
   native manifest and app configuration must allow it. Browser integration is
   disabled until this setup is complete.
5. Restart the agent after editing JSON. Automatic-guidance setup applies its
   own folder/root settings immediately. Invalid configuration fails visibly. One
   agent instance owns a data directory; do not share it between concurrent
   interactive sessions or use portable mode with the production native host.

Precedence: explicit `--config`; otherwise an existing
`%PROGRAMDATA%\PrimEx\SmartSave\appsettings.json`; otherwise the user data directory's
`appsettings.json`. The installer does not create or modify machine policy.
IT should deploy managed ProgramData configuration with administrator write
access and user read access. The agent never elevates to edit it. User configuration
and journals inherit the signed-in user's profile protection. Portable test state
should not be placed in a shared writable folder.

`CompanyServer.DiscoverMappedDrives` lists mapped network drive mappings using
supported Windows APIs; mappings are resolved to UNC before approval comparisons.
Mappings outside approved roots are not approved just because they were discovered.
Empty roots cause no network-share enumeration or automatic write probes. A
write-and-delete probe is used only when a user confirms a selected destination;
it is stronger than an ACL guess and does not guarantee future availability.

`SmartSave.AllowOutsideServer` must be true. False is rejected in this edition,
preserving the project requirement that employees retain an outside option.
`PreferredDestination` must be CompanyServer. The manual-save/background-autosave
flags describe future supported Office behavior; no Office interceptor currently
consumes them. Explicit **Move a file** remains a user action. Download prompting
can be disabled with `ShowForSupportedDownloads=false`.

`Office.Enabled` must remain false. Only `UiLanguage=en` is implemented; unsupported
values are rejected rather than silently falling back. `Logging.Level` accepts
Information, Error, None; logs rotate by MaxBytes and RetainedFiles (number of
archived files, plus the active log). Logs contain event codes and random IDs,
not filenames, source URLs, contents, passwords or full exception messages.

`Monitoring.Enabled` defaults false. Supply explicit named local folders and
eligible extensions before enabling it in a pilot. Monitoring is top-level by
default; `IncludeSubdirectories=true` explicitly includes subfolders of only the
selected roots. It excludes temporary/hidden/system/reparse/staging files, waits for
quiet time and an exclusive read, and offers each new path only once per
monitor session. Existing eligible paths are indexed at startup to suppress
prompts on edits, including atomic replacement saves. It can report new background-generated documents and
miss saves while stopped. It cannot distinguish every user save from autosave.
After a watcher overflow, the status asks for manual review; no broad rescan or
automatic movement occurs. Pending queues and new-file reports are bounded;
the existing-path index grows with eligible documents in the selected folders.
As of 0.2.9, that index is prepared in the background and never enters excluded
cache/server/staging trees. Recursive selections already covered by an active
parent watcher do not create another watcher. No periodic work runs when the
pending queue is empty. The home screen reports active watchers rather than a
one-to-one count of selected roots. Export diagnostics includes startup indexing
time, indexed/pending counts, process CPU time and memory without document names.

`Monitoring.IgnoreDevelopmentProjects` defaults true, also for older JSON that
omits the field. Recognized coding project subtrees are skipped. Set false to
allow eligible project files again. `Monitoring.ExcludedFolders` defaults `[]`;
each entry must be an absolute path and excludes the entire subtree regardless
of the project-detection switch. Both controls are editable in Automatic guidance
and survive reconfiguration/restart. Project exclusions affect monitoring only,
including previously queued automatic requests; explicit manual moves remain
available. See automatic-guidance.md for marker detection and container boundaries.

0.2.11 editor links share IgnoreDevelopmentProjects: turning that switch off
disables both marker and editor-workspace suppression. Explicit ExcludedFolders
still applies. Diagnostics includes an active workspace count, never its paths.
See editor-integrations.md for custom data-directory settings.
Locked files are retried until released while the agent is running; open Office
documents may therefore prompt only after closing. Setup validates selected
folders and never watches whole drives or application cache locations automatically.

As of 0.2.1, `Monitoring.Extensions` defaults to `["*"]`, meaning all eligible
completed file types, including images and extensionless files. The setup has an
All completed file types checkbox (selected by default). Uncheck it for an
explicit extension list such as `[".pdf", ".png"]`; wildcard/list mixtures and
empty or malformed filters are rejected. On load, the exact two default lists
from the earlier builds use `["*"]` in memory. Custom lists remain unchanged;
the upgrade does not rewrite any configuration file or enable monitoring. A
subsequent explicit Apply saves the effective selection.

`StartWithWindows` is honored for `--startup` launches. The installer supplies
the supported per-user Run entry; merely running a developer build does not
register startup. Editing this value does not alter system policy or mapped drives.
