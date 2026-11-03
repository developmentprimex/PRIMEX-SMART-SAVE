using System.Text.Json;
using System.Text.RegularExpressions;
namespace PrimEx.SmartSave.Core.Models;

public sealed class AppOptions
{
    public CompanyServerOptions CompanyServer { get; set; } = new();
    public SmartSaveOptions SmartSave { get; set; } = new();
    public BrowserOptions Browser { get; set; } = new();
    public OfficeOptions Office { get; set; } = new();
    public MonitoringOptions Monitoring { get; set; } = new();
    public LoggingOptions Logging { get; set; } = new();
    public string UiLanguage { get; set; } = "en";
    public bool StartWithWindows { get; set; } = true;
    public bool GuidanceSetupCompleted { get; set; }
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (CompanyServer == null || SmartSave == null || Browser == null || Office == null || Monitoring == null || Logging == null)
            return ["Configuration sections cannot be null."];
        if (CompanyServer.ApprovedRoots == null || Browser.AllowedExtensionIds == null || Browser.DownloadsFolder == null || Monitoring.Folders == null || Monitoring.Extensions == null || CompanyServer.ApprovedRoots.Any(x => x == null) || Browser.AllowedExtensionIds.Any(x => x == null) || Monitoring.Folders.Any(x => x == null) || Monitoring.Extensions.Any(x => x == null))
            return ["Configuration collections and folder values cannot be null."];
        if (Monitoring.QuietSeconds is < 1 or > 60) errors.Add("Monitoring.QuietSeconds must be between 1 and 60.");
        if (!SmartSave.AllowOutsideServer) errors.Add("Outside-server saving must remain available in this edition.");
        if (CompanyServer.PreferredDestination != "CompanyServer") errors.Add("PreferredDestination must be CompanyServer.");
        if (UiLanguage != "en") errors.Add("Only the en UI language is currently implemented.");
        if (Logging.MaxBytes < 4096 || Logging.RetainedFiles is < 1 or > 30) errors.Add("Invalid log rotation limits.");
        if (Logging.Level is not ("Information" or "Error" or "None")) errors.Add("Logging.Level must be Information, Error or None.");
        foreach (var id in Browser.AllowedExtensionIds)
            if (!Regex.IsMatch(id, "^[a-p]{32}$")) errors.Add("Extension IDs must contain exactly 32 letters a-p.");
        foreach (var root in CompanyServer.ApprovedRoots.Concat(Monitoring.Folders).Append(Browser.DownloadsFolder).Where(x => x.Length > 0))
            try { Services.PathRules.NormalizeAbsolute(root); } catch (ArgumentException) { errors.Add("Configured folders must be absolute Windows or UNC paths without traversal."); }
        if (Browser.Enabled && Browser.DownloadsFolder.Length == 0) errors.Add("Browser.DownloadsFolder is required when browser integration is enabled.");
        if (Monitoring.Enabled && Monitoring.Folders.Length == 0) errors.Add("Monitoring requires explicit folders.");
        if (Monitoring.Folders.Any(x => x.StartsWith(@"\\") || string.Equals(Path.GetPathRoot(x)?.TrimEnd('\\'), x.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) errors.Add("Monitor named local directories, not network roots or entire drives.");
        if (Office.Enabled) errors.Add("Office interception is not enabled in this candidate; see docs/office.md.");
        return errors;
    }
    public static AppOptions Load(string path)
    {
        var options = JsonSerializer.Deserialize<AppOptions>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException("Empty configuration.");
        var errors = options.Validate();
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return options;
    }
}
public sealed class CompanyServerOptions
{
    public string PreferredDestination { get; set; } = "CompanyServer";
    public string[] ApprovedRoots { get; set; } = [];
    public bool DiscoverMappedDrives { get; set; } = true;
}
public sealed class SmartSaveOptions
{
    public bool AllowOutsideServer { get; set; } = true;
    public bool ShowForSupportedManualSaves { get; set; } = true;
    public bool ShowForSupportedDownloads { get; set; } = true;
    public bool IgnoreBackgroundAutosaves { get; set; } = true;
}
public sealed class BrowserOptions
{
    public bool Enabled { get; set; }
    public string DownloadsFolder { get; set; } = "";
    public string[] AllowedExtensionIds { get; set; } = [];
    public string StagingRoot => Path.Combine(DownloadsFolder, "PrimEx Smart Save Staging");
}
public sealed class OfficeOptions { public bool Enabled { get; set; } }
public sealed class MonitoringOptions
{
    public bool Enabled { get; set; }
    public string[] Folders { get; set; } = [];
    public bool IncludeSubdirectories { get; set; }
    public string[] Extensions { get; set; } = [".pdf", ".docx", ".docm", ".xlsx", ".xlsm", ".xlsb", ".pptx", ".pptm", ".doc", ".xls", ".csv", ".rtf", ".txt", ".zip"];
    public int QuietSeconds { get; set; } = 3;
}
public sealed class LoggingOptions
{
    public string Level { get; set; } = "Information";
    public long MaxBytes { get; set; } = 1_048_576;
    public int RetainedFiles { get; set; } = 5;
}
