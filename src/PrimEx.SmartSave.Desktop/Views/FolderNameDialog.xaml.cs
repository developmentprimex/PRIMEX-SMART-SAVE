using System.Windows;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class FolderNameDialog : Window
{
    public string FolderName => NameBox.Text;
    public FolderNameDialog() { InitializeComponent(); Loaded += (_, _) => NameBox.Focus(); }
    private void Confirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
