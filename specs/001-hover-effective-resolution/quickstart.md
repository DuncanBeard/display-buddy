# Quickstart: Hover Effective Resolution

**Date**: 2026-03-06
**Feature**: 001-hover-effective-resolution

## Prerequisites

- Windows 11
- .NET 8 SDK installed
- A display with non-100% scaling (for full verification)

## Build & Run

```powershell
cd TaskbarAlignmentTool
dotnet build
dotnet run --project .
```

The app starts in the system tray (notification area).

## Verify Tooltip (US1)

1. Hover over the tray icon.
2. Confirm the tooltip shows:
   - Active profile name
   - Effective resolution as W×H (e.g., `1920×1080`)
   - Native resolution as W×H (e.g., `3840×2160`)
   - Scaling percentage (e.g., `200%`)
3. Open **Windows Settings → System → Display** and confirm the
   values match.

## Verify Context Menu (US2)

1. Right-click the tray icon.
2. Confirm the top of the menu shows resolution details:
   - Effective resolution (W×H)
   - Native resolution (W×H) and scaling %
3. Confirm these items appear above "Open Config" and are
   separated by a divider.
4. Confirm the items are read-only (greyed/disabled).

## Verify Dynamic Update

1. Change display scaling in Windows Settings (or dock/undock).
2. Wait up to one poll cycle (default 5 seconds).
3. Hover and right-click again — values should reflect the change.

## Edge Case Checks

- Set scaling to 100%: effective and native should be identical.
- Set scaling to 125% or 175%: verify fractional results round
  to whole pixels and whole percent.
- Disconnect all external monitors: verify tooltip shows a
  graceful fallback if no primary display is detected.
