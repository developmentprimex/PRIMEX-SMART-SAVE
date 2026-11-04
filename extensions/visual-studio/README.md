# PrimEx Smart Save Workspace Link — Visual Studio

Install the VisualStudio VSIX once, then restart Visual Studio. Package targets:
Visual Studio 2022 17.14+ / 2026, Windows x64, Community/Pro/Enterprise.

The background package uses Microsoft's SDK to capture the solution directory,
project directories (including projects outside the solution folder), and the
Open Folder workspace. New solutions/folders update automatically; no project
files or manual exclusions are required. Multi-root Open Workspace and remote
workspace acceptance require pilot testing.

Smart Save 0.2.11+ must run with Skip coding projects automatically enabled.
Per-window local leases refresh every 30 seconds, are removed on close, and expire
within 90 seconds after a crash. For a custom app data profile, set
`PRIMEX_SMARTSAVE_DATA_DIR` before launching Visual Studio. The normal installed
location requires no configuration.

The package builds against Microsoft's SDK and passes publisher/registry and
VSIX tests. No supported Visual Studio host is installed on the development PC;
actual package loading and solution/Open Folder workflows remain pilot checks.
