using Microsoft.Win32;

namespace TaskbarAlignmentTool;

/// <summary>
/// Renders and owns the lifecycle of the system-tray icon. The icon shows
/// the current display width as text on a teal background, sized to the
/// system DPI and tinted for the current Windows light/dark theme.
///
/// Tracks the underlying HICON of whichever <see cref="Icon"/> is currently
/// installed in the target <see cref="NotifyIcon"/> and explicitly calls
/// <c>DestroyIcon</c> when it's superseded — <see cref="Icon.FromHandle"/>
/// does not transfer HICON ownership and <see cref="Icon.Dispose"/> alone
/// would leak one HICON per render.
/// </summary>
internal sealed class TrayIconRenderer : IDisposable
{
    private nint _currentHicon;

    /// <summary>
    /// Renders a fresh icon for <paramref name="width"/> and installs it on
    /// <paramref name="target"/>, then frees the HICON the renderer
    /// previously installed (if any). Safe to call repeatedly.
    /// </summary>
    public void UpdateIcon(NotifyIcon target, int width)
    {
        var (icon, newHicon) = BuildIcon(width);
        var oldHicon = _currentHicon;

        // Assign first so NotifyIcon switches to the new HICON before we free the old one
        target.Icon = icon;
        _currentHicon = newHicon;

        if (oldHicon != 0)
            NativeMethods.DestroyIcon(oldHicon);
    }

    public void Dispose()
    {
        if (_currentHicon != 0)
        {
            NativeMethods.DestroyIcon(_currentHicon);
            _currentHicon = 0;
        }
    }

    private static (Icon icon, nint hicon) BuildIcon(int width)
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

        var bgColor = isDarkTheme
            ? Color.FromArgb(255, 0, 150, 180)   // Teal on dark taskbar
            : Color.FromArgb(255, 0, 120, 150);  // Darker teal on light taskbar

        using var bgBrush = new SolidBrush(bgColor);
        g.FillRectangle(bgBrush, 0, 0, size, size);

        var label = width.ToString();
        float fontSize = label.Length <= 3 ? 7f : label.Length == 4 ? 5.5f : 4.5f;
        using var font = new Font("Segoe UI", fontSize * scale, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(label, font, Brushes.White, new RectangleF(0, 0, size, size), sf);

        nint hicon = bmp.GetHicon();
        return (Icon.FromHandle(hicon), hicon);
    }

    /// <summary>
    /// Reads HKCU\...\Themes\Personalize\SystemUsesLightTheme.
    /// 0 = dark, 1 = light. Defaults to dark when the registry probe fails.
    /// </summary>
    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
            var value = key?.GetValue("SystemUsesLightTheme");
            if (value is int intVal)
                return intVal == 0;
        }
        catch { }
        return true;
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
