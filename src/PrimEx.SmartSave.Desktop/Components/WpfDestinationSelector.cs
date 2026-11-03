using System.Windows;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.Desktop.Views;
namespace PrimEx.SmartSave.Desktop.Components;
public sealed class WpfDestinationSelector(IServerFolderService servers, IPathResolver resolver, ICompanyFolderPicker picker, RememberedLocationStore? locations = null) : IDestinationSelector
{
    public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Integration == "Monitoring")
        {
            if (locations?.Contains(request.SourcePath) == true) return Task.FromResult<DestinationSelection?>(null);
            var prompt = new PostSaveDialog(request.DisplayName) { Owner = Application.Current.MainWindow };
            if (prompt.ShowDialog() != true)
            {
                if (prompt.KeepChosen && locations != null && !locations.Remember(request.SourcePath))
                    MessageBox.Show(Application.Current.MainWindow, "The document stays here, but this choice could not be remembered. You may be asked again after another save.", "PrimEx Smart Save", MessageBoxButton.OK, MessageBoxImage.Information);
                return Task.FromResult<DestinationSelection?>(null);
            }
            // The user has already chosen Company Server. Ask only for the folder,
            // filename and any explicit conflict decision, without repeating the choice.
            var browser = new CompanyMoveDialog(request, servers, picker) { Owner = Application.Current.MainWindow };
            browser.ShowDialog(); return Task.FromResult(browser.Selection);
        }
        var dialog = new SaveDialog(request, servers, resolver) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog(); return Task.FromResult(dialog.Selection);
    }
}
