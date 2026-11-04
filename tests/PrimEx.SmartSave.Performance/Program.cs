using System.Diagnostics;
using System.Text.Json;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Monitoring.Watchers;

// Reproducible local corpus; no company files, transfers or user settings.
// Build with BenchmarkAssemblyDir to compare an earlier published binary.
string corpus = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "PrimEx-Performance-Corpus"));
Directory.CreateDirectory(corpus);
foreach (string name in new[] { "documents", "node_modules", "Cache" })
{
    string folder = Path.Combine(corpus, name); Directory.CreateDirectory(folder);
    int count = name == "documents" ? 2000 : 6000;
    for (int i = 0; i < count; i++)
    {
        string path = Path.Combine(folder, $"sample-{i}.txt");
        if (!File.Exists(path)) File.WriteAllText(path, "synthetic performance sample");
    }
}
var options = new AppOptions();
options.Monitoring.Enabled = true; options.Monitoring.IncludeSubdirectories = true;
options.Monitoring.Folders = [corpus];
var startupMs = new List<double>(); var allocatedBytes = new List<long>(); var cpuMs = new List<double>();
using var process = Process.GetCurrentProcess();
for (int i = 0; i < 4; i++)
{
    await using var monitor = new DirectoryMonitor(options, new QuietLog());
    long allocated = GC.GetTotalAllocatedBytes(true); TimeSpan cpu = process.TotalProcessorTime;
    var watch = Stopwatch.StartNew(); monitor.Start(); watch.Stop();
    if (i > 0)
    {
        startupMs.Add(watch.Elapsed.TotalMilliseconds);
        allocatedBytes.Add(GC.GetTotalAllocatedBytes(true) - allocated);
        cpuMs.Add((process.TotalProcessorTime - cpu).TotalMilliseconds);
    }
}
await using var idle = new DirectoryMonitor(options, new QuietLog()); idle.Start();
// Allow tiered JIT/background collection to settle before sampling idle CPU.
await Task.Delay(10000); GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(1000);
TimeSpan idleCpu = process.TotalProcessorTime;
await Task.Delay(3000);
Console.WriteLine(JsonSerializer.Serialize(new
{
    version = typeof(DirectoryMonitor).Assembly.GetName().Version?.ToString(),
    eligibleFiles = 2000, excludedFiles = 12000, warmRuns = 3,
    startupMs, allocatedBytes, cpuMs, idleCpuMsOver3Seconds = (process.TotalProcessorTime - idleCpu).TotalMilliseconds
}, new JsonSerializerOptions { WriteIndented = true }));

sealed class QuietLog : IEventLog
{
    public void Write(string eventName, Guid? operationId = null, string? code = null) { }
}
