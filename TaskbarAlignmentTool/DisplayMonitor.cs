namespace TaskbarAlignmentTool;

/// <summary>
/// Monitors the primary display's effective resolution and raises an event when it changes.
/// </summary>
internal sealed class DisplayMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private int _lastWidth;

    public int EffectiveWidth => GetEffectiveWidth();

    private static int GetEffectiveWidth()
    {
        var screen = Screen.PrimaryScreen;
        if (screen == null) return 0;

        int physicalWidth = screen.Bounds.Width;

        // Get the DPI of the primary monitor to compute effective (logical) resolution
        try
        {
            // POINT(0,0) → primary monitor
            var hMonitor = NativeMethods.MonitorFromPoint(0, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (hMonitor != nint.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
                dpiX > 0)
            {
                // effective = physical * 96 / dpi (96 DPI = 100% scaling)
                return (int)(physicalWidth * 96.0 / dpiX);
            }
        }
        catch
        {
            // Fall through to physical width
        }

        return physicalWidth;
    }

    public event EventHandler<int>? ResolutionChanged;

    public DisplayMonitor(int pollIntervalMs = 5000)
    {
        _lastWidth = EffectiveWidth;

        _timer = new System.Windows.Forms.Timer { Interval = pollIntervalMs };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>Updates the polling interval at runtime.</summary>
    public void UpdateInterval(int intervalMs)
    {
        if (intervalMs < 500) intervalMs = 500;
        _timer.Interval = intervalMs;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var current = EffectiveWidth;
        if (current != _lastWidth)
        {
            _lastWidth = current;
            ResolutionChanged?.Invoke(this, current);
        }
    }

    /// <summary>Forces a re-check and fires the event if the width changed (or always if force is true).</summary>
    public void Refresh(bool force = false)
    {
        var current = EffectiveWidth;
        if (force || current != _lastWidth)
        {
            _lastWidth = current;
            ResolutionChanged?.Invoke(this, current);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
