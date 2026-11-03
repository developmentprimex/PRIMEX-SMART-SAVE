using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
namespace PrimEx.SmartSave.FileManager.ServerDiscovery;
public static class AgentPipeName
{
    public static string ForCurrentSession(string dataDirectory)
    {
        string identity = WindowsIdentity.GetCurrent().User!.Value + ":" + Process.GetCurrentProcess().SessionId + ":" + Path.GetFullPath(dataDirectory).TrimEnd('\\').ToUpperInvariant();
        return "PrimEx.SmartSave." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
    }
}
