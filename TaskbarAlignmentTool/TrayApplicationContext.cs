using System.Diagnostics;
using Microsoft.Win32;

namespace TaskbarAlignmentTool;

/// <summary>
/// Application context that manages the system-tray icon, context menu,
/// and wires the display monitor to the taskbar aligner using resolution profiles.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "TaskbarAlignmentTool";

    private AppConfig _config;
    private readonly DisplayMonitor _monitor;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _profileItem;
    private readonly ToolStripMenuItem _startupItem;

    public TrayApplicationContext(AppConfig config)
    {
        _config = config;
        _monitor = new DisplayMonitor(config.RefreshIntervalMs);

        _statusItem = new ToolStripMenuItem("Initializing...") { Enabled = false };
        _profileItem = new ToolStripMenuItem("Profile: —") { Enabled = false };
        _startupItem = new ToolStripMenuItem("Run at Startup", null, OnToggleStartup)
        {
            Checked = IsStartupEnabled()
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(_profileItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Config", null, OnOpenConfig);
        menu.Items.Add("Reload Config", null, OnReloadConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Refresh Now", null, OnRefresh);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Text = "Taskbar Alignment Tool",
            Visible = true
        };

        _monitor.ResolutionChanged += OnResolutionChanged;

        // Apply profile immediately on startup
        ApplyForWidth(_monitor.EffectiveWidth);
    }

    private void OnResolutionChanged(object? sender, int effectiveWidth)
    {
        ApplyForWidth(effectiveWidth);
    }

    private void ApplyForWidth(int effectiveWidth)
    {
        var profile = _config.ResolveProfile(effectiveWidth);
        if (profile != null)
            TaskbarAligner.ApplyProfile(profile);
        UpdateStatus(effectiveWidth, profile);
    }

    private void UpdateStatus(int effectiveWidth, ProfileConfig? profile)
    {
        var profileName = profile?.Name ?? "None";
        _statusItem.Text = $"Width: {effectiveWidth}px";
        _profileItem.Text = $"Profile: {profileName}";
        // NotifyIcon.Text has a 127-char limit
        _notifyIcon.Text = $"Taskbar: {profileName} ({effectiveWidth}px)";
    }

    private void OnRefresh(object? sender, EventArgs e)
    {
        _monitor.Refresh(force: true);
    }

    private void OnOpenConfig(object? sender, EventArgs e)
    {
        var path = AppConfig.GetConfigPath();
        if (!File.Exists(path))
            _config.Save();
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OnReloadConfig(object? sender, EventArgs e)
    {
        _config = AppConfig.Load();
        _monitor.UpdateInterval(_config.RefreshIntervalMs);
        _monitor.Refresh(force: true);
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        if (IsStartupEnabled())
            DisableStartup();
        else
            EnableStartup();

        _startupItem.Checked = IsStartupEnabled();
    }

    private static bool IsMsixPackaged()
    {
        try
        {
            // Windows.ApplicationModel.Package.Current throws if not packaged
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStartupEnabled()
    {
        if (IsMsixPackaged())
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask
                    .GetAsync("TaskbarAlignmentToolStartup").GetAwaiter().GetResult();
                return task.State == Windows.ApplicationModel.StartupTaskState.Enabled;
            }
            catch { return false; }
        }

        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, false);
        return key?.GetValue(StartupValueName) != null;
    }

    private static void EnableStartup()
    {
        if (IsMsixPackaged())
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask
                    .GetAsync("TaskbarAlignmentToolStartup").GetAwaiter().GetResult();
                task.RequestEnableAsync().GetAwaiter().GetResult();
            }
            catch { /* Startup task not available */ }
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, true);
        var exePath = Application.ExecutablePath;
        key?.SetValue(StartupValueName, $"\"{exePath}\"");
    }

    private static void DisableStartup()
    {
        if (IsMsixPackaged())
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask
                    .GetAsync("TaskbarAlignmentToolStartup").GetAwaiter().GetResult();
                task.Disable();
            }
            catch { /* Startup task not available */ }
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, true);
        key?.DeleteValue(StartupValueName, false);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _monitor.Dispose();
        _notifyIcon.Dispose();
        Application.Exit();
    }

    private static Icon LoadIcon()
    {
        var dir = Path.GetDirectoryName(Application.ExecutablePath) ?? ".";
        var iconPath = Path.Combine(dir, "icon.ico");
        return File.Exists(iconPath)
            ? new Icon(iconPath)
            : SystemIcons.Application;
    }
}
