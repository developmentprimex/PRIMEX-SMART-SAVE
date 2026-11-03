using System.Windows;
using Microsoft.Win32;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Desktop.Components;

public interface ICompanyFolderPicker
{
    Task<string?> PickAsync(Window owner, string? initialFolder = null);
}

// The supported Windows Shell folder picker supplies Explorer navigation/search,
// mapped drives, Details view and New folder without embedding or hooking Explorer.
public sealed class WindowsCompanyFolderPicker(IServerFolderService servers, IPathResolver resolver) : ICompanyFolderPicker
{
    public static OpenFolderDialog CreateDialog(IReadOnlyList<ServerRoot> roots, string? initialFolder = null)
    {
        var approved = roots.Where(x => x.Approved).ToArray();
        if (approved.Length == 0) throw new InvalidDataException("No approved company roots are configured. Add your company root in Automatic guidance first.");
        var dialog = new OpenFolderDialog
        {
            Title = "Files PX - choose a destination folder",
            Multiselect = false, AddToRecent = false,
            ClientGuid = new Guid("D16F6F02-F21C-43EE-948A-DF0E68E87B55"),
            InitialDirectory = initialFolder ?? approved[0].Path,
            DefaultDirectory = approved[0].Path
        };
        foreach (var root in approved) dialog.CustomPlaces.Add(new FileDialogCustomPlace(root.Path));
        return dialog;
    }
    public string ResolveApprovedFolder(string selected)
    {
        string path = resolver.Resolve(selected);
        if (!servers.IsApproved(path)) throw new UnauthorizedAccessException("Select a folder inside your configured company server roots.");
        return path;
    }
    public async Task<string?> PickAsync(Window owner, string? initialFolder = null)
    {
        var roots = await Task.Run(servers.GetRoots).WaitAsync(TimeSpan.FromSeconds(12));
        if (!owner.IsVisible) return null;
        if (initialFolder != null) initialFolder = ResolveApprovedFolder(initialFolder);
        var dialog = CreateDialog(roots, initialFolder);
        dialog.FolderOk += (_, e) =>
        {
            try { ResolveApprovedFolder(dialog.FolderName); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                e.Cancel = true;
                MessageBox.Show(owner, "Choose a folder inside the configured company server roots. To leave the document where it is, click Cancel.", "Choose a company folder", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        return dialog.ShowDialog(owner) == true ? ResolveApprovedFolder(dialog.FolderName) : null;
    }
}
