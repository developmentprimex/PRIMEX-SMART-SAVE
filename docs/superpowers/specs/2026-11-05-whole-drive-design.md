# Whole local drive monitoring

The user requested selecting the entire C drive after learning that drive roots
were intentionally rejected. Allow explicit local drive selection, with system,
application and temporary directories excluded. This remains opt-in after-save
guidance; documents never move without a destination choice.

Use the existing folder picker and recursive monitoring switch. Keep UNC and
mapped network monitoring rejected. A drive checkbox must have a visible label.
Use one protected-path policy for live events, startup enumeration and queued
automatic requests. Anchor well-known system folder names to the drive root;
also protect actual Windows/program/common-data locations if relocated. Preserve
ordinary user folders with similar names. Existing cache, project, reparse,
temporary, company-root and user exclusions continue to apply.

Approach: extend the existing validation and watcher policy. Merely removing
validation would scan system/application trees; separate whole-disk machinery is
unnecessary. Startup must prune excluded trees, and ordinary recursive folders
must keep existing baseline/resave behavior. Do not scan the actual whole C drive
in tests; use pure path assertions and disposable local fixtures.

Ship version 0.2.17 with tests, documentation and a staff MSI. Installation and
whole-drive performance on an employee PC remain manual acceptance.
