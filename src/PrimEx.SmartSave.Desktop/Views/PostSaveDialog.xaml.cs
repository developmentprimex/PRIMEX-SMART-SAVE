using System.Windows;
using System.Windows.Input;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class PostSaveDialog : Window
{
    public bool KeepChosen { get; private set; }
    public PostSaveDialog(string name) { InitializeComponent(); FileLabel.Text = name; }
    private void Move(object sender, RoutedEventArgs e) => DialogResult = true;
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
        else DialogResult = false;
    }
}
