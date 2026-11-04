# Startup performance — 0.2.9

On 2026-11-04, a controlled comparison found a startup bottleneck in 0.2.8's
existing-file index. It ran on the UI thread before showing the window, traversed
excluded cache trees, and repeated file/ancestor metadata checks for every eligible
file. Loading the saved operation journal also ran before the window appeared.
This can delay opening on folders with many files or a slower disk. It does not
establish the cause of a particular colleague's slow laptop; its installed version
and hardware/resource measurements were not available.

0.2.9 shows the window before background history loading and index preparation.
The index prunes cache, staging and approved company directory trees before
entering them. It uses metadata already supplied by directory enumeration instead
of reopening each file and checking every ancestor again. Reparse/hidden/system/
temporary entries remain excluded. Overlapping recursive roots share a watcher.
Ignored event paths are filtered before entering the pending queue, and the idle
monitor waits for an event rather than waking on a 250 ms timer.

The existing quiet/unlocked-file check, confirmed moves, integrity verification,
conflict handling, interrupted-transfer review and resave suppression remain in
place. Actions are disabled until history has loaded. The preparation status
stays visible until monitoring is ready; saves during that preparation gap are
not guaranteed to be detected. Exit/reconfiguration cancels and joins the old
initialization without blocking on a UI-thread continuation.

## Controlled comparison

Same Windows development PC and .NET 10.0.8 runtime; the 0.2.8 published monitoring
assemblies versus 0.2.9. Both used the same disposable local corpus outside
OneDrive: 2,000 document files and 12,000 files inside excluded `node_modules` and
`Cache` directories, with recursive monitoring enabled. One warm-up followed by
three recorded runs. Values below are the median of the three runs.

| Initial monitor preparation | 0.2.8 | 0.2.9 |
|---|---:|---:|
| Elapsed time | 4,455 ms | 83 ms |
| Process CPU time during preparation | 4,359 ms | 78 ms |
| Managed allocations during preparation | 117.4 MiB | 22.0 MiB |

Preparation time decreased about 98%; managed allocations decreased about 81%.
With coding-project detection enabled in 0.2.10, the same corpus measured a median
81 ms preparation time and 23.9 MiB managed allocations across three warmed runs.
The settled three-second idle sample recorded 0 ms CPU. The additional project
checks did not introduce a measurable startup-time regression in this test;
raw results are in `artifacts/verification/performance-0.2.10.json`.
These are warm monitor-index measurements, not total application launch time,
cold-start/antivirus timings, resident RAM use, or promises for another PC. A
three-second settled idle sample recorded 15.6 ms CPU for 0.2.8 and 0 ms for
0.2.9; that short sample is too coarse to predict long-term CPU savings.
Raw results are in `artifacts/verification/performance-0.2.8.json` and
`artifacts/verification/performance-0.2.9.json`.
Obsolete installers and generated benchmark/publish copies were removed after
validation at the user's request; the raw measurements are retained. Repeating
the older baseline requires obtaining or rebuilding the corresponding binary.

The reproducible harness is `tests/PrimEx.SmartSave.Performance`. Build normally
to use the current source, or supply `-p:BenchmarkAssemblyDir=<absolute published
Desktop directory>` to use an earlier monitoring/core binary pair. Use separate
output directories and pass the same absolute corpus directory to each executable.
The harness creates only synthetic files in that directory, never moves documents,
and does not change app settings or install/register anything. Its default corpus
is `%TEMP%\PrimEx-Performance-Corpus`. It waits for tiered JIT/background collection
to settle before its idle sample.

## Prompt timing — 0.2.14

The earlier download/save delay includes an intentional three-second quiet
period in Smart Save, followed by the pending-file check (every 250 ms), an
exclusive-read check and UI dispatch. Version 0.2.14 reduces the standard quiet
period to one second. The watcher engine is unchanged: continued write events
restart the wait, temporary downloads stay excluded, and files held open by a
writer retry until they can be read exclusively. There is no extra periodic
work while the pending queue is empty.

On this Windows development PC, an automated native-window test measured the
time from closing a newly written local file to finding its placement popup:

| Build and settings | Popup delay |
|---|---:|
| 0.2.13, former default 3 seconds | 3.517 s |
| 0.2.14, same old configuration upgraded to 1 second | 1.408 s |

These are one sample per build, with a disposable file, a hidden main window
and 100 ms native-window polling. They demonstrate the configured delay reduction,
not a latency guarantee for other PCs or every save/download workflow. Both runs
waited for MonitoringStarted before creating the file. Download duration, user
input, and saving while an application still holds a lock are outside this
measurement. Disk/antivirus load, UI scheduling and another open placement dialog
can still add latency. No Windows setting, application lock or security control
was disabled. Raw evidence: `artifacts/prompt-delay-red-runtime.txt` and
`artifacts/prompt-delay-runtime.txt`.

Old settings without PromptTimingVersion upgrade their former shipped 3-second
value in memory; other custom delays remain. Normal settings saves include the
version marker so a subsequent explicit 3-second choice is preserved. See
[configuration](configuration.md) for managed timing settings.

## Checking another laptop

Install 0.2.9 as the same Windows user after closing the old version via tray Exit.
Wait for Automatic guidance ON, then use Export diagnostics. It now reports the
actual app version, saved-history load time, index preparation time, indexed and
pending file counts, active watcher count, process uptime/CPU time and process
working-set/private memory. It excludes document names, paths and contents.

If opening remains slow, compare those values with Task Manager's CPU, Memory and
Disk activity during opening. This distinguishes a large history/index from
general device load, disk/antivirus delays or runtime startup. A Files PX network
delay is separate: company-root discovery happens when that picker is requested,
not during normal startup. Real cold launch, large-history responsiveness and
exit/reconfiguration during indexing remain in the manual pilot checklist.
