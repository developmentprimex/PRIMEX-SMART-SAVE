using System.Windows;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class ConflictDialog : Window
{
    public ConflictChoice Choice { get; private set; }
    public ConflictDialog() => InitializeComponent();
    private void KeepBoth(object sender, RoutedEventArgs e) { Choice = ConflictChoice.KeepBoth; DialogResult = true; }
    private void Replace(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Replace the existing file? A recovery backup will be retained in the destination folder.", "Confirm replacement", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
        { Choice = ConflictChoice.ReplaceConfirmed; DialogResult = true; }
    }
}
