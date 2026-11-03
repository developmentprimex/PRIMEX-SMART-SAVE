using System.Text.Json;
using PrimEx.SmartSave.Core.Models;
namespace PrimEx.SmartSave.Core.Services;

// One owner (the desktop agent) writes this journal. Recovered transfers require review.
public sealed class OperationStore
{
    private readonly string folder;
    public OperationStore(string folder) { this.folder = folder; Directory.CreateDirectory(folder); }
    public void Save(PendingOperation operation)
    {
        var path = Path.Combine(folder, operation.Request.Id.ToString("N") + ".json");
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, operation); stream.Flush(true); }
        File.Move(temp, path, true);
    }
    public IReadOnlyList<PendingOperation> Load()
    {
        List<PendingOperation> result = [];
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                var item = JsonSerializer.Deserialize<PendingOperation>(File.ReadAllText(path));
                if (item != null) result.Add(item.State == OperationState.Transferring ? item with { State = OperationState.NeedsReview, ErrorCode = "InterruptedTransferReviewDestination" } : item);
            }
            catch (JsonException) { File.Move(path, path + ".invalid", true); }
        }
        return result.OrderBy(x => x.Request.CreatedUtc).ToArray();
    }
}
