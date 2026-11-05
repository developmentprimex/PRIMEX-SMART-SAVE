using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Monitoring.Watchers;
public sealed class EligibleFilePolicy(AppOptions options, EditorWorkspaceRegistry? editorWorkspaces = null, SystemDirectoryPolicy? protectedDirectories = null)
{
    private readonly DevelopmentProjectPolicy projects = new();
    private readonly SystemDirectoryPolicy systemDirectories = protectedDirectories ?? new();
    public void ObserveChange(string path)
    {
        if (!DevelopmentProjectPolicy.IsMarkerName(Path.GetFileName(path))) return;
        if (systemDirectories.IsExcluded(path)) return;
        var root = options.Monitoring.Folders.Where(root => PathRules.IsWithin(path, root)).OrderBy(root => root.Length).FirstOrDefault();
        // Dependency installs can create thousands of package.json files inside
        // ignored trees; they must not repeatedly invalidate real project roots.
        if (root != null && Path.GetRelativePath(root, path).Split('\\').SkipLast(1).Any(part => CacheFolders.Contains(part) || part.Equals("PrimEx Smart Save Staging", StringComparison.OrdinalIgnoreCase))) return;
        projects.ObserveChange(path);
    }
    public bool IsSuppressed(string path, bool directory = false) =>
        systemDirectories.IsExcluded(path) ||
        options.Monitoring.ExcludedFolders.Any(root => PathRules.IsWithin(path, root)) ||
        options.Monitoring.IgnoreDevelopmentProjects && (editorWorkspaces?.Contains(path) == true || projects.IsProject(directory ? path : Path.GetDirectoryName(path)!));
    private static readonly HashSet<string> CacheFolders = new(StringComparer.OrdinalIgnoreCase)
    { ".git", ".vs", ".cache", "node_modules", "bin", "obj", "AppData", "Cache", "Caches", "Code Cache", "GPUCache", "$RECYCLE.BIN", "System Volume Information" };
    private static readonly HashSet<string> TemporaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".crdownload", ".part", ".partial", ".tmp", ".temp", ".download", ".filepart", ".lock", ".lck" };
    // Cheap event filtering without touching the disk or opening files.
    public bool CanBeEligible(string path)
    {
        try
        {
            path = PathRules.NormalizeAbsolute(path); string name = Path.GetFileName(path);
            if (systemDirectories.IsExcluded(path)) return false;
            if (name.StartsWith('.') || name.StartsWith('~') || path.Split('\\').Contains("PrimEx Smart Save Staging", StringComparer.OrdinalIgnoreCase)) return false;
            if (name.Contains(".primex-backup-", StringComparison.OrdinalIgnoreCase) || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)) return false;
            if (TemporaryExtensions.Contains(Path.GetExtension(path))) return false;
            if (!MonitoringFileTypes.Matches(path, options.Monitoring.Extensions)) return false;
            var watchedRoot = options.Monitoring.Folders.Where(root => options.Monitoring.IncludeSubdirectories ? PathRules.IsWithin(path, root) : Path.GetDirectoryName(path)!.Equals(PathRules.NormalizeAbsolute(root), StringComparison.OrdinalIgnoreCase)).OrderBy(root => root.Length).FirstOrDefault();
            if (watchedRoot == null) return false;
            // Filter cache subfolders inside the selected scope, not unrelated
            // ancestors (a legitimate redirected folder can live under AppData).
            if (CacheFolders.Contains(Path.GetFileName(watchedRoot.TrimEnd('\\'))) || Path.GetRelativePath(watchedRoot, path).Split('\\').SkipLast(1).Any(CacheFolders.Contains)) return false;
            if (options.CompanyServer.ApprovedRoots.Any(root => PathRules.IsWithin(path, root))) return false;
            if (options.Monitoring.ExcludedFolders.Any(root => PathRules.IsWithin(path, root))) return false;
            if (options.Monitoring.IgnoreDevelopmentProjects && editorWorkspaces?.Contains(path) == true) return false;
            if (options.Monitoring.IgnoreDevelopmentProjects && projects.IsKnownProject(Path.GetDirectoryName(path)!)) return false;
            return true;
        }
        catch (ArgumentException) { return false; }
    }
    public bool IsEligible(string path)
    {
        if (!CanBeEligible(path)) return false;
        try
        {
            if (!File.Exists(path)) return false;
            PathRules.RejectReparseAncestors(path);
            if (IsSuppressed(path)) return false;
            return (File.GetAttributes(path) & (FileAttributes.System | FileAttributes.Hidden | FileAttributes.Directory | FileAttributes.Temporary)) == 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    public IEnumerable<string> EnumerateExistingFiles(string folder, CancellationToken cancellationToken = default)
    {
        folder = PathRules.NormalizeAbsolute(folder);
        cancellationToken.ThrowIfCancellationRequested();
        PathRules.RejectReparseAncestors(folder);
        if (IsExcludedDirectory(folder)) yield break;
        var directories = new Stack<string>(); directories.Push(folder);
        var enumeration = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary
        };
        while (directories.TryPop(out string? current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerator<FileSystemInfo> entries;
            try { entries = new DirectoryInfo(current).EnumerateFileSystemInfos("*", enumeration).GetEnumerator(); }
            catch (Exception ex) when (current != folder && ex is IOException or UnauthorizedAccessException) { continue; }
            using var iterator = entries;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileSystemInfo entry;
                try
                {
                    if (!iterator.MoveNext()) break;
                    entry = iterator.Current;
                }
                // A child may disappear or lose access after it was queued. Keep
                // indexing its neighbors; failure of the selected root still propagates.
                catch (Exception ex) when (current != folder && ex is IOException or UnauthorizedAccessException) { break; }
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    // Prune entire cache/company/staging trees before enumeration.
                    if (options.Monitoring.IncludeSubdirectories && !IsExcludedDirectory(entry.FullName)) directories.Push(entry.FullName);
                }
                else if (CanBeEligible(entry.FullName))
                {
                    // Enumeration supplies cached attributes. Ancestors were checked
                    // once at the root and reparse directories are never descended.
                    yield return entry.FullName;
                }
            }
        }
    }
    private bool IsExcludedDirectory(string path) =>
        CacheFolders.Contains(Path.GetFileName(path)) ||
        Path.GetFileName(path).Equals("PrimEx Smart Save Staging", StringComparison.OrdinalIgnoreCase) ||
        options.CompanyServer.ApprovedRoots.Any(root => PathRules.IsWithin(path, root)) || IsSuppressed(path, directory: true);
}
