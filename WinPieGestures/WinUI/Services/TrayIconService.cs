using System.Runtime.InteropServices;

namespace WinPieGestures.WinUI.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint WmApp = 0x8000;
    private const uint TrayCallbackMessage = WmApp + 42;
    private const uint WmLButtonDblClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;
    private const uint MenuSettings = 1001;
    private const uint MenuExit = 1002;

    private WindowProc? _windowProcedure;
    private nint _window;
    private nint _icon;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public void Start()
    {
        if (_window != 0)
        {
            return;
        }
        _windowProcedure = WindowProcedure;
        string className = $"StarPie.Tray.{Environment.ProcessId}";
        WndClass windowClass = new()
        {
            WindowProcedure = _windowProcedure,
            Instance = GetModuleHandle(null),
            ClassName = className
        };
        if (RegisterClass(ref windowClass) == 0)
        {
            throw new InvalidOperationException($"Unable to register tray window: {Marshal.GetLastWin32Error()}");
        }
        _window = CreateWindowEx(0, className, "StarPie Tray", 0, 0, 0, 0, 0, new nint(-3), 0, windowClass.Instance, 0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"Unable to create tray window: {Marshal.GetLastWin32Error()}");
        }

        string iconPath = Path.Combine(AppContext.BaseDirectory, "app_icon.ico");
        _icon = LoadImage(0, iconPath, ImageIcon, 32, 32, LrLoadFromFile);
        NotifyIconData data = CreateNotifyData();
        if (!ShellNotifyIcon(NimAdd, ref data))
        {
            throw new InvalidOperationException($"Shell_NotifyIcon failed: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Dispose()
    {
        if (_window != 0)
        {
            NotifyIconData data = CreateNotifyData();
            ShellNotifyIcon(NimDelete, ref data);
            DestroyWindow(_window);
            _window = 0;
        }
        if (_icon != 0)
        {
            DestroyIcon(_icon);
            _icon = 0;
        }
        GC.SuppressFinalize(this);
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        if (message == TrayCallbackMessage)
        {
            uint mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage == WmLButtonDblClick)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
                return 0;
            }
            if (mouseMessage == WmRButtonUp)
            {
                ShowContextMenu();
                return 0;
            }
        }
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        nint menu = CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }
        AppendMenu(menu, MfString, MenuSettings, "打开设置");
        AppendMenu(menu, MfSeparator, 0, null);
        AppendMenu(menu, MfString, MenuExit, "退出 StarPie");
        GetCursorPos(out NativePoint point);
        SetForegroundWindow(_window);
        uint command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCommand, point.X, point.Y, 0, _window, 0);
        DestroyMenu(menu);
        if (command == MenuSettings)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (command == MenuExit)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private NotifyIconData CreateNotifyData() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyIconData>(),
        Window = _window,
        Id = 1,
        Flags = NifMessage | NifIcon | NifTip,
        CallbackMessage = TrayCallbackMessage,
        Icon = _icon,
        Tip = "StarPie v2.0.0-preview.2 · WinUI 3 Touch"
    };

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public WindowProc WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid GuidItem;
        public nint BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll", EntryPoint = "RegisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WndClass windowClass);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint exStyle, string className, string name, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint instance, string name, uint type, int width, int height, uint loadFlags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();
    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(nint menu, uint flags, uint id, string? text);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint owner, nint rectangle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);
}
