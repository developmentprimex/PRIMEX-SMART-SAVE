using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Monitoring.Watchers;
public sealed class EligibleFilePolicy(AppOptions options)
{
    public bool IsEligible(string path)
    {
        try
        {
            path = PathRules.NormalizeAbsolute(path); string name = Path.GetFileName(path);
            if (name.StartsWith('.') || name.StartsWith('~') || path.Split('\\').Contains("PrimEx Smart Save Staging", StringComparer.OrdinalIgnoreCase)) return false;
            if (new[] { ".crdownload", ".part", ".partial", ".tmp", ".temp", ".download" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
            if (!options.Monitoring.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
            if (!options.Monitoring.Folders.Any(root => options.Monitoring.IncludeSubdirectories ? PathRules.IsWithin(path, root) : Path.GetDirectoryName(path)!.Equals(PathRules.NormalizeAbsolute(root), StringComparison.OrdinalIgnoreCase))) return false;
            if (options.CompanyServer.ApprovedRoots.Any(root => PathRules.IsWithin(path, root))) return false;
            if (!File.Exists(path)) return false;
            PathRules.RejectReparseAncestors(path);
            return (File.GetAttributes(path) & (FileAttributes.System | FileAttributes.Hidden | FileAttributes.Directory | FileAttributes.Temporary)) == 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
}
