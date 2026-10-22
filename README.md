# TaskBard

_Make your icons dance._

TaskBard switches Windows 11 taskbar profiles based on your primary display. It runs in the system tray and adjusts taskbar alignment, button combining, and size to match your primary display's effective resolution. When you dock/undock a laptop or change monitors, the taskbar automatically adapts.

## Features

- **Resolution profiles** — Define breakpoints that control taskbar alignment, button combining, and taskbar size
- **System tray** — Runs silently in the notification area; hover shows effective + native resolution with scaling %, right-click for options
- **JSON config** — Edit `config.json` to customize profiles, thresholds, and poll interval
- **Run at Startup** — Toggle auto-start from the tray menu (MSIX StartupTask or registry)
- **No admin required** — All settings are under `HKCU`

## Default Profiles

| Profile | Min Width | Alignment | Combine Buttons | Taskbar Size |
|---------|-----------|-----------|-----------------|--------------|
| Compact | 0 px | Left | Never | Small |
| Standard | 1920 px | Left | When Full | Default |
| Ultrawide | 2560 px | Center | Always | Default |

The highest `minWidth` that fits your current effective resolution wins.

## Requirements

- **Windows 11** (the `TaskbarAl`, `TaskbarGlomLevel`, and `TaskbarSi` registry keys are Windows 11 specific)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)

## Build

```powershell
dotnet build
```

## Test

```powershell
.\make.ps1 test
```

This builds the solution, runs the `TaskBard.Tests` MSTest suite, publishes both release variants and checks the artifacts. It exits non-zero if any check fails. The suite checks project and package metadata and scans active files for abandoned product names and stale repository URLs. ADRs, dated issue records, completed specs and transcripts are allow-listed in `TaskBard.Tests/ProductSurfaceScanner.cs`.

## Run

```powershell
dotnet run --project TaskBard
```

The app starts in the system tray (notification area).

- **Hover** the tray icon to see the active profile, effective resolution, native resolution, and scaling:
  `Standard | 1920×1080 eff (2560×1440 @ 150%)`
- **Right-click** the tray icon for the context menu:
  - `Effective: 1920×1080` *(greyed)*
  - `Native: 2560×1440 @ 150%` *(greyed)*
  - ─
  - `Profile: Standard` *(greyed)*
  - ─
  - Open Config, Reload Config, Run at Startup, Exit TaskBard

## Publish (self-contained executable)

```powershell
dotnet publish TaskBard -c Release -r win-x64 --self-contained -o publish
```

This produces a standalone `publish\TaskBard.exe` that doesn't require .NET to be installed.

To build both release variants and their zips, run `.\make.ps1 publish`. It writes `publish\TaskBard-win-x64-framework-dependent\`, `publish\TaskBard-win-x64-self-contained\`, `publish\TaskBard-win-x64.zip` and `publish\TaskBard-win-x64-self-contained.zip`.

## Configuration

On first run, a default `config.json` is created at:

```
%LOCALAPPDATA%\TaskBard\config.json
```

You can also open it from the tray menu → **Open Config**.

### Example config

```json
{
  "refreshIntervalMs": 5000,
  "profiles": [
    {
      "name": "Compact",
      "minWidth": 0,
      "alignment": "Left",
      "combineButtons": "Never",
      "taskbarSize": "Small"
    },
    {
      "name": "Standard",
      "minWidth": 1920,
      "alignment": "Left",
      "combineButtons": "WhenFull",
      "taskbarSize": "Default"
    },
    {
      "name": "Ultrawide",
      "minWidth": 2560,
      "alignment": "Center",
      "combineButtons": "Always",
      "taskbarSize": "Default"
    }
  ]
}
```

### Config options

| Field | Values |
|-------|--------|
| `refreshIntervalMs` | Poll interval in milliseconds (minimum 500) |
| `alignment` | `Left`, `Center` |
| `combineButtons` | `Always`, `WhenFull`, `Never` |
| `taskbarSize` | `Small`, `Default`, `Large` |

After editing, use tray menu → **Reload Config** to apply changes immediately.

## Registry Keys

The app writes to `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`:

| Key | Values | Effect |
|-----|--------|--------|
| `TaskbarAl` | 0 = Left, 1 = Center | Taskbar alignment |
| `TaskbarGlomLevel` | 0 = Always, 1 = When Full, 2 = Never | Combine taskbar buttons |
| `TaskbarSi` | 0 = Small, 1 = Default, 2 = Large | Taskbar size |

## Project Structure

```
TaskBard/
├── Program.cs                  # Entry point + single-instance mutex
├── TrayApplicationContext.cs   # System tray icon, context menu, monitor → aligner wiring
├── DisplayMonitor.cs           # Watches primary display for resolution / DPI changes
├── MonitorInfo.cs              # Canonical MonitorInfo record + DPI-scaling factory
├── MonitorInfoProvider.cs      # Enumerates all monitors via the Windows CCD API
├── TaskbarAligner.cs           # Writes taskbar registry keys, notifies Explorer
├── TrayIconRenderer.cs         # Renders the live-width tray icon, owns HICON lifecycle
├── RunAtStartup.cs             # IRunAtStartup interface + MSIX / registry adapters
├── AppConfig.cs                # JSON config model, load/save, profile matching
├── NativeMethods.cs            # P/Invoke declarations
├── app.manifest                # Execution level manifest
└── TaskBard.ico                # Executable icon (generated, see assets/)

TaskBard.Package/               # MSIX packaging project (for Microsoft Store)
├── Package.appxmanifest
└── Images/                     # MSIX package images (generated, see assets/)

TaskBard.Tests/                 # MSTest suite: release metadata, asset checks, abandoned-name scan (never shipped)

assets/                         # Icon generator inputs (mark.txt, generate.ps1) and tray icon variants
```

## Project Documentation

- **`CONTEXT.md`** — domain glossary (Monitor, Primary Display, MonitorInfo, Profile, Profile Switch, Tray Icon, Run at Startup) and how the modules relate.
- **`docs/adr/`** — architectural decision records for non-obvious choices that future readers might re-suggest "fixing." Read these before refactoring the modules they cover.
- **`.specify/memory/constitution.md`** — the project constitution (five principles: Lightweight, Self-Contained, Pragmatic Code, Startup-Ready, Store-Distributable).

## Future Work

- Per-icon system tray visibility control (show/hide overflow icons per profile)
- Multi-monitor support (apply profiles per-monitor, not just primary)

## License

MIT
