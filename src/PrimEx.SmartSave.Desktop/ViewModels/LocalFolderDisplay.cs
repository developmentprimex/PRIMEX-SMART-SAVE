namespace PrimEx.SmartSave.Desktop.ViewModels;

public static class LocalFolderDisplay
{
    public static string Name(string folder) => folder.TrimEnd('\\', '/').Split('\\', '/').LastOrDefault() is { Length: > 0 } name ? name : folder;
    public static string AccessText(string text) => text.Replace("_", "__");
}
