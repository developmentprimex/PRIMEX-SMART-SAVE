namespace PrimEx.SmartSave.Core.Models;
public sealed record NativeMessage(int Version, string Command, Guid Id = default, string? SourcePath = null,
    bool Complete = false, long Bytes = -1, DateTimeOffset CreatedUtc = default);
public sealed record NativeEnvelope(string Origin, NativeMessage Message);
public sealed record NativeReply(bool Ok, string Code, string? State = null);
