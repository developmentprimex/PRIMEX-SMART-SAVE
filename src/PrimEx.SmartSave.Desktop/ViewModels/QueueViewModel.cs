using System.Collections.ObjectModel;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.Desktop.ViewModels;
public sealed class QueueViewModel(OperationStore store, ITransferService transfers, IDestinationSelector selector, IEventLog log, bool loadHistory = true) : ObservableObject
{
    private ObservableCollection<PendingOperation> operations = new(loadHistory ? store.Load() : []);
    public ObservableCollection<PendingOperation> Operations { get => operations; private set => Set(ref operations, value); }
    private bool isReady = loadHistory;
    public bool IsReady { get => isReady; private set => Set(ref isReady, value); }
    public async Task InitializeAsync()
    {
        if (IsReady) return;
        var history = await Task.Run(store.Load);
        Operations = new(history);
        IsReady = true;
        Status = "Ready. Choose a file to move, or review pending downloads.";
    }
    private readonly SemaphoreSlim queue = new(1);
    public bool IsProcessing => queue.CurrentCount == 0;
    private CancellationTokenSource? active;
    private string status = "Ready. Choose a file to move, or review pending downloads.";
    private string guidanceStatus = "Automatic guidance is off. Set it up to watch downloads and saved documents.";
    public string GuidanceStatus { get => guidanceStatus; set => Set(ref guidanceStatus, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public void CancelActive() => active?.Cancel();
    public bool Enqueue(SaveRequest request)
    {
        var prior = Operations.FirstOrDefault(x => x.Request.Id == request.Id);
        if (prior != null) { if (prior.Request != request) throw new InvalidDataException("Operation ID reused."); return false; }
        var pending = new PendingOperation(request); store.Save(pending); Operations.Add(pending); return true;
    }
    private void Update(PendingOperation operation)
    {
        store.Save(operation); var old = Operations.First(x => x.Request.Id == operation.Request.Id);
        Operations[Operations.IndexOf(old)] = operation;
    }
    public async Task ProcessAsync(Guid id)
    {
        await queue.WaitAsync();
        try
        {
            var item = Operations.First(x => x.Request.Id == id);
            if (item.State is OperationState.Completed or OperationState.Cancelled or OperationState.NeedsReview) return;
            var selection = await selector.SelectAsync(item.Request);
            if (selection == null) { Update(item with { State = OperationState.Cancelled }); Status = item.Request.Integration == "Monitoring" ? "Kept in its current location. No file was moved." : "Cancelled. The source file is still in its current location."; return; }
            string destination = Path.Combine(selection.Folder, selection.FileName);
            item = item with { State = OperationState.Transferring, Destination = destination, ErrorCode = null }; Update(item);
            log.Write("DestinationSelected", id); Status = "Transferring and verifying. You can cancel this transfer.";
            active = new();
            try
            {
                var result = await Task.Run(() => transfers.TransferAsync(new(id, item.Request.SourcePath, destination, TransferMode.Move, selection.Conflict), active.Token));
                Update(item with { State = OperationState.Completed, Destination = result.DestinationPath });
                Status = result.SourceDeleted ? "Transfer completed and verified." : "Copy completed and verified. The source was retained; review it before deleting manually.";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException or InvalidOperationException)
            {
                // Publication may have succeeded before an error. Never automatically repeat a transfer.
                Update(item with { State = OperationState.NeedsReview, ErrorCode = e.GetType().Name });
                Status = "Transfer stopped. Review the source and destination before retrying. Your source is retained unless the verified move completed.";
                log.Write("TransferNeedsReview", id, e.GetType().Name);
            }
            finally { active.Dispose(); active = null; }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { Status = "Operation could not be queued or opened. Check local storage and configuration."; log.Write("QueueError", id, e.GetType().Name); }
        finally { queue.Release(); }
    }
    public void Retry(Guid id)
    {
        var item = Operations.First(x => x.Request.Id == id);
        if (item.State is OperationState.NeedsReview or OperationState.Failed or OperationState.Cancelled) Update(item with { State = OperationState.Pending, ErrorCode = null });
    }
}
