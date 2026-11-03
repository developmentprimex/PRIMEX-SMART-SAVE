using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.FileManager.ServerDiscovery;

public sealed class ServerFolderService(CompanyServerOptions options, IPathResolver resolver) : IServerFolderService
{
    public Task<string> CreateDirectoryAsync(string parent, string name, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        PathRules.ValidateFileName(name); parent = resolver.Resolve(parent);
        if (!IsApproved(parent)) throw new UnauthorizedAccessException("Choose an approved parent folder.");
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Parent folder is unavailable.");
        var path = Path.Combine(parent, name); PathRules.RejectReparseAncestors(path);
        Directory.CreateDirectory(path); return path;
    }, cancellationToken);
    public bool IsApproved(string path) => options.ApprovedRoots.Any(root => PathRules.IsWithin(resolver.Resolve(path), resolver.Resolve(root)));
    public IReadOnlyList<ServerRoot> GetRoots()
    {
        List<ServerRoot> roots = options.ApprovedRoots.Select(x => new ServerRoot(resolver.Resolve(x), x, true)).ToList();
        if (options.DiscoverMappedDrives)
            foreach (var drive in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Network))
                try
                {
                    var unc = resolver.Resolve(drive.Name);
                    if (!roots.Any(x => x.Path.Equals(unc, StringComparison.OrdinalIgnoreCase))) roots.Add(new(unc, drive.Name + " → " + unc, IsApproved(unc)));
                }
                catch (IOException) { }
        return roots;
    }
    public Task<IReadOnlyList<string>> EnumerateAsync(string path, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<string>>(() =>
    {
        if (!IsApproved(path)) throw new UnauthorizedAccessException("Choose a folder inside a configured approved root.");
        PathRules.RejectReparseAncestors(path);
        return Directory.EnumerateDirectories(path, "*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System }).Take(2000).Order().ToArray();
    }, cancellationToken);
    public Task<DirectoryStatus> CheckAsync(string path, bool probeWrite, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            path = PathRules.NormalizeAbsolute(path); PathRules.RejectReparseAncestors(path);
            if (!Directory.Exists(path)) return new DirectoryStatus(false, "Folder unavailable. Check your network connection or choose Save Outside Server.");
            if (probeWrite)
            {
                // Only invoked for a destination explicitly selected by the user. Uses current-user ACLs.
                using var probe = new FileStream(Path.Combine(path, ".primex-write-test-" + Guid.NewGuid().ToString("N")), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            return new DirectoryStatus(true, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { return new DirectoryStatus(false, e is UnauthorizedAccessException ? "Access denied. Select a folder you can write to." : "Folder unavailable or unsupported. Choose another destination."); }
    }, cancellationToken);
}
