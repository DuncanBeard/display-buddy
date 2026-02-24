# Windows Sandbox Test Environment

Two `.wsb` files for testing the Taskbar Alignment Tool in a clean Windows Sandbox.

## Prerequisites
- Windows Sandbox must be enabled: `Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM`
- Build the publish outputs first:
  ```powershell
  dotnet publish TaskbarAlignmentTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish\win-x64-self-contained
  dotnet publish TaskbarAlignmentTool -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o publish\win-x64-framework-dependent
  ```

## Test Configs

### `TestSelfContained.wsb`
- **Networking:** Disabled (no internet needed)
- **Behavior:** Copies `config.json` to `%LOCALAPPDATA%`, opens it in Notepad, launches the self-contained EXE
- **Use for:** Testing the standalone EXE on a clean machine with no .NET installed

### `TestFrameworkDependent.wsb`
- **Networking:** Enabled (downloads .NET 8 runtime)
- **Behavior:** Installs .NET 8 Desktop Runtime, copies config, opens Notepad, launches the app
- **Use for:** Testing the small framework-dependent build

## Editing Config During Testing
Both sandbox configs auto-open `config.json` in Notepad on startup. Edit the profiles, save, then use the tray menu → **Reload Config** to apply changes instantly.

To re-edit later: tray menu → **Open Config** (opens in Notepad).

## Default Test Config (`config.json`)
Ships with 3 profiles: Compact (0px+), Standard (1920px+), Ultrawide (2560px+). Edit `sandbox\config.json` on the host to change the starting config for future sandbox launches.

## Usage
Double-click either `.wsb` file to launch. The sandbox is destroyed when you close the window — no cleanup needed.

## What to Test
1. App appears in system tray
2. Right-click menu works (status, profiles, config, diagnostics)
3. Edit config in Notepad → save → Reload Config → verify profile changes
4. Change display scaling in sandbox Settings → verify profile switches
5. Check Diagnostics → memory should be low (~30-50 MB)
6. Launch exe again → second instance should silently exit (mutex)
