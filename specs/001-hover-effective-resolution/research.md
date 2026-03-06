# Research: Hover Effective Resolution

**Date**: 2026-03-06
**Feature**: 001-hover-effective-resolution

## Summary

No NEEDS CLARIFICATION items existed in the Technical Context.
All technology choices are determined by the existing codebase.
Research confirmed that the feature is already substantially
implemented — the only remaining delta is a one-line format fix
in the context menu.

## Findings

### Existing Implementation Analysis

- **Decision**: Feature is already implemented in the codebase.
- **Rationale**: Code review of `TrayApplicationContext.cs` and
  `DisplayMonitor.cs` shows all foundational work is in place:
  - `DisplayInfo` record with all five fields (EffectiveWidth,
    EffectiveHeight, NativeWidth, NativeHeight, ScalingPercent)
  - Context menu with `_effectiveResItem` and `_nativeResItem`
    disabled items, separator, and profile item
  - Tooltip with `"ProfileName | W×H (W×H @ Scale%)"` format
    and 127-char truncation logic
  - Display change detection via WM_DISPLAYCHANGE, WM_DPICHANGED,
    SystemEvents, and fallback timer
  - Graceful fallback ("Resolution: unavailable") when display
    is undetectable
- **Alternatives considered**: N/A — implementation already exists.

### Context Menu Format Fix

- **Decision**: Move scaling percentage from the Native line to
  the Effective line.
- **Rationale**: Current code formats as:
  `Effective: W×H` / `Native: W×H @ Scale%`.
  Spec (per clarification Option C) requires:
  `Effective: W×H (Scale%)` / `Native: W×H`.
- **Alternatives considered**: Keeping scaling on native line —
  rejected, user explicitly chose Option C.

### Tooltip Format

- **Decision**: Keep existing tooltip format unchanged.
- **Rationale**: Current code produces
  `"ProfileName | W×H (W×H @ Scale%)"` which matches the spec
  (clarification Option A). The word "effective" does not appear
  in the tooltip, matching the user requirement. Budget is ~50
  chars for typical profiles, well within the 127-char limit.
- **Alternatives considered**: None needed.

### Display Resolution Calculation

- **Decision**: Reuse existing `GetDpiForMonitor` P/Invoke.
- **Rationale**: The call already returns `dpiX` and `dpiY`.
  Both are used in `GetDisplayInfo()` for width and height scaling.
  Zero additional P/Invoke overhead.
- **Alternatives considered**: `GetDeviceCaps` via GDI — rejected
  because `GetDpiForMonitor` is already in `NativeMethods.cs` and
  is per-monitor aware.

### Rounding Behavior

- **Decision**: Existing `Math.Round()` is correct.
- **Rationale**: `DisplayMonitor.GetDisplayInfo()` already rounds
  pixel dimensions and scaling percentage to nearest integer,
  satisfying FR-008.
- **Alternatives considered**: Floor/ceiling — rejected because
  `Math.Round` matches Windows Display Settings behavior.
