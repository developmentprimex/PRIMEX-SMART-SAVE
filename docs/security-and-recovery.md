# Security and recovery

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

Every queued operation is persisted before UI presentation. Transfer-in-progress
state is persisted before starting the copy. A crash, reboot, network interruption,
or cancellation can leave a final file or an orphan partial. On restart,
Transferring becomes NeedsReview. The agent never automatically repeats a
possibly published move or deletes recovery artifacts. Review source/destination,
then use Retry after review, Keep Both, or a new manual move. A cancelled placement
retains the source. Closing the main window hides to tray; tray Exit waits for
the current operation to be finished/cancelled. Windows shutdown can still
interrupt the process. Pending requests remain reviewable after restart.

No automatic synchronization, orphan cleanup, completed-journal pruning or
backup deletion is enabled. IT should define retention after pilot review.
Journals and browser local storage necessarily contain local file paths and IDs;
they are user-private operational state, not exported diagnostics. The optional
diagnostic export contains only version/runtime/OS and operation-state counts.
Do not share the raw state directory as diagnostics without reviewing it.
