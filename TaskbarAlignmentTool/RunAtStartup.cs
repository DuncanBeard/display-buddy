using Microsoft.Win32;

namespace TaskbarAlignmentTool;

/// <summary>
/// Toggle for "launch this app on user login." Two adapters live behind this
/// seam — one for MSIX-packaged builds (StartupTask API), one for portable
/// builds (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). Pick the
/// right one at construction time via <see cref="RunAtStartup.Create"/>.
///
/// Both Enable and Disable swallow underlying-API failures silently — the
/// caller is expected to re-query <see cref="IsEnabled"/> to reflect the
/// real state in the UI rather than assume the toggle succeeded.
/// </summary>
internal interface IRunAtStartup
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

/// <summary>
/// Constructs the right <see cref="IRunAtStartup"/> adapter for the current
/// process — MSIX vs non-packaged is detected once via a probe of
/// <c>Windows.ApplicationModel.Package.Current</c>.
/// </summary>
internal static class RunAtStartup
{
    public static IRunAtStartup Create()
    {
        return IsMsixPackaged()
            ? new MsixRunAtStartup()
            : new RegistryRunAtStartup();
    }

    private static bool IsMsixPackaged()
    {
        try
        {
            // Package.Current throws InvalidOperationException when not packaged
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// MSIX adapter — uses <c>Windows.ApplicationModel.StartupTask</c> with the
/// task ID declared in <c>Package.appxmanifest</c>. Async APIs are unwrapped
/// synchronously; menu-handler callers are on the UI thread.
/// </summary>
internal sealed class MsixRunAtStartup : IRunAtStartup
{
    private const string TaskId = "TaskbarAlignmentToolStartup";

    public bool IsEnabled
    {
        get
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask
                    .GetAsync(TaskId).GetAwaiter().GetResult();
                return task.State == Windows.ApplicationModel.StartupTaskState.Enabled;
            }
            catch
            {
                return false;
            }
        }
    }

    public void Enable()
    {
        try
        {
            var task = Windows.ApplicationModel.StartupTask
                .GetAsync(TaskId).GetAwaiter().GetResult();
            task.RequestEnableAsync().GetAwaiter().GetResult();
        }
        catch { /* Startup task not available */ }
    }

    public void Disable()
    {
        try
        {
            var task = Windows.ApplicationModel.StartupTask
                .GetAsync(TaskId).GetAwaiter().GetResult();
            task.Disable();
        }
        catch { /* Startup task not available */ }
    }
}

/// <summary>
/// Portable / non-packaged adapter — writes the executable path to
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// </summary>
internal sealed class RegistryRunAtStartup : IRunAtStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TaskbarAlignmentTool";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        var exePath = Application.ExecutablePath;
        key?.SetValue(ValueName, $"\"{exePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(ValueName, false);
    }
}
