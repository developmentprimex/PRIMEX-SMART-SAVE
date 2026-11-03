namespace PrimEx.SmartSave.FileManager.FileOperations;

public static class AgentInstanceLease
{
    public static FileStream? TryAcquire(string dataDirectory)
    {
        // Only a Windows sharing/lock violation means another agent owns this state.
        // Access denial, full disk and other IO errors must remain distinguishable.
        try { return new FileStream(Path.Combine(dataDirectory, "agent.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33) { return null; }
    }
}
