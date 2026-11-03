namespace PrimEx.SmartSave.Core.Services;

public static class PathRules
{
    public static string NormalizeAbsolute(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\0', '"', '<', '>', '|', '?', '*']) >= 0)
            throw new ArgumentException("Invalid path.");
        value = value.Replace('/', '\\');
        if (value.StartsWith(@"\\?\") || value.StartsWith(@"\\.\") || value.Split('\\').Any(x => x is "." or ".." || (x.Length > 0 && (x.EndsWith(' ') || x.EndsWith('.')))))
            throw new ArgumentException("Device paths, traversal and ambiguous path components are not permitted.");
        bool drive = value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] == '\\';
        bool unc = value.StartsWith(@"\\") && value.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length >= 2;
        if ((!drive && !unc) || value[(drive ? 2 : 0)..].Contains(':')) throw new ArgumentException("An absolute drive or UNC share path is required.");
        foreach (string component in value.Split('\\', StringSplitOptions.RemoveEmptyEntries).Skip(drive ? 1 : 0)) ValidateFileName(component);
        return value.TrimEnd('\\') + (drive && value.Length == 3 ? "\\" : "");
    }
    public static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny(['\\', '/', ':', '\0', '<', '>', '"', '|', '?', '*']) >= 0 || name.Any(char.IsControl))
            throw new ArgumentException("Invalid filename.");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789¹²³".Contains(stem[3])))
            throw new ArgumentException("Reserved Windows filename.");
    }
    public static bool IsWithin(string path, string root)
    {
        path = NormalizeAbsolute(path); root = NormalizeAbsolute(root);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }
    public static void RejectReparseAncestors(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse points are not permitted for managed transfers. Choose a direct path.");
    }
}
