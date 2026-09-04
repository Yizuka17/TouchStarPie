using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures.WinUI.Input;

/// <summary>Fast one-shot foreground-scene gate evaluated only when a gesture begins.</summary>
internal static class ScenePolicy
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint GaRoot = 2;

    public static bool IsGestureAllowed(AppConfig config)
    {
        nint foreground = GetForegroundWindow();
        if (foreground == 0)
        {
            return true;
        }
        foreground = GetAncestor(foreground, GaRoot);
        string processName = GetProcessName(foreground);
        bool whitelisted = ContainsProcess(config.WhitelistedProcesses, processName);
        if (whitelisted)
        {
            return true;
        }

        if (string.Equals(config.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (ContainsProcess(config.BlacklistedProcesses, processName))
        {
            return false;
        }
        if (config.DisableOnCtrl && IsKeyDown(0x11) ||
            config.DisableOnShift && IsKeyDown(0x10) ||
            config.DisableOnAlt && IsKeyDown(0x12))
        {
            return false;
        }
        return !config.DisableOnFullScreen || !IsExclusiveFullscreen(foreground);
    }

    private static string GetProcessName(nint window)
    {
        GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0)
        {
            return string.Empty;
        }
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ContainsProcess(IEnumerable<string> entries, string processName)
    {
        string normalized = NormalizeProcess(processName);
        return entries.Any(entry => NormalizeProcess(entry).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeProcess(string value)
    {
        string name = Path.GetFileName(value.Trim());
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
    }

    private static bool IsExclusiveFullscreen(nint window)
    {
        StringBuilder className = new(128);
        GetClassName(window, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return false;
        }
        if (IsIconic(window) || !GetWindowRect(window, out NativeRect windowRect))
        {
            return false;
        }
        nint monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        MonitorInfo info = new() { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info))
        {
            return false;
        }
        const int tolerance = 2;
        return windowRect.Left <= info.Monitor.Left + tolerance &&
               windowRect.Top <= info.Monitor.Top + tolerance &&
               windowRect.Right >= info.Monitor.Right - tolerance &&
               windowRect.Bottom >= info.Monitor.Bottom - tolerance;
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
