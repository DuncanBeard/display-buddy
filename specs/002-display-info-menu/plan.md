# Implementation Plan: Display Info Menu

**Branch**: `002-display-info-menu` | **Date**: 2026-03-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-display-info-menu/spec.md`

## Summary

Add per-monitor display information (color depth, HDR status, refresh rate, VRR status) to the system tray context menu. Replace the current standalone Effective/Native/Profile menu items with consolidated per-monitor sections. All data is queried via the `DISPLAYCONFIG_*` / CCD API family (`QueryDisplayConfig` + `DisplayConfigGetDeviceInfo`) in a single enumeration pass — no new dependencies required.

## Technical Context

**Language/Version**: C# / .NET 8.0  
**Primary Dependencies**: WinForms (existing), Win32 P/Invoke to `User32.dll` CCD APIs (new)  
**Storage**: N/A — display info is read-only, queried on demand  
**Testing**: Manual verification against Windows Settings > Display > Advanced Display  
**Target Platform**: Windows 10 19041+ (Windows 11 required for VRR)  
**Project Type**: Desktop app (system tray utility)  
**Performance Goals**: Menu open must feel instantaneous — display enumeration < 10ms  
**Constraints**: <30 MB memory (constitution I), no admin elevation (constitution V), Store-compatible APIs only (constitution V)  
**Scale/Scope**: 1–6 connected monitors (typical desktop/docking scenarios)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Lightweight | **PASS** | No new dependencies. P/Invoke calls to existing Win32 DLLs. Enumeration is on-demand (menu open), not polled. No memory impact when idle. |
| II. Self-Contained | **PASS** | Uses only built-in Win32 APIs via P/Invoke. No external services, no NuGet packages, no network access. |
| III. Pragmatic Code | **PASS** | Single `MonitorInfoProvider` static class with one public method. No abstractions, no patterns — just a flat enumeration helper. |
| IV. Startup-Ready | **PASS** | No changes to startup behavior. Display info is only queried when the user opens the context menu. |
| V. Store-Distributable | **PASS** | All CCD APIs (`QueryDisplayConfig`, `DisplayConfigGetDeviceInfo`) are in API set `ext-ms-win-ntuser-sysparams-ext-l1-1-1`, marked Target Platform: Universal. No HKLM access. No admin elevation. |

**Gate result**: All 5 principles pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/002-display-info-menu/
├── plan.md              # This file
├── research.md          # Phase 0: Windows API research
├── data-model.md        # Phase 1: Entity definitions
├── quickstart.md        # Phase 1: Implementation guide
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
TaskbarAlignmentTool/
├── NativeMethods.cs          # MODIFIED — add CCD P/Invoke signatures & structs
├── MonitorInfoProvider.cs    # NEW — single-pass display enumeration
├── DisplayMonitor.cs         # UNCHANGED — still drives profile switching for primary
├── TrayApplicationContext.cs # MODIFIED — replace static menu items with dynamic per-monitor sections
├── AppConfig.cs              # UNCHANGED
├── Program.cs                # UNCHANGED
└── TaskbarAligner.cs         # UNCHANGED
```

**Structure Decision**: Existing single-project structure. One new file (`MonitorInfoProvider.cs`) contains the enumeration logic. All new P/Invoke signatures go into the existing `NativeMethods.cs`. Menu construction changes are localized to `TrayApplicationContext.cs`.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
