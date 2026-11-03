using System.Collections.Concurrent;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Monitoring.Watchers;
// Reports post-save candidates only. Never moves files and cannot establish manual intent.
public sealed class DirectoryMonitor(AppOptions options, IEventLog log) : IAsyncDisposable
{
    private readonly List<FileSystemWatcher> watchers = [];
    private readonly ConcurrentDictionary<string, DateTimeOffset> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long Size, DateTime Modified, DateTimeOffset ReportedUtc)> reported = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource stopped = new();
    private readonly EligibleFilePolicy policy = new(options);
    private Task? pump;
    public event Action<string>? Candidate;
    public event Action? MissedEvents;
    public int ActiveFolderCount => watchers.Count;
    public void Start()
    {
        if (!options.Monitoring.Enabled || pump != null) return;
        foreach (var folder in options.Monitoring.Folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (new DriveInfo(Path.GetPathRoot(folder)!).DriveType == DriveType.Network) throw new ArgumentException("Monitoring requires a local folder.");
                var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = options.Monitoring.IncludeSubdirectories, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
                watcher.Created += Changed; watcher.Changed += Changed; watcher.Renamed += (_, e) => Schedule(e.FullPath);
                watcher.Error += (_, _) => { log.Write("MonitoringOverflow", null, "SomeEventsMayBeMissing"); MissedEvents?.Invoke(); };
                watcher.EnableRaisingEvents = true; watchers.Add(watcher);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { log.Write("MonitoringFolderUnavailable", null, e.GetType().Name); MissedEvents?.Invoke(); }
        }
        pump = Task.Run(PumpAsync);
        log.Write("MonitoringStarted");
    }
    private void Changed(object sender, FileSystemEventArgs e) => Schedule(e.FullPath);
    private void Schedule(string path)
    {
        if (pending.Count >= 2048) { MissedEvents?.Invoke(); return; }
        pending[path] = DateTimeOffset.UtcNow;
    }
    private async Task PumpAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(stopped.Token))
                foreach (var entry in pending.ToArray())
                {
                    if (DateTimeOffset.UtcNow - entry.Value < TimeSpan.FromSeconds(options.Monitoring.QuietSeconds)) continue;
                    if (!pending.TryRemove(new KeyValuePair<string, DateTimeOffset>(entry.Key, entry.Value))) continue;
                    if (!policy.IsEligible(entry.Key)) continue;
                    try
                    {
                        using var stream = new FileStream(entry.Key, FileMode.Open, FileAccess.Read, FileShare.None);
                        var fingerprint = (stream.Length, File.GetLastWriteTimeUtc(entry.Key));
                        if (reported.TryGetValue(entry.Key, out var prior) && ((prior.Size, prior.Modified) == fingerprint || DateTimeOffset.UtcNow - prior.ReportedUtc < TimeSpan.FromMinutes(30))) continue;
                        if (reported.Count >= 4096) { log.Write("MonitoringLimitReached", null, "RestartMonitorToResume"); continue; }
                        reported[entry.Key] = (fingerprint.Length, fingerprint.Item2, DateTimeOffset.UtcNow);
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
        catch (OperationCanceledException) { }
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var watcher in watchers) watcher.Dispose();
        stopped.Cancel(); if (pump != null) await pump; stopped.Dispose();
    }
}
