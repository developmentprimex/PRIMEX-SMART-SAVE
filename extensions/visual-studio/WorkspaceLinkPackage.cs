using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;

namespace PrimEx.SmartSave.VisualStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad("4646B819-1AE0-4E79-97F4-8A8176FDD664", PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuid)]
    public sealed class WorkspaceLinkPackage : AsyncPackage, IVsSolutionEvents, IVsSolutionEvents7
    {
        public const string PackageGuid = "E7DBA7A8-70C5-44C8-83AC-E435452BD52A";
        private IVsSolution? solution;
        private IVsFolderWorkspaceService? folderService;
        private WorkspaceLeasePublisher? publisher;
        private uint cookie;
        private string? openedFolder;
        private bool closing;
        private long revision;
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            publisher = new WorkspaceLeasePublisher();
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            var components = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            folderService = components?.GetService<IVsFolderWorkspaceService>();
            if (solution != null) ErrorHandler.ThrowOnFailure(solution.AdviseSolutionEvents(this, out cookie));
            await PublishCurrentAsync();
        }
        private async Task PublishCurrentAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!closing)
            {
                AddFolder(roots, openedFolder ?? folderService?.CurrentWorkspace?.Location);
                if (solution != null && ErrorHandler.Succeeded(solution.GetSolutionInfo(out string directory, out string solutionFile, out _)) && !string.IsNullOrEmpty(solutionFile))
                {
                    AddFolder(roots, directory);
                    var type = Guid.Empty;
                    if (ErrorHandler.Succeeded(solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_ALLINSOLUTION, ref type, out IEnumHierarchies projects)))
                    {
                        var item = new IVsHierarchy[1];
                        while (projects.Next(1, item, out uint fetched) == VSConstants.S_OK && fetched == 1)
                            if (item[0] is IVsProject project && ErrorHandler.Succeeded(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out string file))) AddFolder(roots, Path.GetDirectoryName(file));
                    }
                }
            }
            string[] snapshot = new string[roots.Count]; roots.CopyTo(snapshot);
            // Only capture IDE state on the UI thread. Lease IO/heartbeats run off it.
            long generation = Interlocked.Increment(ref revision);
            await Task.Run(() => publisher?.SetFolders(snapshot, generation));
        }
        private static void AddFolder(HashSet<string> roots, string? folder)
        {
            if (!string.IsNullOrWhiteSpace(folder) && Path.IsPathRooted(folder)) roots.Add(folder!);
        }
        private void Changed()
        {
            _ = JoinableTaskFactory.RunAsync(async () =>
            {
                try { await PublishCurrentAsync(); }
                catch (Exception ex) when (ex is OperationCanceledException || ex is COMException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { }
            });
        }
        public int OnAfterOpenSolution(object reserved, int isNew) { closing = false; Changed(); return VSConstants.S_OK; }
        public int OnBeforeCloseSolution(object reserved) { closing = true; Changed(); return VSConstants.S_OK; }
        public int OnAfterCloseSolution(object reserved) { openedFolder = null; Changed(); return VSConstants.S_OK; }
        public int OnAfterOpenProject(IVsHierarchy hierarchy, int added) { closing = false; Changed(); return VSConstants.S_OK; }
        public int OnAfterLoadProject(IVsHierarchy stub, IVsHierarchy real) { Changed(); return VSConstants.S_OK; }
        public int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed) => VSConstants.S_OK;
        public int OnBeforeUnloadProject(IVsHierarchy real, IVsHierarchy stub) => VSConstants.S_OK;
        public int OnQueryCloseProject(IVsHierarchy hierarchy, int removing, ref int cancel) => VSConstants.S_OK;
        public int OnQueryUnloadProject(IVsHierarchy hierarchy, ref int cancel) => VSConstants.S_OK;
        public int OnQueryCloseSolution(object reserved, ref int cancel) => VSConstants.S_OK;
        public void OnAfterOpenFolder(string folder) { openedFolder = folder; closing = false; Changed(); }
        public void OnBeforeCloseFolder(string folder) { closing = true; Changed(); }
        public void OnAfterCloseFolder(string folder) { openedFolder = null; Changed(); }
        public void OnQueryCloseFolder(string folder, ref int cancel) { }
        public void OnAfterLoadAllDeferredProjects() { Changed(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                publisher?.Dispose();
                JoinableTaskFactory.Run(async () =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (solution != null && cookie != 0) { solution.UnadviseSolutionEvents(cookie); cookie = 0; }
                });
            }
            base.Dispose(disposing);
        }
    }
}
