using System.Collections.ObjectModel;
using System.Text.Json;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.FileManager.ServerDiscovery;
namespace PrimEx.SmartSave.Desktop.ViewModels;

public sealed class WatchedFolder : ObservableObject
{
    private bool selected;
    public string Label { get; }
    public string Path { get; }
    public bool Selected { get => selected; set => Set(ref selected, value); }
    public WatchedFolder(string label, string path, bool selected) { Label = label; Path = path; this.selected = selected; }
}
public sealed class AutomaticGuidanceViewModel : ObservableObject
{
    private readonly AppOptions original;
    private string roots, extensions, excludedFolders, error = "";
    private bool enabled, includeSubdirectories, startWithWindows, allFileTypes, ignoreDevelopmentProjects;
    public ObservableCollection<WatchedFolder> Folders { get; } = [];
    public bool Enabled { get => enabled; set => Set(ref enabled, value); }
    public bool IncludeSubdirectories { get => includeSubdirectories; set => Set(ref includeSubdirectories, value); }
    public bool StartWithWindows { get => startWithWindows; set => Set(ref startWithWindows, value); }
    public bool IgnoreDevelopmentProjects { get => ignoreDevelopmentProjects; set => Set(ref ignoreDevelopmentProjects, value); }
    public string ExcludedFolders { get => excludedFolders; set => Set(ref excludedFolders, value); }
    public bool AllFileTypes { get => allFileTypes; set { Set(ref allFileTypes, value); Changed(nameof(CustomFileTypesEnabled)); } }
    public bool CustomFileTypesEnabled => !AllFileTypes;
    public string ApprovedRoots { get => roots; set => Set(ref roots, value); }
    public string Extensions { get => extensions; set => Set(ref extensions, value); }
    public string Error { get => error; set => Set(ref error, value); }
    public AutomaticGuidanceViewModel(AppOptions options, IReadOnlyList<SuggestedFolder> suggestions)
    {
        original = options;
        enabled = options.Monitoring.Enabled;
        includeSubdirectories = options.Monitoring.IncludeSubdirectories;
        startWithWindows = options.StartWithWindows;
        ignoreDevelopmentProjects = options.Monitoring.IgnoreDevelopmentProjects;
        excludedFolders = string.Join(Environment.NewLine, options.Monitoring.ExcludedFolders);
        roots = string.Join(Environment.NewLine, options.CompanyServer.ApprovedRoots);
        allFileTypes = MonitoringFileTypes.IncludesAll(options.Monitoring.Extensions);
        extensions = allFileTypes ? ".pdf, .docx, .xlsx, .png, .jpg, .jpeg, .zip" : string.Join(", ", options.Monitoring.Extensions);
        foreach (var folder in suggestions)
            Folders.Add(new(folder.Label, folder.Path, options.Monitoring.Folders.Contains(folder.Path, StringComparer.OrdinalIgnoreCase)));
        foreach (var path in options.Monitoring.Folders)
            AddFolder(Path.GetFileName(path), path, true);
    }
    public void AddFolder(string label, string path, bool selected = true)
    {
        path = PathRules.NormalizeAbsolute(path);
        var prior = Folders.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (prior != null) { prior.Selected = selected; return; }
        Folders.Add(new(string.IsNullOrWhiteSpace(label) ? path : label, path, selected));
    }
    public AppOptions CreateOptions()
    {
        var result = JsonSerializer.Deserialize<AppOptions>(JsonSerializer.Serialize(original))!;
        result.CompanyServer.ApprovedRoots = ApprovedRoots.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        result.Monitoring.Enabled = Enabled;
        result.StartWithWindows = StartWithWindows;
        result.GuidanceSetupCompleted = true;
        result.Monitoring.IncludeSubdirectories = IncludeSubdirectories;
        result.Monitoring.IgnoreDevelopmentProjects = IgnoreDevelopmentProjects;
        result.Monitoring.ExcludedFolders = ExcludedFolders.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(PathRules.NormalizeAbsolute).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        result.Monitoring.Folders = Folders.Where(x => x.Selected).Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        result.Monitoring.Extensions = AllFileTypes ? ["*"] : Extensions.Split([',', ';', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.ToLowerInvariant()).Distinct().ToArray();
        if (!MonitoringFileTypes.IsValid(result.Monitoring.Extensions))
            throw new InvalidDataException("Select All completed file types, or enter file types such as .pdf, .xlsx, .png, .zip.");
        var errors = result.Validate(); if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        if (Enabled)
            foreach (var folder in result.Monitoring.Folders)
            {
                if (!Directory.Exists(folder)) throw new InvalidDataException("This selected folder is unavailable: " + folder + ". Uncheck it or select an existing local folder.");
                try { PathRules.RejectReparseAncestors(folder); }
                catch (IOException) { throw new InvalidDataException("This folder uses a link or cloud placeholder that is not supported yet: " + folder + ". Uncheck it and choose a direct local folder."); }
                if (new DriveInfo(Path.GetPathRoot(folder)!).DriveType == DriveType.Network) throw new InvalidDataException("This watch folder is a mapped network drive: " + folder + ". Select local folders to watch; company destinations can use network shares.");
            }
        return result;
    }
}
