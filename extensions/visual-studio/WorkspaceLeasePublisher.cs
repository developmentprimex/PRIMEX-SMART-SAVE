using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace PrimEx.SmartSave.VisualStudio
{
    public sealed class WorkspaceLeasePublisher : IDisposable
    {
        [DataContract]
        private sealed class Lease
        {
            [DataMember(Name = "version")] public int Version = 1;
            [DataMember(Name = "editor")] public string Editor = "VisualStudio";
            [DataMember(Name = "processId")] public int ProcessId;
            [DataMember(Name = "updatedUtc")] public string UpdatedUtc = "";
            [DataMember(Name = "folders")] public string[] Folders = new string[0];
        }
        private readonly object gate = new object();
        private readonly string directory, file;
        private readonly Timer timer;
        private string[] folders = new string[0];
        private bool disposed;
        private long revision;
        public WorkspaceLeasePublisher(string? dataDirectory = null)
        {
            string data = dataDirectory ?? Environment.GetEnvironmentVariable("PRIMEX_SMARTSAVE_DATA_DIR") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PrimEx", "SmartSave");
            if (!Path.IsPathRooted(data)) throw new ArgumentException("Smart Save data directory must be absolute.");
            directory = Path.Combine(data, "editor-workspaces");
            file = Path.Combine(directory, "visualstudio-" + Guid.NewGuid().ToString("N") + ".json");
            timer = new Timer(_ => Refresh(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
        public void SetFolders(string[] values, long? generation = null)
        {
            lock (gate)
            {
                if (disposed) return;
                if (generation.HasValue && generation.Value < revision) return;
                revision = generation ?? revision + 1;
                folders = values.Where(value => !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value) && !value.StartsWith(@"\\", StringComparison.Ordinal))
                    .Select(Path.GetFullPath).Where(value => !value.TrimEnd('\\').Equals(Path.GetPathRoot(value)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    .Select(value => value.TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase).Take(128).ToArray();
                WriteLease();
            }
        }
        private void Refresh() { lock (gate) { if (!disposed) WriteLease(); } }
        private void WriteLease()
        {
            string? temporary = null;
            try
            {
                if (folders.Length == 0) { File.Delete(file); return; }
                Directory.CreateDirectory(directory);
                temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var process = Process.GetCurrentProcess())
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    new DataContractJsonSerializer(typeof(Lease)).WriteObject(stream, new Lease
                    {
                        ProcessId = process.Id, UpdatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), Folders = folders
                    });
                if (File.Exists(file)) File.Replace(temporary, file, null); else File.Move(temporary, file);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            finally
            {
                if (temporary != null)
                    try { File.Delete(temporary); } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
        }
        public void Dispose()
        {
            timer.Dispose();
            lock (gate)
            {
                disposed = true;
                try { File.Delete(file); } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
        }
    }
}
