using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BrowserFork;

static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, int[]? ids, uint count, uint flags);

    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder name, ref uint size);
    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationProcess(IntPtr h, int cls, ref ProcessBasicInformation info, int size, out int returned);
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(int eventId, uint flags, IntPtr a, IntPtr b);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

    public static bool IsShiftDown() => (GetAsyncKeyState(0x10 /* VK_SHIFT */) & 0x8000) != 0;

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int AssocQueryString(int flags, int str, string assoc, string? extra, StringBuilder? output, ref uint length);

    /// <summary>
    /// The ProgID Windows actually uses for a protocol such as "https". Reading UserChoice directly isn't enough:
    /// Windows 11 24H2 moved the effective choice to UserChoiceLatest and can leave the old key stale.
    /// </summary>
    public static string? DefaultProgId(string protocol)
    {
        const int IsProtocol = 0x1000, ProgId = 20;
        var sb = new StringBuilder(256);
        uint len = (uint)sb.Capacity;
        return AssocQueryString(IsProtocol, ProgId, protocol, null, sb, ref len) == 0 ? sb.ToString() : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessBasicInformation
    {
        public IntPtr ExitStatus, PebBaseAddress, AffinityMask, BasePriority, UniqueProcessId, InheritedFromUniqueProcessId;
    }

    const uint ProcessQueryLimitedInformation = 0x1000;

    public static uint? GetParentPid(IntPtr processHandle)
    {
        var info = new ProcessBasicInformation();
        return NtQueryInformationProcess(processHandle, 0, ref info, Marshal.SizeOf(info), out _) == 0
            ? (uint)info.InheritedFromUniqueProcessId
            : null;
    }

    public static string? GetProcessPath(uint pid)
    {
        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Extracts a crisp icon of the requested pixel size from an exe/dll/ico.</summary>
    public static ImageSource? ExtractIcon(string file, int index, int size)
    {
        try
        {
            var icons = new IntPtr[1];
            if (PrivateExtractIcons(file, index, size, size, icons, null, 1, 0) == 0 || icons[0] == IntPtr.Zero)
            {
                if (index == 0 || PrivateExtractIcons(file, 0, size, size, icons, null, 1, 0) == 0 || icons[0] == IntPtr.Zero)
                    return null;
            }
            try
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(icons[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            finally { DestroyIcon(icons[0]); }
        }
        catch { return null; }
    }

    /// <summary>Takes foreground even when Windows' foreground lock would refuse it.</summary>
    public static void ForceForeground(IntPtr hwnd)
    {
        var fg = GetForegroundWindow();
        if (fg == hwnd) return;
        uint fgThread = GetWindowThreadProcessId(fg, out _);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(me, fgThread, false);
        }
    }
}
