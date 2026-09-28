# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Taskbar Alignment Tool is a Windows 11 system-tray utility that dynamically switches taskbar settings (alignment, button combining, size) based on the primary display's effective resolution. It monitors for display changes (dock/undock, resolution changes) and applies user-defined resolution profiles automatically. No main window — UI is system tray icon + context menu only.

## Build & Run Commands

All commands use PowerShell. `make.ps1` is the task runner.

| Command | Description |
|---------|-------------|
| `.\make.ps1 build` | Build (Debug) |
| `.\make.ps1 test` | Build + smoke test (verifies EXE and DLL exist) |
| `.\make.ps1 run` | Build and run the app |
| `.\make.ps1 publish` | Build Release packages (framework-dependent + self-contained zips) |
| `.\make.ps1 sandbox` | Publish then launch self-contained Windows Sandbox test |
| `.\make.ps1 sandbox-fd` | Publish then launch framework-dependent Windows Sandbox test |
| `.\make.ps1 clean` | Remove bin/, obj/, publish/ |

Direct dotnet CLI equivalents:
- `dotnet build`
- `dotnet run --project TaskbarAlignmentTool`
- `dotnet publish TaskbarAlignmentTool -c Release -r win-x64 --self-contained -o publish`

There is no automated test suite. Testing is manual via Windows Sandbox or running the app and verifying tray menu behavior.

## Architecture

**Flat module structure** — no DI, no layers, no external NuGet packages. Each file has a single clear responsibility:

1. **Program.cs** — Entry point. Creates single-instance mutex (`Global\TaskbarAlignmentTool_SingleInstance`), loads config, starts `TrayApplicationContext`. Second instance silently exits.
2. **TrayApplicationContext.cs** — System tray icon, dynamic context menu (per-monitor info, config reload, startup toggle, diagnostics), tooltip; wires `DisplayMonitor` → `AppConfig` → `TaskbarAligner`. Owns lifecycle of `TrayIconRenderer` and `IRunAtStartup`.
3. **DisplayMonitor.cs** — Watches the primary display's resolution via three mechanisms: hidden `NativeWindow` for `WM_DISPLAYCHANGE`/`WM_DPICHANGED`, `SystemEvents.DisplaySettingsChanged`, and a low-frequency fallback timer. Publishes `MonitorInfo` via `PrimaryDisplayChanged`.
4. **MonitorInfo.cs** — Canonical record describing a monitor's state. Pure static `Compute(nativeW, nativeH, dpiX, dpiY)` factory holds the only copy of the DPI-scaling math; `ForPrimaryScreen()` is the cheap Win32 convenience used by the watcher.
5. **MonitorInfoProvider.cs** — Enumerates all monitors via Win32 CCD API (`QueryDisplayConfig`/`DisplayConfigGetDeviceInfo`) for friendly names, color depth, HDR, refresh rate, VRR. Calls `MonitorInfo.Compute` for the resolution math, then enriches with CCD fields. Called on-demand when context menu opens.
6. **TaskbarAligner.cs** — Writes taskbar registry keys under `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced` (`TaskbarAl`, `TaskbarGlomLevel`, `TaskbarSi`). Notifies Explorer via `WM_SETTINGCHANGE` + `SHChangeNotify`.
7. **TrayIconRenderer.cs** — Renders the DPI-aware, theme-tinted tray icon showing the current display width. Owns the underlying HICON lifecycle (calls `DestroyIcon` on swap and on `Dispose` — `Icon.FromHandle` doesn't transfer ownership and `Icon.Dispose()` alone leaks).
8. **RunAtStartup.cs** — `IRunAtStartup` interface (`IsEnabled`/`Enable`/`Disable`) plus two sibling adapters: `MsixRunAtStartup` (uses `Windows.ApplicationModel.StartupTask`) and `RegistryRunAtStartup` (uses `HKCU\Run`). `RunAtStartup.Create()` picks one based on a one-time packaging probe.
9. **AppConfig.cs** — JSON config model with enums (`AlignmentOption`, `CombineButtonsOption`, `TaskbarSizeOption`, `ResolutionMode`), profile matching (selects highest `minWidth` ≤ current width), load/save/migration from legacy exe-adjacent path.
10. **NativeMethods.cs** — All P/Invoke declarations (`user32.dll`, `shcore.dll`, `shell32.dll`, including `DestroyIcon`) and CCD API structs.

**Data flow**: `DisplayMonitor` detects change → fires `PrimaryDisplayChanged` with a `MonitorInfo` → `TrayApplicationContext.ApplyForWidth(monitorInfo.EffectiveWidth)` → `AppConfig.ResolveProfile()` picks profile → `TaskbarAligner.ApplyProfile()` writes registry → `NotifyExplorer()` broadcasts setting change.

**Startup toggle** (`RunAtStartup.cs`): Two adapters live behind `IRunAtStartup` — `MsixRunAtStartup` for MSIX `StartupTask`, `RegistryRunAtStartup` for `HKCU\Run`. Picked once at construction by `RunAtStartup.Create()` via a `Package.Current` probe. Adapters silently swallow API failures (see ADR-0003); `OnToggleStartup` re-queries `IsEnabled` to keep the menu checkbox honest.

## Technical Constraints (from project constitution)

- **Windows 11 only** — relies on Win11-specific registry keys
- **.NET 8.0** targeting `net8.0-windows10.0.19041.0`
- **Memory under 30 MB** during normal operation
- **Negligible CPU** when idle
- **Only HKCU registry writes** — never HKLM, never admin elevation
- **No main window** — tray icon + context menu only
- **No external dependencies** — zero NuGet packages beyond .NET/Windows SDK
- **Must pass MSIX/WACK validation** for Store distribution
- **Self-contained publish** must remain portable (USB, fresh install, Store)
- **Pragmatic code** — patterns and abstractions must justify themselves by solving a concrete problem; "good enough and shipping" beats "perfect and stalled"

## Key Configuration

- **Config file**: `%LOCALAPPDATA%\TaskbarAlignmentTool\config.json`
- **JSON Schema**: `schema/config.schema.json` (config files reference this via `$schema` for editor autocompletion)
- **MSIX packaging**: `TaskbarAlignmentTool.Package/` project — keep in sync with main project; version in `Package.appxmanifest` must match assembly version
- **Publish output**: `publish/win-x64-self-contained/` and `publish/win-x64-framework-dependent/`

## Code Conventions

- C# 12 / file-scoped namespaces / nullable reference types / implicit usings
- `sealed` classes by default; `record` types for data-only types
- `internal` visibility for implementation details
- `AllowUnsafeBlocks` enabled for P/Invoke interop
- PerMonitorV2 DPI awareness
- `System.Text.Json` with `JsonStringEnumConverter` for serialization

## Agent skills

### Issue tracker

GitHub Issues at `DuncanBeard/display-buddy` via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context — `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.