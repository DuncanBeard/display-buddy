using System.Diagnostics;

namespace TaskbarAlignmentTool;

/// <summary>
/// Application context that manages the system-tray icon, context menu,
/// and wires the display monitor to the taskbar aligner using resolution profiles.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private AppConfig _config;
    private readonly DisplayMonitor _monitor;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayIconRenderer _iconRenderer;
    private readonly IRunAtStartup _runAtStartup;
    private readonly ToolStripSeparator _monitorSectionEnd;
    private readonly ToolStripMenuItem _startupItem;

    private int _profileSwitchCount;

    public TrayApplicationContext(AppConfig config)
    {
        _config = config;
        _monitor = new DisplayMonitor(config.RefreshIntervalMs, config.ResolutionMode);
        _iconRenderer = new TrayIconRenderer();
        _runAtStartup = RunAtStartup.Create();

        _monitorSectionEnd = new ToolStripSeparator();
        _startupItem = new ToolStripMenuItem("Run at Startup", null, OnToggleStartup)
        {
            Checked = _runAtStartup.IsEnabled
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_monitorSectionEnd);
        menu.Items.Add("Open Config", null, OnOpenConfig);
        menu.Items.Add("Reload Config", null, OnReloadConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Refresh Now", null, OnRefresh);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateDiagnosticsMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        menu.Opening += OnMenuOpening;

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text = "Taskbar Alignment Tool",
            Visible = true
        };
        _iconRenderer.UpdateIcon(_notifyIcon, 0);

        _monitor.PrimaryDisplayChanged += OnPrimaryDisplayChanged;

        // Apply profile immediately on startup
        ApplyForWidth(_monitor.PrimaryDisplay.EffectiveWidth);
    }

    private void OnPrimaryDisplayChanged(object? sender, MonitorInfo info)
    {
        ApplyForWidth(info.EffectiveWidth);
    }

    private void ApplyForWidth(int effectiveWidth)
    {
        var profile = _config.ResolveProfile(effectiveWidth);
        if (profile != null && TaskbarAligner.ApplyProfile(profile))
        {
            _profileSwitchCount++;
            if (_config.ShowNotifications)
            {
                _notifyIcon.ShowBalloonTip(
                    _config.NotificationDurationMs,
                    "Taskbar Alignment Tool",
                    $"Switched to \"{profile.Name}\" ({effectiveWidth}px)",
                    ToolTipIcon.Info);
            }
        }
        var displayInfo = _monitor.PrimaryDisplay;
        UpdateStatus(displayInfo, profile);
    }

    private void UpdateStatus(MonitorInfo info, ProfileConfig? profile)
    {
        var profileName = profile?.Name ?? "None";
        bool unavailable = info.EffectiveWidth == 0 && info.EffectiveHeight == 0;

        if (unavailable)
        {
            _notifyIcon.Text = "Resolution: unavailable";
            return;
        }

        // Format: "ProfileName | 1920×1080 (3840×2160 @ 200%)"
        var resText = $"{info.EffectiveWidth}\u00d7{info.EffectiveHeight} ({info.NativeWidth}\u00d7{info.NativeHeight} @ {info.ScalingPercent}%)";
        var tooltip = $"{profileName} | {resText}";
        // Truncate profile name if tooltip exceeds 127 chars (NotifyIcon.Text limit is 127 + null)
        if (tooltip.Length > 127)
        {
            var maxName = 127 - " | ".Length - resText.Length - "\u2026".Length;
            if (maxName > 0)
                tooltip = $"{profileName[..maxName]}\u2026 | {resText}";
            else
                tooltip = resText[..Math.Min(resText.Length, 127)];
        }
        _notifyIcon.Text = tooltip;

        _iconRenderer.UpdateIcon(_notifyIcon, info.EffectiveWidth);
    }

    private void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var menu = _notifyIcon.ContextMenuStrip;
        if (menu == null) return;

        // Remove and dispose all dynamic items above the sentinel separator
        while (menu.Items.Count > 0 && menu.Items[0] != _monitorSectionEnd)
        {
            var item = menu.Items[0];
            menu.Items.RemoveAt(0);
            item.Dispose();
        }

        var monitors = MonitorInfoProvider.GetAllMonitors();
        var profile = _config.ResolveProfile(_monitor.PrimaryDisplay.EffectiveWidth);

        if (monitors.Count == 0)
        {
            menu.Items.Insert(0, new ToolStripMenuItem("No displays detected") { Enabled = false });
            return;
        }

        int insertIdx = 0;
        for (int i = 0; i < monitors.Count; i++)
        {
            if (i > 0)
                menu.Items.Insert(insertIdx++, new ToolStripSeparator());

            foreach (var item in BuildMonitorMenuItems(monitors[i], profile))
                menu.Items.Insert(insertIdx++, item);
        }
    }

    private static List<ToolStripItem> BuildMonitorMenuItems(MonitorInfo monitor, ProfileConfig? profile)
    {
        var items = new List<ToolStripItem>();

        // Monitor name header
        items.Add(new ToolStripMenuItem(monitor.FriendlyName) { Enabled = false });

        if (monitor.EffectiveWidth > 0)
        {
            items.Add(new ToolStripMenuItem(
                $"  Effective: {monitor.EffectiveWidth}\u00d7{monitor.EffectiveHeight} ({monitor.ScalingPercent}%)") { Enabled = false });
            items.Add(new ToolStripMenuItem(
                $"  Native: {monitor.NativeWidth}\u00d7{monitor.NativeHeight}") { Enabled = false });
        }
        else
        {
            items.Add(new ToolStripMenuItem("  Resolution: unavailable") { Enabled = false });
        }

        // Profile line — primary monitor only
        if (monitor.IsPrimary)
        {
            var profileName = profile?.Name ?? "None";
            items.Add(new ToolStripMenuItem($"  Profile: {profileName}") { Enabled = false });
        }

        // Color depth — default to 8-bit for non-Advanced-Color SDR displays
        string colorText = monitor.BitsPerChannel > 0
            ? $"{monitor.BitsPerChannel}-bit"
            : (monitor.HdrStatus == "N/A" ? "8-bit" : "N/A");
        items.Add(new ToolStripMenuItem($"  Color: {colorText}") { Enabled = false });

        // HDR
        items.Add(new ToolStripMenuItem($"  HDR: {monitor.HdrStatus}") { Enabled = false });

        // Refresh rate — rounded to integer Hz
        string refreshText = monitor.RefreshRateHz > 0
            ? $"{(int)Math.Round(monitor.RefreshRateHz)} Hz"
            : "N/A";
        items.Add(new ToolStripMenuItem($"  Refresh: {refreshText}") { Enabled = false });

        // VRR
        items.Add(new ToolStripMenuItem($"  VRR: {monitor.VrrStatus}") { Enabled = false });

        return items;
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
        _monitor.UpdateInterval(_config.RefreshIntervalMs, _config.ResolutionMode);
        _monitor.Refresh(force: true);
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        if (_runAtStartup.IsEnabled)
            _runAtStartup.Disable();
        else
            _runAtStartup.Enable();

        _startupItem.Checked = _runAtStartup.IsEnabled;
    }

    private ToolStripMenuItem CreateDiagnosticsMenu()
    {
        var diag = new ToolStripMenuItem("Diagnostics");
        diag.DropDownOpening += (_, _) =>
        {
            diag.DropDownItems.Clear();
            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var memMb = proc.WorkingSet64 / (1024.0 * 1024.0);
            var cpuTime = proc.TotalProcessorTime;
            diag.DropDownItems.Add(new ToolStripMenuItem($"Memory: {memMb:F1} MB") { Enabled = false });
            diag.DropDownItems.Add(new ToolStripMenuItem($"CPU time: {cpuTime.TotalSeconds:F2}s") { Enabled = false });
            diag.DropDownItems.Add(new ToolStripMenuItem($"Profile switches: {_profileSwitchCount}") { Enabled = false });
        };
        return diag;
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _monitor.Dispose();
        _notifyIcon.Dispose();
        _iconRenderer.Dispose();
        Application.Exit();
    }
}
