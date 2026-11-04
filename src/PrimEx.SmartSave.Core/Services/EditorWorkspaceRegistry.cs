using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrimEx.SmartSave.Core.Services;

// Local, current-user editor leases. They suppress automatic placement only;
// they never authorize a move, execute a command or enable monitoring.
public sealed class EditorWorkspaceRegistry : IDisposable
{
    private sealed record Lease(int Version, string Editor, int ProcessId, DateTimeOffset UpdatedUtc, string[] Folders);
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static readonly TimeSpan LeaseLifetime = TimeSpan.FromSeconds(90);
    private readonly string directory;
    private readonly FileSystemWatcher watcher;
    private readonly object gate = new();
    private string[] roots = [];
    private DateTimeOffset expires;
    private int dirty = 1;

    public EditorWorkspaceRegistry(string directory)
    {
        this.directory = directory; Directory.CreateDirectory(directory);
        watcher = new(directory, "*.json") { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
        watcher.Created += Changed; watcher.Changed += Changed; watcher.Deleted += Changed;
        watcher.Renamed += (_, _) => Refresh(); watcher.Error += (_, _) => Refresh();
        watcher.EnableRaisingEvents = true;
    }
    private void Changed(object sender, FileSystemEventArgs e) => Refresh();
    public void Refresh() => Interlocked.Exchange(ref dirty, 1);
    public int ActiveWorkspaceCount { get { lock (gate) { ReloadIfNeeded(); return roots.Length; } } }
    public bool Contains(string path)
    {
        path = PathRules.NormalizeAbsolute(path);
        lock (gate)
        {
            ReloadIfNeeded();
            return roots.Any(root => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase));
        }
    }
    private void ReloadIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;
        if (Interlocked.Exchange(ref dirty, 0) == 0 && now < expires) return;
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextExpiry = now + LeaseLifetime;
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            foreach (var path in Directory.EnumerateFiles(directory, "*.json").Take(512))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Length > 65536 || (info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    var lease = JsonSerializer.Deserialize<Lease>(File.ReadAllText(path), Json);
                    if (lease == null || lease.Version != 1 || lease.Editor is not ("VSCode" or "VisualStudio") || lease.Folders == null || lease.Folders.Length > 128 ||
                        lease.UpdatedUtc > now.AddSeconds(5) || lease.UpdatedUtc + LeaseLifetime <= now || lease.ProcessId <= 0) continue;
                    using var process = Process.GetProcessById(lease.ProcessId);
                    if (process.HasExited || process.SessionId != currentProcess.SessionId) continue;
                    var normalized = lease.Folders.Select(PathRules.NormalizeAbsolute).ToArray();
                    if (normalized.Any(root => root.StartsWith(@"\\") || root.TrimEnd('\\').Equals(Path.GetPathRoot(root)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) continue;
                    foreach (string root in normalized) found.Add(root);
                    if (lease.UpdatedUtc + LeaseLifetime < nextExpiry) nextExpiry = lease.UpdatedUtc + LeaseLifetime;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        roots = found.ToArray(); expires = nextExpiry;
    }
    public void Dispose() => watcher.Dispose();
}
