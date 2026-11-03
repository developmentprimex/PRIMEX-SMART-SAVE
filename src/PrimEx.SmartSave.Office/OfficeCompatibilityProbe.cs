using Microsoft.Win32;
namespace PrimEx.SmartSave.Office;
public sealed record OfficeInstallation(string? ProductIds, string? Version, string? Platform, bool InterceptionVerified);
// Read-only diagnostics, not an Office save interceptor or installed add-in.
public static class OfficeCompatibilityProbe
{
    public static OfficeInstallation Inspect()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var office = machine.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration");
            if (office != null) return new(office.GetValue("ProductReleaseIds") as string, office.GetValue("VersionToReport") as string, office.GetValue("Platform") as string, false);
        }
        return new(null, null, null, false);
    }
}
