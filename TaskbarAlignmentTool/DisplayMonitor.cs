using Microsoft.Win32;

namespace TaskbarAlignmentTool;

/// <summary>
/// Watches the primary display for resolution / DPI changes via Windows
/// messages (WM_DISPLAYCHANGE, WM_DPICHANGED), the SystemEvents callback,
/// and a low-frequency fallback timer. Publishes the new <see cref="MonitorInfo"/>
/// snapshot whenever the primary display's state changes.
/// </summary>
internal sealed class DisplayMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _fallbackTimer;
    private readonly DisplayChangeWindow _messageWindow;
    private MonitorInfo _lastDisplay;
    private ResolutionMode _resolutionMode;

    public MonitorInfo PrimaryDisplay => MonitorInfo.ForPrimaryScreen(_resolutionMode);

    public event EventHandler<MonitorInfo>? PrimaryDisplayChanged;

    public DisplayMonitor(int fallbackIntervalMs = 60000, ResolutionMode resolutionMode = ResolutionMode.Effective)
    {
        _resolutionMode = resolutionMode;
        _lastDisplay = PrimaryDisplay;

        // Hidden window to receive WM_DISPLAYCHANGE and WM_DPICHANGED
        _messageWindow = new DisplayChangeWindow(CheckAndNotify);

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

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => CheckAndNotify();

    private void OnFallbackTick(object? sender, EventArgs e) => CheckAndNotify();

    private void CheckAndNotify()
    {
        var current = PrimaryDisplay;
        if (current != _lastDisplay)
        {
            _lastDisplay = current;
            PrimaryDisplayChanged?.Invoke(this, current);
        }
    }

    /// <summary>Forces a re-check and fires the event if the display changed (or always if force is true).</summary>
    public void Refresh(bool force = false)
    {
        var current = PrimaryDisplay;
        if (force || current != _lastDisplay)
        {
            _lastDisplay = current;
            PrimaryDisplayChanged?.Invoke(this, current);
        }
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
