using System.Windows;
using Microsoft.Win32;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Desktop.ViewModels;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class SaveDialog : Window
{
    private readonly SaveDialogViewModel model;
    private readonly IServerFolderService servers;
    public DestinationSelection? Selection { get; private set; }
    public SaveDialog(SaveRequest request, IServerFolderService servers, IPathResolver resolver)
    {
        InitializeComponent(); this.servers = servers; model = new(request, servers, resolver); DataContext = model;
        Loaded += (_, _) => CompanyOption.Focus();
    }
    private void BrowseOutside(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Save Outside Server — select a destination folder", Multiselect = false };
        if (dialog.ShowDialog(this) == true) model.Folder = dialog.FolderName;
    }
    private void BrowseServer(object sender, RoutedEventArgs e)
    {
        var browser = new ServerBrowser(servers) { Owner = this };
        if (browser.ShowDialog() == true) model.Folder = browser.SelectedFolder!;
    }
    private async void Confirm(object sender, RoutedEventArgs e)
    {
        if (model.Busy) return;
        ConfirmButton.IsEnabled = false;
        ChoicePanel.IsEnabled = false;
        try
        {
            if (!await model.ValidateAsync() || !IsVisible) return;
            // Capture the validated values before any further interaction.
            string folder = model.Folder, name = model.FileName;
            var kind = model.CompanySelected ? DestinationKind.CompanyServer : DestinationKind.OutsideServer;
            ConflictChoice conflict = ConflictChoice.Cancel;
            bool exists;
            try { exists = await Task.Run(() => File.Exists(Path.Combine(folder, name))).WaitAsync(TimeSpan.FromSeconds(12)); }
            catch (TimeoutException) { model.Error = "The destination stopped responding. Choose another location or save outside the server."; return; }
            if (!IsVisible) return;
            if (exists)
            {
                var choice = new ConflictDialog { Owner = this };
                if (choice.ShowDialog() != true) return;
                conflict = choice.Choice;
            }
            Selection = new(folder, name, kind, conflict); DialogResult = true;
        }
        finally { ConfirmButton.IsEnabled = true; ChoicePanel.IsEnabled = true; }
    }
}
