# Whole local drive monitoring implementation plan

> Execute inline using superpowers:executing-plans, with regression tests first.

**Goal:** Allow explicit C:\ selection while filtering system/application trees.

**Architecture:** Extend existing options validation, shared eligibility policy
and setup view model; reuse the current watcher and recursive-scope switch.

**Tech stack:** .NET 10, WPF, existing executable test harnesses, WiX.

**Spec:** ../specs/2026-11-05-whole-drive-design.md

## Constraints and review focus

- Never monitor UNC/mapped network drives or move documents automatically.
- Preserve nested user folders named Windows/Program Files and custom exclusions.
- Prune system paths during baseline enumeration as well as live event filtering.
- Accept a drive in setup without starting a real whole-drive watcher in tests.
- Preserve overlapping-root behavior and recursive/nonrecursive choices.

## Task 1: Drive selection and protected paths

- [x] Add failing core tests for local drive validation, network rejection,
  protected root paths, ordinary nested lookalikes, enumeration pruning and
  unchanged explicit exclusions.
- [x] Update WPF setup test to accept the physical local root, retain missing
  folder rejection, and assert a nonempty drive label and saved scope settings.
- [x] Run tests and observe expected validation/policy failures.
- [x] Modify AppOptions.Validate, EligibleFilePolicy and setup label/copy.
- [x] Build and run core/WPF tests; inspect every result.

## Task 2: Package and review

- [x] Bump desktop/editor/installer version to 0.2.17 and update active guides.
- [x] Build a self-contained MSI; run runtime, package and editor checks and
  compare the extracted payload hashes.
- [x] Request fresh read-only code review; fix important findings and verify.
- [x] Publish the local installer folder/checksum and record validation evidence.

Progress: plan recorded before implementation. The user approved the described
whole-drive change with system/application exclusions. Work proceeds inline in
the existing workspace; no installed application or user settings are modified.

Completed: all 176 checks passed; rendered setup inspected. Review identified a
disappearing-child baseline failure; reproduced it first, then added descendant
enumeration recovery and verified root errors/cancellation still propagate.
The reviewed final package is in artifacts/staff-installer-0.2.17. The initial
package candidate is not distributed; final package contains the race fix.
Whole-drive throughput/overflow acceptance and installation remain manual.
