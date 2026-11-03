using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Monitoring.Watchers;
public sealed class EligibleFilePolicy(AppOptions options)
{
    private static readonly HashSet<string> CacheFolders = new(StringComparer.OrdinalIgnoreCase)
    { ".git", ".vs", ".cache", "node_modules", "bin", "obj", "AppData", "Cache", "Caches", "Code Cache", "GPUCache", "$RECYCLE.BIN", "System Volume Information" };
    public bool IsEligible(string path)
    {
        try
        {
            path = PathRules.NormalizeAbsolute(path); string name = Path.GetFileName(path);
            if (name.StartsWith('.') || name.StartsWith('~') || path.Split('\\').Contains("PrimEx Smart Save Staging", StringComparer.OrdinalIgnoreCase)) return false;
            if (name.Contains(".primex-backup-", StringComparison.OrdinalIgnoreCase) || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase)) return false;
            if (new[] { ".crdownload", ".part", ".partial", ".tmp", ".temp", ".download", ".filepart", ".lock", ".lck" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
            if (!MonitoringFileTypes.Matches(path, options.Monitoring.Extensions)) return false;
            var watchedRoot = options.Monitoring.Folders.Where(root => options.Monitoring.IncludeSubdirectories ? PathRules.IsWithin(path, root) : Path.GetDirectoryName(path)!.Equals(PathRules.NormalizeAbsolute(root), StringComparison.OrdinalIgnoreCase)).OrderBy(root => root.Length).FirstOrDefault();
            if (watchedRoot == null) return false;
            // Filter cache subfolders inside the selected scope, not unrelated
            // ancestors (a legitimate redirected folder can live under AppData).
            if (CacheFolders.Contains(Path.GetFileName(watchedRoot.TrimEnd('\\'))) || Path.GetRelativePath(watchedRoot, path).Split('\\').SkipLast(1).Any(CacheFolders.Contains)) return false;
            if (options.CompanyServer.ApprovedRoots.Any(root => PathRules.IsWithin(path, root))) return false;
            if (!File.Exists(path)) return false;
            PathRules.RejectReparseAncestors(path);
            return (File.GetAttributes(path) & (FileAttributes.System | FileAttributes.Hidden | FileAttributes.Directory | FileAttributes.Temporary)) == 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
}
