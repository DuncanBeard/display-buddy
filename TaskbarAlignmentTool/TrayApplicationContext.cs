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
    private readonly ToolStripMenuItem _effectiveResItem;
    private readonly ToolStripMenuItem _nativeResItem;
    private readonly ToolStripMenuItem _profileItem;
    private readonly ToolStripMenuItem _startupItem;

    private static readonly Lazy<bool> _isMsixPackaged = new(DetectMsixPackaged);

    private int _profileSwitchCount;

    public TrayApplicationContext(AppConfig config)
    {
        _config = config;
        _monitor = new DisplayMonitor(config.RefreshIntervalMs, config.ResolutionMode);

        _effectiveResItem = new ToolStripMenuItem("Effective: —") { Enabled = false };
        _nativeResItem = new ToolStripMenuItem("Native: —") { Enabled = false };
        _profileItem = new ToolStripMenuItem("Profile: \u2014") { Enabled = false };
        _startupItem = new ToolStripMenuItem("Run at Startup", null, OnToggleStartup)
        {
            Checked = IsStartupEnabled()
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_effectiveResItem);
        menu.Items.Add(_nativeResItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_profileItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Config", null, OnOpenConfig);
        menu.Items.Add("Reload Config", null, OnReloadConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("Refresh Now", null, OnRefresh);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateDiagnosticsMenu());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Icon = RenderTrayIcon(0),
            ContextMenuStrip = menu,
            Text = "Taskbar Alignment Tool",
            Visible = true
        };

        _monitor.DisplayInfoChanged += OnDisplayInfoChanged;

        // Apply profile immediately on startup
        ApplyForWidth(_monitor.EffectiveWidth);
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfo info)
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
        var displayInfo = _monitor.CurrentDisplayInfo;
        UpdateStatus(displayInfo, profile);
    }

    private void UpdateStatus(DisplayInfo info, ProfileConfig? profile)
    {
        var profileName = profile?.Name ?? "None";
        bool unavailable = info.EffectiveWidth == 0 && info.EffectiveHeight == 0;

        _effectiveResItem.Text = unavailable
            ? "Resolution: unavailable"
            : $"Effective: {info.EffectiveWidth}\u00d7{info.EffectiveHeight} ({info.ScalingPercent}%)";
        _nativeResItem.Text = unavailable
            ? string.Empty
            : $"Native: {info.NativeWidth}\u00d7{info.NativeHeight}";
        _nativeResItem.Visible = !unavailable;
        _profileItem.Text = $"Profile: {profileName}";

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

        // Update tray icon with current width
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = RenderTrayIcon(info.EffectiveWidth);
        oldIcon?.Dispose();
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
        if (IsStartupEnabled())
            DisableStartup();
        else
            EnableStartup();

        _startupItem.Checked = IsStartupEnabled();
    }

    private static bool IsMsixPackaged() => _isMsixPackaged.Value;

    private static bool DetectMsixPackaged()
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
        Application.Exit();
    }

    /// <summary>
    /// Renders a DPI-aware tray icon showing the current display width.
    /// Auto-detects Windows light/dark theme for contrast.
    /// </summary>
    private static Icon RenderTrayIcon(int width)
    {
        int dpi = GetSystemDpi();
        int size = (int)(16 * dpi / 96.0);
        float scale = size / 16f;
        bool isDarkTheme = IsSystemDarkTheme();

        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Background color adapts to theme
        var bgColor = isDarkTheme
            ? Color.FromArgb(255, 0, 150, 180)   // Teal on dark taskbar
            : Color.FromArgb(255, 0, 120, 150);  // Darker teal on light taskbar
        var textBrush = isDarkTheme ? Brushes.White : Brushes.White;

        using var bgBrush = new SolidBrush(bgColor);
        g.FillRectangle(bgBrush, 0, 0, size, size);

        // Display width as the full icon content
        var label = width.ToString();
        float fontSize = label.Length <= 3 ? 7f : label.Length == 4 ? 5.5f : 4.5f;
        using var font = new Font("Segoe UI", fontSize * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        var textRect = new RectangleF(0, 0, size, size);
        g.DrawString(label, font, textBrush, textRect, sf);

        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// Checks if Windows is using dark mode for apps.
    /// Registry: HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme
    /// 0 = dark, 1 = light
    /// </summary>
    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            var value = key?.GetValue("SystemUsesLightTheme");
            if (value is int intVal)
                return intVal == 0;
        }
        catch { }
        return true; // Default to dark
    }

    private static int GetSystemDpi()
    {
        try
        {
            var hMonitor = NativeMethods.MonitorFromPoint(0, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (hMonitor != nint.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
                dpiX > 0)
            {
                return (int)dpiX;
            }
        }
        catch { }
        return 96;
    }
}
