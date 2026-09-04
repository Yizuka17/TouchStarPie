using System.Runtime.InteropServices;

namespace WinPieGestures.WinUI.Input;

internal static class NativeTouchMethods
{
    internal const uint WM_POINTERUPDATE = 0x0245;
    internal const uint WM_POINTERDOWN = 0x0246;
    internal const uint WM_POINTERUP = 0x0247;
    internal const uint WM_POINTERCAPTURECHANGED = 0x024C;

    internal const uint PT_TOUCH = 0x00000002;

    internal const uint POINTER_FLAG_NEW = 0x00000001;
    internal const uint POINTER_FLAG_INRANGE = 0x00000002;
    internal const uint POINTER_FLAG_INCONTACT = 0x00000004;
    internal const uint POINTER_FLAG_PRIMARY = 0x00002000;
    internal const uint POINTER_FLAG_CANCELED = 0x00008000;
    internal const uint POINTER_FLAG_DOWN = 0x00010000;
    internal const uint POINTER_FLAG_UPDATE = 0x00020000;
    internal const uint POINTER_FLAG_UP = 0x00040000;

    internal const uint TOUCH_MASK_CONTACTAREA = 0x00000001;
    internal const uint TOUCH_MASK_ORIENTATION = 0x00000002;
    internal const uint TOUCH_MASK_PRESSURE = 0x00000004;

    internal const uint TOUCH_FEEDBACK_DEFAULT = 0x1;
    internal static readonly nint HwndMessage = new(-3);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WndClassEx
    {
        public uint Size;
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
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointerInfo
    {
        public uint PointerType;
        public uint PointerId;
        public uint FrameId;
        public uint PointerFlags;
        public nint SourceDevice;
        public nint TargetWindow;
        public NativePoint PixelLocation;
        public NativePoint HimetricLocation;
        public NativePoint PixelLocationRaw;
        public NativePoint HimetricLocationRaw;
        public uint Time;
        public uint HistoryCount;
        public int InputData;
        public uint KeyStates;
        public ulong PerformanceCount;
        public uint ButtonChangeType;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointerTouchInfo
    {
        public PointerInfo PointerInfo;
        public uint TouchFlags;
        public uint TouchMask;
        public NativeRect Contact;
        public NativeRect ContactRaw;
        public uint Orientation;
        public uint Pressure;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    internal static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterPointerInputTarget(nint hwnd, uint pointerType);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterPointerInputTarget(nint hwnd, uint pointerType);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPointerInfo(uint pointerId, out PointerInfo pointerInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeTouchInjection(uint maxCount, uint feedbackMode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InjectTouchInput(uint count, [In] PointerTouchInfo[] contacts);

    internal static uint PointerIdFromWParam(nuint wParam) => (uint)(wParam & 0xFFFF);
}
