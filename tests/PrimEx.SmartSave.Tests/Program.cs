using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
using PrimEx.SmartSave.FileManager.FileOperations;
using PrimEx.SmartSave.FileManager.ServerDiscovery;
using System.Security.AccessControl;
using System.Security.Principal;
using PrimEx.SmartSave.Monitoring.Watchers;
using System.Collections.Concurrent;

string root = Path.Combine(Path.GetTempPath(), "PrimEx-Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int passed = 0, failed = 0;
var log = new RotatingEventLog(Path.Combine(root, "logs"), new());
var transfer = new SafeTransferService(log);
async Task Test(string name, Func<Task> action)
{
    try { await action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
}
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
Task Sync(Action action) { action(); return Task.CompletedTask; }
async Task Throws<T>(Func<Task> action) where T : Exception
{ try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
TransferRequest Request(string source, string dest, TransferMode mode = TransferMode.Copy, ConflictChoice conflict = ConflictChoice.Cancel) => new(Guid.NewGuid(), source, dest, mode, conflict);
string NewFile(string content = "test document") { string path = Path.Combine(root, Guid.NewGuid() + ".txt"); File.WriteAllText(path, content); return path; }
try
{
    await Test("UNC containment uses component boundaries", () => Sync(() => {
        Assert(PathRules.IsWithin(@"\\server\share\a", @"\\server\share"));
        Assert(!PathRules.IsWithin(@"\\server\share-other", @"\\server\share"));
        Assert(PathRules.IsWithin(@"c:\work\file", @"C:\WORK"));
    }));
    foreach (string path in new[] { @"..\file", @"C:relative", @"C:\a\..\b", @"\\?\C:\a", @"C:\a:stream", @"C:\CON.txt", @"C:\a.\x", @"\\server", @"C:\a\bad?", "C:\\a\\x\n" })
        await Test("Reject invalid path " + path.Replace('\n', '_'), () => Throws<ArgumentException>(() => Sync(() => PathRules.NormalizeAbsolute(path))));
    await Test("Local drive resolver", () => Sync(() => Assert(new WindowsPathResolver().Resolve(root) == root)));
    await Test("Mapped drive resolved before approval", () => Sync(() => {
        var svc = new ServerFolderService(new() { ApprovedRoots = [@"\\test\share"] }, new FakeResolver());
        Assert(svc.IsApproved(@"P:\Projects")); Assert(!svc.IsApproved(@"\\test\share-other"));
    }));
    await Test("Empty roots approve nothing", () => Sync(() => Assert(!new ServerFolderService(new() { DiscoverMappedDrives = false }, new WindowsPathResolver()).IsApproved(root))));
    await Test("Unavailable destination check", async () => {
        var status = await new ServerFolderService(new(), new WindowsPathResolver()).CheckAsync(Path.Combine(root, "absent"), true);
        Assert(!status.Available);
    });
    await Test("Write permission probe", async () => Assert((await new ServerFolderService(new(), new WindowsPathResolver()).CheckAsync(root, true)).Available));
    await Test("Directory creation respects approval and traversal rules", async () => {
        var service = new ServerFolderService(new() { ApprovedRoots = [root] }, new WindowsPathResolver());
        Assert(Directory.Exists(await service.CreateDirectoryAsync(root, "created")));
        await Throws<ArgumentException>(() => service.CreateDirectoryAsync(root, "..\\escape"));
        await Throws<UnauthorizedAccessException>(() => new ServerFolderService(new(), new WindowsPathResolver()).CreateDirectoryAsync(root, "unapproved"));
    });
    await Test("Denied destination uses current user permissions", async () => {
        string denied = Path.Combine(root, "denied"); Directory.CreateDirectory(denied);
        var dir = new DirectoryInfo(denied); var acl = dir.GetAccessControl();
        var rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.CreateFiles, AccessControlType.Deny);
        acl.AddAccessRule(rule); dir.SetAccessControl(acl);
        try { Assert(!(await new ServerFolderService(new(), new WindowsPathResolver()).CheckAsync(denied, true)).Available); }
        finally { acl.RemoveAccessRuleSpecific(rule); dir.SetAccessControl(acl); }
    });
    await Test("Copy verifies bytes and preserves source", async () => {
        var src = NewFile("hello world"); var dest = src + ".copy"; var result = await transfer.TransferAsync(Request(src, dest));
        Assert(File.ReadAllText(dest) == "hello world" && File.Exists(src) && result.Bytes == 11 && result.Sha256.Length == 64);
    });
    await Test("Move deletes only verified source", async () => {
        var src = NewFile(); var dest = src + ".move"; var result = await transfer.TransferAsync(Request(src, dest, TransferMode.Move));
        Assert(result.SourceDeleted && !File.Exists(src) && File.Exists(dest));
    });
    await Test("Undeletable source retained after verified copy", async () => {
        var src = NewFile(); File.SetAttributes(src, FileAttributes.ReadOnly);
        try
        {
            var result = await transfer.TransferAsync(Request(src, src + ".retained", TransferMode.Move));
            Assert(!result.SourceDeleted && File.Exists(src) && File.ReadAllText(result.DestinationPath) == File.ReadAllText(src));
        }
        finally { File.SetAttributes(src, FileAttributes.Normal); }
    });
    await Test("No silent overwrite", async () => {
        var src = NewFile("new"); var dest = NewFile("old");
        await Throws<IOException>(() => transfer.TransferAsync(Request(src, dest))); Assert(File.ReadAllText(dest) == "old");
    });
    await Test("Keep both", async () => {
        var src = NewFile("new"); var dest = NewFile("old");
        var result = await transfer.TransferAsync(Request(src, dest, conflict: ConflictChoice.KeepBoth));
        Assert(result.DestinationPath != dest && File.ReadAllText(dest) == "old" && File.ReadAllText(result.DestinationPath) == "new");
    });
    await Test("Confirmed replace retains backup", async () => {
        var src = NewFile("new"); var dest = NewFile("old");
        await transfer.TransferAsync(Request(src, dest, conflict: ConflictChoice.ReplaceConfirmed));
        Assert(File.ReadAllText(dest) == "new" && File.ReadAllText(Directory.GetFiles(root, Path.GetFileName(dest) + ".primex-backup-*").Single()) == "old");
    });
    await Test("Locked source remains intact", async () => {
        var src = NewFile(); using var locked = File.Open(src, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await Throws<IOException>(() => transfer.TransferAsync(Request(src, src + ".locked", TransferMode.Move)));
        Assert(!File.Exists(src + ".locked"));
    });
    await Test("Missing destination preserves source", async () => {
        var src = NewFile(); await Throws<DirectoryNotFoundException>(() => transfer.TransferAsync(Request(src, Path.Combine(root, "missing", "x"), TransferMode.Move))); Assert(File.Exists(src));
    });
    await Test("Interrupted transfer preserves source, removes partial", async () => {
        var src = NewFile(); using (var f = File.OpenWrite(src)) f.SetLength(32 * 1024 * 1024);
        using var cancel = new CancellationTokenSource();
        var task = transfer.TransferAsync(Request(src, src + ".interrupted", TransferMode.Move), cancel.Token); cancel.Cancel();
        await Throws<OperationCanceledException>(async () => await task);
        Assert(File.Exists(src) && !File.Exists(src + ".interrupted") && Directory.GetFiles(root, "*.partial").Length == 0);
    });
    await Test("Concurrent duplicate transfer is idempotent", async () => {
        var src = NewFile(); var req = Request(src, src + ".duplicate", TransferMode.Move, ConflictChoice.KeepBoth);
        var results = await Task.WhenAll(transfer.TransferAsync(req), transfer.TransferAsync(req));
        Assert(results[0] == results[1] && Directory.GetFiles(root, Path.GetFileName(src) + ".duplicate*").Length == 1);
        await Throws<InvalidOperationException>(() => transfer.TransferAsync(req with { SourcePath = NewFile() }));
    });
    await Test("Default configuration safe", () => Sync(() => Assert(new AppOptions().Validate().Count == 0)));
    await Test("All completed file types are the default, including images and extensionless files", () => Sync(() => {
        string watched = Path.Combine(root, "all-types"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Folders = [watched];
        Assert(MonitoringFileTypes.IncludesAll(options.Monitoring.Extensions));
        var policy = new EligibleFilePolicy(options);
        foreach (var name in new[] { "photo.png", "photo.JPG", "photo.jpeg", "scan.webp", "drawing.dwg", "archive.7z", "document.odt", "design.psd", "clip.mp4", "custom.xyz", "README" })
        { string path = Path.Combine(watched, name); File.WriteAllText(path, "test"); Assert(policy.IsEligible(path)); }
    }));
    await Test("Wildcard still excludes partial downloads, temporary files, caches and recovery backups", () => Sync(() => {
        string watched = Path.Combine(root, "wildcard-filter"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true;
        var policy = new EligibleFilePolicy(options);
        foreach (var relative in new[] { "image.png.crdownload", "image.png.part", "image.png.partial", "image.png.filepart", "report.tmp", "report.lock", "~$report.docx", "desktop.ini", "thumbs.db", "report.pdf.primex-backup-123", @"Cache\image.png", @"node_modules\image.png", @"bin\image.png", @"obj\image.png", @".git\image.png", @"AppData\image.png", @"PrimEx Smart Save Staging\job\image.png" })
        { string path = Path.Combine(watched, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "ignore"); Assert(!policy.IsEligible(path)); }
    }));
    await Test("Explicit file type restrictions remain effective", () => Sync(() => {
        var options = new AppOptions(); options.Monitoring.Folders = [root]; options.Monitoring.Extensions = [".pdf"];
        string image = Path.Combine(root, "restricted.png"), pdf = Path.Combine(root, "restricted.pdf"); File.WriteAllText(image, "image"); File.WriteAllText(pdf, "pdf");
        var policy = new EligibleFilePolicy(options); Assert(!policy.IsEligible(image) && policy.IsEligible(pdf));
        Assert(!MonitoringFileTypes.IsValid(["*", ".pdf"]) && !MonitoringFileTypes.IsValid([]) && !MonitoringFileTypes.IsValid([".pn?"]));
    }));
    await Test("Old shipped defaults upgrade without rewriting settings or changing destinations", () => Sync(() => {
        foreach (var defaults in new[] {
            new[] { ".pdf", ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".zip" },
            new[] { ".pdf", ".docx", ".docm", ".xlsx", ".xlsm", ".xlsb", ".pptx", ".pptm", ".doc", ".xls", ".csv", ".rtf", ".txt", ".zip" } })
        {
            var old = new AppOptions { StartWithWindows = false, GuidanceSetupCompleted = true };
            old.Monitoring.Enabled = true; old.Monitoring.Folders = [root]; old.Monitoring.Extensions = defaults;
            old.CompanyServer.ApprovedRoots = [@"\\test\share"];
            string path = Path.Combine(root, Guid.NewGuid() + ".json"), json = System.Text.Json.JsonSerializer.Serialize(old); File.WriteAllText(path, json);
            var loaded = AppOptions.Load(path);
            Assert(MonitoringFileTypes.IncludesAll(loaded.Monitoring.Extensions) && loaded.Monitoring.Enabled && loaded.Monitoring.Folders.Single() == root);
            Assert(!loaded.StartWithWindows && loaded.GuidanceSetupCompleted && loaded.CompanyServer.ApprovedRoots.Single() == @"\\test\share" && File.ReadAllText(path) == json);
        }
    }));
    await Test("Loading custom file filters does not expand them", () => Sync(() => {
        var options = new AppOptions(); options.Monitoring.Extensions = [".pdf", ".png"];
        string path = Path.Combine(root, "custom-types.json"); File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(options));
        Assert(AppOptions.Load(path).Monitoring.Extensions.SequenceEqual(new[] { ".pdf", ".png" }));
    }));
    await Test("Legacy three-second prompt delay upgrades once without changing other settings", () => Sync(() => {
        var legacy = new AppOptions { StartWithWindows = false, GuidanceSetupCompleted = true };
        legacy.Monitoring.Enabled = true; legacy.Monitoring.Folders = [root]; legacy.Monitoring.QuietSeconds = 3;
        legacy.Monitoring.Extensions = [".png"]; legacy.CompanyServer.ApprovedRoots = [@"\\test\share"];
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(legacy))!;
        json["Monitoring"]!.AsObject().Remove("PromptTimingVersion");
        string path = Path.Combine(root, "legacy-delay.json"), original = json.ToJsonString(); File.WriteAllText(path, original);
        var loaded = AppOptions.Load(path);
        Assert(loaded.Monitoring.QuietSeconds == 1 && File.ReadAllText(path) == original);
        Assert(loaded.Monitoring.Enabled && loaded.Monitoring.Folders.Single() == root && loaded.Monitoring.Extensions.Single() == ".png");
        Assert(!loaded.StartWithWindows && loaded.GuidanceSetupCompleted && loaded.CompanyServer.ApprovedRoots.Single() == @"\\test\share");
        // Once the upgraded options are saved, an explicitly selected 3-second delay is retained.
        loaded.Monitoring.QuietSeconds = 3; File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(loaded));
        Assert(AppOptions.Load(path).Monitoring.QuietSeconds == 3);
    }));
    await Test("Custom legacy quiet periods and case-insensitive settings survive delay upgrade", () => Sync(() => {
        foreach (int seconds in new[] { 1, 2, 5, 60 })
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new AppOptions()))!;
            var monitoring = json["Monitoring"]!.DeepClone(); monitoring.AsObject().Remove("PromptTimingVersion"); monitoring["QuietSeconds"] = seconds;
            json.AsObject().Remove("Monitoring"); json["monitoring"] = monitoring;
            string path = Path.Combine(root, "custom-delay-" + seconds + ".json"), original = json.ToJsonString(); File.WriteAllText(path, original);
            Assert(AppOptions.Load(path).Monitoring.QuietSeconds == seconds && File.ReadAllText(path) == original);
        }
        var tuned = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new AppOptions()))!;
        tuned["Monitoring"]!["QuietSeconds"] = 3; tuned["Monitoring"]!["prompttimingversion"] = 1;
        tuned["Monitoring"]!.AsObject().Remove("PromptTimingVersion");
        string tunedPath = Path.Combine(root, "tuned-delay.json"); File.WriteAllText(tunedPath, tuned.ToJsonString());
        Assert(AppOptions.Load(tunedPath).Monitoring.QuietSeconds == 3);
    }));
    await Test("Agent instance lease detects a duplicate and releases cleanly", () => Sync(() => {
        string data = Path.Combine(root, "instance"); Directory.CreateDirectory(data);
        using (var first = AgentInstanceLease.TryAcquire(data)) { Assert(first != null); using var second = AgentInstanceLease.TryAcquire(data); Assert(second == null); }
        using var reopened = AgentInstanceLease.TryAcquire(data); Assert(reopened != null);
    }));
    await Test("Agent instance lease does not hide unavailable storage", () => Throws<DirectoryNotFoundException>(() => Sync(() => AgentInstanceLease.TryAcquire(Path.Combine(root, "missing-instance-folder")))));
    await Test("Invalid configuration rejected", () => Sync(() => {
        var options = new AppOptions { UiLanguage = "xx" }; options.SmartSave.AllowOutsideServer = false; options.Browser.AllowedExtensionIds = ["*"];
        Assert(options.Validate().Count >= 3);
    }));
    await Test("Durable journal recovery requires review", () => Sync(() => {
        var store = new OperationStore(Path.Combine(root, "queue")); var req = new SaveRequest(Guid.NewGuid(), NewFile(), "x", "test", DateTimeOffset.UtcNow);
        store.Save(new(req, OperationState.Transferring)); Assert(store.Load().Single().State == OperationState.NeedsReview);
        store.Save(new(req, OperationState.Completed)); Assert(store.Load().Single().State == OperationState.Completed);
    }));
    await Test("Journals from before Move and Copy selection retain Move semantics", () => Sync(() => {
        string folder = Path.Combine(root, "legacy-transfer-mode"); Directory.CreateDirectory(folder);
        var req = new SaveRequest(Guid.NewGuid(), NewFile(), "legacy.txt", "Manual", DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(folder, req.Id.ToString("N") + ".json"), System.Text.Json.JsonSerializer.Serialize(new { Request = req, State = OperationState.Transferring, Destination = req.SourcePath + ".server", ErrorCode = (string?)null }));
        var recovered = new OperationStore(folder).Load().Single();
        Assert(recovered.Mode == TransferMode.Move && recovered.State == OperationState.NeedsReview);
        var selection = System.Text.Json.JsonSerializer.Deserialize<DestinationSelection>("{\"Folder\":\"legacy\",\"FileName\":\"file.txt\",\"Kind\":0,\"Conflict\":0}");
        Assert(selection?.Mode == TransferMode.Move);
    }));
    await Test("Logs exclude filenames and rotate", () => Sync(() => {
        var logger = new RotatingEventLog(Path.Combine(root, "rotate"), new() { MaxBytes = 4096, RetainedFiles = 2 });
        for (int i = 0; i < 200; i++) logger.Write("TransferCompleted", Guid.NewGuid());
        Assert(Directory.GetFiles(Path.Combine(root, "rotate")).Length <= 3);
    }));
    await Test("Mark of the Web preserved", async () => {
        var src = NewFile(); File.WriteAllText(src + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        var result = await transfer.TransferAsync(Request(src, src + ".zoned", TransferMode.Move));
        Assert(File.ReadAllText(result.DestinationPath + ":Zone.Identifier").Contains("ZoneId=3"));
    });
    var browserOptions = new AppOptions(); browserOptions.Browser.Enabled = true;
    browserOptions.Browser.DownloadsFolder = root; browserOptions.Browser.AllowedExtensionIds = [new string('a', 32)];
    string origin = "chrome-extension://" + new string('a', 32) + "/";
    Guid downloadId = Guid.NewGuid(); string staging = Path.Combine(browserOptions.Browser.StagingRoot, downloadId.ToString("N")); Directory.CreateDirectory(staging);
    string stagedFile = Path.Combine(staging, "sample.pdf"); File.WriteAllText(stagedFile, "test");
    var download = new NativeMessage(1, "complete", downloadId, stagedFile, true, 4, DateTimeOffset.UtcNow);
    await Test("Authorized completed staging request", () => Sync(() => { NativeProtocol.Validate(new(origin, download), browserOptions); NativeProtocol.ValidateCompletedFile(download); }));
    await Test("Unapproved extension rejected", () => Throws<UnauthorizedAccessException>(() => Sync(() => NativeProtocol.Validate(new("chrome-extension://" + new string('b', 32) + "/", download), browserOptions))));
    await Test("Arbitrary source outside staging rejected", () => Throws<UnauthorizedAccessException>(() => Sync(() => NativeProtocol.Validate(new(origin, download with { SourcePath = NewFile() }), browserOptions))));
    await Test("Different job folder rejected", () => Throws<UnauthorizedAccessException>(() => Sync(() => NativeProtocol.Validate(new(origin, download with { Id = Guid.NewGuid() }), browserOptions))));
    await Test("Partial download rejected", () => Throws<InvalidDataException>(() => Sync(() => NativeProtocol.Validate(new(origin, download with { Complete = false }), browserOptions))));
    await Test("Download size mismatch rejected", () => Throws<InvalidDataException>(() => Sync(() => NativeProtocol.ValidateCompletedFile(download with { Bytes = 5 }))));
    await Test("Null message rejected", () => Throws<InvalidDataException>(() => Sync(() => NativeProtocol.Validate(new(origin, null!), browserOptions))));
    await Test("Browser disabled fails closed", () => Throws<UnauthorizedAccessException>(() => Sync(() => NativeProtocol.Validate(new(origin, download), new AppOptions()))));
    await Test("Unsupported protocol rejected", () => Throws<InvalidDataException>(() => Sync(() => NativeProtocol.Validate(new(origin, download with { Version = 2 }), browserOptions))));
    await Test("Framed messaging roundtrip", async () => {
        using var stream = new MemoryStream(); await NativeProtocol.WriteAsync(stream, download); stream.Position = 0;
        Assert(await NativeProtocol.ReadAsync<NativeMessage>(stream) == download);
    });
    await Test("Oversized native frame rejected", async () => {
        using var stream = new MemoryStream(BitConverter.GetBytes(NativeProtocol.MaxMessageBytes + 1));
        await Throws<InvalidDataException>(async () => await NativeProtocol.ReadAsync<NativeMessage>(stream));
    });
    await Test("Truncated native frame rejected", async () => {
        using var stream = new MemoryStream(new byte[] { 10, 0, 0, 0, 123 });
        await Throws<EndOfStreamException>(async () => await NativeProtocol.ReadAsync<NativeMessage>(stream));
    });
    await Test("Unknown message fields rejected", async () => {
        using var stream = new MemoryStream(); await NativeProtocol.WriteAsync(stream, new { version = 1, command = "ping", destination = "arbitrary" }); stream.Position = 0;
        await Throws<System.Text.Json.JsonException>(async () => await NativeProtocol.ReadAsync<NativeMessage>(stream));
    });
    await Test("Null config sections rejected", () => Sync(() => Assert(new AppOptions { Browser = null! }.Validate().Count > 0)));
    await Test("Settings save validates before replacing configuration", () => Sync(() => {
        string path = Path.Combine(root, "settings.json"); string prior = System.Text.Json.JsonSerializer.Serialize(new AppOptions()); File.WriteAllText(path, prior);
        var updated = new AppOptions(); updated.Monitoring.Enabled = true;
        bool denied = false; try { ConfigurationStore.Save(path, updated, prior); } catch (InvalidDataException) { denied = true; }
        Assert(denied && File.ReadAllText(path) == prior);
        updated.Monitoring.Enabled = false; updated.Monitoring.IncludeSubdirectories = true; ConfigurationStore.Save(path, updated, prior);
        Assert(AppOptions.Load(path).Monitoring.IncludeSubdirectories && !Directory.GetFiles(root, "settings.json.*.tmp").Any());
    }));
    await Test("Settings save refuses to overwrite concurrent edits", () => Sync(() => {
        string path = Path.Combine(root, "edited-settings.json"); File.WriteAllText(path, "changed by another editor");
        bool denied = false; try { ConfigurationStore.Save(path, new AppOptions(), "original"); } catch (InvalidDataException) { denied = true; }
        Assert(denied && File.ReadAllText(path) == "changed by another editor");
    }));
    await Test("Whole-drive monitoring rejected", () => Sync(() => {
        var options = new AppOptions(); options.Monitoring.Folders = [Path.GetPathRoot(root)!]; Assert(options.Validate().Count > 0);
    }));
    await Test("Default watcher offers a completed new save within two and a half seconds", async () => {
        string watched = Path.Combine(root, "fast-prompt"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched];
        var offered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += path => offered.TrySetResult(path); await monitor.StartAsync();
        string document = Path.Combine(watched, "completed.pdf"); File.WriteAllText(document, "complete");
        Assert(await offered.Task.WaitAsync(TimeSpan.FromSeconds(2.5)) == document);
    });
    await Test("Faster default waits while a new document keeps changing", async () => {
        string watched = Path.Combine(root, "fast-prompt-writing"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched];
        var offered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += path => offered.TrySetResult(path); await monitor.StartAsync();
        string document = Path.Combine(watched, "still-saving.pdf");
        for (int part = 0; part < 7; part++)
        {
            File.AppendAllText(document, "next part\n"); await Task.Delay(250); Assert(!offered.Task.IsCompleted);
        }
        Assert(await offered.Task.WaitAsync(TimeSpan.FromSeconds(2.5)) == document);
    });
    await Test("Opt-in watcher ignores temporary files, debounces saves, waits for locks", async () => {
        string watched = Path.Combine(root, "watched"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.QuietSeconds = 1;
        var events = new ConcurrentQueue<string>();
        await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += events.Enqueue; monitor.Start();
        string doc = Path.Combine(watched, "report.pdf"), lockedPath = Path.Combine(watched, "locked.docx");
        File.WriteAllText(Path.Combine(watched, "~$lock.docx"), "ignore"); File.WriteAllText(Path.Combine(watched, "partial.tmp"), "ignore");
        File.WriteAllText(doc, "report");
        using (var held = new FileStream(lockedPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            held.Write(new byte[100]);
            for (int i = 0; i < 60 && !events.Contains(doc); i++) await Task.Delay(100);
            Assert(events.Count == 1 && events.Contains(doc));
        }
        for (int i = 0; i < 60 && !events.Contains(lockedPath); i++) await Task.Delay(100);
        Assert(events.Count == 2 && events.Contains(lockedPath));
        File.AppendAllText(doc, " changed"); await Task.Delay(1800);
        Assert(events.Count(x => x == doc) == 1);
    });
    await Test("Remembered location survives edits and a new store instance", () => Sync(() => {
        string data = Path.Combine(root, "remembered-edit"), document = NewFile();
        var choices = new RememberedLocationStore(data);
        Assert(!choices.Contains(document) && choices.Remember(document));
        File.WriteAllText(document, "second save");
        Assert(new RememberedLocationStore(data).Contains(document));
        File.AppendAllText(document, "third save"); Assert(choices.Contains(document));
        Assert(!File.ReadAllText(Directory.GetFiles(data).Single()).Contains(document));
    }));
    await Test("A different path or recreated file does not inherit a Keep choice", () => Sync(() => {
        var choices = new RememberedLocationStore(Path.Combine(root, "remembered-new"));
        string first = NewFile(), copy = first + ".new.txt"; File.Copy(first, copy);
        Assert(choices.Remember(first) && !choices.Contains(copy));
        DateTime created = File.GetCreationTimeUtc(first);
        File.SetCreationTimeUtc(first, created.AddMinutes(1)); Assert(!choices.Contains(first));
        Assert(!choices.Contains(first + ".missing") && !choices.Remember(first + ".missing"));
    }));
    await Test("Damaged or unavailable location memory never suppresses a prompt", () => Sync(() => {
        string data = Path.Combine(root, "remembered-damaged"), document = NewFile();
        var choices = new RememberedLocationStore(data); Assert(choices.Remember(document));
        File.WriteAllText(Directory.GetFiles(data).Single(), "damaged"); Assert(!choices.Contains(document));
        Assert(choices.Remember(document) && choices.Contains(document));
        Directory.Delete(data, true); File.WriteAllText(data, "not a directory");
        Assert(!choices.Contains(document) && !choices.Remember(document));
    }));
    await Test("Restarted watcher skips repeated saves for remembered files", async () => {
        string watched = Path.Combine(root, "remembered-watch"); Directory.CreateDirectory(watched);
        string document = Path.Combine(watched, "existing.txt"); File.WriteAllText(document, "first save");
        string memory = Path.Combine(root, "remembered-watch-state"); Assert(new RememberedLocationStore(memory).Remember(document));
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.QuietSeconds = 1;
        var events = new ConcurrentQueue<string>();
        await using var monitor = new DirectoryMonitor(options, log, new RememberedLocationStore(memory)); monitor.Candidate += events.Enqueue; monitor.Start();
        File.WriteAllText(document, "second save"); await Task.Delay(1600);
        File.WriteAllText(document, "third save"); string other = Path.Combine(watched, "new.txt"); File.WriteAllText(other, "new document");
        for (int i = 0; i < 60 && !events.Contains(other); i++) await Task.Delay(100);
        Assert(events.Count == 1 && events.Single() == other && File.ReadAllText(document) == "third save");
    });
    await Test("Existing documents resave in place without guidance, including atomic replacement and rename", async () => {
        string watched = Path.Combine(root, "existing-resaves"), nested = Path.Combine(watched, "project"); Directory.CreateDirectory(nested);
        string document = Path.Combine(nested, "existing.docx"), renamed = Path.Combine(nested, "renamed.docx");
        File.WriteAllText(document, "already saved before startup");
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true; options.Monitoring.QuietSeconds = 1;
        var events = new ConcurrentQueue<string>();
        await using (var monitor = new DirectoryMonitor(options, log))
        {
            monitor.Candidate += events.Enqueue; monitor.Start();
            File.WriteAllText(document, "ordinary resave"); await Task.Delay(1600); Assert(events.IsEmpty);
            string temporary = Path.Combine(nested, "office-save.tmp"); File.WriteAllText(temporary, "atomic resave");
            File.SetCreationTimeUtc(temporary, DateTime.UtcNow.AddMinutes(1));
            File.Move(temporary, document, true); await Task.Delay(1600); Assert(events.IsEmpty);
            Assert(File.ReadAllText(document) == "atomic resave");
            File.Move(document, renamed); await Task.Delay(1600); Assert(events.IsEmpty);
            File.AppendAllText(renamed, " after rename");
            string fresh = Path.Combine(nested, "new.pdf"); File.WriteAllText(fresh, "first save");
            for (int i = 0; i < 60 && !events.Contains(fresh); i++) await Task.Delay(100);
            Assert(events.Count == 1 && events.Single() == fresh);
        }
        events.Clear();
        await using (var restarted = new DirectoryMonitor(options, log))
        {
            restarted.Candidate += events.Enqueue; restarted.Start();
            File.AppendAllText(renamed, " after restart");
            File.AppendAllText(Path.Combine(nested, "new.pdf"), " after restart without a remembered choice");
            await Task.Delay(1600); Assert(events.IsEmpty);
        }
    });
    await Test("First-save guidance is offered only once per path even when the document is replaced", async () => {
        string watched = Path.Combine(root, "new-resaves"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.QuietSeconds = 1;
        var events = new ConcurrentQueue<string>();
        await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += events.Enqueue; monitor.Start();
        string document = Path.Combine(watched, "new.xlsx"); File.WriteAllText(document, "first save");
        for (int i = 0; i < 60 && !events.Contains(document); i++) await Task.Delay(100);
        Assert(events.Count == 1 && events.Single() == document);
        File.AppendAllText(document, " resaved"); await Task.Delay(1600); Assert(events.Count == 1);
        string temporary = Path.Combine(watched, "replacement.tmp"); File.WriteAllText(temporary, "replacement save");
        File.SetCreationTimeUtc(temporary, DateTime.UtcNow.AddMinutes(1)); File.Move(temporary, document, true);
        await Task.Delay(1600);
        Assert(events.Count == 1 && File.ReadAllText(document) == "replacement save");
        string saveAs = Path.Combine(watched, "save-as.xlsx"); File.Copy(document, saveAs);
        for (int i = 0; i < 60 && !events.Contains(saveAs); i++) await Task.Delay(100);
        Assert(events.Count == 2 && events.Contains(saveAs));
    });
    await Test("Disabled watcher has no filesystem subscriptions", async () => {
        var options = new AppOptions(); options.Monitoring.Folders = [root];
        int count = 0; await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += _ => Interlocked.Increment(ref count); monitor.Start();
        File.WriteAllText(Path.Combine(root, "disabled.pdf"), "test"); await Task.Delay(1100); Assert(count == 0);
    });
    await Test("Startup index preserves scope and exclusions and supports cancellation", async () => {
        string watched = Path.Combine(root, "indexed-policy"), nested = Path.Combine(watched, "project"), cache = Path.Combine(watched, "node_modules"), company = Path.Combine(watched, "company");
        foreach (string folder in new[] { watched, nested, cache, company }) Directory.CreateDirectory(folder);
        string top = Path.Combine(watched, "top.pdf"), child = Path.Combine(nested, "child.docx");
        foreach (string path in new[] { top, child, Path.Combine(cache, "ignored.pdf"), Path.Combine(company, "ignored.pdf"), Path.Combine(watched, "unfinished.crdownload"), Path.Combine(watched, "~$lock.docx") }) File.WriteAllText(path, "sample");
        string hidden = Path.Combine(watched, "hidden.pdf"); File.WriteAllText(hidden, "hidden"); File.SetAttributes(hidden, FileAttributes.Hidden);
        var options = new AppOptions(); options.Monitoring.Folders = [watched]; options.CompanyServer.ApprovedRoots = [company];
        var policy = new EligibleFilePolicy(options);
        Assert(policy.EnumerateExistingFiles(watched).SequenceEqual(new[] { top }));
        options.Monitoring.IncludeSubdirectories = true;
        Assert(policy.EnumerateExistingFiles(watched).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(new[] { top, child }));
        using var cancelled = new CancellationTokenSource();
        using var enumerator = policy.EnumerateExistingFiles(watched, cancelled.Token).GetEnumerator(); Assert(enumerator.MoveNext());
        cancelled.Cancel(); await Throws<OperationCanceledException>(() => Sync(() => enumerator.MoveNext()));
    });
    await Test("Overlapping recursive roots use one watcher and ignored events do not enter the pending queue", async () => {
        string watched = Path.Combine(root, "overlapping"), nested = Path.Combine(watched, "project"); Directory.CreateDirectory(nested);
        string existing = Path.Combine(nested, "existing.pdf"); File.WriteAllText(existing, "before startup");
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.IncludeSubdirectories = true; options.Monitoring.QuietSeconds = 1;
        options.Monitoring.Folders = [nested, watched.Replace('\\', '/')];
        var events = new ConcurrentQueue<string>(); await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += events.Enqueue;
        await monitor.StartAsync(); Assert(monitor.ActiveFolderCount == 1 && monitor.IndexedFileCount == 1);
        File.AppendAllText(existing, " resaved");
        for (int i = 0; i < 50; i++) File.WriteAllText(Path.Combine(watched, $"partial-{i}.tmp"), "ignored");
        await Task.Delay(500); Assert(monitor.PendingFileCount == 0 && events.IsEmpty);
        string fresh = Path.Combine(nested, "new.pdf"); File.WriteAllText(fresh, "first save");
        for (int i = 0; i < 60 && !events.Contains(fresh); i++) await Task.Delay(100);
        Assert(events.Count == 1 && events.Single() == fresh);
    });
    await Test("Async startup and concurrent disposal do not block the caller or leak watchers", async () => {
        string watched = Path.Combine(root, "async-startup"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched];
        using var blocking = new BlockingStartupLog();
        var monitor = new DirectoryMonitor(options, blocking);
        Task starting = monitor.StartAsync();
        try
        {
            await blocking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert(!starting.IsCompleted && ReferenceEquals(starting, monitor.StartAsync()));
            Task firstStop = monitor.DisposeAsync().AsTask(), secondStop = monitor.DisposeAsync().AsTask();
            Assert(!firstStop.IsCompleted && ReferenceEquals(firstStop, secondStop));
            blocking.Release.Set();
            await Task.WhenAll(starting, firstStop, secondStop).WaitAsync(TimeSpan.FromSeconds(5));
            bool rejected = false; try { await monitor.StartAsync(); } catch (ObjectDisposedException) { rejected = true; } Assert(rejected);
        }
        finally { blocking.Release.Set(); await monitor.DisposeAsync(); }
    });
    await Test("Coding project markers suppress the entire project while neighboring documents remain eligible", () => Sync(() => {
        string watched = Path.Combine(root, "coding-scope"); Directory.CreateDirectory(watched);
        var options = new AppOptions(); options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true;
        foreach (string marker in new[] { ".git", ".vscode", ".vs", "package.json", "pyproject.toml", "app.csproj", "workspace.code-workspace" })
        {
            string project = Path.Combine(watched, "project-" + marker.Replace('.', '_')), nested = Path.Combine(project, "src"); Directory.CreateDirectory(nested);
            string markerPath = Path.Combine(project, marker);
            if (marker is ".git" or ".vscode" or ".vs") Directory.CreateDirectory(markerPath); else File.WriteAllText(markerPath, "project marker");
            var policy = new EligibleFilePolicy(options);
            foreach (string name in new[] { "source.cs", "settings.json", "README.md", "image.png", "document.pdf" })
            {
                string path = Path.Combine(nested, name); File.WriteAllText(path, "project asset");
                Assert(!policy.IsEligible(path) && policy.IsSuppressed(path));
            }
            Assert(!policy.EnumerateExistingFiles(watched).Any(path => PathRules.IsWithin(path, project)));
            policy.ObserveChange(Path.Combine(project, "node_modules", "dependency", "package.json"));
            Assert(!policy.CanBeEligible(Path.Combine(nested, "source.cs")));
            // Selecting a subfolder directly still recognizes its project ancestor.
            options.Monitoring.Folders = [nested]; Assert(!new EligibleFilePolicy(options).IsEligible(Path.Combine(nested, "document.pdf")));
            options.Monitoring.Folders = [watched];
        }
        string worktree = Path.Combine(watched, "worktree"); Directory.CreateDirectory(worktree); File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: elsewhere");
        string worktreeFile = Path.Combine(worktree, "new.txt"); File.WriteAllText(worktreeFile, "worktree document"); Assert(!new EligibleFilePolicy(options).IsEligible(worktreeFile));
        string ordinary = Path.Combine(watched, "normal.pdf"); File.WriteAllText(ordinary, "normal document"); Assert(new EligibleFilePolicy(options).IsEligible(ordinary));
        options.Monitoring.IgnoreDevelopmentProjects = false; Assert(new EligibleFilePolicy(options).IsEligible(worktreeFile));
        var projects = new DevelopmentProjectPolicy();
        Assert(!projects.IsProject(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) && !projects.IsProject(Path.GetTempPath()));
        foreach (var known in KnownFolderService.GetSuggestions()) Assert(!projects.IsProject(known.Path));
    }));
    await Test("Explicit excluded folders use path boundaries and persist independently of project detection", () => Sync(() => {
        string watched = Path.Combine(root, "excluded-scope"), excluded = Path.Combine(watched, "My Project"), sibling = excluded + "-other";
        Directory.CreateDirectory(Path.Combine(excluded, "src")); Directory.CreateDirectory(sibling);
        string ignored = Path.Combine(excluded, "src", "readme.md"), ordinary = Path.Combine(sibling, "report.pdf"); File.WriteAllText(ignored, "code"); File.WriteAllText(ordinary, "document");
        var options = new AppOptions(); options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true; options.Monitoring.IgnoreDevelopmentProjects = false;
        options.Monitoring.ExcludedFolders = [excluded.ToUpperInvariant().Replace('\\', '/')];
        var policy = new EligibleFilePolicy(options);
        Assert(!policy.CanBeEligible(ignored) && !policy.IsEligible(ignored) && policy.IsEligible(ordinary));
        Assert(policy.EnumerateExistingFiles(watched).SequenceEqual(new[] { ordinary }));
        string config = Path.Combine(root, "exclusions.json"); File.WriteAllText(config, System.Text.Json.JsonSerializer.Serialize(options));
        var loaded = AppOptions.Load(config); Assert(!loaded.Monitoring.IgnoreDevelopmentProjects && loaded.Monitoring.ExcludedFolders.SequenceEqual(options.Monitoring.ExcludedFolders));
        options.Monitoring.ExcludedFolders = ["relative"]; Assert(options.Validate().Count > 0);
        options.Monitoring.ExcludedFolders = [""]; Assert(options.Validate().Count > 0);
        options.Monitoring.ExcludedFolders = null!; Assert(options.Validate().Count > 0);
    }));
    await Test("Old configuration gains coding-project protection without rewriting its settings", () => Sync(() => {
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new AppOptions()))!;
        json["Monitoring"]!.AsObject().Remove("ExcludedFolders"); json["Monitoring"]!.AsObject().Remove("IgnoreDevelopmentProjects");
        string config = Path.Combine(root, "old-project-settings.json"), original = json.ToJsonString(); File.WriteAllText(config, original);
        var loaded = AppOptions.Load(config); Assert(loaded.Monitoring.IgnoreDevelopmentProjects && loaded.Monitoring.ExcludedFolders.Length == 0 && File.ReadAllText(config) == original);
    }));
    await Test("Projects initialized while monitoring is running never prompt for code or assets", async () => {
        string watched = Path.Combine(root, "live-projects"), git = Path.Combine(watched, "git-project"), manifest = Path.Combine(watched, "node-project");
        Directory.CreateDirectory(git); Directory.CreateDirectory(manifest);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true; options.Monitoring.QuietSeconds = 1;
        var events = new ConcurrentQueue<string>(); await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += events.Enqueue; await monitor.StartAsync();
        File.WriteAllText(Path.Combine(git, "README.md"), "created just before git init"); Directory.CreateDirectory(Path.Combine(git, ".git"));
        File.WriteAllText(Path.Combine(git, "new.cs"), "source"); File.WriteAllText(Path.Combine(git, "asset.png"), "asset");
        File.WriteAllText(Path.Combine(manifest, "first.json"), "before manifest"); File.WriteAllText(Path.Combine(manifest, "package.json"), "{}");
        File.WriteAllText(Path.Combine(manifest, "report.pdf"), "project document");
        await Task.Delay(1800); Assert(events.IsEmpty);
        string outside = Path.Combine(watched, "normal.pdf"); File.WriteAllText(outside, "normal document");
        for (int i = 0; i < 60 && !events.Contains(outside); i++) await Task.Delay(100);
        Assert(events.Count == 1 && events.Single() == outside);
    });
    await Test("Editor leases handle multiple windows, path boundaries, close and invalid records", () => Sync(() => {
        string data = Path.Combine(root, "editor-registry"), first = Path.Combine(root, "unmarked-editor-one"), second = Path.Combine(root, "unmarked-editor-two");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        using var registry = new EditorWorkspaceRegistry(data);
        void Publish(string name, string[] folders, string editor = "VSCode", DateTimeOffset? updated = null, int? pid = null, int version = 1)
        {
            File.WriteAllText(Path.Combine(data, name + ".json"), System.Text.Json.JsonSerializer.Serialize(new { version, editor, processId = pid ?? Environment.ProcessId, updatedUtc = updated ?? DateTimeOffset.UtcNow, folders })); registry.Refresh();
        }
        Publish("code", [first]); Publish("studio", [second], "VisualStudio");
        Assert(registry.ActiveWorkspaceCount == 2 && registry.Contains(Path.Combine(first, "src", "new.json")) && registry.Contains(second.ToUpperInvariant()));
        Assert(!registry.Contains(first + "-other\\file.pdf"));
        File.Delete(Path.Combine(data, "code.json")); registry.Refresh(); Assert(!registry.Contains(first) && registry.Contains(second));
        Publish("studio", [second], "VisualStudio", DateTimeOffset.UtcNow.AddMinutes(-2)); Assert(!registry.Contains(second));
        Publish("studio", [second], "VisualStudio", DateTimeOffset.UtcNow.AddMinutes(1)); Assert(!registry.Contains(second));
        Publish("studio", [second], "VisualStudio", pid: int.MaxValue); Assert(!registry.Contains(second));
        Publish("studio", [second], "UnknownEditor"); Assert(!registry.Contains(second));
        Publish("studio", [second], version: 2); Assert(!registry.Contains(second));
        Publish("studio", ["relative"]); Assert(!registry.Contains(second));
        Publish("studio", [Path.GetPathRoot(root)!]); Assert(!registry.Contains(second));
        File.WriteAllText(Path.Combine(data, "studio.json"), "{damaged"); registry.Refresh(); Assert(registry.ActiveWorkspaceCount == 0);
        File.WriteAllText(Path.Combine(data, "studio.json"), new string('x', 65537)); registry.Refresh(); Assert(registry.ActiveWorkspaceCount == 0);
        Publish("studio", [second], "VisualStudio"); Assert(registry.Contains(second));
    }));
    await Test("Registry notices atomic publisher updates without restarting the monitor", async () => {
        string data = Path.Combine(root, "editor-atomic"), workspace = Path.Combine(root, "editor-atomic-workspace"); Directory.CreateDirectory(workspace);
        using var registry = new EditorWorkspaceRegistry(data); Assert(!registry.Contains(workspace));
        string temporary = Path.Combine(data, "lease.tmp"), final = Path.Combine(data, "lease.json");
        File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(new { version = 1, editor = "VSCode", processId = Environment.ProcessId, updatedUtc = DateTimeOffset.UtcNow, folders = new[] { workspace } }));
        File.Move(temporary, final);
        for (int i = 0; i < 40 && !registry.Contains(workspace); i++) await Task.Delay(50);
        Assert(registry.Contains(workspace)); File.Delete(final);
        for (int i = 0; i < 40 && registry.Contains(workspace); i++) await Task.Delay(50);
        Assert(!registry.Contains(workspace));
    });
    await Test("Open unmarked editor workspaces suppress new files and closing restores guidance", async () => {
        string watched = Path.Combine(root, "editor-live"), project = Path.Combine(watched, "new-empty-project"), data = Path.Combine(root, "editor-live-leases"); Directory.CreateDirectory(project);
        using var registry = new EditorWorkspaceRegistry(data);
        string lease = Path.Combine(data, "code.json");
        File.WriteAllText(lease, System.Text.Json.JsonSerializer.Serialize(new { version = 1, editor = "VSCode", processId = Environment.ProcessId, updatedUtc = DateTimeOffset.UtcNow, folders = new[] { project } })); registry.Refresh();
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true; options.Monitoring.QuietSeconds = 1;
        var policy = new EligibleFilePolicy(options, registry); Assert(!policy.CanBeEligible(Path.Combine(project, "new.txt")));
        options.Monitoring.IgnoreDevelopmentProjects = false; Assert(policy.CanBeEligible(Path.Combine(project, "new.txt"))); options.Monitoring.IgnoreDevelopmentProjects = true;
        var events = new ConcurrentQueue<string>(); await using var monitor = new DirectoryMonitor(options, log, editorWorkspaces: registry); monitor.Candidate += events.Enqueue; await monitor.StartAsync();
        foreach (string name in new[] { "asset.png", "README.md", "settings.json", "report.pdf" }) File.WriteAllText(Path.Combine(project, name), "editor asset");
        await Task.Delay(1600); Assert(events.IsEmpty && monitor.PendingFileCount == 0);
        File.Delete(lease); registry.Refresh();
        string afterClose = Path.Combine(project, "after-close.pdf"); File.WriteAllText(afterClose, "ordinary file after editor close");
        for (int i = 0; i < 60 && !events.Contains(afterClose); i++) await Task.Delay(100);
        Assert(events.Count == 1 && events.Single() == afterClose);
    });
    await Test("Both editor publishers produce leases readable by the desktop registry", async () => {
        string data = Path.Combine(root, "editor-publishers"), workspace = Path.Combine(root, "publisher-empty-project"); Directory.CreateDirectory(workspace);
        using var registry = new EditorWorkspaceRegistry(Path.Combine(data, "editor-workspaces"));
        var start = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.GetFullPath("tests/editors/publish-lease.cjs")); start.ArgumentList.Add(Path.Combine(data, "editor-workspaces")); start.ArgumentList.Add(workspace);
        using var publisher = System.Diagnostics.Process.Start(start)!;
        for (int i = 0; i < 100 && !registry.Contains(workspace); i++) { Assert(!publisher.HasExited); await Task.Delay(50); }
        Assert(registry.Contains(workspace));
        File.WriteAllText(Path.Combine(data, "editor-workspaces", "stop"), "done"); await publisher.WaitForExitAsync(); registry.Refresh(); Assert(publisher.ExitCode == 0 && !registry.Contains(workspace));
        var studio = new PrimEx.SmartSave.VisualStudio.WorkspaceLeasePublisher(data);
        studio.SetFolders([workspace]); registry.Refresh(); Assert(registry.Contains(workspace));
        studio.SetFolders([], generation: 10); studio.SetFolders([workspace], generation: 9); registry.Refresh(); Assert(!registry.Contains(workspace));
        studio.Dispose();
    });
    await Test("Recursive scope stays inside selected folders and excludes partial downloads", () => Sync(() => {
        string watched = Path.Combine(root, "scope"); string nested = Path.Combine(watched, "subfolder"); Directory.CreateDirectory(nested);
        string document = Path.Combine(nested, "report.xlsx"); File.WriteAllText(document, "test");
        string partial = Path.Combine(watched, "download.part"); File.WriteAllText(partial, "unfinished");
        var options = new AppOptions(); options.Monitoring.Folders = [watched]; options.Monitoring.Extensions = [".xlsx", ".part"];
        var policy = new EligibleFilePolicy(options); Assert(!policy.IsEligible(document));
        options.Monitoring.IncludeSubdirectories = true; Assert(policy.IsEligible(document) && !policy.IsEligible(partial));
        string other = Path.Combine(root, "scope-other"); Directory.CreateDirectory(other); string outside = Path.Combine(other, "report.xlsx"); File.WriteAllText(outside, "outside"); Assert(!policy.IsEligible(outside));
        options.CompanyServer.ApprovedRoots = [nested]; Assert(!policy.IsEligible(document));
    }));
    await Test("Live recursive watcher detects completed browser-style rename", async () => {
        string watched = Path.Combine(root, "recursive"); string nested = Path.Combine(watched, "project"); Directory.CreateDirectory(nested);
        var options = new AppOptions(); options.Monitoring.Enabled = true; options.Monitoring.Folders = [watched]; options.Monitoring.IncludeSubdirectories = true; options.Monitoring.QuietSeconds = 1;
        var events = new ConcurrentQueue<string>(); await using var monitor = new DirectoryMonitor(options, log); monitor.Candidate += events.Enqueue; monitor.Start();
        Assert(monitor.ActiveFolderCount == 1);
        string temporary = Path.Combine(nested, "download.xlsx.crdownload"); string final = Path.Combine(nested, "download.xlsx"); File.WriteAllText(temporary, "completed test download");
        await Task.Delay(1400); Assert(events.IsEmpty); File.Move(temporary, final);
        for (int i = 0; i < 60 && !events.Contains(final); i++) await Task.Delay(100);
        Assert(events.Count == 1 && events.Single() == final);
    });
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

sealed class FakeResolver : IPathResolver
{
    public string Resolve(string path) => PathRules.NormalizeAbsolute(path.StartsWith("P:", StringComparison.OrdinalIgnoreCase) ? @"\\test\share" + path[2..] : path);
}

sealed class BlockingStartupLog : IEventLog, IDisposable
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ManualResetEventSlim Release { get; } = new();
    public void Write(string eventName, Guid? operationId = null, string? code = null)
    {
        if (eventName != "MonitoringStarted") return;
        Entered.TrySetResult(); Release.Wait();
    }
    public void Dispose() => Release.Dispose();
}
