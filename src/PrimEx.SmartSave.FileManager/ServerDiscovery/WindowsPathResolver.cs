using System.Runtime.InteropServices;
using System.Text;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.FileManager.ServerDiscovery;

public sealed class WindowsPathResolver : IPathResolver
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);
    public string Resolve(string path)
    {
        path = PathRules.NormalizeAbsolute(path);
        if (path.StartsWith(@"\\")) return path;
        int length = 32768;
        var buffer = new StringBuilder(length);
        int error = WNetGetConnection(path[..2], buffer, ref length);
        if (error == 0) return PathRules.NormalizeAbsolute(buffer + path[2..]);
        if (error is 2250 or 1200) return path; // local or unmapped drive
        throw new IOException("Mapped drive resolution failed (Windows error " + error + ").");
    }
}
