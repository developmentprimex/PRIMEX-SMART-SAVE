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
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prompt = Prepare(new PostSaveDialog(request.DisplayName, request.SourcePath));
                if (prompt.ShowDialog() != true)
                {
                    if (!prompt.KeepChosen) throw new OperationCanceledException("Destination choice was interrupted.");
                    RememberKeep(request.SourcePath);
                    return Task.FromResult<DestinationSelection?>(null);
                }
                var browser = Prepare(new CompanyMoveDialog(request, servers, picker, initialMode, returnToChoices: true));
                browser.ShowDialog();
                if (browser.Selection != null) return Task.FromResult<DestinationSelection?>(browser.Selection);
                if (Application.Current.Dispatcher.HasShutdownStarted || Application.Current is App { IsExiting: true }) throw new OperationCanceledException("Destination choice was interrupted.");
                // Back/cancelling the native picker is navigation, not a local-save choice.
            }
        }
        string? localFolder = request.Integration == "Browser" && options?.Browser.DownloadsFolder is { Length: > 0 } downloads ? downloads : null;
        var dialog = Prepare(new SaveDialog(request, servers, resolver, initialMode, localFolder));
        dialog.ShowDialog();
        if (!dialog.KeepChosen && dialog.Selection == null) throw new OperationCanceledException("Destination choice was interrupted.");
        if (dialog.KeepChosen) RememberKeep(request.SourcePath);
        return Task.FromResult(dialog.Selection);
    }
    private void RememberKeep(string path)
    {
        if (locations == null || locations.Remember(path)) return;
        const string message = "The document stays here, but this choice could not be remembered. You may be asked again after another save.";
        if (VisibleOwner is { } owner) MessageBox.Show(owner, message, "PrimEx Smart Save", MessageBoxButton.OK, MessageBoxImage.Information);
        else MessageBox.Show(message, "PrimEx Smart Save", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
