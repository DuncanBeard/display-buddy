# Data Model: Display Info Menu

**Feature**: 002-display-info-menu  
**Date**: 2026-03-06

## Entities

### MonitorDisplayInfo

A read-only snapshot of a single connected monitor's display properties, gathered from the CCD API enumeration. Not persisted — rebuilt each time the context menu is opened.

| Field | Type | Description | Source API |
|-------|------|-------------|------------|
| `FriendlyName` | `string` | EDID-sourced monitor name (e.g., "DELL U2723QE") or fallback "Display N" | `GET_TARGET_NAME` |
| `IsPrimary` | `bool` | Whether this is the system's primary monitor | `GET_SOURCE_NAME` + `Screen.AllScreens` |
| `EffectiveWidth` | `int` | Logical width after DPI scaling | CCD mode info + DPI |
| `EffectiveHeight` | `int` | Logical height after DPI scaling | CCD mode info + DPI |
| `NativeWidth` | `int` | Physical pixel width | `Screen.Bounds` or CCD source mode |
| `NativeHeight` | `int` | Physical pixel height | `Screen.Bounds` or CCD source mode |
| `ScalingPercent` | `int` | DPI scaling as percentage (100, 125, 150, 200, etc.) | `GetDpiForMonitor` |
| `BitsPerChannel` | `uint` | Color depth per channel (8, 10, 12); 0 = unknown | `GET_ADVANCED_COLOR_INFO` |
| `HdrStatus` | `string` | "On", "Off", or "N/A" | `GET_ADVANCED_COLOR_INFO` |
| `RefreshRateHz` | `double` | Current refresh rate (e.g., 144.0, 59.94); 0 = unknown | `PATH_TARGET_INFO.refreshRate` |
| `VrrStatus` | `string` | "On", "Off", or "N/A" | `GET_MONITOR_SPECIALIZATION` |

### Validation Rules

- `FriendlyName`: Never null or empty. Falls back to `"Display {index}"` where index is the 1-based Windows display number.
- `BitsPerChannel`: When the API returns 0 or fails, display as "N/A". When > 0, display as `"{value}-bit"`.
- `HdrStatus`: Derived from `advancedColorSupported` and `advancedColorEnabled` flags. If not supported → "N/A", if supported and enabled → "On", if supported and not enabled → "Off".
- `VrrStatus`: If API call fails (pre-Win11) or monitor doesn't support specialization → "N/A". Otherwise "On"/"Off" based on `IsSpecializationEnabled`.
- `RefreshRateHz`: When denominator is 0 → display "N/A". Otherwise round to nearest integer for display (e.g., 59.94 → "60 Hz").
- `ScalingPercent`: Always ≥ 100. Default 100 if DPI query fails.

### State Transitions

This entity has no mutable state. It is a snapshot record created fresh on each enumeration. There are no state transitions.

## Relationships

```
TrayApplicationContext
  └── opens menu → calls MonitorInfoProvider.GetAllMonitors()
        └── returns List<MonitorDisplayInfo>
              ├── [0] primary monitor (always first)
              ├── [1] secondary monitor (by display number)
              └── [N] additional monitors...
```

### Ordering

1. Primary monitor always appears first in the list.
2. Remaining monitors are ordered by their Windows display number (derived from GDI device name `\\.\DISPLAY{N}`).

### Relationship to Existing Entities

- **DisplayInfo** (existing): The current `DisplayInfo` record in `DisplayMonitor.cs` tracks the primary monitor's resolution for profile switching. It remains unchanged. `MonitorDisplayInfo` is a separate, richer snapshot used only for the context menu.
- **ProfileConfig** (existing): The active profile is resolved from `AppConfig.ResolveProfile(effectiveWidth)` using the primary monitor's effective width. The profile name is displayed only in the primary monitor's menu section.
- **AppConfig** (existing): No changes. No new configuration settings are required for this feature.
