using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Desktop.ViewModels;
public sealed class SaveDialogViewModel(SaveRequest request, IServerFolderService servers, IPathResolver resolver, TransferMode initialMode = TransferMode.Move, string? initialLocalFolder = null) : ObservableObject
{
    public TransferChoiceViewModel TransferChoice { get; } = new(initialMode);
    private bool company = true, busy;
    private string folder = "", fileName = request.DisplayName, error = "";
    private string localFolder = initialLocalFolder ?? Path.GetDirectoryName(request.SourcePath)!;
    public string LocalLabel => LocalFolderDisplay.Name(localFolder);
    public string LocalAccessLabel => "_" + LocalFolderDisplay.AccessText(LocalLabel);
    public string LocalFolder => localFolder;
    public string FileName { get => fileName; set => Set(ref fileName, value); }
    public string Folder { get => folder; set { Set(ref folder, value); if (!company && !string.IsNullOrWhiteSpace(value)) { localFolder = value; Changed(nameof(LocalLabel)); Changed(nameof(LocalAccessLabel)); Changed(nameof(LocalFolder)); } } }
    public bool CompanySelected { get => company; set { if (company == value) return; Set(ref company, value); Folder = value ? "" : localFolder; Error = ""; Changed(nameof(OutsideSelected)); } }
    public bool OutsideSelected { get => !company; set { if (value) CompanySelected = false; } }
    public bool Busy { get => busy; set => Set(ref busy, value); }
    public string Error { get => error; set => Set(ref error, value); }
    public string Source => request.SourcePath;
    public async Task<bool> ValidateAsync()
    {
        Busy = true; Error = "";
        try
        {
            PathRules.ValidateFileName(FileName);
            Folder = resolver.Resolve(Folder);
            if (CompanySelected && !servers.IsApproved(Folder)) { Error = "Choose an approved Files PX folder. Configure roots in Settings, or choose the local destination."; return false; }
            var status = await servers.CheckAsync(Folder, true).WaitAsync(TimeSpan.FromSeconds(12));
            Error = status.Error ?? "";
            return status.Available;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or TimeoutException)
        { Error = e is TimeoutException ? "The server is taking too long to respond. You can save outside the server." : "Invalid or unavailable destination. Check the folder and filename."; return false; }
        finally { Busy = false; }
    }
}
