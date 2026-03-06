# Research: Display Info Menu — Windows API Investigation

**Date**: 2026-03-06
**Feature**: 002-display-info-menu

## Summary

This document identifies the best Windows API for each per-monitor display
property required by the feature spec: color depth, HDR status, refresh rate,
VRR status, monitor friendly names, and primary monitor identification. All
recommendations target a .NET 8 WinForms desktop app
(`net8.0-windows10.0.19041.0`) with no admin elevation.

The **core strategy** is to use the `DISPLAYCONFIG_*` / CCD (Connecting and
Configuring Displays) API family — specifically `QueryDisplayConfig` +
`DisplayConfigGetDeviceInfo`. These APIs:

- Are per-monitor and per-path (not just primary)
- Live in `User32.dll` with API set `ext-ms-win-ntuser-sysparams-ext-l1-1-1`
- Are marked **Target Platform: Universal** (Store-compatible, passes WACK)
- Are available from **Windows 7+** (our minimum is 19041)
- Can be called from standard user context (no elevation)

---

## Shared Foundation: QueryDisplayConfig Enumeration

All six properties use `QueryDisplayConfig` as the enumeration backbone. This
call returns `DISPLAYCONFIG_PATH_INFO[]` (one per active source→target path)
and `DISPLAYCONFIG_MODE_INFO[]`. Each path carries `adapterId` + target `id`
that serve as keys for `DisplayConfigGetDeviceInfo` queries.

### P/Invoke Signatures (shared)

```csharp
[DllImport("user32.dll")]
static extern int GetDisplayConfigBufferSizes(
    uint flags,
    out uint numPathArrayElements,
    out uint numModeInfoArrayElements);

[DllImport("user32.dll")]
static extern int QueryDisplayConfig(
    uint flags,
    ref uint numPathArrayElements,
    [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
    ref uint numModeInfoArrayElements,
    [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
    IntPtr currentTopologyId);

[DllImport("user32.dll")]
static extern int DisplayConfigGetDeviceInfo(
    ref DISPLAYCONFIG_DEVICE_INFO_HEADER requestPacket);

// — or with a generic by-ref wrapper for each struct type:
[DllImport("user32.dll")]
static extern int DisplayConfigGetDeviceInfo(
    ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

[DllImport("user32.dll")]
static extern int DisplayConfigGetDeviceInfo(
    ref DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO requestPacket);
```

### Flags

```csharp
const uint QDC_ONLY_ACTIVE_PATHS        = 0x00000002;
const uint QDC_VIRTUAL_MODE_AWARE        = 0x00000010; // Win10+
const uint QDC_VIRTUAL_REFRESH_RATE_AWARE = 0x00000040; // Win11+
const uint ERROR_SUCCESS = 0;
```

### Enumeration Pattern (C# sketch)

```csharp
static (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes) GetActiveDisplayConfig()
{
    uint flags = QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE;
    int result;
    DISPLAYCONFIG_PATH_INFO[] paths;
    DISPLAYCONFIG_MODE_INFO[] modes;

    do
    {
        result = GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
        if (result != ERROR_SUCCESS) throw new Win32Exception(result);

        paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        result = QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        Array.Resize(ref paths, (int)pathCount);
        Array.Resize(ref modes, (int)modeCount);
    }
    while (result == 122 /* ERROR_INSUFFICIENT_BUFFER */);

    if (result != ERROR_SUCCESS) throw new Win32Exception(result);
    return (paths, modes);
}
```

---

## 1. Color Depth (Bits-per-Channel)

### Recommended API

**`DisplayConfigGetDeviceInfo`** with type
**`DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO`** (enum value `9`).

This returns `DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO` which contains a
`bitsPerColorChannel` field — exactly the per-channel value the spec requires
(8, 10, 12, etc.).

### Why Not Alternatives

| Alternative | Issue |
|---|---|
| `DEVMODE.dmBitsPerPel` via `EnumDisplaySettings` | Returns **bits-per-pixel** (typically 32 for 8-bit SDR), not bits-per-channel. Also marked "desktop apps only" — may not pass WACK. Does not distinguish 8-bit vs 10-bit when both report 32 bpp. |
| WinRT `AdvancedColorInfo` | Only available to UWP apps with a `CoreWindow`; desktop win32 apps (including WinForms) cannot use it. |

### Struct Layout

```csharp
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint value; // bit-field union
    public DISPLAYCONFIG_COLOR_ENCODING colorEncoding;
    public uint bitsPerColorChannel;

    public bool AdvancedColorSupported => (value & 0x1) != 0;
    public bool AdvancedColorEnabled   => (value & 0x2) != 0;
    public bool WideColorEnforced      => (value & 0x4) != 0;
    public bool AdvancedColorForceDisabled => (value & 0x8) != 0;
}

enum DISPLAYCONFIG_COLOR_ENCODING : uint
{
    RGB    = 0,
    YCBCR444 = 1,
    YCBCR422 = 2,
    YCBCR420 = 3,
    Intensity = 4,
}
```

### Code Sketch

```csharp
static (uint bitsPerChannel, bool advancedColorEnabled) GetColorInfo(
    LUID adapterId, uint targetId)
{
    var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
    info.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO; // 9
    info.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
    info.header.adapterId = adapterId;
    info.header.id = targetId;

    int result = DisplayConfigGetDeviceInfo(ref info);
    if (result != ERROR_SUCCESS)
        return (0, false); // N/A

    return (info.bitsPerColorChannel, info.AdvancedColorEnabled);
}

// Display: $"{bitsPerChannel}-bit" (e.g., "10-bit")
```

### Compatibility

- **Store-compatible**: Yes — `User32.dll`, API set `ext-ms-win-ntuser-sysparams-ext-l1-1-1`
- **Minimum Windows version**: Windows 10 1703 (build 15063) for HDR advanced color info
- **Fallback**: Returns `ERROR_INVALID_PARAMETER` or `ERROR_NOT_SUPPORTED` on older builds → show "N/A"

---

## 2. HDR Status (On/Off)

### Recommended API

**Same struct as Color Depth**: `DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO` via
`DisplayConfigGetDeviceInfo` with type `9`.

The `AdvancedColorEnabled` bit (`value & 0x2`) indicates whether HDR (or
Advanced Color) is currently enabled on that target.

### Interpretation

| Bit | Meaning |
|---|---|
| `advancedColorSupported` | Hardware/driver supports advanced color (HDR/WCG) |
| `advancedColorEnabled` | User has enabled HDR in Windows Settings for this display |

**Display logic**:
- If `!advancedColorSupported` → "HDR: N/A"
- If `advancedColorSupported && advancedColorEnabled` → "HDR: On"
- If `advancedColorSupported && !advancedColorEnabled` → "HDR: Off"

### Code Sketch

```csharp
// Reuses the GetColorInfo() method from section 1.
// The AdvancedColorEnabled flag directly maps to "HDR: On/Off".
string hdrStatus = !info.AdvancedColorSupported ? "N/A"
    : info.AdvancedColorEnabled ? "On" : "Off";
```

### Compatibility

- Same as Color Depth (section 1).

---

## 3. Refresh Rate (Hz)

### Recommended API

**`DISPLAYCONFIG_PATH_TARGET_INFO.refreshRate`** from the
`DISPLAYCONFIG_PATH_INFO` array returned by `QueryDisplayConfig`.

This is a `DISPLAYCONFIG_RATIONAL` (numerator / denominator) giving the exact
refresh rate. For example, 144 Hz = {144000, 1000} or {144, 1}. 59.94 Hz =
{59940, 1000}.

### Why Not Alternatives

| Alternative | Issue |
|---|---|
| `DEVMODE.dmDisplayFrequency` | Returns integer Hz only (e.g., 59 not 59.94). Marked "desktop apps only". |
| `DISPLAYCONFIG_VIDEO_SIGNAL_INFO.vSyncFreq` (from mode array) | Also works and is more precise. Available from the target mode info. Either source is valid; path-level `refreshRate` is simpler. |

### Struct Layout

```csharp
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}
```

Already available in `DISPLAYCONFIG_PATH_TARGET_INFO.refreshRate`.

### Code Sketch

```csharp
static double GetRefreshRate(DISPLAYCONFIG_PATH_INFO path)
{
    var rate = path.targetInfo.refreshRate;
    if (rate.Denominator == 0) return 0;
    return (double)rate.Numerator / rate.Denominator;
}

// Display: round to integer for clean display
// e.g., "144 Hz", "60 Hz"
// For fractional: Math.Round(hz, 2) → "59.94 Hz" if desired
string label = $"{Math.Round(GetRefreshRate(path))} Hz";
```

### VSync Frequency Alternative (higher precision)

The target mode's `DISPLAYCONFIG_VIDEO_SIGNAL_INFO.vSyncFreq` provides the
same data from a different path. For displays using
`QDC_VIRTUAL_REFRESH_RATE_AWARE` (Windows 11), the `vSyncFreqDivider` field
in `AdditionalSignalInfo` gives the ratio between the monitor's actual VSync
and the desktop refresh rate (relevant for Dynamic Refresh Rate / DRR).

### Compatibility

- **Store-compatible**: Yes
- **Minimum Windows version**: Windows 7 (path info always populated for active paths)
- **Fallback**: If `refreshRate.Denominator == 0`, show "N/A"

---

## 4. VRR / Variable Refresh Rate Status

### Assessment

**There is no single, documented, public Win32 API that returns a per-monitor
"VRR on/off" boolean.** VRR (FreeSync / G-Sync / Adaptive Sync) is managed at
the driver level and surfaced in vendor-specific control panels (NVIDIA
Control Panel, AMD Adrenalin). Windows Settings shows VRR status on Windows 11
22H2+ under **System → Display → Graphics → Change default graphics settings →
Variable refresh rate**, but this is a *global* toggle, not per-monitor.

### Best Available Options

#### Option A: DXGI Tearing Support (system-wide, not per-monitor)

`IDXGIFactory5::CheckFeatureSupport(DXGI_FEATURE_PRESENT_ALLOW_TEARING)`
tells whether the system supports VRR globally, but does not indicate per-
monitor status or whether a specific monitor is currently using VRR.

#### Option B: DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION (Win11+)

`DisplayConfigGetDeviceInfo` with type `DISPLAYCONFIG_DEVICE_INFO_GET_MONITOR_SPECIALIZATION`
(enum value `12`) returns monitor specialization flags. This relates to
**Dynamic Refresh Rate (DRR)** — Windows 11's mechanism to switch between
refresh rates (e.g., 60 Hz for desktop → 120 Hz for ink/scrolling). DRR is
related but not identical to VRR/Adaptive Sync.

```csharp
[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint value;

    public bool IsSpecializationEnabled             => (value & 0x1) != 0;
    public bool IsSpecializationAvailableForMonitor  => (value & 0x2) != 0;
    public bool IsSpecializationAvailableForSystem   => (value & 0x4) != 0;
}
```

- `IsSpecializationAvailableForMonitor` indicates the monitor hardware
  supports DRR-style variable refresh
- `IsSpecializationEnabled` indicates it's currently active

This is the closest per-monitor signal available but it covers DRR
(Dynamic Refresh Rate) rather than true game-mode VRR.

#### Option C: Registry Key (undocumented, fragile)

The Windows Settings UI reads VRR state and stores it in registry paths under
`HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers`. These paths are
undocumented, version-specific, and require HKLM read access (though standard
users can typically read them).

**Not recommended** for a shipping app due to fragility.

#### Option D: QDC_VIRTUAL_REFRESH_RATE_AWARE flag (Win11+)

When `QueryDisplayConfig` is called with `QDC_VIRTUAL_REFRESH_RATE_AWARE`
(0x40, Windows 11+), the returned path info includes virtual refresh rate
awareness. If the path shows a different virtual vs physical refresh rate, VRR
is plausibly active. This is an indirect signal at best.

### Recommendation

Use **Option B** (`DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION`) as the primary
data source, with the following display logic:

- If the API call fails (pre-Win11 or unsupported) → "VRR: N/A"
- If `IsSpecializationAvailableForMonitor && IsSpecializationEnabled` → "VRR: On"
- If `IsSpecializationAvailableForMonitor && !IsSpecializationEnabled` → "VRR: Off"
- If `!IsSpecializationAvailableForMonitor` → "VRR: N/A"

This aligns with what Windows 11 Settings displays under Display → Advanced
display → Dynamic refresh rate. The label "VRR" is what the spec uses; we
should note in code comments that this actually reflects DRR capability which
is the OS-level VRR proxy.

### Code Sketch

```csharp
static string GetVrrStatus(LUID adapterId, uint targetId)
{
    var info = new DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION();
    info.header.type = (DISPLAYCONFIG_DEVICE_INFO_TYPE)12; // GET_MONITOR_SPECIALIZATION
    info.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION>();
    info.header.adapterId = adapterId;
    info.header.id = targetId;

    int result = DisplayConfigGetDeviceInfo(ref info);
    if (result != ERROR_SUCCESS)
        return "N/A";

    if (!info.IsSpecializationAvailableForMonitor)
        return "N/A";

    return info.IsSpecializationEnabled ? "On" : "Off";
}
```

### Compatibility

- **Store-compatible**: Yes (same API surface as other `DisplayConfigGetDeviceInfo` calls)
- **Minimum Windows version**: Windows 11 21H2 (build 22000) for monitor specialization
- **Fallback**: Returns error on Windows 10 / older Win11 → "N/A"

---

## 5. Monitor Enumeration with Friendly Names

### Recommended API

**`DisplayConfigGetDeviceInfo`** with type
**`DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME`** (enum value `2`).

Returns `DISPLAYCONFIG_TARGET_DEVICE_NAME` which contains:
- `monitorFriendlyDeviceName[64]` — EDID-sourced name, e.g., "DELL U2723QE"
- `monitorDevicePath[128]` — device path for SetupAPI correlation
- `flags.friendlyNameFromEdid` — whether the name is valid
- `outputTechnology` — connector type (HDMI, DisplayPort, etc.)
- `edidManufactureId`, `edidProductCodeId` — raw EDID IDs

### Why Not Alternatives

| Alternative | Issue |
|---|---|
| `EnumDisplayDevices` | Returns GDI device names like `\\.\DISPLAY1` and adapter-level names. The monitor-level friendly name requires a second call with `iDevNum` and is less reliable than EDID data. |
| `SetupAPI` (`SetupDiGetDeviceProperty`) | Works but requires enumerating the `Monitor` device class and correlating instance IDs. Much more code for the same EDID data. |
| `WMI/CIM` (`WmiMonitorID`) | Requires WMI queries (`Win32_DesktopMonitor`), is slow, and has reliability issues. The `UserFriendlyName` from `WmiMonitorID` is the same EDID data. |

### Struct Layout

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct DISPLAYCONFIG_TARGET_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
    public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string monitorFriendlyDeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string monitorDevicePath;
}

[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
{
    public uint value;
    public bool FriendlyNameFromEdid => (value & 0x1) != 0;
    public bool FriendlyNameForced   => (value & 0x2) != 0;
    public bool EdidIdsValid         => (value & 0x4) != 0;
}

enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
{
    Other = unchecked((uint)-1),
    Hd15                = 0,
    Svideo              = 1,
    CompositeVideo      = 2,
    ComponentVideo      = 3,
    Dvi                 = 4,
    Hdmi                = 5,
    Lvds                = 6,
    DJpn                = 8,
    Sdi                 = 9,
    DisplayportExternal = 10,
    DisplayportEmbedded = 11,
    UdiExternal         = 12,
    UdiEmbedded         = 13,
    Sdtvdongle          = 14,
    Miracast            = 15,
    IndirectWired       = 16,
    IndirectVirtual     = 17,
    Internal            = unchecked(0x80000000),
}
```

### Code Sketch

```csharp
static string GetMonitorFriendlyName(LUID adapterId, uint targetId)
{
    var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
    deviceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME; // 2
    deviceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
    deviceName.header.adapterId = adapterId;
    deviceName.header.id = targetId;

    int result = DisplayConfigGetDeviceInfo(ref deviceName);
    if (result != ERROR_SUCCESS)
        return "Unknown Display";

    if (deviceName.flags.FriendlyNameFromEdid &&
        !string.IsNullOrWhiteSpace(deviceName.monitorFriendlyDeviceName))
    {
        return deviceName.monitorFriendlyDeviceName;
    }

    return "Unknown Display";
}
```

### Compatibility

- **Store-compatible**: Yes
- **Minimum Windows version**: Windows 7
- **Fallback**: Returns empty friendly name if EDID is unavailable → use "Display N"

---

## 6. Primary Monitor Identification

### Recommended API

Use the **GDI source name** from `DisplayConfigGetDeviceInfo` with type
`DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME` (enum value `1`) to get the GDI
device name (e.g., `\\.\DISPLAY1`), then correlate with `Screen.AllScreens` to
find the primary.

Alternatively, and more directly:

### Option A: Screen.PrimaryScreen + MONITORINFOEX (simplest)

The existing codebase already uses `Screen.PrimaryScreen`. For multi-monitor
correlation, use `MONITORINFOEX` with `EnumDisplayMonitors` — the
`MONITORINFOF_PRIMARY` flag (`0x1`) in `MONITORINFO.dwFlags` identifies the
primary.

### Option B: DISPLAYCONFIG source name → GDI correlation

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string viewGdiDeviceName; // e.g., "\\.\DISPLAY1"
}
```

Then compare `viewGdiDeviceName` against `Screen.AllScreens[i].DeviceName` —
whichever `Screen` has `Primary == true` is the primary, and you know which
`DISPLAYCONFIG_PATH_INFO` it maps to.

### Option C: EnumDisplaySettings + DEVMODE (alternative)

Call `EnumDisplaySettingsW(deviceName, ENUM_CURRENT_SETTINGS, &devmode)`. The
device name from `EnumDisplayDevices` with `DISPLAY_DEVICE_PRIMARY_DEVICE`
flag identifies primary.

### Recommendation

**Option B** is the best fit because:
1. We're already enumerating all paths via `QueryDisplayConfig`
2. Getting the source name via `DisplayConfigGetDeviceInfo` gives us the GDI
   device name per path
3. Matching against `Screen.AllScreens` to find `.Primary` is trivial
4. This keeps all logic within the CCD API family

### Code Sketch

```csharp
static bool IsPathPrimary(DISPLAYCONFIG_PATH_INFO path)
{
    var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
    sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME; // 1
    sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
    sourceName.header.adapterId = path.sourceInfo.adapterId;
    sourceName.header.id = path.sourceInfo.id;

    int result = DisplayConfigGetDeviceInfo(ref sourceName);
    if (result != ERROR_SUCCESS) return false;

    return Screen.AllScreens
        .Any(s => s.Primary && s.DeviceName == sourceName.viewGdiDeviceName);
}
```

### Compatibility

- **Store-compatible**: Yes (`DisplayConfigGetDeviceInfo` is Universal; `Screen.AllScreens` is WinForms which is already a dependency)
- **Minimum Windows version**: Windows 7
- **Fallback**: If correlation fails, use Windows display number ordering

---

## Shared Struct Definitions

```csharp
[StructLayout(LayoutKind.Sequential)]
struct LUID
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential)]
struct DISPLAYCONFIG_DEVICE_INFO_HEADER
{
    public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
    public uint size;
    public LUID adapterId;
    public uint id;
}

enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
{
    GET_SOURCE_NAME                = 1,
    GET_TARGET_NAME                = 2,
    GET_TARGET_PREFERRED_MODE      = 3,
    GET_ADAPTER_NAME               = 4,
    SET_TARGET_PERSISTENCE         = 5,
    GET_TARGET_BASE_TYPE           = 6,
    GET_SUPPORT_VIRTUAL_RESOLUTION = 7,
    SET_SUPPORT_VIRTUAL_RESOLUTION = 8,
    GET_ADVANCED_COLOR_INFO        = 9,
    SET_ADVANCED_COLOR_STATE       = 10,
    GET_SDR_WHITE_LEVEL            = 11,
    GET_MONITOR_SPECIALIZATION     = 12,
    SET_MONITOR_SPECIALIZATION     = 13,
}
```

For `DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`, and their sub-
structures, the full layout is large (~200 bytes per path). The implementation
should define these with `[StructLayout(LayoutKind.Sequential)]` matching the
Windows SDK `wingdi.h` header definitions.

---

## Compatibility Summary

| Property | API | Store-OK | Min Windows | Per-Monitor |
|---|---|---|---|---|
| Color depth | `GET_ADVANCED_COLOR_INFO` | Yes | 10 1703 | Yes |
| HDR status | `GET_ADVANCED_COLOR_INFO` | Yes | 10 1703 | Yes |
| Refresh rate | `PATH_TARGET_INFO.refreshRate` | Yes | 7 | Yes |
| VRR status | `GET_MONITOR_SPECIALIZATION` | Yes | 11 21H2 | Yes |
| Friendly name | `GET_TARGET_NAME` | Yes | 7 | Yes |
| Primary ID | `GET_SOURCE_NAME` + `Screen` | Yes | 7 | Yes |

---

## Architecture Recommendation

### Single Enumeration Pass

All six properties can be gathered in a **single enumeration pass**:

1. Call `GetDisplayConfigBufferSizes` + `QueryDisplayConfig` → paths + modes
2. For each active path:
   a. `GET_TARGET_NAME` → friendly name
   b. `GET_SOURCE_NAME` → GDI device name → primary check
   c. `GET_ADVANCED_COLOR_INFO` → color depth + HDR status
   d. `GET_MONITOR_SPECIALIZATION` → VRR status
   e. Read `path.targetInfo.refreshRate` → refresh rate

This results in ~4 P/Invoke calls per monitor + 1 for the initial enumeration.

### Data Model Suggestion

```csharp
record MonitorDisplayInfo(
    string FriendlyName,        // "DELL U2723QE" or "Display 1"
    bool IsPrimary,
    int EffectiveWidth,
    int EffectiveHeight,
    int NativeWidth,
    int NativeHeight,
    int ScalingPercent,
    uint BitsPerChannel,        // 8, 10, 12; 0 = unknown
    string HdrStatus,           // "On", "Off", "N/A"
    double RefreshRateHz,       // 144.0, 59.94, 0 = unknown
    string VrrStatus);          // "On", "Off", "N/A"
```

### Error Handling

Each `DisplayConfigGetDeviceInfo` call can independently fail. The pattern
should be:

- If the call returns `ERROR_SUCCESS` → use the data
- If it returns any error → use fallback value ("N/A" or 0)
- Never throw from display info queries — the menu must always render

This matches **FR-008** (show "N/A" rather than errors).

---

## Open Questions

1. **VRR label accuracy**: The spec uses "VRR" but the underlying API
   (`GET_MONITOR_SPECIALIZATION`) technically reports DRR (Dynamic Refresh
   Rate). Should the label say "VRR" (matching user request), "DRR" (matching
   the API), or something else? **Current recommendation**: Use "VRR" per the
   spec — it's the term users expect.

2. **Color depth on non-Advanced-Color displays**: When
   `GET_ADVANCED_COLOR_INFO` returns `advancedColorSupported = false` and
   `bitsPerColorChannel = 0`, should we fall back to
   `DEVMODE.dmBitsPerPel / 3` (approximate) or show "8-bit" (assumed default)?
   **Current recommendation**: Fall back to 8-bit as the safe default, since
   all standard SDR displays are 8-bit per channel. Only show the queried
   value when `bitsPerColorChannel > 0`.

3. **Refresh rate display precision**: Should we show integer Hz (e.g., "60 Hz")
   or fractional (e.g., "59.94 Hz")? Windows Settings shows integer values.
   **Current recommendation**: Round to integer to match Windows Settings UX.
