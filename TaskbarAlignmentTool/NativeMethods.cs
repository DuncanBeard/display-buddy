using System.Runtime.InteropServices;

namespace TaskbarAlignmentTool;

internal static partial class NativeMethods
{
    public const int HWND_BROADCAST = 0xFFFF;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int SMTO_ABORTIFHUNG = 0x0002;

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint SendMessageTimeoutW(
        nint hWnd,
        uint msg,
        nuint wParam,
        string? lParam,
        uint fuFlags,
        uint uTimeout,
        out nuint lpdwResult);

    [LibraryImport("shell32.dll")]
    public static partial void SHChangeNotify(
        int wEventId,
        uint uFlags,
        nint dwItem1,
        nint dwItem2);

    // SHCNE_ASSOCCHANGED triggers Explorer to refresh
    public const int SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_IDLIST = 0x0000;

    // Monitor DPI
    public const int MDT_EFFECTIVE_DPI = 0;

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(
        nint hmonitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(long pt, uint dwFlags);

    public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
}
