# Tasks: Display Info Menu

**Input**: Design documents from `/specs/002-display-info-menu/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Not requested — test tasks omitted.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup

**Purpose**: Add CCD API P/Invoke infrastructure to the existing project

- [X] T001 Add CCD enumeration P/Invoke signatures (GetDisplayConfigBufferSizes, QueryDisplayConfig) to TaskbarAlignmentTool/NativeMethods.cs
- [X] T002 Add DISPLAYCONFIG struct definitions (LUID, DISPLAYCONFIG_DEVICE_INFO_HEADER, DISPLAYCONFIG_PATH_INFO, DISPLAYCONFIG_MODE_INFO, DISPLAYCONFIG_RATIONAL, DISPLAYCONFIG_PATH_TARGET_INFO, DISPLAYCONFIG_PATH_SOURCE_INFO) to TaskbarAlignmentTool/NativeMethods.cs
- [X] T003 Add DISPLAYCONFIG_DEVICE_INFO_TYPE enum and QDC flag constants to TaskbarAlignmentTool/NativeMethods.cs
- [X] T004 Add DisplayConfigGetDeviceInfo overloads for DISPLAYCONFIG_TARGET_DEVICE_NAME, DISPLAYCONFIG_SOURCE_DEVICE_NAME, DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO, and DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION to TaskbarAlignmentTool/NativeMethods.cs
- [X] T005 Add struct definitions for DISPLAYCONFIG_TARGET_DEVICE_NAME, DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS, DISPLAYCONFIG_SOURCE_DEVICE_NAME to TaskbarAlignmentTool/NativeMethods.cs
- [X] T006 Add struct definitions for DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO, DISPLAYCONFIG_COLOR_ENCODING enum, DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION to TaskbarAlignmentTool/NativeMethods.cs
- [X] T007 Define MonitorDisplayInfo record (FriendlyName, IsPrimary, EffectiveWidth, EffectiveHeight, NativeWidth, NativeHeight, ScalingPercent, BitsPerChannel, HdrStatus, RefreshRateHz, VrrStatus) in TaskbarAlignmentTool/MonitorInfoProvider.cs

**Checkpoint**: All P/Invoke signatures and data structures compiled. Ready for enumeration logic.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Single-pass monitor enumeration that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 Implement GetActiveDisplayConfig helper (QueryDisplayConfig with QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE, retry on ERROR_INSUFFICIENT_BUFFER) in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T009 Implement GetMonitorFriendlyName helper (DisplayConfigGetDeviceInfo with GET_TARGET_NAME, fallback to "Display N") in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T010 Implement IsPathPrimary helper (DisplayConfigGetDeviceInfo with GET_SOURCE_NAME, correlate viewGdiDeviceName against Screen.AllScreens to find primary) in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T011 Implement GetColorAndHdrInfo helper (DisplayConfigGetDeviceInfo with GET_ADVANCED_COLOR_INFO, return bitsPerChannel + hdrStatus with N/A fallback) in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T012 Implement GetRefreshRate helper (extract DISPLAYCONFIG_RATIONAL from path.targetInfo.refreshRate, compute Hz as double, 0 on failure) in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T013 Implement GetVrrStatus helper (DisplayConfigGetDeviceInfo with GET_MONITOR_SPECIALIZATION, return On/Off/N/A with graceful pre-Win11 fallback) in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T014 Implement GetResolutionAndScaling helper (for a given GDI device name, find matching Screen in Screen.AllScreens, get Bounds + DPI via GetDpiForMonitor, compute effective resolution and scaling percent) in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T015 Implement public static GetAllMonitors method that calls GetActiveDisplayConfig, iterates paths, calls all helpers to build List<MonitorDisplayInfo>, sorts primary first then by GDI device name, and handles empty result with empty list in TaskbarAlignmentTool/MonitorInfoProvider.cs
- [X] T016 Build the project and verify MonitorInfoProvider compiles with no errors

**Checkpoint**: MonitorInfoProvider.GetAllMonitors() is callable and returns correct data. Foundation ready.

---

## Phase 3: User Story 1 — Single Monitor Display Properties (Priority: P1) 🎯 MVP

**Goal**: Replace standalone Effective/Native/Profile menu items with a consolidated per-monitor section showing resolution, scaling, profile, color depth, HDR, refresh rate, and VRR.

**Independent Test**: Right-click tray icon on a single-monitor system. Verify the section shows all properties matching Windows Settings > Display > Advanced display.

### Implementation for User Story 1

- [X] T017 [US1] Remove static _effectiveResItem, _nativeResItem, _profileItem fields and their menu.Items.Add calls from the constructor in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T018 [US1] Add a ContextMenuStrip.Opening event handler that calls MonitorInfoProvider.GetAllMonitors() and rebuilds the monitor section dynamically in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T019 [US1] Implement BuildMonitorMenuItems helper method that takes a MonitorDisplayInfo and active ProfileConfig, returns a list of disabled ToolStripMenuItems: monitor name header, Effective line, Native line, Profile line (primary only), Color line, HDR line, Refresh rate line, VRR line in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T020 [US1] Update the menu layout in the constructor: insert a sentinel ToolStripSeparator marking where dynamic monitor items begin, keep all static items (Open Config, Reload Config, Startup, Refresh, Diagnostics, Exit) below in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T021 [US1] Update UpdateStatus method to no longer set text on removed static items; keep tooltip and tray icon update using primary monitor info from MonitorInfoProvider or existing DisplayMonitor in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T022 [US1] Build and manually verify: open tray menu on single monitor, confirm consolidated section shows resolution, scaling, profile, color depth, HDR, refresh rate, VRR

**Checkpoint**: User Story 1 complete — single-monitor menu shows all display properties in one block.

---

## Phase 4: User Story 2 — Multiple Monitor Sections (Priority: P2)

**Goal**: When multiple monitors are connected, show each in its own labeled section stacked vertically, primary first then by Windows display number.

**Independent Test**: Connect 2+ monitors, open tray menu, verify each monitor has its own labeled section with correct properties and primary is listed first.

### Implementation for User Story 2

- [X] T023 [US2] Update the Opening event handler to iterate all MonitorDisplayInfo items from GetAllMonitors() and insert a ToolStripSeparator between each monitor section in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T024 [US2] Ensure monitor name header uses FriendlyName (e.g., "DELL U2723QE") and falls back to "Display N" when EDID name is unavailable in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T025 [US2] Ensure Profile line is only added for the monitor where IsPrimary is true in BuildMonitorMenuItems in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T026 [US2] Build and manually verify: open tray menu with 2+ monitors, confirm each monitor has its own section, primary is first, properties are correct per monitor

**Checkpoint**: User Story 2 complete — multi-monitor systems show stacked per-monitor sections.

---

## Phase 5: User Story 3 — Graceful Handling of Unavailable Properties (Priority: P3)

**Goal**: When a property cannot be determined, show "N/A" gracefully. Handle monitor disconnect, no-monitors, and pre-Win11 VRR unavailability.

**Independent Test**: Verify VRR shows "N/A" on Windows 10 or unsupported monitors. Disconnect a monitor and re-open the menu to confirm the section disappears.

### Implementation for User Story 3

- [X] T027 [US3] In BuildMonitorMenuItems, show "Color: 8-bit" as safe default when BitsPerChannel is 0 and HdrStatus is "N/A" (non-Advanced-Color SDR display) in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T028 [US3] In BuildMonitorMenuItems, show "Refresh: N/A" when RefreshRateHz is 0, otherwise show rounded integer Hz in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T029 [US3] In the Opening event handler, when GetAllMonitors() returns an empty list, show a single disabled item "No displays detected" in TaskbarAlignmentTool/TrayApplicationContext.cs
- [X] T030 [US3] Build and manually verify: check menu after disconnecting a monitor (section disappears), check VRR shows N/A on unsupported hardware, check color/HDR show correct fallback values

**Checkpoint**: User Story 3 complete — all N/A and edge cases handled gracefully.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and validation

- [X] T031 [P] Verify the project builds with no warnings in both Debug and Release configurations
- [X] T032 [P] Run quickstart.md validation: test each scenario listed in Testing Notes (single monitor, multi-monitor, HDR toggle, connect/disconnect)
- [X] T033 Confirm tooltip still shows primary monitor info correctly and tray icon still renders effective width

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion
- **User Story 2 (Phase 4)**: Depends on Phase 3 completion (builds on menu structure from US1)
- **User Story 3 (Phase 5)**: Depends on Phase 4 completion (refines N/A handling in existing menu)
- **Polish (Phase 6)**: Depends on Phase 5 completion

### User Story Dependencies

- **User Story 1 (P1)**: Requires Foundational phase. No dependencies on other stories.
- **User Story 2 (P2)**: Builds on the menu structure created in US1 (same file, same method). Requires US1 complete.
- **User Story 3 (P3)**: Refines fallback behavior in the menu built by US1+US2. Requires US2 complete.

### Within Each Phase

- T001–T006 all modify NativeMethods.cs — execute sequentially
- T007 creates MonitorInfoProvider.cs — can parallel with T001–T006
- T008–T015 modify MonitorInfoProvider.cs — execute sequentially
- Within US1–US3: tasks modify TrayApplicationContext.cs — execute sequentially

### Parallel Opportunities

- T007 (MonitorDisplayInfo record) can run in parallel with T001–T006 (NativeMethods structs) since they are different files
- T031 and T032 in Polish phase can run in parallel

---

## Parallel Example: Setup Phase

```
# These can run in parallel (different files):
Task T001–T006: NativeMethods.cs P/Invoke additions
Task T007:      MonitorInfoProvider.cs record definition
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (P/Invoke declarations)
2. Complete Phase 2: Foundational (monitor enumeration)
3. Complete Phase 3: User Story 1 (single-monitor menu)
4. **STOP and VALIDATE**: Open tray menu, verify all properties display correctly
5. Ship if ready — single-monitor users get full value

### Incremental Delivery

1. Complete Setup + Foundational → Enumeration ready
2. Add User Story 1 → Single-monitor menu works → **MVP!**
3. Add User Story 2 → Multi-monitor sections work → Deploy
4. Add User Story 3 → N/A and edge cases handled → Deploy
5. Polish → Warnings clean, all scenarios verified → Final

---

## Notes

- All P/Invoke in NativeMethods.cs uses `LibraryImport` (source-generated) matching existing codebase style
- No new NuGet dependencies — all APIs are Win32 P/Invoke to user32.dll and shcore.dll
- Existing DisplayMonitor.cs is UNCHANGED — profile switching still primary-monitor-only
- VRR will always show "N/A" on Windows 10 — this is by design per research.md
- Color depth defaults to "8-bit" for non-Advanced-Color SDR displays per research.md recommendation
- Refresh rate is rounded to integer Hz to match Windows Settings UX per research.md
