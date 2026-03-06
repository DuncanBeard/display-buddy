# Data Model: Hover Effective Resolution

**Date**: 2026-03-06
**Feature**: 001-hover-effective-resolution

## Entities

### DisplayInfo

Represents the current state of the primary display as surfaced
to the user via tooltip and context menu. This is an ephemeral
data structure recomputed on each poll cycle — not persisted.

| Field | Type | Description |
|-------|------|-------------|
| EffectiveWidth | int | Logical width after DPI scaling (px) |
| EffectiveHeight | int | Logical height after DPI scaling (px) |
| NativeWidth | int | Physical display width (px) |
| NativeHeight | int | Physical display height (px) |
| ScalingPercent | int | Display scaling as whole % (e.g., 125) |

**Derivation rules**:

- `EffectiveWidth = NativeWidth × 96 / dpiX` (rounded to nearest int)
- `EffectiveHeight = NativeHeight × 96 / dpiY` (rounded to nearest int)
- `ScalingPercent = (int)Math.Round(dpiX / 96.0 × 100)`

**Identity**: Singleton — only one DisplayInfo exists at a time
(primary display only).

**Lifecycle**: Created fresh on each poll/event. No state
transitions — each instance is immutable and replaced entirely.

## Relationships

- `DisplayInfo` is consumed by `TrayApplicationContext` to format
  the tooltip string and context menu status items.
- `DisplayInfo` is produced by `DisplayMonitor` which already
  owns the DPI detection and poll/event loop.
- The existing `EffectiveWidth` property on `DisplayMonitor` will
  be supplemented (not replaced) by the full `DisplayInfo`.
