using System.Text.Json;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Core.Services;

public sealed class RotatingEventLog(string folder, LoggingOptions options) : IEventLog
{
    private readonly object gate = new();
    public void Write(string eventName, Guid? operationId = null, string? code = null)
    {
        if (options.Level == "None" || options.Level == "Error" && code == null) return;
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "events.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length >= options.MaxBytes)
                {
                    for (int i = options.RetainedFiles; i >= 1; i--)
                    {
                        var previous = i == 1 ? path : path + "." + (i - 1);
                        if (File.Exists(previous)) File.Move(previous, path + "." + i, true);
                    }
                }
                File.AppendAllText(path, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, eventName, operationId, code }) + Environment.NewLine);
            }
            catch (IOException) { /* A full disk must not destroy a user's file. */ }
            catch (UnauthorizedAccessException) { }
        }
    }
}
