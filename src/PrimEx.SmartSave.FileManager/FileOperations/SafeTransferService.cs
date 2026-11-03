using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PrimEx.SmartSave.Core.Interfaces;
using PrimEx.SmartSave.Core.Models;
using PrimEx.SmartSave.Core.Services;
namespace PrimEx.SmartSave.FileManager.FileOperations;

public sealed class SafeTransferService(IEventLog log) : ITransferService
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();
    private readonly ConcurrentDictionary<Guid, (TransferRequest Request, TransferResult Result)> completed = new();
    public async Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        var mutex = locks.GetOrAdd(request.Id, _ => new(1));
        await mutex.WaitAsync(cancellationToken);
        try
        {
            if (completed.TryGetValue(request.Id, out var existing))
                return existing.Request == request ? existing.Result : throw new InvalidOperationException("Operation ID cannot be reused for another transfer.");
            var result = await TransferCoreAsync(request, cancellationToken);
            completed[request.Id] = (request, result);
            return result;
        }
        finally { mutex.Release(); }
    }
    private async Task<TransferResult> TransferCoreAsync(TransferRequest request, CancellationToken ct)
    {
        string source = PathRules.NormalizeAbsolute(request.SourcePath), target = PathRules.NormalizeAbsolute(request.DestinationPath);
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase)) throw new IOException("Source and destination must differ.");
        PathRules.ValidateFileName(Path.GetFileName(target));
        PathRules.RejectReparseAncestors(source); PathRules.RejectReparseAncestors(target);
        string folder = Path.GetDirectoryName(target) ?? throw new ArgumentException("Destination folder required.");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Destination is unavailable. Select or create a folder first.");
        if (request.Conflict == ConflictChoice.Cancel && File.Exists(target)) throw new IOException("Destination already exists. Choose a conflict option.");
        string temp = Path.Combine(folder, ".primex-" + Guid.NewGuid().ToString("N") + ".partial");
        bool published = false;
        log.Write("TransferStarted", request.Id);
        try
        {
            // Denies writers and deletion for the entire copy/verification interval.
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                long size = input.Length;
                await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 131072, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await input.CopyToAsync(output, ct);
                    await output.FlushAsync(ct); output.Flush(true);
                    input.Position = 0; output.Position = 0;
                    byte[] sourceHash = await SHA256.HashDataAsync(input, ct), targetHash = await SHA256.HashDataAsync(output, ct);
                    if (output.Length != size || !CryptographicOperations.FixedTimeEquals(sourceHash, targetHash)) throw new IOException("Transfer integrity check failed; source preserved.");
                }
                await MarkOfTheWeb.PreserveAsync(source, temp, ct);
                ct.ThrowIfCancellationRequested();
                PathRules.RejectReparseAncestors(target);
                if (request.Conflict == ConflictChoice.KeepBoth)
                {
                    string original = target;
                    for (int counter = 1; ; counter++)
                    {
                        try { File.Move(temp, target, false); break; }
                        catch (IOException) when (File.Exists(target) && counter <= 10000)
                        { target = Path.Combine(folder, Path.GetFileNameWithoutExtension(original) + " (" + counter + ")" + Path.GetExtension(original)); }
                    }
                }
                else if (request.Conflict == ConflictChoice.ReplaceConfirmed && File.Exists(target))
                {
                    // Windows ReplaceFile retains a recovery copy of the previous document.
                    File.Replace(temp, target, target + ".primex-backup-" + Guid.NewGuid().ToString("N"));
                }
                else File.Move(temp, target, false);
                published = true;
            }
            // Re-open exclusively and re-hash before deletion: if source changed after publication,
            // retain it. DeleteOnClose binds deletion to this handle (no path-based delete race).
            bool deleted = false;
            string hash;
            await using (var verify = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, FileOptions.Asynchronous))
            {
                hash = Convert.ToHexString(await SHA256.HashDataAsync(verify, ct));
                if (request.Mode == TransferMode.Move)
                {
                    try
                    {
                        using var handle = CreateFile(source, 0x80010000, 0, IntPtr.Zero, 3, 0x08000000, IntPtr.Zero); // GENERIC_READ | DELETE
                        if (handle.IsInvalid) throw new IOException("Source cannot be opened for safe deletion.");
                        using var src = new FileStream(handle, FileAccess.Read);
                        if (Convert.ToHexString(await SHA256.HashDataAsync(src, ct)) == hash)
                            deleted = DeleteOpenedFile(handle);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { log.Write("SourceRetained", request.Id, e.GetType().Name); }
                }
            }
            var result = new TransferResult(target, new FileInfo(target).Length, hash, deleted);
            log.Write("TransferCompleted", request.Id);
            return result;
        }
        catch (Exception e) { log.Write(published ? "TransferPublishedNeedsReview" : "TransferFailed", request.Id, e.GetType().Name); throw; }
        finally { if (File.Exists(temp)) try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref int info, int size);
    private static bool DeleteOpenedFile(SafeFileHandle handle)
    {
        int delete = 1;
        return SetFileInformationByHandle(handle, 4 /* FileDispositionInfo */, ref delete, sizeof(int));
    }
}
