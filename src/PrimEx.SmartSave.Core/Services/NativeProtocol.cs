using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Core.Services;
public static class NativeProtocol
{
    public const int MaxMessageBytes = 65536;
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct = default)
    {
        byte[] header = new byte[4];
        int first = await stream.ReadAsync(header.AsMemory(0, 1), ct); if (first == 0) return default;
        await stream.ReadExactlyAsync(header.AsMemory(1), ct);
        int size = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (size is <= 0 or > MaxMessageBytes) throw new InvalidDataException("Invalid message length.");
        byte[] body = new byte[size]; await stream.ReadExactlyAsync(body, ct);
        return JsonSerializer.Deserialize<T>(body, Json) ?? throw new InvalidDataException("Null message.");
    }
    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken ct = default)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (body.Length > MaxMessageBytes) throw new InvalidDataException("Message too large.");
        byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
        await stream.WriteAsync(header, ct); await stream.WriteAsync(body, ct); await stream.FlushAsync(ct);
    }
    public static void Validate(NativeEnvelope envelope, AppOptions options)
    {
        if (!options.Browser.Enabled || !options.SmartSave.ShowForSupportedDownloads) throw new UnauthorizedAccessException("Browser integration disabled.");
        if (!options.Browser.AllowedExtensionIds.Any(id => envelope.Origin == "chrome-extension://" + id + "/")) throw new UnauthorizedAccessException("Extension origin not allowed.");
        var m = envelope.Message ?? throw new InvalidDataException("Message required.");
        if (m.Version != 1 || m.Command is not ("ping" or "complete" or "status")) throw new InvalidDataException("Unsupported request.");
        if (m.Command == "ping") return;
        if (m.Id == Guid.Empty) throw new InvalidDataException("Operation ID required.");
        if (m.Command == "status") return;
        if (!m.Complete || m.Bytes < 0 || m.SourcePath == null || m.SourcePath.Length > 32000 || m.CreatedUtc == default || m.CreatedUtc > DateTimeOffset.UtcNow.AddMinutes(5)) throw new InvalidDataException("Completed download metadata required.");
        string path = PathRules.NormalizeAbsolute(m.SourcePath);
        string expectedFolder = Path.Combine(options.Browser.StagingRoot, m.Id.ToString("N"));
        if (!string.Equals(Path.GetDirectoryName(path), expectedFolder, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Only this operation's staging file is permitted.");
        PathRules.ValidateFileName(Path.GetFileName(path));
        if (Path.GetExtension(path).Equals(".crdownload", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).StartsWith('.')) throw new InvalidDataException("Incomplete or hidden staging file.");
    }
    public static void ValidateCompletedFile(NativeMessage message)
    {
        PathRules.RejectReparseAncestors(message.SourcePath!);
        using var file = new FileStream(message.SourcePath!, FileMode.Open, FileAccess.Read, FileShare.None);
        if (file.Length != message.Bytes) throw new InvalidDataException("Download length changed.");
    }
}
