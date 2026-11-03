using System.Text.RegularExpressions;
namespace PrimEx.SmartSave.Core.Services;

public static class MonitoringFileTypes
{
    private static readonly string[][] LegacyDefaults =
    [
        [".pdf", ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".zip"],
        [".pdf", ".docx", ".docm", ".xlsx", ".xlsm", ".xlsb", ".pptx", ".pptm", ".doc", ".xls", ".csv", ".rtf", ".txt", ".zip"]
    ];
    public static bool IncludesAll(IReadOnlyCollection<string> extensions) => extensions.Count == 1 && extensions.Contains("*");
    public static bool IsValid(IReadOnlyCollection<string> extensions) => IncludesAll(extensions) ||
        extensions.Count > 0 && extensions.All(x => x != null && Regex.IsMatch(x, @"^\.[a-zA-Z0-9]{1,12}$"));
    public static bool Matches(string filePath, IReadOnlyCollection<string> extensions) => IncludesAll(extensions) || extensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
    public static bool IsLegacyDefault(IReadOnlyCollection<string> extensions) =>
        LegacyDefaults.Any(previous => extensions.Count == previous.Length && new HashSet<string>(previous, StringComparer.OrdinalIgnoreCase).SetEquals(extensions));
}
