using System.Windows;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class PostSaveDialog : Window
{
    public PostSaveDialog(string name) { InitializeComponent(); FileLabel.Text = name; }
    private void Move(object sender, RoutedEventArgs e) => DialogResult = true;
}
