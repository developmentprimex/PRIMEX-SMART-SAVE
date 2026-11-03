using System.Windows;
using System.Windows.Input;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class PostSaveDialog : Window
{
    public bool KeepChosen { get; private set; }
    public PostSaveDialog(string name) { InitializeComponent(); FileLabel.Text = name; }
    private void Move(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Keep(object sender, RoutedEventArgs e) { KeepChosen = true; DialogResult = false; }
    private void Cancel(object sender, ExecutedRoutedEventArgs e) { e.Handled = true; DialogResult = false; }
}
