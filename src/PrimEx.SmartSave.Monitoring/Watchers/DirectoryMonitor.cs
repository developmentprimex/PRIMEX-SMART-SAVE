using System.Collections.Concurrent;
using System.Diagnostics;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Monitoring.Watchers;
// Reports newly saved files only. Existing documents keep their current location.
// Never moves files and cannot establish manual intent.
public sealed class DirectoryMonitor(AppOptions options, IEventLog log, RememberedLocationStore? locations = null, EditorWorkspaceRegistry? editorWorkspaces = null) : IAsyncDisposable
{
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly ConcurrentDictionary<string, DateTimeOffset> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> existing = new(StringComparer.OrdinalIgnoreCase);
    private int reportedCount;
    private readonly CancellationTokenSource stopped = new();
    private readonly EligibleFilePolicy policy = new(options, editorWorkspaces);
    private readonly object lifecycle = new();
    private readonly SemaphoreSlim scheduled = new(0, 1);
    private Task? startup;
    private Task? shutdown;
    private Task? pump;
    private volatile bool disposed;
    public double StartupMilliseconds { get; private set; }
    public int IndexedFileCount => existing.Count;
    public int PendingFileCount => pending.Count;
    public event Action<string>? Candidate;
    public event Action? MissedEvents;
    public int ActiveFolderCount => watchers.Count;
    public void Start() => StartAsync().GetAwaiter().GetResult();
    public Task StartAsync()
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return startup ??= options.Monitoring.Enabled ? Task.Run(Initialize) : Task.CompletedTask;
        }
    }
    private void Initialize()
    {
        var watch = Stopwatch.StartNew();
        try
        {
            foreach (var folder in options.Monitoring.Folders.Select(PathRules.NormalizeAbsolute).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Length))
            {
                stopped.Token.ThrowIfCancellationRequested();
                if (options.Monitoring.IncludeSubdirectories && watchers.Any(w => PathRules.IsWithin(folder, w.Path))) continue;
                try
                {
                    if (new DriveInfo(Path.GetPathRoot(folder)!).DriveType == DriveType.Network) throw new ArgumentException("Monitoring requires a local folder.");
                    // Seed paths, not timestamps: Office can replace an existing document
                    // with a temporary file whose creation time and file identity change.
                    // Rebuild this baseline on restart or when settings are reapplied.
                    foreach (string path in policy.EnumerateExistingFiles(folder, stopped.Token)) existing.TryAdd(path, 0);
                    stopped.Token.ThrowIfCancellationRequested();
                    var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = options.Monitoring.IncludeSubdirectories, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
                    watcher.Created += Changed; watcher.Changed += Changed; watcher.Renamed += Renamed;
                    watcher.Deleted += (_, e) => policy.ObserveChange(e.FullPath);
                    watcher.Error += (_, _) => { log.Write("MonitoringOverflow", null, "SomeEventsMayBeMissing"); MissedEvents?.Invoke(); };
                    watcher.EnableRaisingEvents = true; watchers.Add(watcher);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { log.Write("MonitoringFolderUnavailable", null, e.GetType().Name); MissedEvents?.Invoke(); }
            }
            pump = Task.Run(PumpAsync);
            StartupMilliseconds = watch.Elapsed.TotalMilliseconds;
            log.Write("MonitoringStarted");
        }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
    }
    private void Changed(object sender, FileSystemEventArgs e) { policy.ObserveChange(e.FullPath); Schedule(e.FullPath); }
    private void Renamed(object sender, RenamedEventArgs e)
    {
        policy.ObserveChange(e.OldFullPath); policy.ObserveChange(e.FullPath);
        // Renaming a known document is not a first save. Keep the old path too,
        // since atomic saves may rename the original away before replacing it.
        // Temporary downloads are never in the baseline, so their final rename
        // still produces the first-save prompt.
        if (existing.ContainsKey(e.OldFullPath) && policy.IsEligible(e.FullPath))
            existing.TryAdd(e.FullPath, 0);
        Schedule(e.FullPath);
    }
    private void Schedule(string path)
    {
        if (disposed) return;
        if (existing.ContainsKey(path)) return;
        if (!policy.CanBeEligible(path)) return;
        if (pending.Count >= 2048) { MissedEvents?.Invoke(); return; }
        pending[path] = DateTimeOffset.UtcNow;
        if (scheduled.CurrentCount == 0)
            try { scheduled.Release(); } catch (SemaphoreFullException) { }
    }
    private async Task PumpAsync()
    {
        try
        {
            while (!stopped.IsCancellationRequested)
            {
                // No periodic wakeups or dictionary snapshots while idle.
                if (pending.IsEmpty) await scheduled.WaitAsync(stopped.Token);
                await Task.Delay(250, stopped.Token);
                foreach (var entry in pending.ToArray())
                {
                    if (DateTimeOffset.UtcNow - entry.Value < TimeSpan.FromSeconds(options.Monitoring.QuietSeconds)) continue;
                    if (!pending.TryRemove(new KeyValuePair<string, DateTimeOffset>(entry.Key, entry.Value))) continue;
                    if (existing.ContainsKey(entry.Key)) continue;
                    if (!policy.IsEligible(entry.Key)) continue;
                    if (locations?.Contains(entry.Key) == true) continue;
                    try
                    {
                        using var stream = new FileStream(entry.Key, FileMode.Open, FileAccess.Read, FileShare.None);
                        if (reportedCount >= 4096) { log.Write("MonitoringLimitReached", null, "RestartMonitorToResume"); continue; }
                        // Once offered, later saves to this path are ordinary edits,
                        // even if the prompt was dismissed or the file is recreated.
                        if (!existing.TryAdd(entry.Key, 0)) continue;
                        reportedCount++;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        // Office may keep a document locked for hours. Keep this bounded pending
                        // entry until it can be read exclusively, instead of losing it after two minutes.
                        pending.TryAdd(entry.Key, DateTimeOffset.UtcNow); continue;
                    }
                    Candidate?.Invoke(entry.Key);
                }
            }
        }
        catch (OperationCanceledException) { }
    }
    public ValueTask DisposeAsync()
    {
        lock (lifecycle)
        {
            if (shutdown != null) return new(shutdown);
            disposed = true;
            stopped.Cancel();
            return new(shutdown = StopAsync(startup));
        }
    }
    private async Task StopAsync(Task? starting)
    {
        if (starting != null) await starting.ConfigureAwait(false);
        foreach (var watcher in watchers) watcher.Dispose();
        if (pump != null) await pump.ConfigureAwait(false);
        stopped.Dispose();
    }
}
