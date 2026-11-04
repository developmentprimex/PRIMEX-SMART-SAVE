using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Desktop.ViewModels;
namespace PrimEx.SmartSave.Desktop.Views;
public partial class MainWindow : Window
{
    private readonly QueueViewModel model;
    public MainWindow(QueueViewModel model) { InitializeComponent(); this.model = model; DataContext = model; Closing += (_, e) => { if (!((App)Application.Current).IsExiting) { e.Cancel = true; Hide(); } }; }
    private async void MoveFile(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Choose a completed file to move or copy", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, "Choose a destination and select Move or Copy for Files PX. Keep the original application closed during the transfer.", "Move or copy file", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        var request = new SaveRequest(Guid.NewGuid(), picker.FileName, Path.GetFileName(picker.FileName), "Manual", DateTimeOffset.UtcNow);
        try { model.Enqueue(request); await model.ProcessAsync(request.Id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { model.Status = "Could not persist the request. The source is unchanged."; }
    }
    private async void Review(object sender, RoutedEventArgs e) { if (Queue.SelectedItem is PendingOperation item) await model.ProcessAsync(item.Request.Id); }
    private async void Retry(object sender, RoutedEventArgs e)
    {
        if (Queue.SelectedItem is not PendingOperation item || item.State == OperationState.Completed || item.State == OperationState.Transferring) return;
        if (MessageBox.Show(this, "Check whether the destination already contains a verified copy. Retry may create another copy. Continue?", "Review before retry", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        model.Retry(item.Request.Id); await model.ProcessAsync(item.Request.Id);
    }
    private void CancelTransfer(object sender, RoutedEventArgs e) => model.CancelActive();
    private void Settings(object sender, RoutedEventArgs e) => ((App)Application.Current).OpenSettings();
    private async void AutomaticGuidance(object sender, RoutedEventArgs e) => await ((App)Application.Current).OpenAutomaticGuidanceAsync();
    private void Diagnostics(object sender, RoutedEventArgs e)
    {
        var picker = new SaveFileDialog { Title = "Export non-sensitive diagnostics", FileName = "PrimEx-diagnostics.json", Filter = "JSON|*.json", OverwritePrompt = true };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            using var process = Process.GetCurrentProcess();
            File.WriteAllText(picker.FileName, JsonSerializer.Serialize(new
            {
                version = typeof(App).Assembly.GetName().Version?.ToString(3),
                runtime = Environment.Version.ToString(), os = Environment.OSVersion.VersionString,
                logicalProcessorCount = Environment.ProcessorCount,
                uptimeSeconds = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
                processCpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds,
                workingSetBytes = process.WorkingSet64, privateMemoryBytes = process.PrivateMemorySize64,
                monitoring = ((App)Application.Current).MonitoringDiagnostics,
                states = model.Operations.GroupBy(x => x.State).ToDictionary(x => x.Key.ToString(), x => x.Count())
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { model.Status = "Could not write diagnostics to that location."; }
    }
}
