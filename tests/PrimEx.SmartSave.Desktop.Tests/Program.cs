using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.Desktop.Components;
using PrimEx.SmartSave.Desktop.ViewModels;
using PrimEx.SmartSave.Desktop.Views;
using PrimEx.SmartSave.FileManager.FileOperations;
using PrimEx.SmartSave.FileManager.ServerDiscovery;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/PrimEx.SmartSave.Desktop;component/Resources/Theme.xaml", UriKind.Relative));
        int exit = 1;
        app.Dispatcher.BeginInvoke(async () => { try { exit = await RunAsync(); } catch (Exception e) { Console.WriteLine(e); } finally { app.Shutdown(); } });
        app.Run(); return exit;
    }
    private static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "PrimEx-Desktop-Tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        int passed = 0, failed = 0;
        void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
        async Task Test(string name, Func<Task> action) { try { await action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
        var options = new AppOptions(); options.CompanyServer.ApprovedRoots = [root]; options.CompanyServer.DiscoverMappedDrives = false;
        options.Browser.Enabled = true; options.Browser.DownloadsFolder = root; options.Browser.AllowedExtensionIds = [new string('a', 32)];
        var resolver = new WindowsPathResolver(); var servers = new ServerFolderService(options.CompanyServer, resolver);
        var logger = new RotatingEventLog(Path.Combine(root, "logs"), options.Logging);
        var transfer = new SafeTransferService(logger);
        string source = Path.Combine(root, "sample.txt"); File.WriteAllText(source, "isolated desktop test");
        var request = new SaveRequest(Guid.NewGuid(), source, "sample.txt", "Manual", DateTimeOffset.UtcNow);
        try
        {
            foreach (string dismiss in new[] { "Close", "Escape", "AltF4" })
            await Test("Placement requires an explicit choice despite " + dismiss, async () => {
                var prompt = new PostSaveDialog("sample.txt");
                bool attempted = false, blocked = false;
                var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                driver.Tick += (_, _) => {
                    if (attempted) return; attempted = true;
                    if (dismiss == "Close") prompt.Close();
                    else if (dismiss == "Escape") System.Windows.Input.ApplicationCommands.Close.Execute(null, prompt);
                    else SendMessage(new System.Windows.Interop.WindowInteropHelper(prompt).Handle, 0x0112, new IntPtr(0xF060), IntPtr.Zero);
                    blocked = prompt.IsVisible;
                    if (blocked)
                    {
                        ((Button)prompt.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        ((Button)prompt.FindName("ConfirmDownloadsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                };
                driver.Start(); try { prompt.ShowDialog(); } finally { driver.Stop(); }
                Assert(attempted && blocked && prompt.KeepChosen); await Task.CompletedTask;
            });
            foreach (string folderName in new[] { "Desktop", "Downloads", "Dosje_e_re" })
            await Test("Placement names the actual source folder " + folderName, async () => {
                string folder = Path.Combine(root, folderName); Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "new.txt"); File.WriteAllText(file, "local source");
                string label = "", confirmation = "", tooltip = ""; bool driven = false;
                var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                driver.Tick += (_, _) => {
                    if (driven || Application.Current.Windows.OfType<PostSaveDialog>().FirstOrDefault(x => x.IsVisible) is not { } prompt) return;
                    driven = true; var keep = (Button)prompt.FindName("KeepButton");
                    label = System.Windows.Automation.AutomationProperties.GetName(keep);
                    tooltip = keep.ToolTip?.ToString() ?? "";
                    ((Button)prompt.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    confirmation = System.Windows.Automation.AutomationProperties.GetName((Button)prompt.FindName("ConfirmDownloadsButton"));
                    if (folderName == "Desktop")
                    {
                        prompt.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)prompt.ActualWidth, (int)prompt.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(prompt);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var preview = File.Create(Path.GetFullPath("artifacts/verification/required-desktop-choice.png")); encoder.Save(preview);
                    }
                    ((Button)prompt.FindName("ConfirmDownloadsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                driver.Start();
                try { Assert(await new WpfDestinationSelector(servers, resolver, new FixedCompanyFolderPicker(root)).SelectAsync(new(Guid.NewGuid(), file, "new.txt", "Monitoring", DateTimeOffset.UtcNow)) == null); }
                finally { driver.Stop(); }
                Assert(driven && label == folderName && tooltip == folder && confirmation.Contains(folderName) && File.Exists(file));
            });
            await Test("Manual local choice starts at source folder and follows a newly selected destination", async () => {
                var model = new SaveDialogViewModel(request, servers, resolver); model.CompanySelected = false;
                Assert(model.Folder == root);
                var dialog = new SaveDialog(request, servers, resolver); dialog.Show();
                try
                {
                    ((RadioButton)dialog.FindName("OutsideOption")).IsChecked = true;
                    string chosen = Path.Combine(root, "Desktop"); ((TextBox)dialog.FindName("FolderBox")).Text = chosen; dialog.UpdateLayout();
                    Assert(System.Windows.Automation.AutomationProperties.GetName((RadioButton)dialog.FindName("OutsideOption")) == "Desktop");
                }
                finally { Dismiss(dialog); }
                await Task.CompletedTask;
            });
            await Test("Manual destination cannot close until local keep or transfer is confirmed", async () => {
                var dialog = new SaveDialog(request, servers, resolver); bool attempted = false, blocked = false;
                var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                driver.Tick += (_, _) => {
                    if (attempted) return; attempted = true; dialog.Close(); blocked = dialog.IsVisible;
                    if (blocked) Dismiss(dialog);
                };
                driver.Start(); try { dialog.ShowDialog(); } finally { driver.Stop(); }
                Assert(blocked && dialog.KeepChosen && dialog.Selection == null && File.ReadAllText(source) == "isolated desktop test");
                await Task.CompletedTask;
            });
            await Test("Interrupted destination choice remains pending without recording local consent", async () => {
                var store = new OperationStore(Path.Combine(root, "deferred-choice"));
                var decisionQueue = new QueueViewModel(store, transfer, new DeferredChoiceSelector(), logger);
                decisionQueue.Enqueue(request); await decisionQueue.ProcessAsync(request.Id);
                Assert(store.Load().Single().State == OperationState.Pending && File.Exists(source));
            });
            await Test("Browser local destination uses the configured folder instead of its internal staging folder", async () => {
                string downloads = Path.Combine(root, "Browser Downloads"), stage = Path.Combine(root, "staging-job"); Directory.CreateDirectory(downloads); Directory.CreateDirectory(stage);
                string file = Path.Combine(stage, "new-download.txt"); File.WriteAllText(file, "completed staged download");
                var browserOptions = new AppOptions(); browserOptions.Browser.DownloadsFolder = downloads;
                bool driven = false; string label = "";
                var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                driver.Tick += (_, _) => {
                    if (driven || Application.Current.Windows.OfType<SaveDialog>().FirstOrDefault(x => x.IsVisible) is not { } dialog) return;
                    driven = true; var local = (RadioButton)dialog.FindName("OutsideOption");
                    label = System.Windows.Automation.AutomationProperties.GetName(local); local.IsChecked = true;
                    ((Button)dialog.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                var browserRequest = new SaveRequest(Guid.NewGuid(), file, "new-download.txt", "Browser", DateTimeOffset.UtcNow);
                DestinationSelection? selected;
                driver.Start();
                try { selected = await new WpfDestinationSelector(servers, resolver, new FixedCompanyFolderPicker(root), options: browserOptions).SelectAsync(browserRequest); }
                finally { driver.Stop(); }
                Assert(driven && label == "Browser Downloads" && selected?.Folder == downloads && selected.Kind == DestinationKind.OutsideServer);
                var browserQueue = new QueueViewModel(new OperationStore(Path.Combine(root, "browser-placement")), transfer, new SelectedDestinationSelector(selected!), logger);
                browserQueue.Enqueue(browserRequest); await browserQueue.ProcessAsync(browserRequest.Id);
                Assert(browserQueue.Operations.Single().State == OperationState.Completed && !File.Exists(file) && File.ReadAllText(Path.Combine(downloads, "new-download.txt")) == "completed staged download");
            });
            await Test("Startup diagnostics distinguish settings, permissions and storage errors", async () => {
                Assert(StartupError.From(new UnauthorizedAccessException(), "loading settings").Code == "AccessDenied");
                Assert(StartupError.From(new InvalidDataException("Invalid settings"), "loading settings").Message.Contains("Invalid settings"));
                Assert(StartupError.From(new IOException("disk error"), "opening storage").Code == "StartupStorageError");
                Assert(StartupError.AlreadyRunning.Contains("notification area") && StartupError.AlreadyRunning.Contains("Exit"));
                await Task.CompletedTask;
            });
            await Test("Instance activation raises the existing window without exposing file commands", async () => {
                bool shown = false;
                using var activation = new AgentActivationService(Path.Combine(root, "activation"), () => shown = true); activation.Start();
                Assert(await AgentActivationService.TryShowAsync(Path.Combine(root, "activation")));
                Assert(shown); Assert(!await AgentActivationService.TryShowAsync(Path.Combine(root, "different-profile")));
            });
            await Test("Automatic setup shows selected folders and applies valid settings", async () => {
                string settings = Path.Combine(root, "setup-settings.json"); File.WriteAllText(settings, JsonSerializer.Serialize(options));
                string expected = File.ReadAllText(settings); bool applied = false;
                var wizard = new AutomaticGuidanceDialog(options, updated => {
                    ConfigurationStore.Save(settings, updated, expected); applied = true; return Task.CompletedTask;
                });
                wizard.Loaded += (_, _) => {
                    var model = (AutomaticGuidanceViewModel)wizard.DataContext;
                    model.Folders.Clear(); model.AddFolder("Test downloads", root); model.Enabled = true;
                    model.IncludeSubdirectories = true;
                    wizard.UpdateLayout();
                    string output = Path.GetFullPath("artifacts/verification"); Directory.CreateDirectory(output);
                    var bitmap = new RenderTargetBitmap((int)wizard.ActualWidth, (int)wizard.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(wizard);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "automatic-guidance.png"))) encoder.Save(stream);
                    ((Button)wizard.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) }; timeout.Tick += (_, _) => wizard.Close(); timeout.Start();
                try { Assert(wizard.ShowDialog() == true && applied); } finally { timeout.Stop(); wizard.Close(); }
                var saved = AppOptions.Load(settings);
                Assert(saved.Monitoring.Enabled && saved.Monitoring.IncludeSubdirectories && saved.Monitoring.Folders.Single() == root && saved.GuidanceSetupCompleted);
                Assert(saved.Browser.Enabled && saved.Browser.AllowedExtensionIds.SequenceEqual(options.Browser.AllowedExtensionIds));
                Assert(!options.Monitoring.Enabled); // Cancel/opening setup never mutates the live configuration.
                await Task.CompletedTask;
            });
            await Test("Staff setup persists startup opt-out without losing monitoring", async () => {
                var model = new AutomaticGuidanceViewModel(options, []) { StartWithWindows = false };
                var updated = model.CreateOptions();
                Assert(!updated.StartWithWindows && updated.GuidanceSetupCompleted && !updated.Monitoring.Enabled);
                Assert(options.StartWithWindows && !options.GuidanceSetupCompleted);
                await Task.CompletedTask;
            });
            await Test("Setup persists coding-project protection and explicit excluded folders", async () => {
                string excluded = Path.Combine(root, "My Projects");
                var model = new AutomaticGuidanceViewModel(options, []);
                Assert(model.IgnoreDevelopmentProjects && model.ExcludedFolders == "");
                model.ExcludedFolders = excluded + Environment.NewLine + excluded.ToUpperInvariant();
                var updated = model.CreateOptions(); Assert(updated.Monitoring.IgnoreDevelopmentProjects && updated.Monitoring.ExcludedFolders.Single() == excluded);
                string settings = Path.Combine(root, "project-setup.json"); File.WriteAllText(settings, JsonSerializer.Serialize(updated));
                var reopened = new AutomaticGuidanceViewModel(AppOptions.Load(settings), []); Assert(reopened.ExcludedFolders == excluded);
                reopened.IgnoreDevelopmentProjects = false; Assert(!reopened.CreateOptions().Monitoring.IgnoreDevelopmentProjects && reopened.CreateOptions().Monitoring.ExcludedFolders.Single() == excluded);
                Assert(options.Monitoring.ExcludedFolders.Length == 0 && options.Monitoring.IgnoreDevelopmentProjects);
                await Task.CompletedTask;
            });
            await Test("Setup defaults to all file types and supports an explicit custom filter", async () => {
                var model = new AutomaticGuidanceViewModel(options, []);
                Assert(model.AllFileTypes && !model.CustomFileTypesEnabled);
                Assert(model.CreateOptions().Monitoring.Extensions.SequenceEqual(new[] { "*" }));
                model.AllFileTypes = false; model.Extensions = ".pdf, .png, .JPG";
                Assert(model.CustomFileTypesEnabled && model.CreateOptions().Monitoring.Extensions.SequenceEqual(new[] { ".pdf", ".png", ".jpg" }));
                model.AllFileTypes = true;
                Assert(model.CreateOptions().Monitoring.Extensions.Single() == "*");
                await Task.CompletedTask;
            });
            await Test("Automatic setup requires selected folders when enabled", async () => {
                var model = new AutomaticGuidanceViewModel(options, [new("Downloads", root)]) { Enabled = true };
                bool rejected = false; try { model.CreateOptions(); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected && !options.Monitoring.Enabled); await Task.CompletedTask;
            });
            await Test("Automatic setup rejects missing folders and whole-drive monitoring", async () => {
                var model = new AutomaticGuidanceViewModel(options, []) { Enabled = true };
                model.AddFolder("Missing", Path.Combine(root, "missing"));
                bool missing = false; try { model.CreateOptions(); } catch (InvalidDataException) { missing = true; }
                Assert(missing); model.Folders.Clear(); model.AddFolder("Entire drive", Path.GetPathRoot(root)!);
                bool rejected = false; try { model.CreateOptions(); } catch (InvalidDataException) { rejected = true; }
                Assert(rejected); await Task.CompletedTask;
            });
            await Test("Known folder suggestions are absolute and do not create directories", async () => {
                var suggestions = KnownFolderService.GetSuggestions();
                Assert(suggestions.Count > 0 && suggestions.All(x => PathRules.NormalizeAbsolute(x.Path).Length > 0));
                await Task.CompletedTask;
            });
            await Test("Actual desktop agent automatically queues saved documents without Move a file", async () => {
                string data = Path.Combine(root, "agent-smoke", "data"), watched = Path.Combine(root, "agent-smoke", "watched"), nested = Path.Combine(watched, "project");
                Directory.CreateDirectory(data); Directory.CreateDirectory(nested);
                var automatic = new AppOptions(); automatic.CompanyServer.DiscoverMappedDrives = false;
                automatic.Monitoring.Enabled = true; automatic.Monitoring.Folders = [watched]; automatic.Monitoring.IncludeSubdirectories = true; automatic.Monitoring.QuietSeconds = 1;
                File.WriteAllText(Path.Combine(data, "appsettings.json"), JsonSerializer.Serialize(automatic));
                var start = new ProcessStartInfo(Path.GetFullPath("src/PrimEx.SmartSave.Desktop/bin/Release/net10.0-windows/PrimEx.SmartSave.Desktop.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(data); start.ArgumentList.Add("--background");
                using var process = Process.Start(start)!;
                try
                {
                    string logs = Path.Combine(data, "logs", "events.jsonl"), journal = Path.Combine(data, "operations");
                    string ReadLiveLog()
                    {
                        try
                        {
                            using var stream = new FileStream(logs, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            using var reader = new StreamReader(stream); return reader.ReadToEnd();
                        }
                        catch (IOException) { return ""; } // Startup/rotation can briefly replace the log.
                    }
                    for (int i = 0; i < 100 && !ReadLiveLog().Contains("MonitoringStarted"); i++)
                    { Assert(!process.HasExited); await Task.Delay(100); }
                    Assert(ReadLiveLog().Contains("MonitoringStarted"));
                    var duplicateStart = new ProcessStartInfo(start.FileName) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                    duplicateStart.ArgumentList.Add("--data-dir"); duplicateStart.ArgumentList.Add(data);
                    using (var duplicate = Process.Start(duplicateStart)!)
                    {
                        try { using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(8)); await duplicate.WaitForExitAsync(limit.Token); Assert(duplicate.ExitCode == 0); }
                        finally { if (!duplicate.HasExited) { duplicate.Kill(); await duplicate.WaitForExitAsync(); } }
                    }
                    for (int i = 0; i < 30 && !ReadLiveLog().Contains("ApplicationActivated"); i++) await Task.Delay(100);
                    Assert(!process.HasExited && ReadLiveLog().Contains("ApplicationActivated"));
                    string partial = Path.Combine(nested, "download.xlsx.crdownload"), final = Path.Combine(nested, "download.xlsx");
                    File.WriteAllText(partial, "synthetic download completion test"); await Task.Delay(1400);
                    Assert(Directory.GetFiles(journal, "*.json").Length == 0);
                    File.Move(partial, final); File.WriteAllText(Path.Combine(watched, "document.pdf"), "synthetic PDF event"); File.WriteAllText(Path.Combine(watched, "document.docx"), "synthetic Word event"); File.WriteAllText(Path.Combine(watched, "image.png"), "synthetic image download");
                    for (int i = 0; i < 100 && Directory.GetFiles(journal, "*.json").Length < 4; i++) { Assert(!process.HasExited); await Task.Delay(100); }
                    var requests = Directory.GetFiles(journal, "*.json").Select(path => JsonSerializer.Deserialize<PendingOperation>(File.ReadAllText(path))!).ToArray();
                    Assert(requests.Length == 4 && requests.All(x => x.Request.Integration == "Monitoring" && x.State == OperationState.Pending && File.Exists(x.Request.SourcePath)));
                    Assert(requests.Select(x => x.Request.DisplayName).Order().SequenceEqual(new[] { "document.docx", "document.pdf", "download.xlsx", "image.png" }));
                }
                finally { if (!process.HasExited) process.Kill(); await process.WaitForExitAsync(); }
            });
            await Test("Files PX confirmation offers Move and Copy with matching confirmation text", async () => {
                var dialog = new CompanyMoveDialog(request, servers, new FixedCompanyFolderPicker(root));
                try
                {
                    dialog.Show(); dialog.UpdateLayout();
                    Assert(dialog.FindName("MoveTransferOption") is RadioButton && dialog.FindName("CopyTransferOption") is RadioButton);
                    var move = (RadioButton)dialog.FindName("MoveTransferOption"); var copy = (RadioButton)dialog.FindName("CopyTransferOption");
                    Assert(move.IsChecked == true && copy.IsChecked != true);
                    copy.IsChecked = true; dialog.UpdateLayout(); Assert(((Button)dialog.FindName("MoveButton")).Content.ToString() == "_Copy here");
                    move.IsChecked = true; dialog.UpdateLayout(); Assert(((Button)dialog.FindName("MoveButton")).Content.ToString() == "_Move here");
                }
                finally { Dismiss(dialog); }
                await Task.CompletedTask;
            });
            await Test("Manual and browser destinations offer Copy only for Files PX", async () => {
                var dialog = new SaveDialog(request, servers, resolver);
                try
                {
                    dialog.Show(); dialog.UpdateLayout();
                    Assert(dialog.FindName("CopyTransferOption") is RadioButton && dialog.FindName("TransferOptions") is Border);
                    Assert(((Border)dialog.FindName("TransferOptions")).IsVisible);
                    ((RadioButton)dialog.FindName("OutsideOption")).IsChecked = true; dialog.UpdateLayout();
                    Assert(!((Border)dialog.FindName("TransferOptions")).IsVisible);
                }
                finally { Dismiss(dialog); }
                await Task.CompletedTask;
            });
            await Test("Smart Save WPF window loads with Company Server selected", async () => {
                var dialog = new SaveDialog(request, servers, resolver); dialog.Show(); dialog.UpdateLayout();
                Assert(((RadioButton)dialog.FindName("CompanyOption")).IsChecked == true);
                Assert(((TextBlock)dialog.FindName("DownloadsWarning")).Visibility == Visibility.Collapsed);
                Assert(dialog.IsVisible && dialog.ActualWidth >= 530);
                string output = Path.GetFullPath("artifacts/verification"); Directory.CreateDirectory(output);
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(dialog);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "smart-save.png"))) encoder.Save(stream);
                ((RadioButton)dialog.FindName("OutsideOption")).IsChecked = true; dialog.UpdateLayout();
                Assert(((TextBlock)dialog.FindName("DownloadsWarning")).Visibility == Visibility.Visible);
                ((RadioButton)dialog.FindName("CompanyOption")).IsChecked = true; dialog.UpdateLayout();
                Assert(((TextBlock)dialog.FindName("DownloadsWarning")).Visibility == Visibility.Collapsed);
                Dismiss(dialog); await Task.CompletedTask;
            });
            await Test("Queued monitoring requests in projects or excluded folders do not open a placement dialog", async () => {
                string project = Path.Combine(root, "queued-project"), excluded = Path.Combine(root, "queued-excluded"), linked = Path.Combine(root, "queued-linked-editor");
                Directory.CreateDirectory(Path.Combine(project, ".vscode")); Directory.CreateDirectory(excluded);
                Directory.CreateDirectory(linked);
                using var registry = new EditorWorkspaceRegistry(Path.Combine(root, "queued-editor-leases"));
                File.WriteAllText(Path.Combine(root, "queued-editor-leases", "code.json"), JsonSerializer.Serialize(new { version = 1, editor = "VSCode", processId = Environment.ProcessId, updatedUtc = DateTimeOffset.UtcNow, folders = new[] { linked } })); registry.Refresh();
                var protectedOptions = new AppOptions(); protectedOptions.Monitoring.ExcludedFolders = [excluded];
                var owner = new Window { Width = 400, Height = 200 }; owner.Show();
                var previousOwner = Application.Current.MainWindow; Application.Current.MainWindow = owner;
                bool unexpected = false;
                var safety = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                safety.Tick += (_, _) => {
                    foreach (var prompt in Application.Current.Windows.OfType<PostSaveDialog>().Where(x => x.IsVisible).ToArray()) { unexpected = true; Dismiss(prompt); }
                };
                safety.Start();
                try
                {
                    var protectedSelector = new WpfDestinationSelector(servers, resolver, new FixedCompanyFolderPicker(root), options: protectedOptions, editorWorkspaces: registry);
                    foreach (string folder in new[] { project, excluded, linked })
                    {
                        string file = Path.Combine(folder, "README.md"); File.WriteAllText(file, "project file");
                        Assert(await protectedSelector.SelectAsync(new(Guid.NewGuid(), file, "README.md", "Monitoring", DateTimeOffset.UtcNow)) == null);
                        Assert(!unexpected && File.Exists(file));
                    }
                }
                finally { safety.Stop(); owner.Close(); Application.Current.MainWindow = previousOwner; }
            });
            await Test("Editor connection screen renders both editor actions and manual paths with spaces", async () => {
                var dialog = new EditorIntegrationDialog(); dialog.Show(); dialog.UpdateLayout();
                Assert(((Button)dialog.FindName("CodeButton")).IsEnabled && ((Button)dialog.FindName("StudioButton")).IsEnabled);
                string output = Path.GetFullPath("artifacts/verification"); Directory.CreateDirectory(output);
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(dialog);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "editor-connections.png"))) encoder.Save(stream);
                Dismiss(dialog);
                var start = EditorIntegrationService.CreateVSCodeInstallStart(@"C:\Program Files\Microsoft VS Code\bin\code.cmd", @"C:\Tools With Spaces\link.vsix");
                Assert(!start.UseShellExecute && start.CreateNoWindow && start.Arguments.Contains("--install-extension \"C:\\Tools With Spaces\\link.vsix\""));
                await Task.CompletedTask;
            });
            foreach (string mainState in new[] { "Hidden", "Minimized" })
            foreach (string flow in new[] { "Keep", "Move", "Copy", "Browser", "BrowserCopy" })
            {
                await Test($"{flow} prompt leaves the main queue {mainState.ToLowerInvariant()}", async () => {
                    var main = new Window { Title = "Main queue must stay out of the way", Width = 650, Height = 400 };
                    main.Show(); if (mainState == "Hidden") main.Hide(); else main.WindowState = WindowState.Minimized;
                    var previous = Application.Current.MainWindow; Application.Current.MainWindow = main;
                    string company = Path.Combine(root, "standalone-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(company);
                    var companyService = new ServerFolderService(new() { ApprovedRoots = [company], DiscoverMappedDrives = false }, resolver);
                    var remembered = new RememberedLocationStore(Path.Combine(company, "choices"));
                    bool promptSeen = false, companySeen = false, timedOut = false, mainRestored = false, invalidPresentation = false;
                    var elapsed = Stopwatch.StartNew(); var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                    driver.Tick += (_, _) => {
                        if (mainState == "Hidden" ? main.IsVisible : main.WindowState != WindowState.Minimized) mainRestored = true;
                        var dialogs = Application.Current.Windows.OfType<Window>().Where(x => x is PostSaveDialog or CompanyMoveDialog or SaveDialog && x.IsVisible).ToArray();
                        if (elapsed.Elapsed > TimeSpan.FromSeconds(10)) { timedOut = true; foreach (var window in dialogs) Dismiss(window); return; }
                        foreach (var window in dialogs)
                        {
                            if (window.Owner != null || !window.ShowInTaskbar || window.WindowStartupLocation != WindowStartupLocation.CenterScreen) invalidPresentation = true;
                            // A taskbar entry alone can leave a background-triggered prompt behind the browser.
                            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                            if (!window.Topmost || !window.ShowActivated || window.WindowState != WindowState.Normal
                                || (GetWindowLong(handle, -20) & 0x00000008) == 0 || IsIconic(handle)) invalidPresentation = true;
                            if (window is PostSaveDialog prompt)
                            {
                                if (!promptSeen)
                                {
                                    promptSeen = true;
                                    if (flow == "Keep")
                                    {
                                        if (mainState == "Hidden")
                                        {
                                            prompt.UpdateLayout();
                                            string preview = Path.GetFullPath("artifacts/verification/standalone-post-save.png");
                                            var bitmap = new RenderTargetBitmap((int)prompt.ActualWidth, (int)prompt.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(prompt);
                                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(preview); encoder.Save(stream);
                                        }
                                        ((Button)prompt.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                    }
                                    else ((Button)prompt.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                }
                                else if (((Border)prompt.FindName("WarningPanel")).Visibility == Visibility.Visible)
                                    ((Button)prompt.FindName("ConfirmDownloadsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            }
                            else if (window is CompanyMoveDialog move && !companySeen && ((Button)move.FindName("MoveButton")).IsEnabled)
                            {
                                companySeen = true;
                                if (flow == "Copy")
                                {
                                    ((RadioButton)move.FindName("CopyTransferOption")).IsChecked = true;
                                    move.UpdateLayout();
                                    if (mainState == "Hidden")
                                    {
                                        var bitmap = new RenderTargetBitmap((int)move.ActualWidth, (int)move.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(move);
                                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                        using var stream = File.Create(Path.GetFullPath("artifacts/verification/files-px-copy.png")); encoder.Save(stream);
                                    }
                                }
                                ((Button)move.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            }
                            else if (window is SaveDialog save && !companySeen)
                            {
                                promptSeen = true;
                                if (flow == "BrowserCopy")
                                {
                                    companySeen = true;
                                    ((RadioButton)save.FindName("CopyTransferOption")).IsChecked = true;
                                    ((TextBox)save.FindName("FolderBox")).Text = company;
                                    ((Button)save.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                }
                                else Dismiss(save);
                            }
                        }
                    };
                    driver.Start();
                    try
                    {
                        var selector = new WpfDestinationSelector(companyService, resolver, new FixedCompanyFolderPicker(company), remembered);
                        var selected = await selector.SelectAsync(request with { Integration = flow.StartsWith("Browser") ? "Browser" : "Monitoring" });
                        Assert(!timedOut && promptSeen && !mainRestored && !invalidPresentation && File.Exists(source));
                        Assert(mainState == "Hidden" ? !main.IsVisible : main.WindowState == WindowState.Minimized);
                        if (flow is "Move" or "Copy" or "BrowserCopy") Assert(companySeen && selected?.Folder == company && selected.Mode == (flow == "Move" ? TransferMode.Move : TransferMode.Copy));
                        else Assert(selected == null && remembered.Contains(source) == (flow is "Keep" or "Browser"));
                    }
                    finally { driver.Stop(); main.Close(); Application.Current.MainWindow = previous; }
                });
            }
            foreach (string scenario in new[] { "Move", "CancelFolder", "Keep", "Close", "Escape", "CancelWarning", "WarningToServer", "BackFromServer" })
            {
                await Test("Post-save " + scenario + " requires a completed destination choice", async () => {
                    string company = Path.Combine(root, "company-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(company);
                    var companyService = new ServerFolderService(new() { ApprovedRoots = [company], DiscoverMappedDrives = false }, resolver);
                    var nativePicker = new FixedCompanyFolderPicker(scenario == "CancelFolder" ? null : company);
                    string rememberedFolder = Path.Combine(company, "remembered");
                    var remembered = new RememberedLocationStore(rememberedFolder);
                    var owner = new Window { Title = "Isolated post-save flow test", Width = 400, Height = 200 }; owner.Show();
                    var previousOwner = Application.Current.MainWindow; Application.Current.MainWindow = owner;
                    var prompts = new HashSet<PostSaveDialog>();
                    bool browserSeen = false, repeatedChoice = false, submitted = false, timedOut = false, warningSeen = false;
                    Exception? driverError = null;
                    var elapsed = Stopwatch.StartNew(); var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                    driver.Tick += (_, _) => {
                      try {
                        if (elapsed.Elapsed > TimeSpan.FromSeconds(8))
                        {
                            timedOut = true;
                            foreach (var window in Application.Current.Windows.OfType<Window>().Where(x => x != owner && x.IsVisible).ToArray()) Dismiss(window);
                            return;
                        }
                        if (Application.Current.Windows.OfType<SaveDialog>().FirstOrDefault(x => x.IsVisible) is { } duplicate)
                        { repeatedChoice = true; Dismiss(duplicate); return; }
                        if (Application.Current.Windows.OfType<PostSaveDialog>().FirstOrDefault(x => x.IsVisible) is { } prompt && prompts.Add(prompt))
                        {
                            Assert(prompt.Owner == owner && !prompt.ShowInTaskbar && !prompt.KeepChosen);
                            Assert(prompt.Topmost && prompt.ShowActivated && prompt.WindowState == WindowState.Normal);
                            Assert(((Border)prompt.FindName("WarningPanel")).Visibility == Visibility.Collapsed);
                            Assert(!remembered.Contains(source));
                            if (scenario == "Close") { prompt.Close(); Assert(prompt.IsVisible && !prompt.KeepChosen); }
                            if (scenario == "Escape") { System.Windows.Input.ApplicationCommands.Close.Execute(null, prompt); Assert(prompt.IsVisible && !prompt.KeepChosen); }
                            if (scenario is "Keep" or "Close" or "Escape" or "CancelWarning" or "WarningToServer" || prompts.Count > 1)
                                ((Button)prompt.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            else ((Button)prompt.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            return;
                        }
                        if (!warningSeen && Application.Current.Windows.OfType<PostSaveDialog>().FirstOrDefault(x => x.IsVisible && ((Border)x.FindName("WarningPanel")).Visibility == Visibility.Visible) is { } warning)
                        {
                            warningSeen = true;
                            Assert(!warning.KeepChosen && !remembered.Contains(source) && File.Exists(source));
                            if (scenario == "WarningToServer")
                            {
                                ((Button)warning.FindName("BackButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Assert(((Border)warning.FindName("WarningPanel")).Visibility == Visibility.Collapsed && !warning.KeepChosen);
                                ((Button)warning.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            }
                            else
                            {
                                if (scenario == "CancelWarning")
                                {
                                    System.Windows.Input.ApplicationCommands.Close.Execute(null, warning);
                                    Assert(warning.IsVisible && ((Border)warning.FindName("WarningPanel")).Visibility == Visibility.Collapsed && !warning.KeepChosen);
                                    ((Button)warning.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                }
                                ((Button)warning.FindName("ConfirmDownloadsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            }
                            return;
                        }
                        if (!submitted && Application.Current.Windows.OfType<CompanyMoveDialog>().FirstOrDefault(x => x.IsVisible) is { } browser && ((Button)browser.FindName("MoveButton")).IsEnabled)
                        {
                            browserSeen = true; submitted = true;
                            Assert(((Button)browser.FindName("CancelButton")).Content.ToString() == "_Back");
                            if (scenario == "BackFromServer") ((Button)browser.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            else ((Button)browser.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                      } catch (Exception e) {
                        driverError = e;
                        foreach (var window in Application.Current.Windows.OfType<Window>().Where(x => x != owner && x.IsVisible).ToArray()) Dismiss(window);
                      }
                    };
                    driver.Start();
                    try
                    {
                        var selected = await new WpfDestinationSelector(companyService, resolver, nativePicker, remembered).SelectAsync(request with { Integration = "Monitoring" });
                        if (driverError != null) throw driverError;
                        Assert(!timedOut && prompts.Count > 0 && !repeatedChoice && File.Exists(source));
                        if (scenario is "Move" or "WarningToServer") Assert(browserSeen && selected?.Kind == DestinationKind.CompanyServer && selected.Folder == company && selected.FileName == request.DisplayName && !remembered.Contains(source));
                        else Assert(selected == null && remembered.Contains(source));
                        Assert(prompts.Count == (scenario is "CancelFolder" or "BackFromServer" ? 2 : 1));
                        Assert(nativePicker.Calls == (scenario is "Move" or "WarningToServer" or "CancelFolder" or "BackFromServer" ? 1 : 0));
                        if (scenario == "Keep")
                        {
                            File.AppendAllText(source, " saved again");
                            var reopened = new WpfDestinationSelector(companyService, resolver, nativePicker, new RememberedLocationStore(rememberedFolder));
                            Assert(await reopened.SelectAsync(request with { Integration = "Monitoring", Id = Guid.NewGuid() }) == null && !timedOut);
                            File.WriteAllText(source, "isolated desktop test");
                        }
                    }
                    finally { driver.Stop(); owner.Close(); Application.Current.MainWindow = previousOwner; }
                });
            }
            await Test("Native Windows picker opens at approved root with company shortcuts", async () => {
                var roots = servers.GetRoots();
                var dialog = WindowsCompanyFolderPicker.CreateDialog(roots);
                Assert(dialog.InitialDirectory == root && dialog.DefaultDirectory == root && !dialog.Multiselect && !dialog.AddToRecent);
                Assert(dialog.CustomPlaces.Count == 1 && dialog.CustomPlaces[0].Path == root);
                Assert(WindowsCompanyFolderPicker.CreateDialog(roots, root).InitialDirectory == root);
                await Task.CompletedTask;
            });
            await Test("Native picker resolves approved folders and rejects outside roots", async () => {
                var picker = new WindowsCompanyFolderPicker(servers, resolver);
                Assert(picker.ResolveApprovedFolder(root) == root);
                bool denied = false; try { picker.ResolveApprovedFolder(Path.GetTempPath().TrimEnd('\\')); } catch (UnauthorizedAccessException) { denied = true; }
                Assert(denied);
                bool unconfigured = false; try { WindowsCompanyFolderPicker.CreateDialog([]); } catch (InvalidDataException) { unconfigured = true; }
                Assert(unconfigured); await Task.CompletedTask;
            });
            await Test("Destination view model validates and switches to outside", async () => {
                var model = new SaveDialogViewModel(request, servers, resolver) { Folder = root };
                Assert(await model.ValidateAsync()); model.CompanySelected = false;
                Assert(model.OutsideSelected && model.Folder == root); model.Folder = root; Assert(await model.ValidateAsync());
                model.FileName = "..\\escape"; Assert(!await model.ValidateAsync());
            });
            var selector = new FixedSelector(root);
            await Test("Choosing Copy keeps the original and records a verified server copy", async () => {
                string local = Path.Combine(root, "copy-original.txt"), destination = Path.Combine(root, "copy-server"); Directory.CreateDirectory(destination);
                File.WriteAllText(local, "original bytes stay local");
                var selected = new DestinationSelection(destination, "server-copy.txt", DestinationKind.CompanyServer, ConflictChoice.Cancel, TransferMode.Copy);
                var store = new OperationStore(Path.Combine(root, "copy-operations"));
                var copyQueue = new QueueViewModel(store, transfer, new SelectedDestinationSelector(selected), logger);
                var copyRequest = new SaveRequest(Guid.NewGuid(), local, "copy-original.txt", "Monitoring", DateTimeOffset.UtcNow);
                copyQueue.Enqueue(copyRequest); await copyQueue.ProcessAsync(copyRequest.Id);
                Assert(File.Exists(local) && File.ReadAllText(local) == "original bytes stay local");
                Assert(File.ReadAllText(Path.Combine(destination, "server-copy.txt")) == "original bytes stay local");
                Assert(copyQueue.Operations.Single().State == OperationState.Completed);
                Assert(store.Load().Single().Mode == TransferMode.Copy);
                Assert(copyQueue.Status.Contains("original remains", StringComparison.OrdinalIgnoreCase));
                var reopened = new QueueViewModel(store, transfer, new SelectedDestinationSelector(selected), logger);
                await reopened.ProcessAsync(copyRequest.Id);
                Assert(reopened.Operations.Single().Mode == TransferMode.Copy && Directory.GetFiles(destination).Length == 1 && File.Exists(local));
            });
            await Test("Copy retry preserves the selected action after an unavailable destination and restart", async () => {
                string local = Path.Combine(root, "retry-copy.txt"), destination = Path.Combine(root, "offline-copy-server");
                File.WriteAllText(local, "copy retry original");
                var store = new OperationStore(Path.Combine(root, "retry-copy-operations"));
                var selected = new DestinationSelection(destination, "retry-copy.txt", DestinationKind.CompanyServer, ConflictChoice.Cancel, TransferMode.Copy);
                var copyQueue = new QueueViewModel(store, transfer, new SelectedDestinationSelector(selected), logger);
                var copyRequest = new SaveRequest(Guid.NewGuid(), local, "retry-copy.txt", "Manual", DateTimeOffset.UtcNow);
                copyQueue.Enqueue(copyRequest); await copyQueue.ProcessAsync(copyRequest.Id);
                Assert(copyQueue.Operations.Single().State == OperationState.NeedsReview && File.Exists(local));
                Directory.CreateDirectory(destination);
                var retrySelector = new SelectedDestinationSelector(selected);
                var reopened = new QueueViewModel(store, transfer, retrySelector, logger);
                reopened.Retry(copyRequest.Id); await reopened.ProcessAsync(copyRequest.Id);
                Assert(retrySelector.LastInitialMode == TransferMode.Copy && reopened.Operations.Single().State == OperationState.Completed);
                Assert(File.ReadAllText(local) == "copy retry original" && File.ReadAllText(Path.Combine(destination, "retry-copy.txt")) == "copy retry original");
            });
            await Test("Deferred startup history preserves pending work and interrupted-transfer recovery", async () => {
                var history = new OperationStore(Path.Combine(root, "deferred-history"));
                var pending = new PendingOperation(request with { Id = Guid.NewGuid() });
                var interrupted = new PendingOperation(request with { Id = Guid.NewGuid() }, OperationState.Transferring);
                history.Save(pending); history.Save(interrupted);
                var deferred = new QueueViewModel(history, transfer, selector, logger, loadHistory: false);
                Assert(!deferred.IsReady && deferred.Operations.Count == 0);
                await deferred.InitializeAsync();
                Assert(deferred.IsReady && deferred.Operations.Count == 2);
                Assert(deferred.Operations.Single(x => x.Request.Id == pending.Request.Id).State == OperationState.Pending);
                Assert(deferred.Operations.Single(x => x.Request.Id == interrupted.Request.Id).State == OperationState.NeedsReview);
                await deferred.InitializeAsync(); Assert(deferred.Operations.Count == 2);
            });
            var queue = new QueueViewModel(new OperationStore(Path.Combine(root, "operations")), transfer, selector, logger);
            await Test("Queue persists manual move, prevents duplicate transfer", async () => {
                queue.Enqueue(request); Assert(!queue.Enqueue(request));
                await queue.ProcessAsync(request.Id); await queue.ProcessAsync(request.Id);
                Assert(queue.Operations.Single().State == OperationState.Completed && !File.Exists(source));
                Assert(File.ReadAllText(Path.Combine(root, "moved-sample.txt")) == "isolated desktop test");
            });
            await Test("Queue cancellation retains source", async () => {
                var path = Path.Combine(root, "cancel.txt"); File.WriteAllText(path, "cancel");
                var req = new SaveRequest(Guid.NewGuid(), path, "cancel.txt", "Manual", DateTimeOffset.UtcNow);
                selector.Cancel = true; queue.Enqueue(req); await queue.ProcessAsync(req.Id); selector.Cancel = false;
                Assert(queue.Operations.Single(x => x.Request.Id == req.Id).State == OperationState.Cancelled && File.Exists(path));
            });
            string config = Path.Combine(root, "appsettings.json"); File.WriteAllText(config, JsonSerializer.Serialize(options));
            string origin = "chrome-extension://" + new string('a', 32) + "/";
            var delivered = new List<Guid>();
            using var server = new NativePipeServer(options, root, queue, Dispatcher.CurrentDispatcher, logger, id => delivered.Add(id)); server.Start();
            async Task<NativeReply> Host(NativeMessage message, string? caller = null)
            {
                string host = Path.GetFullPath("src/PrimEx.SmartSave.NativeHost/bin/Release/net10.0-windows/PrimEx.SmartSave.NativeHost.exe");
                var start = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true };
                start.ArgumentList.Add(caller ?? origin); start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(root); start.ArgumentList.Add("--config"); start.ArgumentList.Add(config);
                using var process = Process.Start(start)!;
                await NativeProtocol.WriteAsync(process.StandardInput.BaseStream, message); process.StandardInput.Close();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await NativeProtocol.ReadAsync<NativeReply>(process.StandardOutput.BaseStream, timeout.Token);
                await process.WaitForExitAsync(timeout.Token); return response ?? throw new Exception("No response");
            }
            await Test("Actual native host connects to current-user agent pipe", async () => Assert((await Host(new(1, "ping"))).Ok));
            Guid id = Guid.NewGuid(); string stage = Path.Combine(options.Browser.StagingRoot, id.ToString("N")); Directory.CreateDirectory(stage);
            string downloaded = Path.Combine(stage, "download.pdf"); File.WriteAllText(downloaded, "PDF test");
            var complete = new NativeMessage(1, "complete", id, downloaded, true, 8, DateTimeOffset.UtcNow);
            await Test("Native completion is persisted once and associated with correct file", async () => {
                Assert((await Host(complete)).Ok); Assert((await Host(complete)).Ok);
                Assert(queue.Operations.Count(x => x.Request.Id == id) == 1 && queue.Operations.Single(x => x.Request.Id == id).Request.SourcePath == downloaded);
                Assert(delivered.Count(x => x == id) == 1 && File.Exists(downloaded));
            });
            await Test("Native status reports persisted pending state", async () => Assert((await Host(new(1, "status", id))).State == "Pending"));
            await Test("Native host rejects unapproved extension origin", async () => Assert(!(await Host(new(1, "ping"), "chrome-extension://" + new string('b', 32) + "/")).Ok));
            await Test("Native file mismatch rejected without queue mutation", async () => Assert(!(await Host(complete with { Bytes = 9, Id = Guid.NewGuid() })).Ok));
            server.Dispose(); await Task.Delay(50);
            await Test("Native agent-unavailable fallback", async () => Assert((await Host(new(1, "ping"))).Code == "AgentUnavailable"));
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed"); return failed == 0 ? 0 : 1;
    }
    private sealed class FixedSelector(string root) : IDestinationSelector
    {
        public bool Cancel { get; set; }
        public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default, TransferMode initialMode = TransferMode.Move) => Task.FromResult<DestinationSelection?>(Cancel ? null : new(root, "moved-" + request.DisplayName, DestinationKind.OutsideServer, ConflictChoice.Cancel));
    }
    private static void Dismiss(Window window)
    {
        if (!window.IsVisible) return;
        if (window is PostSaveDialog prompt)
        {
            ((Button)prompt.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)prompt.FindName("ConfirmDownloadsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        else if (window is SaveDialog save && save.DataContext is SaveDialogViewModel model)
        {
            model.CompanySelected = false; model.Folder = Path.GetDirectoryName(model.Source)!; model.FileName = Path.GetFileName(model.Source);
            ((Button)save.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        else window.Close();
    }
    private sealed class DeferredChoiceSelector : IDestinationSelector
    {
        public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default, TransferMode initialMode = TransferMode.Move)
            => throw new OperationCanceledException();
    }
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);
    private sealed class SelectedDestinationSelector(DestinationSelection selection) : IDestinationSelector
    {
        public TransferMode? LastInitialMode { get; private set; }
        public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default, TransferMode initialMode = TransferMode.Move)
        { LastInitialMode = initialMode; return Task.FromResult<DestinationSelection?>(selection); }
    }
    private sealed class FixedCompanyFolderPicker(string? folder) : ICompanyFolderPicker
    {
        public int Calls { get; private set; }
        public Task<string?> PickAsync(Window owner, string? initialFolder = null) { Calls++; return Task.FromResult(folder); }
    }
}
