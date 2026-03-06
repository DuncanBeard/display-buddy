using Microsoft.Win32;

namespace TaskbarAlignmentTool;

/// <summary>
/// Snapshot of the primary display's resolution and scaling state.
/// Recomputed on each poll cycle — not persisted.
/// </summary>
internal sealed record DisplayInfo(
    int EffectiveWidth,
    int EffectiveHeight,
    int NativeWidth,
    int NativeHeight,
    int ScalingPercent);

/// <summary>
/// Monitors the primary display's effective resolution using Windows messages
/// (WM_DISPLAYCHANGE, WM_DPICHANGED) and a low-frequency fallback timer.
/// </summary>
internal sealed class DisplayMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _fallbackTimer;
    private readonly DisplayChangeWindow _messageWindow;
    private int _lastWidth;
    private DisplayInfo _lastDisplayInfo;
    private ResolutionMode _resolutionMode;

    public int EffectiveWidth => GetWidth(_resolutionMode);

    public DisplayInfo CurrentDisplayInfo => GetDisplayInfo(_resolutionMode);

    public event EventHandler<int>? ResolutionChanged;
    public event EventHandler<DisplayInfo>? DisplayInfoChanged;

    public DisplayMonitor(int fallbackIntervalMs = 60000, ResolutionMode resolutionMode = ResolutionMode.Effective)
    {
        _resolutionMode = resolutionMode;
        _lastWidth = EffectiveWidth;
        _lastDisplayInfo = CurrentDisplayInfo;

        // Hidden window to receive WM_DISPLAYCHANGE and WM_DPICHANGED
        _messageWindow = new DisplayChangeWindow(OnDisplayMessage);

        // Also subscribe to the .NET event for display settings changes
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // Low-frequency fallback timer (default 60s) as a safety net
        _fallbackTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(fallbackIntervalMs, 5000)
        };
        _fallbackTimer.Tick += OnFallbackTick;
        _fallbackTimer.Start();
    }

    /// <summary>Updates the fallback timer interval and resolution mode at runtime.</summary>
    public void UpdateInterval(int intervalMs, ResolutionMode? resolutionMode = null)
    {
        _fallbackTimer.Interval = Math.Max(intervalMs, 5000);
        if (resolutionMode.HasValue)
            _resolutionMode = resolutionMode.Value;
    }

    private void OnDisplayMessage()
    {
        CheckAndNotify();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        CheckAndNotify();
    }

    private void OnFallbackTick(object? sender, EventArgs e)
    {
        CheckAndNotify();
    }

    private void CheckAndNotify()
    {
        var current = EffectiveWidth;
        var currentInfo = CurrentDisplayInfo;
        if (current != _lastWidth || currentInfo != _lastDisplayInfo)
        {
            _lastWidth = current;
            _lastDisplayInfo = currentInfo;
            ResolutionChanged?.Invoke(this, current);
            DisplayInfoChanged?.Invoke(this, currentInfo);
        }
    }

    /// <summary>Forces a re-check and fires the event if the width changed (or always if force is true).</summary>
    public void Refresh(bool force = false)
    {
        var current = EffectiveWidth;
        var currentInfo = CurrentDisplayInfo;
        if (force || current != _lastWidth || currentInfo != _lastDisplayInfo)
        {
            _lastWidth = current;
            _lastDisplayInfo = currentInfo;
            ResolutionChanged?.Invoke(this, current);
            DisplayInfoChanged?.Invoke(this, currentInfo);
        }
    }

    private static int GetWidth(ResolutionMode mode)
    {
        var screen = Screen.PrimaryScreen;
        if (screen == null) return 0;

        int physicalWidth = screen.Bounds.Width;

        if (mode == ResolutionMode.Physical)
            return physicalWidth;

        try
        {
            var hMonitor = NativeMethods.MonitorFromPoint(0, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (hMonitor != nint.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 &&
                dpiX > 0)
            {
                return (int)(physicalWidth * 96.0 / dpiX);
            }
        }
        catch
        {
            // Fall through to physical width
        }

        return physicalWidth;
    }

    private static DisplayInfo GetDisplayInfo(ResolutionMode mode)
    {
        var screen = Screen.PrimaryScreen;
        if (screen == null)
            return new DisplayInfo(0, 0, 0, 0, 100);

        int nativeWidth = screen.Bounds.Width;
        int nativeHeight = screen.Bounds.Height;

        if (mode == ResolutionMode.Physical)
            return new DisplayInfo(nativeWidth, nativeHeight, nativeWidth, nativeHeight, 100);

        try
        {
            var hMonitor = NativeMethods.MonitorFromPoint(0, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (hMonitor != nint.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 &&
                dpiX > 0 && dpiY > 0)
            {
                int effectiveWidth = (int)Math.Round(nativeWidth * 96.0 / dpiX);
                int effectiveHeight = (int)Math.Round(nativeHeight * 96.0 / dpiY);
                int scalingPercent = (int)Math.Round(dpiX / 96.0 * 100);
                return new DisplayInfo(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight, scalingPercent);
            }
        }
        catch
        {
            // Fall through to physical = effective
        }

        return new DisplayInfo(nativeWidth, nativeHeight, nativeWidth, nativeHeight, 100);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _fallbackTimer.Stop();
        _fallbackTimer.Dispose();
        _messageWindow.DestroyHandle();
    }

    /// <summary>
    /// Hidden NativeWindow that receives WM_DISPLAYCHANGE and WM_DPICHANGED messages.
    /// </summary>
    private sealed class DisplayChangeWindow : NativeWindow
    {
        private readonly Action _onChange;

        public DisplayChangeWindow(Action onChange)
        {
            _onChange = onChange;
            var cp = new CreateParams
            {
                Caption = "TaskbarAlignmentTool_DisplayMonitor",
                // Message-only window (HWND_MESSAGE parent)
                Parent = new nint(-3)
            };
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg is NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_DPICHANGED)
            {
                _onChange();
            }
            base.WndProc(ref m);
        }
    }
}
