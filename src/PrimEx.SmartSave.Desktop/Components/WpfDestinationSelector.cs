using System.Windows;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.Desktop.Views;
using PrimEx.SmartSave.Monitoring.Watchers;
namespace PrimEx.SmartSave.Desktop.Components;
public sealed class WpfDestinationSelector(IServerFolderService servers, IPathResolver resolver, ICompanyFolderPicker picker, RememberedLocationStore? locations = null, AppOptions? options = null, EditorWorkspaceRegistry? editorWorkspaces = null) : IDestinationSelector
{
    private static Window? VisibleOwner => Application.Current.MainWindow is { IsVisible: true, WindowState: not WindowState.Minimized } main ? main : null;
    private static T Prepare<T>(T dialog) where T : Window
    {
        // Tray-triggered prompts must not restore/show the main queue behind them.
        // A standalone dialog still needs its own taskbar/Alt+Tab entry.
        dialog.Owner = VisibleOwner;
        dialog.WindowStartupLocation = dialog.Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        dialog.ShowInTaskbar = dialog.Owner == null;
        return dialog;
    }
    public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default, TransferMode initialMode = TransferMode.Move)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Integration == "Monitoring")
        {
            // Recheck queued requests too: a project marker or exclusion may have
            // appeared after the watcher event, or this request predates the upgrade.
            if (options != null && new EligibleFilePolicy(options, editorWorkspaces).IsSuppressed(request.SourcePath)) return Task.FromResult<DestinationSelection?>(null);
            if (locations?.Contains(request.SourcePath) == true) return Task.FromResult<DestinationSelection?>(null);
            var prompt = Prepare(new PostSaveDialog(request.DisplayName));
            if (prompt.ShowDialog() != true)
            {
                if (prompt.KeepChosen && locations != null && !locations.Remember(request.SourcePath))
                {
                    const string message = "The document stays here, but this choice could not be remembered. You may be asked again after another save.";
                    if (VisibleOwner is { } owner) MessageBox.Show(owner, message, "PrimEx Smart Save", MessageBoxButton.OK, MessageBoxImage.Information);
                    else MessageBox.Show(message, "PrimEx Smart Save", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                return Task.FromResult<DestinationSelection?>(null);
            }
            // The user has already chosen Company Server. Ask only for the folder,
            // filename and any explicit conflict decision, without repeating the choice.
            var browser = Prepare(new CompanyMoveDialog(request, servers, picker, initialMode));
            browser.ShowDialog(); return Task.FromResult(browser.Selection);
        }
        var dialog = Prepare(new SaveDialog(request, servers, resolver, initialMode));
        dialog.ShowDialog(); return Task.FromResult(dialog.Selection);
    }
}
