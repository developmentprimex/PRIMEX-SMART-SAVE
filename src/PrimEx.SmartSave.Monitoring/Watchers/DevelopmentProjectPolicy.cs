using System.Collections.Concurrent;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.FileManager.ServerDiscovery;

namespace PrimEx.SmartSave.Monitoring.Watchers;

// Detect projects by folder, not by file extension: README/PDF/image assets in a
// project should stay there, while the same formats in Downloads remain eligible.
public sealed class DevelopmentProjectPolicy
{
    private static readonly string[] MarkerDirectories = [".git", ".vscode", ".vs", ".idea", ".hg", ".svn"];
    private static readonly string[] MarkerFiles = [".git", "package.json", "pyproject.toml", "Cargo.toml", "go.mod", "composer.json"];
    private static readonly HashSet<string> MarkerExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".sln", ".slnx", ".csproj", ".fsproj", ".vbproj", ".code-workspace" };
    private readonly ConcurrentDictionary<string, (bool Project, DateTimeOffset Expires)> cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ContainerFolders = new(
        KnownFolderService.GetSuggestions().Select(x => x.Path.TrimEnd('\\'))
            .Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\'))
            .Append(Path.GetTempPath().TrimEnd('\\')), StringComparer.OrdinalIgnoreCase);

    public static bool IsMarkerName(string name) => MarkerDirectories.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        MarkerFiles.Contains(name, StringComparer.OrdinalIgnoreCase) || MarkerExtensions.Contains(Path.GetExtension(name));

    public void ObserveChange(string path)
    {
        if (IsMarkerName(Path.GetFileName(path))) cache.Clear();
    }

    public bool IsKnownProject(string directory)
    {
        for (string? current = directory; current != null && !IsBoundary(current); current = Path.GetDirectoryName(current))
            if (cache.TryGetValue(current, out var entry) && entry.Project && entry.Expires > DateTimeOffset.UtcNow) return true;
        return false;
    }

    public bool IsProject(string directory)
    {
        directory = PathRules.NormalizeAbsolute(directory);
        if (IsBoundary(directory)) return false;
        if (cache.TryGetValue(directory, out var prior) && prior.Expires > DateTimeOffset.UtcNow) return prior.Project;
        try
        {
            bool project = HasMarker(directory) || (Path.GetDirectoryName(directory) is { } parent && IsProject(parent));
            if (cache.Count >= 8192) cache.Clear();
            cache[directory] = (project, DateTimeOffset.UtcNow.AddSeconds(30));
            return project;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // Global VS Code settings or a downloaded manifest/workspace attachment must
    // not turn the entire profile, Downloads, Desktop or Documents into a project.
    private static bool IsBoundary(string directory) => ContainerFolders.Contains(directory.TrimEnd('\\')) ||
        directory.TrimEnd('\\').Equals(Path.GetPathRoot(directory)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static bool HasMarker(string directory)
    {
        if (MarkerDirectories.Any(name => Directory.Exists(Path.Combine(directory, name))) ||
            MarkerFiles.Any(name => File.Exists(Path.Combine(directory, name)))) return true;
        return Directory.EnumerateFiles(directory, "*", new EnumerationOptions
        {
            IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
        }).Any(path => MarkerExtensions.Contains(Path.GetExtension(path)));
    }
}
