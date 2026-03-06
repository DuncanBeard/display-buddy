using System.Runtime.InteropServices;

namespace TaskbarAlignmentTool;

/// <summary>
/// Snapshot of a connected monitor's display properties.
/// Rebuilt on each context menu open — not persisted.
/// </summary>
internal sealed record MonitorDisplayInfo(
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
    string VrrStatus);

/// <summary>
/// Enumerates all connected monitors and their display properties in a single
/// pass using the CCD (Connecting and Configuring Displays) API family.
/// </summary>
internal static class MonitorInfoProvider
{
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    /// <summary>
    /// Returns display info for all connected monitors.
    /// Primary monitor is first, remaining sorted by Windows display number.
    /// Returns an empty list if enumeration fails.
    /// </summary>
    public static List<MonitorDisplayInfo> GetAllMonitors()
    {
        try
        {
            var (paths, _) = GetActiveDisplayConfig();
            var entries = new List<(string gdiName, MonitorDisplayInfo info)>();
            var seenTargets = new HashSet<(uint low, int high, uint id)>();

            foreach (var path in paths)
            {
                if ((path.flags & 0x1) == 0) continue; // Skip inactive paths

                var targetKey = (path.targetInfo.adapterId.LowPart, path.targetInfo.adapterId.HighPart, path.targetInfo.id);
                if (!seenTargets.Add(targetKey)) continue; // Skip duplicate targets

                var (gdiDeviceName, isPrimary) = GetSourceInfo(path);
                var friendlyName = GetMonitorFriendlyName(path.targetInfo.adapterId, path.targetInfo.id);
                var (bitsPerChannel, hdrStatus) = GetColorAndHdrInfo(path.targetInfo.adapterId, path.targetInfo.id);
                var refreshRate = GetRefreshRate(path);
                var vrrStatus = GetVrrStatus(path.targetInfo.adapterId, path.targetInfo.id);
                var (effW, effH, natW, natH, scalePct) = GetResolutionAndScaling(gdiDeviceName);

                entries.Add((gdiDeviceName, new MonitorDisplayInfo(
                    friendlyName, isPrimary,
                    effW, effH, natW, natH, scalePct,
                    bitsPerChannel, hdrStatus, refreshRate, vrrStatus)));
            }

            // Sort: primary first, then by Windows display number
            entries.Sort((a, b) =>
            {
                if (a.info.IsPrimary != b.info.IsPrimary)
                    return a.info.IsPrimary ? -1 : 1;
                return GetDisplayNumber(a.gdiName).CompareTo(GetDisplayNumber(b.gdiName));
            });

            // Assign fallback "Display N" names where EDID name is unavailable
            var result = new List<MonitorDisplayInfo>(entries.Count);
            foreach (var (gdiName, info) in entries)
            {
                if (info.FriendlyName == "Unknown Display")
                {
                    int num = GetDisplayNumber(gdiName);
                    result.Add(info with { FriendlyName = $"Display {num}" });
                }
                else
                {
                    result.Add(info);
                }
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private static (NativeMethods.DISPLAYCONFIG_PATH_INFO[] paths, NativeMethods.DISPLAYCONFIG_MODE_INFO[] modes)
        GetActiveDisplayConfig()
    {
        uint flags = NativeMethods.QDC_ONLY_ACTIVE_PATHS | NativeMethods.QDC_VIRTUAL_MODE_AWARE;
        NativeMethods.DISPLAYCONFIG_PATH_INFO[] paths;
        NativeMethods.DISPLAYCONFIG_MODE_INFO[] modes;
        int result;

        do
        {
            result = NativeMethods.GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
            if (result != ERROR_SUCCESS)
                return ([], []);

            paths = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            modes = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];

            result = NativeMethods.QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, nint.Zero);
            if (result == ERROR_SUCCESS)
            {
                Array.Resize(ref paths, (int)pathCount);
                Array.Resize(ref modes, (int)modeCount);
            }
        }
        while (result == ERROR_INSUFFICIENT_BUFFER);

        if (result != ERROR_SUCCESS)
            return ([], []);

        return (paths, modes);
    }

    private static string GetMonitorFriendlyName(NativeMethods.LUID adapterId, uint targetId)
    {
        var deviceName = new NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME();
        deviceName.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_TARGET_NAME;
        deviceName.header.size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        deviceName.header.adapterId = adapterId;
        deviceName.header.id = targetId;

        int result = NativeMethods.DisplayConfigGetDeviceInfo(ref deviceName);
        if (result != ERROR_SUCCESS)
            return "Unknown Display";

        if (deviceName.flags.FriendlyNameFromEdid &&
            !string.IsNullOrWhiteSpace(deviceName.monitorFriendlyDeviceName))
        {
            return deviceName.monitorFriendlyDeviceName;
        }

        return "Unknown Display";
    }

    private static (string gdiDeviceName, bool isPrimary) GetSourceInfo(NativeMethods.DISPLAYCONFIG_PATH_INFO path)
    {
        var sourceName = new NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
        sourceName.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_SOURCE_NAME;
        sourceName.header.size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
        sourceName.header.adapterId = path.sourceInfo.adapterId;
        sourceName.header.id = path.sourceInfo.id;

        int result = NativeMethods.DisplayConfigGetDeviceInfo(ref sourceName);
        if (result != ERROR_SUCCESS)
            return ("", false);

        string gdiName = sourceName.viewGdiDeviceName ?? "";
        bool isPrimary = Screen.AllScreens.Any(s =>
            s.Primary && string.Equals(s.DeviceName, gdiName, StringComparison.OrdinalIgnoreCase));

        return (gdiName, isPrimary);
    }

    private static (uint bitsPerChannel, string hdrStatus) GetColorAndHdrInfo(
        NativeMethods.LUID adapterId, uint targetId)
    {
        var info = new NativeMethods.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
        info.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_ADVANCED_COLOR_INFO;
        info.header.size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        info.header.adapterId = adapterId;
        info.header.id = targetId;

        int result = NativeMethods.DisplayConfigGetDeviceInfo(ref info);
        if (result != ERROR_SUCCESS)
            return (0, "N/A");

        string hdrStatus = !info.AdvancedColorSupported ? "N/A"
            : info.AdvancedColorEnabled ? "On" : "Off";

        return (info.bitsPerColorChannel, hdrStatus);
    }

    private static double GetRefreshRate(NativeMethods.DISPLAYCONFIG_PATH_INFO path)
    {
        var rate = path.targetInfo.refreshRate;
        if (rate.Denominator == 0) return 0;
        return (double)rate.Numerator / rate.Denominator;
    }

    // Uses DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION (Win11+) as an OS-level VRR proxy.
    // Pre-Win11 gracefully returns "N/A".
    private static string GetVrrStatus(NativeMethods.LUID adapterId, uint targetId)
    {
        var info = new NativeMethods.DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION();
        info.header.type = NativeMethods.DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_MONITOR_SPECIALIZATION;
        info.header.size = (uint)Marshal.SizeOf<NativeMethods.DISPLAYCONFIG_GET_MONITOR_SPECIALIZATION>();
        info.header.adapterId = adapterId;
        info.header.id = targetId;

        int result = NativeMethods.DisplayConfigGetDeviceInfo(ref info);
        if (result != ERROR_SUCCESS)
            return "N/A";

        if (!info.IsSpecializationAvailableForMonitor)
            return "N/A";

        return info.IsSpecializationEnabled ? "On" : "Off";
    }

    private static (int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight, int scalingPercent)
        GetResolutionAndScaling(string gdiDeviceName)
    {
        var screen = Screen.AllScreens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, gdiDeviceName, StringComparison.OrdinalIgnoreCase));
        if (screen == null)
            return (0, 0, 0, 0, 100);

        int nativeWidth = screen.Bounds.Width;
        int nativeHeight = screen.Bounds.Height;

        try
        {
            // Pack POINT(x, y) into a long for MonitorFromPoint
            long packedPoint = (screen.Bounds.X & 0xFFFFFFFFL) | ((long)screen.Bounds.Y << 32);
            var hMonitor = NativeMethods.MonitorFromPoint(packedPoint, NativeMethods.MONITOR_DEFAULTTONEAREST);

            if (hMonitor != nint.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 &&
                dpiX > 0 && dpiY > 0)
            {
                int effectiveWidth = (int)Math.Round(nativeWidth * 96.0 / dpiX);
                int effectiveHeight = (int)Math.Round(nativeHeight * 96.0 / dpiY);
                int scalingPercent = (int)Math.Round(dpiX / 96.0 * 100);
                return (effectiveWidth, effectiveHeight, nativeWidth, nativeHeight, scalingPercent);
            }
        }
        catch
        {
            // Fall through to physical = effective
        }

        return (nativeWidth, nativeHeight, nativeWidth, nativeHeight, 100);
    }

    private static int GetDisplayNumber(string gdiDeviceName)
    {
        // \\.\DISPLAY1 → 1, \\.\DISPLAY2 → 2, etc.
        const string prefix = @"\\.\DISPLAY";
        if (gdiDeviceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(gdiDeviceName.AsSpan(prefix.Length), out int num))
        {
            return num;
        }
        return int.MaxValue;
    }
}
