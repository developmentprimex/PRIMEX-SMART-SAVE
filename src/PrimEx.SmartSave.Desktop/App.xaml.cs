using System.Diagnostics;
using System.Windows;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.FileManager.FileOperations;
using PrimEx.SmartSave.FileManager.ServerDiscovery;
using PrimEx.SmartSave.Desktop.Components;
using PrimEx.SmartSave.Desktop.ViewModels;
using PrimEx.SmartSave.Desktop.Views;
using PrimEx.SmartSave.Monitoring.Watchers;
namespace PrimEx.SmartSave.Desktop;
public partial class App : Application
{
    private ServiceProvider? services;
    private System.Windows.Forms.NotifyIcon? tray;
    private FileStream? instanceLock;
    private NativePipeServer? pipe;
    private DirectoryMonitor? monitor;
    private AgentActivationService? activation;
    private int monitorGeneration;
    private bool setupOpen;
    private AppOptions options = new();
    public bool IsExiting { get; private set; }
    public string ConfigPath { get; private set; } = "";
    public string DataDirectory { get; private set; } = "";
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string startupStage = "opening local storage";
        try
        {
            string? Arg(string name) { int index = Array.IndexOf(e.Args, name); return index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] : null; }
            DataDirectory = Arg("--data-dir") ?? (File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.mode")) ? Path.Combine(AppContext.BaseDirectory, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimEx", "SmartSave"));
            Directory.CreateDirectory(DataDirectory);
            startupStage = "acquiring the agent instance lock";
            instanceLock = AgentInstanceLease.TryAcquire(DataDirectory);
            if (instanceLock == null)
            {
                if (!e.Args.Contains("--background") && !await AgentActivationService.TryShowAsync(DataDirectory))
                    MessageBox.Show(StartupError.AlreadyRunning, "PrimEx Smart Save is already running", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(); return;
            }
            startupStage = "loading configuration";
            string central = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PrimEx", "SmartSave", "appsettings.json");
            ConfigPath = Arg("--config") ?? (File.Exists(central) ? central : Path.Combine(DataDirectory, "appsettings.json"));
            if (!File.Exists(ConfigPath)) File.Copy(Path.Combine(AppContext.BaseDirectory, "appsettings.example.json"), ConfigPath);
            options = AppOptions.Load(ConfigPath);
            if (e.Args.Contains("--startup") && !options.StartWithWindows) { Shutdown(); return; }
            startupStage = "opening the saved operation queue";
            var collection = new ServiceCollection();
            collection.AddSingleton(options);
            collection.AddSingleton<IEventLog>(new RotatingEventLog(Path.Combine(DataDirectory, "logs"), options.Logging));
            collection.AddSingleton<IPathResolver, WindowsPathResolver>();
            collection.AddSingleton<IServerFolderService>(s => new ServerFolderService(options.CompanyServer, s.GetRequiredService<IPathResolver>()));
            collection.AddSingleton<ITransferService, SafeTransferService>();
            collection.AddSingleton(new OperationStore(Path.Combine(DataDirectory, "operations")));
            collection.AddSingleton<IDestinationSelector, WpfDestinationSelector>();
            collection.AddSingleton<QueueViewModel>(); collection.AddSingleton<MainWindow>();
            services = collection.BuildServiceProvider();
            MainWindow = services.GetRequiredService<MainWindow>();
            activation = new AgentActivationService(DataDirectory, () => Dispatcher.BeginInvoke(() => {
                ShowMain(); services!.GetRequiredService<IEventLog>().Write("ApplicationActivated");
            })); activation.Start();
            services.GetRequiredService<IEventLog>().Write("ApplicationStarted");
            SetupTray();
            if (options.Browser.Enabled)
            {
                var queue = services.GetRequiredService<QueueViewModel>();
                pipe = new NativePipeServer(options, DataDirectory, queue, Dispatcher, services.GetRequiredService<IEventLog>(), id => { ShowMain(); _ = queue.ProcessAsync(id); }); pipe.Start();
            }
            StartMonitoring();
            if (!e.Args.Contains("--background")) MainWindow.Show();
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "installed.mode")) && !options.GuidanceSetupCompleted)
            {
                services.GetRequiredService<IEventLog>().Write("FirstRunSetupRequested");
                _ = Dispatcher.BeginInvoke(async () => await OpenAutomaticGuidanceAsync());
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or System.Text.Json.JsonException)
        {
            var error = StartupError.From(ex, startupStage);
            MessageBox.Show(error.Message, "PrimEx Smart Save - " + error.Code, MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1);
        }
    }
    private void SetupTray()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Smart Save", null, (_, _) => ShowMain());
        menu.Items.Add("Open Company Server", null, (_, _) => {
            ShowMain(); new ServerBrowser(services!.GetRequiredService<IServerFolderService>()) { Owner = MainWindow }.ShowDialog();
        });
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add("Automatic guidance…", null, async (_, _) => await OpenAutomaticGuidanceAsync());
        menu.Items.Add("View Pending Operations", null, (_, _) => ShowMain());
        menu.Items.Add("About", null, (_, _) => MessageBox.Show("PrimEx Smart Save 0.2.0\nDocument Storage Manager\nPilot candidate. Office interception is not enabled.", "About PrimEx Smart Save"));
        menu.Items.Add("Exit", null, (_, _) => {
            var queue = services!.GetRequiredService<QueueViewModel>();
            if (queue.IsProcessing) { queue.Status = "Finish or cancel the current operation before exiting."; ShowMain(); return; }
            IsExiting = true; Shutdown();
        });
        tray = new() { Icon = System.Drawing.SystemIcons.Application, Text = "PrimEx Smart Save", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowMain();
    }
    public void ShowMain() { MainWindow!.Show(); MainWindow.WindowState = WindowState.Normal; MainWindow.Activate(); }
    private void StartMonitoring()
    {
        var queue = services!.GetRequiredService<QueueViewModel>();
        int generation = ++monitorGeneration;
        if (options.Monitoring.Enabled)
        {
            // A running watcher sees a fixed settings snapshot until explicitly reconfigured.
            var snapshot = JsonSerializer.Deserialize<AppOptions>(JsonSerializer.Serialize(options))!;
            monitor = new DirectoryMonitor(snapshot, services!.GetRequiredService<IEventLog>());
            monitor.MissedEvents += () => Dispatcher.BeginInvoke(() => { if (generation == monitorGeneration) queue.Status = "Some folders or monitoring events are unavailable. Review the automatic guidance settings."; });
            monitor.Candidate += path => Dispatcher.BeginInvoke(async () => {
                if (generation != monitorGeneration) return;
                try
                {
                    var request = new SaveRequest(Guid.NewGuid(), path, Path.GetFileName(path), "Monitoring", DateTimeOffset.UtcNow);
                    queue.Enqueue(request);
                    if (setupOpen) { queue.Status = "A saved document is pending. Review it after finishing setup."; return; }
                    ShowMain(); await queue.ProcessAsync(request.Id);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { queue.Status = "A monitored file could not be queued. It remains in its current location."; }
            });
            monitor.Start();
        }
        queue.GuidanceStatus = options.Monitoring.Enabled
            ? $"Automatic guidance ON — {monitor?.ActiveFolderCount ?? 0} of {options.Monitoring.Folders.Length} selected folders active. Prompts appear after files are saved and unlocked."
            : "Automatic guidance is off. Set it up to watch downloads and saved documents.";
    }
    public async Task OpenAutomaticGuidanceAsync()
    {
        var queue = services!.GetRequiredService<QueueViewModel>();
        if (setupOpen) return;
        if (queue.IsProcessing) { queue.Status = "Finish or cancel the current operation before changing automatic guidance."; ShowMain(); return; }
        setupOpen = true;
        try
        {
            string expected = File.ReadAllText(ConfigPath);
            var current = AppOptions.Load(ConfigPath);
            ShowMain();
            new AutomaticGuidanceDialog(current, async updated => {
                if (queue.IsProcessing) throw new InvalidOperationException("Finish the current operation first.");
                await Task.Run(() => ConfigurationStore.Save(ConfigPath, updated, expected));
                ++monitorGeneration;
                if (monitor != null) { await monitor.DisposeAsync(); monitor = null; }
                options.CompanyServer.ApprovedRoots = updated.CompanyServer.ApprovedRoots;
                options.CompanyServer.DiscoverMappedDrives = updated.CompanyServer.DiscoverMappedDrives;
                options.Monitoring = updated.Monitoring;
                options.StartWithWindows = updated.StartWithWindows;
                options.GuidanceSetupCompleted = updated.GuidanceSetupCompleted;
                StartMonitoring();
                services!.GetRequiredService<IEventLog>().Write(updated.Monitoring.Enabled ? "AutomaticGuidanceEnabled" : "AutomaticGuidanceDisabled");
                queue.Status = updated.Monitoring.Enabled ? "Automatic guidance is active. Download or save a new document into a selected folder to test it." : "Automatic guidance is disabled. Your files remain where they are.";
            }) { Owner = MainWindow }.ShowDialog();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { queue.Status = "Could not open automatic guidance settings. Check the configuration file or contact your administrator."; }
        finally { setupOpen = false; }
        if (options.Monitoring.Enabled)
            foreach (var item in queue.Operations.Where(x => x.Request.Integration == "Monitoring" && x.State == OperationState.Pending).ToArray())
                await queue.ProcessAsync(item.Request.Id);
    }
    public void OpenSettings()
    {
        Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { ConfigPath }, UseShellExecute = true });
        MessageBox.Show("Edit approved roots and integration settings in this configuration file. Restart the agent after saving. Managed configuration may require your IT administrator.", "PrimEx Smart Save Settings");
    }
    protected override void OnExit(ExitEventArgs e) { activation?.Dispose(); pipe?.Dispose(); if (monitor != null) monitor.DisposeAsync().AsTask().GetAwaiter().GetResult(); tray?.Dispose(); services?.Dispose(); instanceLock?.Dispose(); base.OnExit(e); }
}
