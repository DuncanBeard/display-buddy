# Feature Specification: Hover Effective Resolution

**Feature Branch**: `001-hover-effective-resolution`
**Created**: 2026-03-06
**Status**: Draft
**Input**: User description: "Show effective resolution (W×H) in
tray tooltip on hover AND at the top of the right-click context
menu. Effective = native resolution reduced by display scaling."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — See Effective Resolution on Hover (Priority: P1)

A user docks their laptop to an external monitor and wants to
quickly confirm what effective resolution the tool is using to
select a profile. They hover over the tray icon and immediately
see the full effective resolution (W×H), the native resolution
(W×H), and the current scaling factor — without opening any menu.

**Why this priority**: The tooltip is the fastest way to check
resolution at a glance. Showing W×H (not just width) gives
complete context that matches how users think about resolution.

**Independent Test**: Hover over the tray icon on a display with
non-100% scaling. Verify the tooltip shows effective W×H, native
W×H, and scaling percentage, all matching Windows Display Settings.

**Acceptance Scenarios**:

1. **Given** the app is running on a 3840×2160 display at 200%
   scaling with profile "Ultrawide", **When** the user hovers
   over the tray icon, **Then** the tooltip displays:
   `Ultrawide | 1920×1080 (3840×2160 @ 200%)`.
2. **Given** the app is running on a 1920×1080 display at 100%
   scaling with profile "Default", **When** the user hovers over
   the tray icon, **Then** the tooltip displays:
   `Default | 1920×1080 (1920×1080 @ 100%)`.
3. **Given** the user undocks from an external monitor and the
   laptop's built-in 2560×1600 display at 150% scaling becomes
   primary with profile "Laptop", **When** the user hovers over
   the tray icon, **Then** the tooltip updates to show:
   `Laptop | 1707×1067 (2560×1600 @ 150%)`.

---

### User Story 2 — Resolution Details in Context Menu (Priority: P1)

A user right-clicks the tray icon and sees the current resolution
breakdown at the top of the context menu, providing a persistent
reference that does not disappear like a tooltip. This information
appears above the existing menu items.

**Why this priority**: The user explicitly requested the info on
both hover and click. The context menu is the primary interaction
surface; resolution details at the top give immediate visibility
every time the menu opens.

**Independent Test**: Right-click the tray icon. Verify the top of
the context menu shows effective W×H, native W×H, and scaling
percentage matching the actual display settings.

**Acceptance Scenarios**:

1. **Given** the app is running on a 3840×2160 display at 200%
   scaling, **When** the user right-clicks the tray icon, **Then**
   the top of the context menu shows two disabled items:
   `Effective: 1920×1080 (200%)` and `Native: 3840×2160`.
2. **Given** the display scaling changes while the app is running,
   **When** the user right-clicks the tray icon, **Then** the
   context menu reflects the updated values on next poll cycle.
3. **Given** the context menu is open, **When** the user reads the
   resolution items, **Then** the existing menu items (Open Config,
   Reload Config, Run at Startup, etc.) appear below the resolution
   details, separated by a divider.

---

### Edge Cases

- What happens when the scaling factor is a non-standard value
  (e.g., 125%, 175%)? The displayed percentage MUST match the
  actual system DPI scaling, rounded to the nearest whole percent.
- What happens when the primary display cannot be detected (e.g.,
  headless or remote desktop with no physical monitor)? The tooltip
  and context menu MUST show a graceful fallback message such as
  "Resolution: unavailable" rather than crashing or showing stale
  data.
- What happens when effective and native resolution are identical
  (100% scaling)? Both the tooltip and context menu MUST still show
  both W×H values and indicate 100% scaling for consistency.
- What happens with fractional scaling values like 137.5%? The
  display MUST round the percentage to the nearest whole number
  and both width and height to the nearest whole pixel.
- What happens when the tooltip text exceeds the 128-character
  system limit? The format MUST be concise enough to fit; if the
  profile name is long, it may be truncated with an ellipsis to
  keep resolution data intact.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The tray icon tooltip MUST display the current
  effective resolution as W×H in pixels.
- **FR-002**: The tray icon tooltip MUST display the native
  (physical) resolution of the primary display as W×H in pixels.
- **FR-003**: The tray icon tooltip MUST display the current display
  scaling factor as a percentage (e.g., "125%", "200%").
- **FR-004**: The context menu MUST display two disabled menu items
  at the top, above all existing items: first
  `Effective: W×H (Scale%)`, then `Native: W×H`.
- **FR-005**: The context menu resolution items MUST be separated
  from the rest of the menu by a visual divider.
- **FR-006**: All displayed resolution values MUST update
  automatically when the primary display changes (monitor switch,
  dock/undock, scaling change) within the existing poll interval.
- **FR-007**: The tooltip MUST remain within the operating system's
  tooltip character limit (currently 128 characters on Windows).
- **FR-008**: The tooltip and context menu MUST display whole-number
  pixel values and whole-number percentages (rounding to nearest).

### Key Entities

- **Display Info**: Represents the current primary display state —
  native resolution (W×H physical pixels), effective resolution
  (W×H logical pixels after scaling), and scaling factor
  (percentage).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can determine the effective resolution (W×H),
  native resolution (W×H), and scaling factor of their primary
  display by hovering over the tray icon, without opening any
  menu.
- **SC-002**: Users can see the same resolution breakdown at the
  top of the right-click context menu every time they open it.
- **SC-003**: The displayed resolution values match the values shown
  in Windows Display Settings for the primary monitor in 100% of
  tested scaling configurations (100%, 125%, 150%, 175%, 200%,
  225%, 250%, 300%).
- **SC-004**: Resolution information updates within one poll cycle
  (default 5 seconds) after a display change event (dock, undock,
  scaling change).
- **SC-005**: The tooltip text fits within the system tooltip
  character limit and is fully readable without truncation.

## Clarifications

### Session 2026-03-06

- Q: What format should the tooltip use (without "effective" label)? → A: Option A — `ProfileName | EffW×EffH (NativeW×NativeH @ Scale%)`
- Q: Context menu resolution format? → A: Option C — Two lines: `Effective: W×H (Scale%)` and `Native: W×H`

## Assumptions

- The tooltip format MUST follow the pattern:
  `"Ultrawide | 1920×1080 (3840×2160 @ 200%)"` — the effective
  resolution appears as the primary unlabeled number, native
  resolution and scaling are parenthesized. The word "effective"
  does not appear in the tooltip. All four data points (effective
  W×H, native W×H, scaling, and active profile) MUST be present.
- The context menu will present the resolution info as read-only
  (disabled/greyed) menu items at the top, consistent with the
  existing status item style.
- The existing poll/event mechanism for detecting display changes
  is sufficient; no new detection mechanism is needed.
- Only the primary display's resolution is shown (multi-monitor
  display is out of scope for this feature).
- Height values are derived by applying the same scaling formula
  used for width (physicalHeight × 96 / dpiY).
