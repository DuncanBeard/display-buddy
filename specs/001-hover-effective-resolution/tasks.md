# Tasks: Hover Effective Resolution

**Input**: Design documents from `/specs/001-hover-effective-resolution/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

**Tests**: No test framework in project. Tests are not requested.
All verification is manual per quickstart.md.

**Organization**: Tasks grouped by user story. Both stories are P1.
Most foundational work and US1 are already implemented. The only
code change is a one-line format fix for US2 (context menu).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)

## Path Conventions

- **Single project**: `TaskbarAlignmentTool/` at repository root
- Only one file needs modification: `TrayApplicationContext.cs`

---

## Phase 1: Foundational (Already Complete)

**Purpose**: `DisplayInfo` record and full resolution data pipeline.

**Status**: ✅ All foundational work is already implemented.

- [x] T001 `DisplayInfo` record with EffectiveWidth, EffectiveHeight, NativeWidth, NativeHeight, ScalingPercent in TaskbarAlignmentTool/DisplayMonitor.cs
- [x] T002 `GetDisplayInfo()` static method computing all five fields via `GetDpiForMonitor` and `Screen.PrimaryScreen.Bounds` in TaskbarAlignmentTool/DisplayMonitor.cs
- [x] T003 `CurrentDisplayInfo` property and `DisplayInfoChanged` event in TaskbarAlignmentTool/DisplayMonitor.cs
- [x] T004 Fallback: returns `DisplayInfo(0, 0, 0, 0, 100)` when `Screen.PrimaryScreen` is null in TaskbarAlignmentTool/DisplayMonitor.cs
- [x] T005 Rounding via `Math.Round()` for effective pixels and scaling percent in TaskbarAlignmentTool/DisplayMonitor.cs

**Checkpoint**: `DisplayMonitor` exposes full W×H + scaling data. ✅

---

## Phase 2: User Story 1 — See Effective Resolution on Hover (Priority: P1) 🎯 MVP (Already Complete)

**Goal**: Hovering over the tray icon shows effective W×H, native W×H, and scaling % in the tooltip.

**Independent Test**: Hover over the tray icon on a display with non-100% scaling. Verify the tooltip shows `ProfileName | EffW×EffH (NativeW×NativeH @ Scale%)`.

**Status**: ✅ Already implemented — tooltip format matches spec.

- [x] T006 [US1] Tooltip formatted as `"{profileName} | {ew}×{eh} ({nw}×{nh} @ {scale}%)"` in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T007 [US1] Profile name truncation with ellipsis when tooltip exceeds 127 chars in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T008 [US1] Fallback `"Resolution: unavailable"` when DisplayInfo has all-zero values in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T009 [US1] `DisplayInfoChanged` event subscription driving `ApplyForWidth()` → `UpdateStatus()` in TaskbarAlignmentTool/TrayApplicationContext.cs

**Checkpoint**: Tooltip shows full resolution breakdown on hover. ✅

---

## Phase 3: User Story 2 — Resolution Details in Context Menu (Priority: P1)

**Goal**: Right-clicking the tray icon shows `Effective: W×H (Scale%)` and `Native: W×H` as disabled items at the top of the context menu.

**Independent Test**: Right-click the tray icon. Verify the top shows two disabled items: `Effective: 1920×1080 (200%)` and `Native: 3840×2160`, then a divider, then existing menu items.

**Status**: Partially complete — menu items exist but scaling % is on the wrong line.

**Current code** (in `UpdateStatus()`):
```
Effective: {ew}×{eh}              ← missing (Scale%)
Native: {nw}×{nh} @ {scale}%     ← has scale, spec says just W×H
```

**Spec requires** (per clarification Option C):
```
Effective: {ew}×{eh} ({scale}%)   ← scaling here
Native: {nw}×{nh}                 ← no scaling
```

### Implementation for User Story 2

- [x] T010 [US2] Disabled `_effectiveResItem` and `_nativeResItem` menu items created in constructor in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T011 [US2] Separator between resolution items and profile item in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T012 [US2] `UpdateStatus()` refreshes menu item text on each poll/event in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T013 [US2] Fallback: shows `"Resolution: unavailable"` and hides native item when display unavailable in TaskbarAlignmentTool/TrayApplicationContext.cs
- [x] T014 [US2] Move scaling percentage from `_nativeResItem` to `_effectiveResItem` — change format from `"Effective: {ew}×{eh}"` to `"Effective: {ew}×{eh} ({scale}%)"` and from `"Native: {nw}×{nh} @ {scale}%"` to `"Native: {nw}×{nh}"` in TaskbarAlignmentTool/TrayApplicationContext.cs

**Checkpoint**: Context menu shows correct format per spec. Both user stories complete.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Manual verification of all acceptance scenarios.

- [x] T015 Build and run: `dotnet run --project TaskbarAlignmentTool` — verify no compilation errors
- [x] T016 Verify tooltip format by hovering on displays with 100%, 125%, 150%, 200% scaling
- [x] T017 Verify context menu format by right-clicking: confirm `Effective: W×H (Scale%)` and `Native: W×H`
- [x] T018 Verify display change detection: change scaling in Windows Settings, wait one poll cycle, confirm both tooltip and context menu update
- [x] T019 Verify tooltip stays ≤128 chars with a profile name of 20+ characters in config.json
- [x] T020 Run full quickstart.md validation walkthrough from specs/001-hover-effective-resolution/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: ✅ Complete
- **US1 Tooltip (Phase 2)**: ✅ Complete
- **US2 Context Menu (Phase 3)**: T014 is the only remaining code task
- **Polish (Phase 4)**: Depends on T014 completion

### Execution Plan (Remaining Work)

```text
T014 (code fix) → T015 (build) → T016–T020 (verify)
```

### Parallel Opportunities

- T016–T019 are independent manual verification steps that can run in any order after T015.
- T020 (quickstart walkthrough) is a superset that covers T016–T019.

---

## Implementation Strategy

### Remaining Work

1. T014: One format string fix in `UpdateStatus()` (~2 lines changed)
2. T015: Build and confirm no errors
3. T016–T020: Manual verification per quickstart.md
4. Commit

### Files Modified

| File | Phase | Changes |
|------|-------|---------|
| TaskbarAlignmentTool/TrayApplicationContext.cs | 3 | Move `@ {scale}%` from native line to effective line (T014) |

---

## Notes

- 20 total tasks; 13 already complete, 1 code change remaining, 6 verification tasks
- Only one source file modified for remaining work
- No new files, no new dependencies, no new P/Invoke calls
- Commit after T014 + T015 for clean history
