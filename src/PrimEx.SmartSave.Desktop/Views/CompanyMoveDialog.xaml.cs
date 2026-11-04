using System.Windows;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.Desktop.Components;
using PrimEx.SmartSave.Desktop.ViewModels;
namespace PrimEx.SmartSave.Desktop.Views;

public partial class CompanyMoveDialog : Window
{
    private readonly IServerFolderService servers;
    private readonly ICompanyFolderPicker picker;
    private readonly TransferChoiceViewModel choice;
    private bool busy;
    public DestinationSelection? Selection { get; private set; }
    public CompanyMoveDialog(SaveRequest request, IServerFolderService servers, ICompanyFolderPicker picker, TransferMode initialMode = TransferMode.Move, bool returnToChoices = false)
    {
        InitializeComponent(); this.servers = servers; this.picker = picker;
        choice = new(initialMode); DataContext = choice;
        if (returnToChoices) { CancelButton.Content = "_Back"; RequiredDestinationChoice.HideCloseButton(this); }
        FileNameBox.Text = request.DisplayName;
        Loaded += async (_, _) => await SelectFolderAsync(true);
    }
    private async Task SelectFolderAsync(bool initial)
    {
        if (busy) return;
        busy = true; MoveButton.IsEnabled = false; DetailsPanel.IsEnabled = false; Error.Text = "Opening Windows folder browser…";
        try
        {
            string? path = await picker.PickAsync(this, string.IsNullOrEmpty(FolderBox.Text) ? null : FolderBox.Text);
            if (!IsVisible) return;
            if (path == null) { Error.Text = ""; if (initial) DialogResult = false; return; }
            if (!servers.IsApproved(path)) throw new UnauthorizedAccessException();
            FolderBox.Text = path; Error.Text = "";
        }
        catch (InvalidDataException ex) { if (IsVisible) Error.Text = ex.Message; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or TimeoutException)
        { if (IsVisible) Error.Text = "The company folder browser is unavailable or access was denied. Choose another authorized folder, or cancel."; }
        finally { busy = false; DetailsPanel.IsEnabled = true; MoveButton.IsEnabled = !string.IsNullOrEmpty(FolderBox.Text); }
    }
    private async void ChooseAnotherFolder(object sender, RoutedEventArgs e) => await SelectFolderAsync(false);
    private void Back(object sender, RoutedEventArgs e) => DialogResult = false;
    private async void ConfirmMove(object sender, RoutedEventArgs e)
    {
        if (busy || string.IsNullOrEmpty(FolderBox.Text)) return;
        busy = true; MoveButton.IsEnabled = false; DetailsPanel.IsEnabled = false; Error.Text = "Checking destination…";
        try
        {
            string folder = FolderBox.Text, name = FileNameBox.Text;
            var mode = choice.Mode;
            PathRules.ValidateFileName(name);
            if (!servers.IsApproved(folder)) throw new UnauthorizedAccessException();
            var status = await servers.CheckAsync(folder, true).WaitAsync(TimeSpan.FromSeconds(12));
            if (!IsVisible) return;
            if (!status.Available) { Error.Text = "This folder is unavailable or not writable. Choose another company folder, or cancel."; return; }
            bool exists = await Task.Run(() => File.Exists(Path.Combine(folder, name))).WaitAsync(TimeSpan.FromSeconds(12));
            if (!IsVisible) return;
            ConflictChoice conflict = ConflictChoice.Cancel;
            if (exists)
            {
                var choice = new ConflictDialog { Owner = this };
                if (choice.ShowDialog() != true) { Error.Text = "Choose a different filename or folder, or cancel."; return; }
                conflict = choice.Choice;
            }
            Selection = new(folder, name, DestinationKind.CompanyServer, conflict, mode); DialogResult = true;
        }
        catch (ArgumentException) { if (IsVisible) Error.Text = "Enter a valid filename."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
        { if (IsVisible) Error.Text = "The company folder is unavailable or access was denied. Choose another company folder, or cancel."; }
        finally { busy = false; DetailsPanel.IsEnabled = true; MoveButton.IsEnabled = !string.IsNullOrEmpty(FolderBox.Text); }
    }
}
