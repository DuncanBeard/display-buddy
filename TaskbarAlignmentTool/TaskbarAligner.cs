using Microsoft.Win32;

namespace TaskbarAlignmentTool;

/// <summary>
/// Reads and writes Windows 11 taskbar registry keys and notifies Explorer of changes.
/// </summary>
internal static class TaskbarAligner
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    // Windows 11 23H2+ (Build 22631+) inverted the TaskbarGlomLevel values:
    //   Old: 0=Always, 1=WhenFull, 2=Never
    //   New: 0=Never, 1=WhenFull, 2=Always
    private static readonly bool _invertCombine = Environment.OSVersion.Version.Build >= 22631;

    /// <summary>
    /// Applies all settings from a profile to the registry and notifies Explorer.
    /// Returns true if any value was actually changed.
    /// </summary>
    public static bool ApplyProfile(ProfileConfig profile)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
        if (key == null)
            return false;

        bool changed = false;
        changed |= SetIfDifferent(key, "TaskbarAl", (int)profile.Alignment);
        changed |= SetIfDifferent(key, "TaskbarGlomLevel", MapCombineValue(profile.CombineButtons));
        changed |= SetIfDifferent(key, "TaskbarSi", (int)profile.TaskbarSize);

        if (changed)
            NotifyExplorer();

        return changed;
    }

    /// <summary>
    /// Maps the logical CombineButtons value to the correct registry value,
    /// accounting for the inverted mapping on Windows 11 23H2+.
    /// </summary>
    private static int MapCombineValue(CombineButtonsOption option)
    {
        if (!_invertCombine)
            return (int)option; // Old mapping: Always=0, WhenFull=1, Never=2

        // New mapping (23H2+): Never=0, WhenFull=1, Always=2
        return option switch
        {
            CombineButtonsOption.Always => 2,
            CombineButtonsOption.WhenFull => 1,
            CombineButtonsOption.Never => 0,
            _ => (int)option
        };
    }

    private static bool SetIfDifferent(RegistryKey key, string valueName, int desired)
    {
        var current = key.GetValue(valueName);
        if (current is int intVal && intVal == desired)
            return false;

        key.SetValue(valueName, desired, RegistryValueKind.DWord);
        return true;
    }

    private static void NotifyExplorer()
    {
        NativeMethods.SendMessageTimeoutW(
            (nint)NativeMethods.HWND_BROADCAST,
            NativeMethods.WM_SETTINGCHANGE,
            0,
            null,
            NativeMethods.SMTO_ABORTIFHUNG,
            1000,
            out _);

        NativeMethods.SHChangeNotify(
            NativeMethods.SHCNE_ASSOCCHANGED,
            NativeMethods.SHCNF_IDLIST,
            nint.Zero,
            nint.Zero);
    }
}
