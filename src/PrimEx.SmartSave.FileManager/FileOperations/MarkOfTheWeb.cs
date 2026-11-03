namespace PrimEx.SmartSave.FileManager.FileOperations;
internal static class MarkOfTheWeb
{
    public static async Task PreserveAsync(string source, string temporaryTarget, CancellationToken ct)
    {
        byte[] zone;
        try
        {
            await using var input = new FileStream(source + ":Zone.Identifier", FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 65536) throw new IOException("Unexpected security-zone metadata size.");
            zone = new byte[(int)input.Length]; await input.ReadExactlyAsync(zone, ct);
        }
        catch (FileNotFoundException) { return; }
        await File.WriteAllBytesAsync(temporaryTarget + ":Zone.Identifier", zone, ct);
        if (!(await File.ReadAllBytesAsync(temporaryTarget + ":Zone.Identifier", ct)).AsSpan().SequenceEqual(zone))
            throw new IOException("Could not preserve downloaded-file security metadata.");
    }
}
