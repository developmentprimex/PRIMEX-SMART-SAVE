using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Core.Interfaces;

public interface IPathResolver { string Resolve(string path); }
public interface ITransferService { Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default); }
public interface IServerFolderService
{
    IReadOnlyList<ServerRoot> GetRoots();
    Task<IReadOnlyList<string>> EnumerateAsync(string path, CancellationToken cancellationToken = default);
    Task<DirectoryStatus> CheckAsync(string path, bool probeWrite, CancellationToken cancellationToken = default);
    Task<string> CreateDirectoryAsync(string parent, string name, CancellationToken cancellationToken = default);
    bool IsApproved(string path);
}
public interface IDestinationSelector { Task<DestinationSelection?> SelectAsync(SaveRequest request, CancellationToken cancellationToken = default, TransferMode initialMode = TransferMode.Move); }
public interface IEventLog { void Write(string eventName, Guid? operationId = null, string? code = null); }
