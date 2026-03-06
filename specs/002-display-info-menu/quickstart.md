# Quickstart: Display Info Menu

**Feature**: 002-display-info-menu  
**Date**: 2026-03-06

## Overview

This feature adds per-monitor display information (color depth, HDR, refresh rate, VRR) to the system tray context menu, replacing the current standalone resolution/profile items with consolidated per-monitor sections.

## Architecture at a Glance

```
User opens tray menu
  → TrayApplicationContext rebuilds monitor section
    → MonitorInfoProvider.GetAllMonitors()
      → QueryDisplayConfig (1 call)
      → DisplayConfigGetDeviceInfo (4 calls per monitor)
    → For each MonitorDisplayInfo: add menu items
    → Primary monitor section includes active profile
```

## Key Files

| File | Role | Change Type |
|------|------|-------------|
| `NativeMethods.cs` | P/Invoke signatures for CCD APIs and struct definitions | Modified |
| `MonitorInfoProvider.cs` | Single-pass monitor enumeration returning `List<MonitorDisplayInfo>` | New |
| `TrayApplicationContext.cs` | Context menu construction — replace static items with dynamic per-monitor sections | Modified |
| `DisplayMonitor.cs` | Primary monitor polling for profile switching (unchanged) | Unchanged |

## Implementation Order

### Step 1: P/Invoke Declarations (NativeMethods.cs)

Add to the existing `NativeMethods` partial class:

- `GetDisplayConfigBufferSizes` — get buffer sizes for active paths
- `QueryDisplayConfig` — enumerate active display paths and modes
- `DisplayConfigGetDeviceInfo` overloads — query per-target/source properties
- Struct definitions: `LUID`, `DISPLAYCONFIG_DEVICE_INFO_HEADER`, `DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`, `DISPLAYCONFIG_TARGET_DEVICE_NAME`, `DISPLAYCONFIG_SOURCE_DEVICE_NAME`, `DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO`, `DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION`, `DISPLAYCONFIG_RATIONAL`
- Enum definitions: `DISPLAYCONFIG_DEVICE_INFO_TYPE`, `DISPLAYCONFIG_COLOR_ENCODING`

All P/Invoke uses `LibraryImport` (source-generated) consistent with the existing codebase style.

### Step 2: Monitor Enumeration (MonitorInfoProvider.cs)

Create a new static class with one public method:

```
static List<MonitorDisplayInfo> GetAllMonitors()
```

Implementation:
1. Call `QueryDisplayConfig` with `QDC_ONLY_ACTIVE_PATHS`
2. For each active path:
   - `GET_TARGET_NAME` → friendly name
   - `GET_SOURCE_NAME` → GDI device name → correlate with `Screen.AllScreens` for primary check, resolution, and DPI
   - `GET_ADVANCED_COLOR_INFO` → color depth + HDR status
   - `GET_MONITOR_SPECIALIZATION` → VRR status (graceful N/A on failure)
   - `path.targetInfo.refreshRate` → refresh rate
3. Sort: primary first, then by GDI device name (natural display number order)
4. Assign fallback names ("Display 1", "Display 2") where EDID name is unavailable

Each `DisplayConfigGetDeviceInfo` call is independently error-handled — a failure in one property doesn't prevent others from being shown.

### Step 3: Menu Construction (TrayApplicationContext.cs)

Replace the current static `_effectiveResItem`, `_nativeResItem`, `_profileItem` fields with dynamic menu building:

1. Add a marker/placeholder in the ContextMenuStrip to identify where monitor sections go
2. On `ContextMenuStrip.Opening` (or equivalent event), clear and rebuild the monitor section:
   - Call `MonitorInfoProvider.GetAllMonitors()`
   - For each monitor, add a group of disabled `ToolStripMenuItem` items:
     - **Header**: Monitor friendly name (bold or with separator)
     - `Effective: {W}×{H} ({scaling}%)`
     - `Native: {W}×{H}`
     - `Profile: {name}` (primary only)
     - `Color: {bits}-bit` or `Color: N/A`
     - `HDR: On/Off/N/A`
     - `{rate} Hz` or `Refresh: N/A`
     - `VRR: On/Off/N/A`
   - Add a `ToolStripSeparator` between monitor sections
3. Keep existing menu items below (Open Config, Reload Config, Startup, Refresh, Diagnostics, Exit)

The `UpdateStatus` method is simplified: it no longer updates individual items but triggers a full monitor section rebuild.

### Step 4: Update Tooltip and Tray Icon

The existing tooltip logic in `UpdateStatus` should continue showing primary monitor info. The tray icon still renders based on primary monitor effective width.

## Fallback Behavior

| Condition | Behavior |
|-----------|----------|
| `GET_ADVANCED_COLOR_INFO` fails | Color: N/A, HDR: N/A |
| `bitsPerColorChannel == 0` and `advancedColorSupported == false` | Color: 8-bit (safe default for SDR) |
| `GET_MONITOR_SPECIALIZATION` fails (pre-Win11) | VRR: N/A |
| `refreshRate.Denominator == 0` | Refresh: N/A |
| EDID friendly name empty | "Display N" (N = Windows display number) |
| No monitors detected | Single item: "No displays detected" |

## Testing Notes

- Compare each property against **Windows Settings → System → Display → Advanced display** for each connected monitor
- Test with HDR on/off toggle
- Test monitor connect/disconnect between menu opens
- Test on a system with one monitor (single-section layout)
- Test on a system with 2+ monitors (multi-section, primary first)
- VRR will show "N/A" on Windows 10 — this is expected
