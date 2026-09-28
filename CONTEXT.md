# TaskBard

TaskBard is a Windows 11 system-tray utility that switches taskbar settings based on the primary display's effective resolution.

## Language

**TaskBard**:
The Windows 11 system-tray utility that switches taskbar settings for the **Primary Display** according to the active **Profile**.
_Avoid_: Taskbar Alignment Tool, Display Buddy.

**Monitor**:
A physical display device connected to the system.
_Avoid_: Screen (used by .NET Forms — keep there only when referring to the `System.Windows.Forms.Screen` type).

**Primary Display**:
The monitor Windows reports as the user's main one. The only monitor whose resolution drives a profile switch.
_Avoid_: main monitor, default screen.

**MonitorInfo**:
The canonical record describing a monitor's current state — `EffectiveWidth/Height`, `NativeWidth/Height`, `ScalingPercent`, plus `FriendlyName`, `IsPrimary`, color depth, HDR, refresh rate, VRR. Produced by both the watcher (cheap path, primary only, rich fields defaulted) and the snapshotter (full CCD enrichment, all monitors).
_Avoid_: DisplayInfo, MonitorDisplayInfo.

**Effective Width / Height**:
The DPI-scaled resolution — what apps see and what profile matching uses. Computed as `native * 96 / dpi`.
_Avoid_: logical resolution, virtual size.

**Native Width / Height**:
The physical pixel resolution of the monitor.
_Avoid_: physical resolution (overloaded with `ResolutionMode.Physical`).

**Resolution Mode**:
A user config choice: `Effective` (DPI-scaled, default) or `Physical` (raw pixels). Determines which width drives profile matching.

**Profile**:
A named bundle of taskbar settings (alignment, combine-buttons mode, taskbar size) that activates when the primary display's width crosses a threshold.

**Profile Switch**:
A registry write that changes one or more taskbar settings. Triggered when the primary display's effective width selects a different profile than the active one. Counted by `_profileSwitchCount` for diagnostics.

**Tray Icon**:
TaskBard's system-tray presence: a static medieval pixel-art brand mark whose hover text reports the active **Profile** and **Primary Display** resolution.

**Run at Startup**:
The user-facing setting that asks Windows to launch the app on user login. Two adapters live behind one seam: MSIX builds use the `StartupTask` API, portable builds write to `HKCU\...\Run`. Selected once at startup based on packaging mode.

## Relationships

- A **Profile Switch** is caused by a change in the **Primary Display**'s **Effective Width** (or **Native Width** in `Physical` **Resolution Mode**) crossing a profile's `MinWidth` threshold.
- One **MonitorInfo** describes one **Monitor** at one point in time. The watcher publishes the **Primary Display**'s `MonitorInfo`; the snapshotter returns a list with the **Primary Display** first.
- The DPI math that produces **Effective Width / Height** lives in exactly one place: `MonitorInfo.Compute`.

## Example dialogue

> **Dev:** "When the user docks their laptop, what kicks off the taskbar change?"
> **Maintainer:** "The dock event triggers `WM_DISPLAYCHANGE`, the watcher rebuilds the **Primary Display**'s `MonitorInfo`, sees the **Effective Width** changed, fires `PrimaryDisplayChanged`, and the tray context resolves a new **Profile** and applies it — that's a **Profile Switch**."
> **Dev:** "And the menu shows info for all monitors, not just the primary?"
> **Maintainer:** "Right — the menu calls the snapshotter (`MonitorInfoProvider.GetAllMonitors`), which enumerates via CCD and returns enriched `MonitorInfo` for each connected **Monitor**. The watcher's primary-only record is for change detection and tooltip; it doesn't drive the menu."

## Flagged ambiguities

- "Display" vs "Monitor" vs "Screen" were used interchangeably. Resolved: **Monitor** is the canonical noun for a physical device; **Primary Display** is a specific role; "Screen" is reserved for the .NET `System.Windows.Forms.Screen` type.
- "DisplayInfo" and "MonitorDisplayInfo" both existed for the same concept. Resolved: a single **MonitorInfo** record covers both the watcher and snapshotter paths; rich fields default when sourced from the cheap watcher path.
- "Make your icons dance" is a branding metaphor for **Profile Switches** rearranging taskbar buttons; it does not promise animated icons. Use "taskbar buttons" in functional descriptions.
