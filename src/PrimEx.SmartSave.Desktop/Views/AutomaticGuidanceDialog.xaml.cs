using System.Windows;
using Microsoft.Win32;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Desktop.ViewModels;
using PrimEx.SmartSave.FileManager.ServerDiscovery;
namespace PrimEx.SmartSave.Desktop.Views;

public partial class AutomaticGuidanceDialog : Window
{
    private readonly AutomaticGuidanceViewModel model;
    private readonly Func<AppOptions, Task> apply;
    public AutomaticGuidanceDialog(AppOptions options, Func<AppOptions, Task> apply)
    {
        InitializeComponent(); this.apply = apply;
        model = new(options, KnownFolderService.GetSuggestions()); DataContext = model;
        Loaded += (_, _) => EnableBox.Focus();
    }
    private void AddFolder(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Select a local folder to watch", Multiselect = false };
        if (picker.ShowDialog(this) == true) model.AddFolder(Path.GetFileName(picker.FolderName), picker.FolderName);
    }
    private void BrowseRoot(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose your authorized company server root", Multiselect = false };
        if (picker.ShowDialog(this) == true) model.ApprovedRoots = string.Join(Environment.NewLine, model.ApprovedRoots.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Append(picker.FolderName).Distinct(StringComparer.OrdinalIgnoreCase));
    }
    private async void Apply(object sender, RoutedEventArgs e)
    {
        ApplyButton.IsEnabled = false; OptionsPanel.IsEnabled = false; model.Error = "";
        try
        {
            var updated = model.CreateOptions(); await apply(updated);
            if (IsVisible) DialogResult = true;
        }
        catch (InvalidDataException ex) { model.Error = ex.Message; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { model.Error = ex is UnauthorizedAccessException ? "These settings may be managed by your administrator, or the file is not writable. No permission changes were made." : "A selected path is unavailable or unsupported. Choose an existing local folder without links, or uncheck it. Finish the current operation before applying settings."; }
        finally { ApplyButton.IsEnabled = true; OptionsPanel.IsEnabled = true; }
    }
}
