using System.Windows;
using PrimEx.SmartSave.Desktop.Components;

namespace PrimEx.SmartSave.Desktop.Views;
public partial class EditorIntegrationDialog : Window
{
    private readonly EditorIntegrationService installer = new();
    public EditorIntegrationDialog() { InitializeComponent(); }
    private async void ConnectCode(object sender, RoutedEventArgs e) => await ConnectAsync(true);
    private async void ConnectStudio(object sender, RoutedEventArgs e) => await ConnectAsync(false);
    private async Task ConnectAsync(bool code)
    {
        CodeButton.IsEnabled = StudioButton.IsEnabled = false;
        StatusText.Text = code ? "Installing the VS Code link…" : "Opening the Visual Studio extension installer…";
        try
        {
            if (code) await installer.InstallVSCodeAsync(); else await installer.LaunchVisualStudioInstallerAsync();
            StatusText.Text = code ? "Link installed. Reload your VS Code windows once; every new local workspace will connect automatically." : "Complete the Visual Studio installer, then restart the editor once. No per-project exclusion is needed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception) { StatusText.Text = ex.Message; }
        finally { CodeButton.IsEnabled = StudioButton.IsEnabled = true; }
    }
}
