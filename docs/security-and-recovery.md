# Security and recovery

The 0.2.11 editor links write current-user local workspace leases only to suppress
automatic placement. They cannot enable monitoring, authorize transfers or execute
commands in the agent. The reader validates size/count/expiry, local path boundaries
and a live process in the same session. Editor process IDs are not authentication
against malicious same-user software. No document content or network traffic is
involved. Workspace paths are local user state and excluded from MSI/diagnostics.

Run at the signed-in user's privilege level. Native hosts must be registered with
exact approved extension origins. The host checks the browser-provided origin
argument against configuration, and the agent repeats schema/origin/path checks.
The named pipe is restricted to the current Windows user and named using that
user's SID, session ID and data-directory identity. This limits cross-user and
cross-session access; it does not authenticate arbitrary already-running programs
under the same Windows user. Native Messaging's origin argument is not a secret:
a malicious same-user process can launch the host itself. No privileged service
is exposed, no passwords are stored, and destination selection always requires
the user's desktop interaction.

Transfer paths must be absolute Windows/UNC paths without device prefixes,
ADS syntax, traversal, reserved names, control characters or ambiguous trailing
spaces/dots. Reparse-point ancestors are rejected, including some OneDrive/DFS
or symlink-based layouts. Use direct ordinary paths for this pilot. These checks
are defense in depth, not a sandbox against a malicious same-user process racing
directory replacement. Enterprise release review should evaluate handle-relative
path traversal and destination identity binding for stronger same-user race defense.

The transfer creates a unique `.primex-<guid>.partial` beside the final target,
streams data, flushes, verifies length and SHA-256, preserves downloaded security
zone metadata, then publishes with a no-overwrite rename. Keep Both retries on
collisions. Replace uses Windows ReplaceFile only after explicit UI confirmation
and keeps the old document in a uniquely named `.primex-backup-<guid>` recovery
file. Filesystems/SMB implementations that cannot support those semantics fail
and preserve the source. No unsafe delete-then-copy replacement fallback is used.

Move opens the source with a DELETE-capable exclusive handle, re-hashes it while
holding a read handle on the published destination, and requests deletion through
that source handle. A changed, locked, or undeletable source is retained and the
UI reports copy completion with the source retained. No download is executed.
Source timestamps, all alternate streams other than Zone.Identifier, EFS,
sparse/compressed layout, and every application-specific metadata type are not
preserved by this candidate. Non-NTFS security-stream behavior needs validation.
Disk flush and hash verification cannot guarantee remote hardware durability.

Since 0.2.13, Files PX also offers explicit Copy. It uses the same verified
publication and conflict handling, without requesting source deletion. The
selected Move/Copy action is persisted before transfer and restored for reviewed
retries after restart. Pre-0.2.13 journal records default to Move. Intentional Copy
completion is distinguished from a Move that retained an undeletable source.
This is a one-time copy; subsequent edits are not synchronized.

Every queued operation is persisted before UI presentation. Transfer-in-progress
state is persisted before starting the copy. A crash, reboot, network interruption,
or cancellation can leave a final file or an orphan partial. On restart,
Transferring becomes NeedsReview. The agent never automatically repeats a
possibly published move or deletes recovery artifacts. Review source/destination,
then use Retry after review, Keep Both, or a new manual move. A cancelled placement
retains the source. Closing the main window hides to tray; tray Exit waits for
the current operation to be finished/cancelled. Windows shutdown can still
interrupt the process. Pending requests remain reviewable after restart.

Since 0.2.15, placement windows require explicit local keeping or a valid
transfer destination; Close/Escape/Alt+F4 do not count as consent. The title-bar
Close control is hidden. Back/native-folder cancellation returns to the choice
screen. Local labels identify the actual source/selected folder and the full
path is shown. Keeping the unchanged path/name performs no transfer or overwrite.
Session shutdown is allowed; an interrupted choice remains Pending without
being marked as an explicit local choice. This is a desktop interaction rule,
not protection against terminating the process. Local saving remains available.

No automatic synchronization, orphan cleanup, completed-journal pruning or
backup deletion is enabled. IT should define retention after pilot review.
Journals and browser local storage necessarily contain local file paths and IDs;
they are user-private operational state, not exported diagnostics. The optional
diagnostic export contains only version/runtime/OS and operation-state counts.
Do not share the raw state directory as diagnostics without reviewing it.

Explicit Keep choices are stored separately in `remembered-locations`, with
SHA-256 keys of normalized paths and creation-time ticks. They contain no document
contents or plaintext filenames. These local markers only suppress repeat advice;
they never authorize file transfers or replacements. Damaged/unreadable markers
do not suppress prompts. They are retained with user state on uninstall. No choice
is persisted just because a dialog was closed/cancelled.
