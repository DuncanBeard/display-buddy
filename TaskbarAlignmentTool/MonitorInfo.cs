namespace TaskbarAlignmentTool;

/// <summary>
/// Snapshot of a connected monitor's display properties. The single canonical
/// shape used both by the change-watcher (DisplayMonitor, primary-only) and
/// the all-monitors snapshotter (MonitorInfoProvider).
///
/// Fields the cheap watcher path can't fill (FriendlyName, color depth, HDR,
/// refresh rate, VRR) carry sensible defaults — only MonitorInfoProvider
/// enriches them via the CCD API on demand.
/// </summary>
internal sealed record MonitorInfo(
    string FriendlyName,
    bool IsPrimary,
    int EffectiveWidth,
    int EffectiveHeight,
    int NativeWidth,
    int NativeHeight,
    int ScalingPercent,
    uint BitsPerChannel,
    string HdrStatus,
    double RefreshRateHz,
    string VrrStatus)
{
    /// <summary>
    /// Pure DPI-scaling computation. When dpiX or dpiY is 0, treats the display
    /// as unscaled (effective = native, scaling = 100) — used both as a fallback
    /// when DPI lookup fails and intentionally by callers in
    /// <see cref="ResolutionMode.Physical"/> mode.
    /// </summary>
    public static MonitorInfo Compute(
        int nativeWidth,
        int nativeHeight,
        uint dpiX,
        uint dpiY,
        bool isPrimary = true,
        string friendlyName = "Primary Display")
    {
        if (dpiX == 0 || dpiY == 0)
        {
            return new MonitorInfo(
                friendlyName, isPrimary,
                nativeWidth, nativeHeight, nativeWidth, nativeHeight, 100,
                BitsPerChannel: 0, HdrStatus: "N/A",
                RefreshRateHz: 0, VrrStatus: "N/A");
        }

        int effectiveWidth = (int)Math.Round(nativeWidth * 96.0 / dpiX);
        int effectiveHeight = (int)Math.Round(nativeHeight * 96.0 / dpiY);
        int scalingPercent = (int)Math.Round(dpiX / 96.0 * 100);

        return new MonitorInfo(
            friendlyName, isPrimary,
            effectiveWidth, effectiveHeight, nativeWidth, nativeHeight, scalingPercent,
            BitsPerChannel: 0, HdrStatus: "N/A",
            RefreshRateHz: 0, VrrStatus: "N/A");
    }

    /// <summary>
    /// Cheap-path snapshot of the primary display via Screen.PrimaryScreen +
    /// per-monitor DPI lookup. Used by DisplayMonitor's polling/event loop —
    /// avoids the heavier CCD enumeration.
    ///
    /// In <see cref="ResolutionMode.Physical"/> mode, skips the DPI lookup and
    /// reports the physical resolution as the effective resolution.
    /// </summary>
    public static MonitorInfo ForPrimaryScreen(ResolutionMode mode = ResolutionMode.Effective)
    {
        var screen = Screen.PrimaryScreen;
        if (screen == null)
            return Compute(0, 0, 0, 0);

        int nativeWidth = screen.Bounds.Width;
        int nativeHeight = screen.Bounds.Height;

        if (mode == ResolutionMode.Physical)
            return Compute(nativeWidth, nativeHeight, 0, 0);

        try
        {
            var hMonitor = NativeMethods.MonitorFromPoint(0, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
            if (hMonitor != nint.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
            {
                return Compute(nativeWidth, nativeHeight, dpiX, dpiY);
            }
        }
        catch
        {
            // Fall through to unscaled fallback
        }

        return Compute(nativeWidth, nativeHeight, 0, 0);
    }
}
