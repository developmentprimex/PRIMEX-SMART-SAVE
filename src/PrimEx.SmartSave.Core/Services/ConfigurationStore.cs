using System.Text.Json;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Core.Services;

public static class ConfigurationStore
{
    public static void Save(string path, AppOptions options, string expectedContents)
    {
        var errors = options.Validate();
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        // Refuse to replace edits made after the setup screen was opened.
        if (File.ReadAllText(path) != expectedContents) throw new InvalidDataException("Settings changed while setup was open. Close setup and open it again.");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, options, new JsonSerializerOptions { WriteIndented = true }); stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
