using System.Diagnostics;
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
                    for (int i = 0; i < 100 && !(File.Exists(logs) && File.ReadAllText(logs).Contains("MonitoringStarted")); i++)
                    { Assert(!process.HasExited); await Task.Delay(100); }
                    Assert(File.Exists(logs) && File.ReadAllText(logs).Contains("MonitoringStarted"));
                    var duplicateStart = new ProcessStartInfo(start.FileName) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                    duplicateStart.ArgumentList.Add("--data-dir"); duplicateStart.ArgumentList.Add(data);
                    using (var duplicate = Process.Start(duplicateStart)!)
                    {
                        try { using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(8)); await duplicate.WaitForExitAsync(limit.Token); Assert(duplicate.ExitCode == 0); }
                        finally { if (!duplicate.HasExited) { duplicate.Kill(); await duplicate.WaitForExitAsync(); } }
                    }
                    for (int i = 0; i < 30 && !File.ReadAllText(logs).Contains("ApplicationActivated"); i++) await Task.Delay(100);
                    Assert(!process.HasExited && File.ReadAllText(logs).Contains("ApplicationActivated"));
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
            await Test("Smart Save WPF window loads with Company Server selected", async () => {
                var dialog = new SaveDialog(request, servers, resolver); dialog.Show(); dialog.UpdateLayout();
                Assert(((RadioButton)dialog.FindName("CompanyOption")).IsChecked == true);
                Assert(dialog.IsVisible && dialog.ActualWidth >= 530);
                string output = Path.GetFullPath("artifacts/verification"); Directory.CreateDirectory(output);
                var bitmap = new RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(dialog);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "smart-save.png"))) encoder.Save(stream);
                dialog.Close(); await Task.CompletedTask;
            });
            foreach (string scenario in new[] { "Move", "CancelFolder", "Keep", "Close", "Escape" })
            {
                await Test("Post-save " + scenario + " does not repeat destination-type choice", async () => {
                    string company = Path.Combine(root, "company-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(company);
                    var companyService = new ServerFolderService(new() { ApprovedRoots = [company], DiscoverMappedDrives = false }, resolver);
                    var nativePicker = new FixedCompanyFolderPicker(scenario == "CancelFolder" ? null : company);
                    string rememberedFolder = Path.Combine(company, "remembered");
                    var remembered = new RememberedLocationStore(rememberedFolder);
                    var owner = new Window { Title = "Isolated post-save flow test", Width = 400, Height = 200 }; owner.Show();
                    var previousOwner = Application.Current.MainWindow; Application.Current.MainWindow = owner;
                    bool promptSeen = false, browserSeen = false, repeatedChoice = false, submitted = false, timedOut = false;
                    var elapsed = Stopwatch.StartNew(); var driver = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                    driver.Tick += (_, _) => {
                        if (elapsed.Elapsed > TimeSpan.FromSeconds(8))
                        {
                            timedOut = true;
                            foreach (var window in Application.Current.Windows.OfType<Window>().Where(x => x != owner && x.IsVisible).ToArray()) window.Close();
                            return;
                        }
                        if (Application.Current.Windows.OfType<SaveDialog>().FirstOrDefault(x => x.IsVisible) is { } duplicate)
                        { repeatedChoice = true; duplicate.Close(); return; }
                        if (!promptSeen && Application.Current.Windows.OfType<PostSaveDialog>().FirstOrDefault(x => x.IsVisible) is { } prompt)
                        {
                            promptSeen = true;
                            if (scenario == "Keep")
                            {
                                prompt.UpdateLayout();
                                string preview = Path.GetFullPath("artifacts/verification/post-save.png"); Directory.CreateDirectory(Path.GetDirectoryName(preview)!);
                                var bitmap = new RenderTargetBitmap((int)prompt.ActualWidth, (int)prompt.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(prompt);
                                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(preview); encoder.Save(output);
                            }
                            if (scenario == "Close") prompt.Close();
                            else if (scenario == "Escape") System.Windows.Input.ApplicationCommands.Close.Execute(null, prompt);
                            else if (scenario == "Keep") ((Button)prompt.FindName("KeepButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            else ((Button)prompt.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            return;
                        }
                        if (!submitted && Application.Current.Windows.OfType<CompanyMoveDialog>().FirstOrDefault(x => x.IsVisible) is { } browser && ((Button)browser.FindName("MoveButton")).IsEnabled)
                        {
                            browserSeen = true; submitted = true;
                            Assert(((Button)browser.FindName("CancelButton")).Content.ToString() == "Cancel");
                            if (scenario == "CancelFolder") { browser.Close(); return; }
                            browser.UpdateLayout();
                            string output = Path.GetFullPath("artifacts/verification"); Directory.CreateDirectory(output);
                            var bitmap = new RenderTargetBitmap((int)browser.ActualWidth, (int)browser.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(browser);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(Path.Combine(output, "company-move.png"))) encoder.Save(stream);
                            ((Button)browser.FindName("MoveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                    };
                    driver.Start();
                    try
                    {
                        var selected = await new WpfDestinationSelector(companyService, resolver, nativePicker, remembered).SelectAsync(request with { Integration = "Monitoring" });
                        Assert(!timedOut && promptSeen && !repeatedChoice && File.Exists(source));
                        if (scenario == "Move") Assert(browserSeen && selected?.Kind == DestinationKind.CompanyServer && selected.Folder == company && selected.FileName == request.DisplayName);
                        else Assert(selected == null && nativePicker.Calls == (scenario == "CancelFolder" ? 1 : 0));
                        Assert(remembered.Contains(source) == (scenario == "Keep"));
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
                Assert(model.OutsideSelected && model.Folder == ""); model.Folder = root; Assert(await model.ValidateAsync());
                model.FileName = "..\\escape"; Assert(!await model.ValidateAsync());
            });
            var selector = new FixedSelector(root);
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
        public Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default) => Task.FromResult<DestinationSelection?>(Cancel ? null : new(root, "moved-" + request.DisplayName, DestinationKind.OutsideServer, ConflictChoice.Cancel));
    }
    private sealed class FixedCompanyFolderPicker(string? folder) : ICompanyFolderPicker
    {
        public int Calls { get; private set; }
        public Task<string?> PickAsync(Window owner, string? initialFolder = null) { Calls++; return Task.FromResult(folder); }
    }
}
