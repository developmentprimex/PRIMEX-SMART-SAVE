# Implementation plan and environment

2026-11-03: empty repository, Windows 10.0.26300, .NET SDK 10.0.300,
WindowsDesktop runtime 10.0.8, Node 24.12.0. Office Click-to-Run reports
ProPlus2024Retail 16.0.17928.20148 x64. Word and Excel binaries exist.
No .NET Framework developer reference assemblies were found.

Sequence: core and safe filesystem services with isolated tests; WPF agent and
serialized dialogs; browser staging and authenticated per-user IPC; Office
compatibility assessment; opt-in monitoring; per-user packaging; final verification.

Architecture: dependency-injected WPF shell, Core contracts/config/journal/logging,
Windows filesystem service, native messaging executable, MV3 extension, optional
watchers. No database, privileged service, cloud service, hooks or injection.

The candidate must not be represented as production-approved until the manual
browser, SMB, installer, accessibility and Office compatibility gates are complete.
