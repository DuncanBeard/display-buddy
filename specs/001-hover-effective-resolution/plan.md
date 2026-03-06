# Implementation Plan: Hover Effective Resolution

**Branch**: `001-hover-effective-resolution` | **Date**: 2026-03-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-hover-effective-resolution/spec.md`

## Summary

Show effective resolution (W×H), native resolution (W×H), and scaling percentage
in both the tray icon tooltip and the right-click context menu. The codebase
already contains a nearly complete implementation — the tooltip format matches
the spec, the context menu has both resolution items, and the `DisplayInfo`
record provides all needed data. The only delta is moving the scaling percentage
from the Native menu line to the Effective menu line.

## Technical Context

**Language/Version**: C# / .NET 8.0 (targeting `net8.0-windows10.0.19041.0`)
**Primary Dependencies**: Windows Forms (system tray/context menu), Win32 P/Invoke (`shcore.dll` for `GetDpiForMonitor`, `user32.dll` for `MonitorFromPoint`)
**Storage**: JSON config in `%LOCALAPPDATA%\TaskbarAlignmentTool\config.json`
**Testing**: Manual acceptance testing (no test project)
**Target Platform**: Windows 11
**Project Type**: Desktop app (system tray utility)
**Performance Goals**: Negligible CPU when idle, <30 MB memory
**Constraints**: ≤127-char tooltip (NotifyIcon.Text limit), standard user only (no admin), MSIX-compatible
**Scale/Scope**: Single-user utility, ~6 source files, 1 project

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight | ✅ PASS | No new dependencies, no new frameworks. Change is ~4 lines of string formatting. |
| II. Self-Contained | ✅ PASS | No external services, no network access. Uses existing Win32 DPI APIs already in the project. |
| III. Pragmatic Code | ✅ PASS | Minimal change to existing `UpdateStatus` method. No new abstractions needed. |
| IV. Startup-Ready | ✅ PASS | No impact on startup behavior. |
| V. Store-Distributable | ✅ PASS | No new P/Invoke calls. Uses only existing Store-compatible APIs (`GetDpiForMonitor`, `MonitorFromPoint`). No admin elevation. |

**Gate result**: PASS — all five principles satisfied. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/001-hover-effective-resolution/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
TaskbarAlignmentTool/
├── Program.cs              # Entry point, single-instance mutex
├── TrayApplicationContext.cs  # Tray icon, context menu, tooltip ← PRIMARY EDIT TARGET
├── DisplayMonitor.cs       # Display polling, DisplayInfo record ← already complete
├── NativeMethods.cs        # Win32 P/Invoke declarations ← already complete
├── AppConfig.cs            # Config model & JSON I/O ← no changes needed
├── TaskbarAligner.cs       # Registry writes ← no changes needed
└── TaskbarAlignmentTool.csproj
```

**Structure Decision**: Single project, no structural changes. All edits are
within `TrayApplicationContext.cs` — specifically the `UpdateStatus` method
where the context menu item text is formatted.

## Complexity Tracking

No constitution violations. Table omitted.
