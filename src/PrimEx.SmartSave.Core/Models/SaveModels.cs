namespace PrimEx.SmartSave.Core.Models;

public enum DestinationKind { CompanyServer, OutsideServer }
public enum ConflictChoice { Cancel, KeepBoth, ReplaceConfirmed }
public enum TransferMode { Copy, Move }
public enum OperationState { Pending, Transferring, Completed, Failed, Cancelled, NeedsReview }
public sealed record SaveRequest(Guid Id, string SourcePath, string DisplayName, string Integration, DateTimeOffset CreatedUtc);
public sealed record DestinationSelection(string Folder, string FileName, DestinationKind Kind, ConflictChoice Conflict,
    TransferMode Mode = TransferMode.Move);
public sealed record TransferRequest(Guid Id, string SourcePath, string DestinationPath, TransferMode Mode, ConflictChoice Conflict);
public sealed record TransferResult(string DestinationPath, long Bytes, string Sha256, bool SourceDeleted);
public sealed record PendingOperation(SaveRequest Request, OperationState State = OperationState.Pending,
    string? Destination = null, string? ErrorCode = null, TransferMode Mode = TransferMode.Move);
public sealed record DirectoryStatus(bool Available, string? Error);
public sealed record ServerRoot(string Path, string Label, bool Approved);
