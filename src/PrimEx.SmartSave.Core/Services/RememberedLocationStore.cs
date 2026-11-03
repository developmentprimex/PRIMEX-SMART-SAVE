using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PrimEx.SmartSave.Core.Services;

// Remembers an explicit Keep choice, not a transfer authorization. Never moves a
// document. Hashes full paths so the state directory does not list document names.
// Creation time separates ordinary edits from a file recreated at the same path.
public sealed class RememberedLocationStore
{
    private readonly string folder;
    public RememberedLocationStore(string folder) { this.folder = folder; Directory.CreateDirectory(folder); }

    private static (string Key, long Created)? Identify(string path)
    {
        string normalized = PathRules.NormalizeAbsolute(path);
        var file = new FileInfo(normalized);
        if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0) return null;
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant())));
        return (key, file.CreationTimeUtc.Ticks);
    }

    public bool Contains(string path)
    {
        try
        {
            var identity = Identify(path);
            if (identity == null) return false;
            var marker = new FileInfo(Path.Combine(folder, identity.Value.Key + ".choice"));
            return marker.Exists && marker.Length <= 32 &&
                long.TryParse(File.ReadAllText(marker.FullName), NumberStyles.None, CultureInfo.InvariantCulture, out long created) && created == identity.Value.Created;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    public bool Remember(string path)
    {
        string? temporary = null;
        try
        {
            var identity = Identify(path);
            if (identity == null) return false;
            string marker = Path.Combine(folder, identity.Value.Key + ".choice");
            temporary = marker + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] value = Encoding.ASCII.GetBytes(identity.Value.Created.ToString(CultureInfo.InvariantCulture));
                stream.Write(value); stream.Flush(true);
            }
            File.Move(temporary, marker, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
