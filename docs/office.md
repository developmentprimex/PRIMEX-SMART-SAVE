# Office compatibility gate — interception NOT implemented

The Office project currently contains a read-only Click-to-Run inventory probe.
It does not install an add-in, subscribe to saves, or change documents.
`Office.Enabled=true` is rejected so an unfinished integration cannot be silently enabled.

This workstation has Office 2024 Retail x64 16.0.17928.20148. The presence of
Word/Excel binaries is not evidence of working save interception. .NET Framework
developer reference assemblies and VSTO project tooling were not found.

Microsoft documents that VSTO remains on .NET Framework, rather than .NET 10:
https://learn.microsoft.com/en-us/visualstudio/vsto/create-vsto-add-ins-for-office-by-using-visual-studio?view=vs-2022

BeforeSave also runs for AutoSave. Its arguments alone do not reliably establish
manual intent. Do not deploy a blanket cancellation handler, turn off users'
AutoSave, or display dialogs on every event:
https://learn.microsoft.com/en-us/office/vba/library-reference/concepts/how-autosave-impacts-addins-and-macros

Next implementation: separately packaged .NET Framework 4.8 VSTO Word and Excel
add-ins, with an explicit Ribbon Smart Save command as the reliable manual entry
point. Investigate native command routing and BeforeSave only on verified
non-cloud workflows. The agent should return a user-selected destination over a
separate authenticated, bounded Office protocol. Office must perform SaveAs2 /
SaveAs on its own STA thread, preserving the current FileFormat, macro-enabled
formats, read-only state and cancellation, with a try/finally recursion guard.
Never use the browser's completed-file-move protocol for an open Office document.

Required verification matrix: Microsoft 365 enterprise, 2021 and 2024, each x86
and x64; new and existing documents; Save and Save As; DOC/DOCX/DOCM and
XLS/XLSX/XLSM/XLSB; local and SMB; read-only; protected view; unsaved changes;
Cancel; unavailable server; AutoRecover; AutoSave; coauthoring; save-on-close;
programmatic saves; agent failure; Office restart. No cell is certified yet.

PowerPoint and Outlook are subsequent modules and are not implemented.
Until verification, use the manual Move a file command after closing the document,
or explicitly enable post-save monitoring in a pilot. Neither is pre-save interception.
