# Feature Specification: Display Info Menu

**Feature Branch**: `002-display-info-menu`  
**Created**: 2025-03-06  
**Status**: Draft  
**Input**: User description: "Show more info on the click menu: color settings (8-bit, 10-bit, 12-bit, etc), HDR on/off, current refresh rate, VRR enabled/disabled. Multiple screens each in their own section, stacked on top."

## Clarifications

### Session 2026-03-06

- Q: How should the new per-monitor display-info sections relate to the existing standalone Effective/Native/Profile menu items? → A: Merge existing resolution/profile info into the per-monitor sections so each monitor shows everything in one block; remove the standalone Effective/Native/Profile items.
- Q: In what order should monitor sections appear in the menu? → A: Primary monitor first, then remaining monitors ordered by Windows display number.
- Q: Should the active profile be shown per-monitor or only on the primary monitor? → A: Show active profile on primary monitor section only; non-primary sections show display info without a profile line.
- Q: Should color depth be displayed as bits-per-pixel or bits-per-channel? → A: Bits-per-channel (e.g., "8-bit", "10-bit", "12-bit") to match Windows Advanced Display and monitor specs.
- Q: Should profile switching expand beyond primary-monitor-only, or stay as-is? → A: Profile switching stays primary-monitor-only; this feature only adds read-only display info for all monitors.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Display Properties for a Single Monitor (Priority: P1)

A user with a single monitor right-clicks the tray icon and opens the context menu. They see a single unified section for their display showing resolution, scaling, profile, color depth (e.g., "10-bit"), whether HDR is on or off, the current refresh rate (e.g., "144 Hz"), and whether Variable Refresh Rate (VRR) is enabled or disabled. The previous standalone Effective/Native/Profile menu items are replaced by this consolidated per-monitor block. Since this is the primary (and only) monitor, the active profile is included in the section.

**Why this priority**: This is the core value of the feature — surfacing display properties that users currently need third-party tools or Windows Settings to check. Even with a single monitor, the information is immediately useful.

**Independent Test**: Can be fully tested by right-clicking the tray icon on a single-monitor system and verifying that color depth, HDR status, refresh rate, and VRR status are displayed and match Windows Display Settings.

**Acceptance Scenarios**:

1. **Given** the application is running on a system with one monitor, **When** the user opens the tray context menu, **Then** they see a section for their display showing color depth, HDR status, refresh rate, and VRR status.
2. **Given** the user's display is running at 10-bit color and 144 Hz with HDR on and VRR enabled, **When** they open the menu, **Then** the menu shows "10-bit", "HDR: On", "144 Hz", and "VRR: On".
3. **Given** the user changes their display settings in Windows (e.g., toggles HDR off), **When** they re-open the menu, **Then** the updated values are reflected immediately.

---

### User Story 2 - View Display Properties for Multiple Monitors (Priority: P2)

A user with two or more monitors opens the tray context menu and sees a separate, labeled section for each connected display. Each section is stacked vertically and shows that monitor's name, resolution info, color depth, HDR status, refresh rate, and VRR status.

**Why this priority**: Multi-monitor setups are common for power users who are the primary audience for this tool. Showing per-monitor details extends the single-monitor feature to cover the full user base.

**Independent Test**: Can be tested by connecting two or more monitors (or using a virtual display adapter), opening the tray menu, and verifying each monitor has its own labeled section with correct properties.

**Acceptance Scenarios**:

1. **Given** the application is running on a system with two monitors, **When** the user opens the tray context menu, **Then** they see two stacked sections ordered with the primary monitor first then by Windows display number, each labeled with the monitor's display name (e.g., "DELL U2723QE" or "Display 1").
2. **Given** monitor 1 is 8-bit SDR at 60 Hz with VRR off, and monitor 2 is 10-bit HDR at 165 Hz with VRR on, **When** the user opens the menu, **Then** each section shows the correct values for its respective monitor, and only the primary monitor's section includes an active profile line.
3. **Given** a third monitor is connected while the application is running, **When** the user opens the menu, **Then** the newly connected monitor appears as an additional section.

---

### User Story 3 - Graceful Handling of Unavailable Properties (Priority: P3)

A user has a monitor that does not report one or more of the display properties (e.g., an older monitor that doesn't support HDR or VRR). The menu still displays the monitor section but shows "N/A" or omits the unsupported properties gracefully instead of showing errors or blank entries.

**Why this priority**: Not all monitors support every property. The feature must degrade gracefully to avoid confusion or visual clutter.

**Independent Test**: Can be tested by connecting a monitor that does not support HDR or VRR and verifying the menu handles missing data without errors.

**Acceptance Scenarios**:

1. **Given** a monitor does not support HDR, **When** the user opens the menu, **Then** the HDR field shows "HDR: N/A" or is omitted entirely.
2. **Given** the system cannot determine VRR capabilities for a monitor, **When** the user opens the menu, **Then** the VRR field shows "VRR: N/A" or is omitted entirely.
3. **Given** color depth information is unavailable, **When** the user opens the menu, **Then** the color depth field shows "Color: N/A" or is omitted entirely.

---

### Edge Cases

- What happens when a monitor is disconnected between menu opens? The section for that monitor should no longer appear.
- What happens when all monitors are disconnected (e.g., remote desktop)? The menu should show "No displays detected" or similar.
- What happens when display properties change while the menu is open? The menu reflects the state at the time it was opened; the next open will show updated values.
- What happens on systems where the OS does not support querying HDR or VRR (e.g., older Windows versions)? Those fields should show "N/A" or be omitted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display the current color bit depth as bits-per-channel (e.g., "8-bit", "10-bit", "12-bit") for each connected monitor in the tray context menu, matching the format used in Windows Advanced Display settings.
- **FR-002**: System MUST display the current HDR status (On or Off) for each connected monitor in the tray context menu.
- **FR-003**: System MUST display the current refresh rate in Hz (e.g., "60 Hz", "144 Hz") for each connected monitor in the tray context menu.
- **FR-004**: System MUST display the current VRR (Variable Refresh Rate) status (On or Off) for each connected monitor in the tray context menu.
- **FR-005**: Each monitor section MUST consolidate all display information (resolution, scaling, color depth, HDR, refresh rate, VRR) into a single block, replacing the previous standalone Effective/Native/Profile menu items. The active profile line MUST appear only in the primary monitor's section.
- **FR-006**: Each monitor section MUST include a human-readable label identifying the display (e.g., monitor name or "Display 1", "Display 2").
- **FR-007**: Display information MUST be refreshed each time the context menu is opened, reflecting the current state of the display at that moment.
- **FR-008**: When a property cannot be determined for a monitor, the system MUST show "N/A" for that property rather than displaying an error or blank.
- **FR-009**: When a monitor is disconnected, its section MUST no longer appear in the menu the next time it is opened.
- **FR-010**: When multiple monitors are connected, the system MUST show each monitor in its own labeled section, stacked vertically in the context menu, with the primary monitor first and remaining monitors ordered by Windows display number.

### Key Entities

- **Monitor**: A connected display device. Key attributes: display name/identifier, color bit depth, HDR status, refresh rate, VRR status, resolution, and scaling.
- **Display Property**: An individual piece of information about a monitor (color depth, HDR, refresh rate, VRR). Can be available or unavailable for a given monitor.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view color depth, HDR status, refresh rate, and VRR status for any connected monitor within one click (opening the tray menu).
- **SC-002**: On a multi-monitor system, each monitor's properties are correctly attributed to its own labeled section with zero cross-monitor data errors.
- **SC-003**: Display property values shown in the menu match the values reported by Windows Display Settings for the same monitor 100% of the time.
- **SC-004**: When a display property is unavailable, the corresponding field shows "N/A" rather than an error, blank, or crash — 100% of the time.
- **SC-005**: The menu correctly reflects monitor connect/disconnect events, showing only currently connected monitors each time it is opened.

## Assumptions

- The application runs on Windows 10 version 1903 (19H1) or later, which provides the necessary APIs for querying HDR and advanced display properties.
- Monitor display names are sourced from the operating system's display enumeration (e.g., "DELL U2723QE") and fall back to a generic label ("Display 1", "Display 2") when a friendly name is not available.
- VRR status refers to the OS-level adaptive sync / variable refresh rate setting, not GPU-vendor-specific implementations (e.g., G-SYNC, FreeSync branding).
- Color depth refers to the bits-per-channel setting derived from the current display mode (e.g., 30 bpp → "10-bit"), matching the convention used in Windows Advanced Display settings and monitor specifications.
- Refresh rate refers to the current active refresh rate, not the maximum supported rate.
- Profile switching remains driven by the primary monitor's effective resolution only. This feature does not change which monitor drives profile selection; it only adds read-only display information for all connected monitors.
