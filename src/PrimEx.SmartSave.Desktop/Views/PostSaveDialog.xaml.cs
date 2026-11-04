using System.Windows;
using System.Windows.Input;
using System.Windows.Automation;
using PrimEx.SmartSave.Desktop.Components;
using PrimEx.SmartSave.Desktop.ViewModels;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class PostSaveDialog : Window
{
    public bool KeepChosen { get; private set; }
    private bool companyChosen;
    public PostSaveDialog(string name, string? sourcePath = null)
    {
        InitializeComponent(); FileLabel.Text = name;
        string folder = sourcePath == null ? Environment.CurrentDirectory : Path.GetDirectoryName(sourcePath)!;
        string label = LocalFolderDisplay.Name(folder);
        Title = label + " or Files PX";
        LocalLocation.Text = folder;
        KeepLabel.Text = "_" + LocalFolderDisplay.AccessText(label);
        ConfirmationLabel.Text = "_Vazhdo me " + LocalFolderDisplay.AccessText(label);
        KeepButton.ToolTip = folder;
        AutomationProperties.SetName(KeepButton, label);
        AutomationProperties.SetName(ConfirmDownloadsButton, "Vazhdo me " + label);
        RequiredDestinationChoice.Attach(this, () => companyChosen || KeepChosen);
    }
    private void Move(object sender, RoutedEventArgs e) { companyChosen = true; DialogResult = true; }
    private void Keep(object sender, RoutedEventArgs e)
    {
        WarningPanel.Visibility = Visibility.Visible;
        MoveButton.IsDefault = false; ConfirmDownloadsButton.IsDefault = true;
        BackButton.Focus();
    }
    private void ConfirmKeep(object sender, RoutedEventArgs e)
    {
        if (WarningPanel.Visibility != Visibility.Visible) return;
        KeepChosen = true; DialogResult = false;
    }
    private void Back(object sender, RoutedEventArgs e)
    {
        WarningPanel.Visibility = Visibility.Collapsed;
        ConfirmDownloadsButton.IsDefault = false; MoveButton.IsDefault = true;
        KeepButton.Focus();
    }
    private void Cancel(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (WarningPanel.Visibility == Visibility.Visible) Back(sender, e);
    }
}
