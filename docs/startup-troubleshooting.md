# Startup and duplicate launches

If an older build is already running, open the hidden-icons arrow (^) beside the
Windows clock, right-click each old PrimEx Smart Save icon and choose **Exit**.
Finish or cancel any active transfer first. Closing the main window only hides
the agent, so it does not release its state directory. Then open the updated app
at `artifacts/publish-automatic/Desktop/PrimEx.SmartSave.Desktop.exe`.

Updated builds use a current-user/current-session activation pipe. Launching a
second copy with the same state directory shows the existing window and exits
the duplicate cleanly. It does not kill the existing process, repeat a transfer,
reset settings, or delete a queued operation. Older builds without this pipe
show the explicit Already Running instructions instead. Background startup
duplicates exit silently. Other per-user sessions are not remotely activated.

Only Windows sharing/lock violations on the instance lease count as a duplicate.
The startup error now distinguishes access denied, invalid settings/state and
other storage errors, and identifies the startup stage. An `agent.lock` file
remaining on disk after exit is normal: the open handle provides the lock;
merely finding the file does not block startup. Recovery does not delete it.

Tests cover real lease contention/release, unavailable storage, activation
isolation by profile, error classification, and a real second desktop process
that activates the first and exits with code zero while the first keeps running.

The development account could inspect the older processes but could not reliably
activate the interactive error window through computer use (the helper reported
`coordinate input geometry is unavailable`, then `foreground window did not
report a process id` during recovery). Those user processes were left running;
no force termination, permission changes or state reset was performed.
