# Taskbar Alignment Tool

A Windows 11 system-tray app that dynamically adjusts taskbar settings based on your primary display's effective resolution. When you dock/undock a laptop or change monitors, the taskbar automatically adapts.

## Features

- **Resolution profiles** — Define breakpoints that control taskbar alignment, button combining, and taskbar size
- **System tray** — Runs silently in the notification area with a right-click context menu
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

## Run

```powershell
dotnet run --project TaskbarAlignmentTool
```

The app starts in the system tray (notification area). Right-click the tray icon for options.

## Publish (self-contained executable)

```powershell
dotnet publish TaskbarAlignmentTool -c Release -r win-x64 --self-contained -o publish
```

This produces a standalone `publish\TaskbarAlignmentTool.exe` that doesn't require .NET to be installed.

## Configuration

On first run, a default `config.json` is created at:

```
%LOCALAPPDATA%\TaskbarAlignmentTool\config.json
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
TaskbarAlignmentTool/
├── Program.cs                  # Entry point
├── TrayApplicationContext.cs   # System tray icon and context menu
├── DisplayMonitor.cs           # Polls primary display effective resolution
├── TaskbarAligner.cs           # Writes registry keys, notifies Explorer
├── AppConfig.cs                # JSON config model, load/save, profile matching
├── NativeMethods.cs            # P/Invoke declarations
├── app.manifest                # Execution level manifest
└── icon.ico                    # Tray icon

TaskbarAlignmentTool.Package/   # MSIX packaging project (for Microsoft Store)
├── Package.appxmanifest
└── Images/                     # Store icon assets
```

## License

MIT
