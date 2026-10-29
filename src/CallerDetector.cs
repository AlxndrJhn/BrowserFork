using System.Diagnostics;
using System.IO;
using System.Windows.Media;

namespace BrowserFork;

/// <summary>The app that (most likely) opened the link.</summary>
sealed record CallerInfo(string Key, string DisplayName, string Path, ImageSource? Icon);

static class CallerDetector
{
    // Processes that only relay a launch on behalf of someone else; never the real caller.
    static readonly HashSet<string> Brokers = new(StringComparer.OrdinalIgnoreCase)
    {
        "svchost.exe", "sihost.exe", "runtimebroker.exe", "openwith.exe", "dllhost.exe",
        "applicationframehost.exe", "launchwinapp.exe", "browserfork.exe", "services.exe",
        "wininit.exe", "conhost.exe", "rundll32.exe",
    };

    static readonly HashSet<string> Shell = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "searchhost.exe", "startmenuexperiencehost.exe", "shellexperiencehost.exe",
    };

    /// <param name="foregroundAtStart">Foreground window captured as early as possible at startup.</param>
    public static CallerInfo? Detect(IntPtr foregroundAtStart)
    {
        string? parentPath = null;
        try
        {
            using var me = Process.GetCurrentProcess();
            if (Native.GetParentPid(me.Handle) is uint ppid && ppid != 0)
                parentPath = Native.GetProcessPath(ppid);
        }
        catch { }

        string? fgPath = null;
        if (foregroundAtStart != IntPtr.Zero)
        {
            Native.GetWindowThreadProcessId(foregroundAtStart, out uint fgPid);
            if (fgPid != 0 && fgPid != Environment.ProcessId)
                fgPath = Native.GetProcessPath(fgPid);
        }

        // Direct child of a real app: that's the caller.
        if (IsRealApp(parentPath)) return Describe(parentPath!);
        // Launched via a broker or the shell: the window the user was in is the best guess.
        if (IsRealApp(fgPath)) return Describe(fgPath!);
        // Clicked something in Explorer/Start/Search.
        if (parentPath != null && Shell.Contains(Path.GetFileName(parentPath))) return Describe(parentPath);
        if (fgPath != null && Shell.Contains(Path.GetFileName(fgPath))) return Describe(fgPath);
        return null;
    }

    static bool IsRealApp(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var name = Path.GetFileName(path);
        return !Brokers.Contains(name) && !Shell.Contains(name);
    }

    static CallerInfo Describe(string path)
    {
        var exe = Path.GetFileName(path);
        string display = Path.GetFileNameWithoutExtension(path);
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrWhiteSpace(vi.FileDescription)) display = vi.FileDescription.Trim();
            else if (!string.IsNullOrWhiteSpace(vi.ProductName)) display = vi.ProductName.Trim();
        }
        catch { }
        if (exe.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)) display = "Windows";
        return new CallerInfo(exe.ToLowerInvariant(), display, path, Native.ExtractIcon(path, 0, 32));
    }
}
