using PrimEx.SmartSave.Core.Services;

namespace PrimEx.SmartSave.Monitoring.Watchers;

// Drive-wide guidance covers user documents, not OS/application maintenance.
// Root names are anchored to the drive, so C:\Work\Windows remains ordinary work.
public sealed class SystemDirectoryPolicy
{
    private static readonly HashSet<string> DriveRootFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "Windows.old", "Program Files", "Program Files (x86)", "ProgramData",
        "Recovery", "PerfLogs", "Temp", "tmp", "MSOCache", "Config.Msi",
        "$Windows.~BT", "$Windows.~WS", "$WinREAgent", "$RECYCLE.BIN", "System Volume Information"
    };
    private readonly string[] protectedRoots;

    // Extra roots allow relocated installations and disposable enumeration fixtures.
    // They extend, rather than replace, the actual Windows known-folder locations.
    public SystemDirectoryPolicy(IEnumerable<string>? additionalProtectedRoots = null)
    {
        protectedRoots = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        }.Concat(additionalProtectedRoots ?? []).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(PathRules.NormalizeAbsolute).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool IsExcluded(string path)
    {
        path = PathRules.NormalizeAbsolute(path);
        string drive = Path.GetPathRoot(path)!;
        string firstFolder = path[drive.Length..].Split('\\', 2)[0];
        return DriveRootFolders.Contains(firstFolder) || protectedRoots.Any(root => PathRules.IsWithin(path, root));
    }
}
