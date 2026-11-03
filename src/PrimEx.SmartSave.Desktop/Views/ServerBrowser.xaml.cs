using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class ServerBrowser : Window
{
    private readonly IServerFolderService servers;
    private readonly bool moveMode;
    private int generation;
    public string? SelectedFolder { get; private set; }
    public DestinationSelection? MoveSelection { get; private set; }
    public ServerBrowser(IServerFolderService servers, SaveRequest? moveRequest = null)
    {
        InitializeComponent(); this.servers = servers;
        moveMode = moveRequest != null;
        if (moveRequest != null)
        {
            Title = "Move to Company Server"; Heading.Text = "Choose a company folder";
            Height = 640; MinHeight = 480; FileNamePanel.Visibility = Visibility.Visible;
            FileNameBox.Text = moveRequest.DisplayName;
            SelectButton.Content = "_Move here"; CancelButton.Content = "_Keep Current Location";
        }
        SelectButton.IsEnabled = false;
        Loaded += async (_, _) => {
            try { Roots.ItemsSource = await Task.Run(() => servers.GetRoots().Where(x => x.Approved).ToArray()).WaitAsync(TimeSpan.FromSeconds(10)); if (Roots.Items.Count > 0) Roots.SelectedIndex = 0; else Error.Text = moveMode ? "No approved company roots are configured. Configure a root in Automatic guidance, or Keep Current Location." : "No approved roots configured. Open Settings to add company UNC roots. Save Outside Server remains available."; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException) { Error.Text = "Company roots are currently unavailable."; }
        };
    }
    private async Task Navigate(string path)
    {
        int thisGeneration = ++generation; SelectButton.IsEnabled = false; Error.Text = "Loading folders…";
        try
        {
            var folders = await servers.EnumerateAsync(path).WaitAsync(TimeSpan.FromSeconds(10));
            if (thisGeneration != generation || !IsVisible) return;
            CurrentPath.Text = path; Folders.ItemsSource = folders; Error.Text = ""; SelectButton.IsEnabled = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or TimeoutException or ArgumentException)
        { if (thisGeneration == generation) Error.Text = moveMode ? "This company folder is unavailable or access was denied. Choose another company folder, or Keep Current Location." : "This folder is unavailable or access was denied. You can cancel and save outside the server."; }
    }
    private async void RootChanged(object sender, SelectionChangedEventArgs e) { if (Roots.SelectedItem is ServerRoot root) await Navigate(root.Path); }
    private async void OpenFolder(object sender, MouseButtonEventArgs e) { if (Folders.SelectedItem is string path) await Navigate(path); }
    private async void FolderKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter && Folders.SelectedItem is string path) { e.Handled = true; await Navigate(path); } }
    private async void GoUp(object sender, RoutedEventArgs e)
    {
        if (Roots.SelectedItem is not ServerRoot root) return;
        var parent = Path.GetDirectoryName(CurrentPath.Text);
        if (parent != null && PathRules.IsWithin(parent, root.Path)) await Navigate(parent);
    }
    private async void NewFolder(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CurrentPath.Text)) return;
        var prompt = new FolderNameDialog { Owner = this };
        if (prompt.ShowDialog() != true) return;
        try
        {
            await servers.CreateDirectoryAsync(CurrentPath.Text, prompt.FolderName).WaitAsync(TimeSpan.FromSeconds(10));
            await Navigate(CurrentPath.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or TimeoutException) { Error.Text = "Could not create the folder. Check the name and your permissions."; }
    }
    private async void Select(object sender, RoutedEventArgs e)
    {
        if (CurrentPath.Text.Length == 0) return;
        if (!moveMode) { SelectedFolder = CurrentPath.Text; DialogResult = true; return; }
        string folder = CurrentPath.Text, name = FileNameBox.Text;
        SelectButton.IsEnabled = false; BrowserPanel.IsEnabled = false; Folders.IsEnabled = false; Error.Text = "Checking destination…";
        try
        {
            PathRules.ValidateFileName(name);
            if (!servers.IsApproved(folder)) throw new UnauthorizedAccessException();
            var status = await servers.CheckAsync(folder, true).WaitAsync(TimeSpan.FromSeconds(12));
            if (!IsVisible) return;
            if (!status.Available) { Error.Text = "This company folder is unavailable or not writable. Choose another folder, or Keep Current Location."; return; }
            bool exists = await Task.Run(() => File.Exists(Path.Combine(folder, name))).WaitAsync(TimeSpan.FromSeconds(12));
            if (!IsVisible) return;
            ConflictChoice conflict = ConflictChoice.Cancel;
            if (exists)
            {
                var choice = new ConflictDialog { Owner = this };
                if (choice.ShowDialog() != true) { Error.Text = "Choose a different filename or folder, or Keep Current Location."; return; }
                conflict = choice.Choice;
            }
            MoveSelection = new(folder, name, DestinationKind.CompanyServer, conflict);
            SelectedFolder = folder; DialogResult = true;
        }
        catch (ArgumentException) { Error.Text = "Enter a valid filename."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
        { if (IsVisible) Error.Text = "The company folder is unavailable or access was denied. Choose another folder, or Keep Current Location."; }
        finally { BrowserPanel.IsEnabled = true; Folders.IsEnabled = true; SelectButton.IsEnabled = true; }
    }
}
