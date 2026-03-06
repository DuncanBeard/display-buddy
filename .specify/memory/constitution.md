<!--
Sync Impact Report
- Version change: N/A → 1.0.0 (initial ratification)
- Added principles:
  - I. Lightweight
  - II. Self-Contained
  - III. Pragmatic Code
  - IV. Startup-Ready
  - V. Store-Distributable
- Added sections:
  - Technical Constraints
  - Distribution & Packaging
- Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no changes needed
  - .specify/templates/spec-template.md ✅ no changes needed
  - .specify/templates/tasks-template.md ✅ no changes needed
- Follow-up TODOs: none
-->

# Taskbar Alignment Tool Constitution

## Core Principles

### I. Lightweight

The tool MUST remain minimal in binary size and runtime resource
consumption. Background polling MUST use negligible CPU when idle.
Memory footprint MUST stay under 30 MB during normal operation.
New features MUST NOT introduce heavy frameworks or large dependency
trees. If a capability can be achieved with a smaller approach, the
smaller approach wins.

**Rationale**: This is a background utility that users forget is
running. It MUST behave like one.

### II. Self-Contained

The published artifact MUST be fully portable — a single executable
or MSIX package that requires no pre-installed runtimes, no external
services, and no network access. All configuration MUST be local
(`%LOCALAPPDATA%`). The tool MUST function identically whether run
from a USB drive, a fresh Windows install, or the Microsoft Store.

**Rationale**: Users expect system tray utilities to "just work"
without setup ceremonies.

### III. Pragmatic Code

Working, readable code is valued over architectural purity. Patterns
and abstractions MUST justify themselves by solving a concrete
problem — not by satisfying a design principle. Refactoring is
welcome when it reduces complexity, but MUST NOT be pursued for
its own sake. "Good enough and shipping" beats "perfect and
stalled."

**Rationale**: This is a small single-purpose tool. Over-engineering
increases maintenance burden without user benefit.

### IV. Startup-Ready

The tool MUST support automatic launch at user logon via MSIX
StartupTask (preferred) or `HKCU` registry entry (fallback). Startup
registration and deregistration MUST be toggleable from the system
tray context menu without requiring administrator privileges. First
launch MUST NOT require user interaction — sensible defaults apply
immediately.

**Rationale**: The tool's value comes from running continuously and
invisibly. Manual launch defeats the purpose.

### V. Store-Distributable

All code and packaging decisions MUST keep Microsoft Store
distribution viable. The app MUST pass MSIX/WACK validation. Only
`HKCU` registry writes are permitted (no `HKLM`, no admin elevation).
P/Invoke calls MUST be limited to documented, Store-compatible Win32
APIs. The MSIX packaging project MUST be kept in sync with the main
application project.

**Rationale**: Store distribution provides auto-updates, trust
signals, and discoverability that a raw `.exe` cannot match.

## Technical Constraints

- **Platform**: Windows 11 only (relies on Windows 11-specific
  registry keys `TaskbarAl`, `TaskbarGlomLevel`, `TaskbarSi`)
- **Runtime**: .NET 8+ (self-contained publish eliminates runtime
  dependency for end users)
- **UI surface**: System tray icon + context menu only — no main
  window unless explicitly justified
- **Registry scope**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`
  exclusively — never `HKLM`
- **Configuration**: Single JSON file in `%LOCALAPPDATA%\TaskbarAlignmentTool\`
- **Privileges**: Standard user only — MUST NOT require or request
  elevation

## Distribution & Packaging

- **Primary channel**: MSIX package via `TaskbarAlignmentTool.Package`
  project, targeting Microsoft Store submission
- **Secondary channel**: Self-contained single-file publish
  (`dotnet publish -r win-x64 --self-contained`) for sideloading
- **Startup**: MSIX `StartupTask` declaration in `Package.appxmanifest`;
  registry fallback for non-packaged builds
- **Assets**: Store icon assets MUST be maintained in
  `TaskbarAlignmentTool.Package\Images\`
- **Versioning**: MAJOR.MINOR.PATCH following semver; package version
  in `Package.appxmanifest` MUST match assembly version

## Governance

This constitution is the authoritative source of project direction.
All changes — features, refactors, dependency additions — MUST be
evaluated against these principles before implementation.

- **Amendments**: Any principle change MUST be documented with a
  rationale, reflected in a version bump, and propagated to dependent
  templates in `.specify/templates/`.
- **Versioning**: Constitution follows semantic versioning. Principle
  removals or redefinitions are MAJOR. New principles or sections are
  MINOR. Wording and clarification changes are PATCH.
- **Compliance**: Feature specs and implementation plans MUST include
  a constitution check confirming alignment with all five principles.
  Violations MUST be justified in a complexity tracking table.

**Version**: 1.0.0 | **Ratified**: 2026-03-06 | **Last Amended**: 2026-03-06
