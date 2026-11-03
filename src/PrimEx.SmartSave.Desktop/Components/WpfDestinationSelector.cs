using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Desktop.Views;
namespace PrimEx.SmartSave.Desktop.Components;
public sealed class WpfDestinationSelector(IServerFolderService servers, IPathResolver resolver) : IDestinationSelector
{
    public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Integration == "Monitoring")
        {
            if (new PostSaveDialog(request.DisplayName) { Owner = Application.Current.MainWindow }.ShowDialog() != true)
                return Task.FromResult<DestinationSelection?>(null);
            // The user has already chosen Company Server. Ask only for the folder,
            // filename and any explicit conflict decision, without repeating the choice.
            var browser = new ServerBrowser(servers, request) { Owner = Application.Current.MainWindow };
            browser.ShowDialog(); return Task.FromResult(browser.MoveSelection);
        }
        var dialog = new SaveDialog(request, servers, resolver) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog(); return Task.FromResult(dialog.Selection);
    }
}
