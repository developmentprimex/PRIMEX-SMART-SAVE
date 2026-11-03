using System.Runtime.InteropServices;
namespace PrimEx.SmartSave.FileManager.ServerDiscovery;

public sealed record SuggestedFolder(string Label, string Path);
public static class KnownFolderService
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);
    public static IReadOnlyList<SuggestedFolder> GetSuggestions()
    {
        List<SuggestedFolder> result = [];
        int error = SHGetKnownFolderPath(DownloadsId, 0x4000 /* DONT_VERIFY; never create or scan */, IntPtr.Zero, out var pointer);
        try { if (error == 0 && Marshal.PtrToStringUni(pointer) is { Length: > 0 } downloads) result.Add(new("Downloads", downloads)); }
        finally { if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer); }
        foreach (var (label, kind) in new[] { ("Desktop", Environment.SpecialFolder.DesktopDirectory), ("Documents", Environment.SpecialFolder.MyDocuments) })
        {
            string path = Environment.GetFolderPath(kind, Environment.SpecialFolderOption.DoNotVerify);
            if (path.Length > 0) result.Add(new(label, path));
        }
        return result.DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
