# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TaskBard is a Windows 11 system-tray utility that dynamically switches taskbar settings (alignment, button combining, size) based on the primary display's effective resolution. It monitors for display changes (dock/undock, resolution changes) and applies user-defined resolution profiles automatically. No main window — UI is system tray icon + context menu only.

Tagline: "Make your icons dance." It's a metaphor. Functional copy should describe taskbar buttons and **Profile Switches** plainly and never imply animation (see `CONTEXT.md`).

## Build & Run Commands

All commands use PowerShell. `make.ps1` is the task runner.

| Command | Description |
|---------|-------------|
| `.\make.ps1 build` | Build the solution (Debug) |
| `.\make.ps1 test` | Build, run MSTest, publish both variants and check artifacts (non-zero exit on failure) |
| `.\make.ps1 run` | Build and run the app |
| `.\make.ps1 publish` | Build Release packages (framework-dependent + self-contained zips) |
| `.\make.ps1 sandbox` | Publish then launch self-contained Windows Sandbox test |
| `.\make.ps1 sandbox-fd` | Publish then launch framework-dependent Windows Sandbox test |
| `.\make.ps1 msix` | Build the MSIX with Visual Studio's MSBuild, sign it with a local self-signed test cert (UAC once to trust it), reinstall and launch it |
| `.\make.ps1 clean` | Remove bin/, obj/ (app, tests, package), publish/ |

Direct dotnet CLI equivalents:
- `dotnet build`
- `dotnet test TaskBard.slnx`
- `dotnet run --project TaskBard`
- `dotnet publish TaskBard -c Release -r win-x64 --self-contained -o publish`

`TaskBard.Tests` (MSTest) is the only automated test project. It covers release metadata (it parses the solution, project files and `Package.appxmanifest` as XML), package assets (every manifest image and ICO has its exact pixel sizes, and there's a single package-image tree) and an abandoned-name scan. That scan checks every text file for retired product names and stale repo URLs, except the historical records allow-listed in `ProductSurfaceScanner.cs`. `make.ps1 test` adds publish-artifact checks: the expected `TaskBard.exe`, archive names, and no test assemblies. Don't add tests for unchanged profile matching, monitor enumeration or registry behavior. Tray rendering and the rest of the runtime are still tested by hand, in Windows Sandbox or by running the app and checking the tray menu.

## Architecture

**Flat module structure** — no DI, no layers, no external NuGet packages. Each file has a single clear responsibility:

1. **Program.cs** — Entry point. Creates single-instance mutex (`Global\TaskBard_SingleInstance`), loads config, starts `TrayApplicationContext`. Second instance silently exits.
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
- **Memory under 70 MB working set** during normal operation (the Diagnostics figure; measured baseline 65.8 MB, may only go down)
- **Negligible CPU** when idle
- **Only HKCU registry writes** — never HKLM, never admin elevation
- **No main window** — tray icon + context menu only
- **No external dependencies** — zero NuGet packages beyond .NET/Windows SDK in the app project (test-only packages are confined to `TaskBard.Tests`, which is never shipped)
- **Must pass MSIX/WACK validation** for Store distribution
- **Self-contained publish** must remain portable (USB, fresh install, Store)
- **Pragmatic code** — patterns and abstractions must justify themselves by solving a concrete problem; "good enough and shipping" beats "perfect and stalled"

## Key Configuration

- **Config file**: `%LOCALAPPDATA%\TaskBard\config.json`
- **JSON Schema**: `schema/config.schema.json` (config files reference this via `$schema` for editor autocompletion)
- **MSIX packaging**: `TaskBard.Package/` project — keep in sync with main project; version in `Package.appxmanifest` must match assembly version
- **Icons and package images**: generated by `assets/generate.ps1` from `assets/mark.txt` (run by hand, never part of the build); outputs are `TaskBard/TaskBard.ico`, `TaskBard.Package/Images/` (the only package-image tree) and `assets/tray/`. Current art is a placeholder pending #5 — see `assets/README.md`
- **Publish output**: `publish/TaskBard-win-x64-self-contained/` and `publish/TaskBard-win-x64-framework-dependent/`

## Code Conventions

- C# 12 / file-scoped namespaces / nullable reference types / implicit usings
- `sealed` classes by default; `record` types for data-only types
- `internal` visibility for implementation details
- `AllowUnsafeBlocks` enabled for P/Invoke interop
- PerMonitorV2 DPI awareness
- `System.Text.Json` with `JsonStringEnumConverter` for serialization

## Agent skills

### Issue tracker

GitHub Issues at `DuncanBeard/taskbard` via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default canonical vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context — `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.