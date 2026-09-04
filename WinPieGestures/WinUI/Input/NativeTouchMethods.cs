using System.Runtime.InteropServices;

namespace WinPieGestures.WinUI.Input;

internal static class NativeTouchMethods
{
    internal const uint WM_INPUT = 0x00FF;
    internal const uint WM_INPUT_DEVICE_CHANGE = 0x00FE;
    internal const nuint GIDC_ARRIVAL = 1;
    internal const nuint GIDC_REMOVAL = 2;

    internal const uint RID_INPUT = 0x10000003;
    internal const uint RIDI_PREPARSEDDATA = 0x20000005;
    internal const uint RIM_TYPEHID = 2;

    internal const uint RIDEV_REMOVE = 0x00000001;
    internal const uint RIDEV_INPUTSINK = 0x00000100;
    internal const uint RIDEV_DEVNOTIFY = 0x00002000;

    internal const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    internal const ushort HID_USAGE_GENERIC_X = 0x30;
    internal const ushort HID_USAGE_GENERIC_Y = 0x31;
    internal const ushort HID_USAGE_PAGE_DIGITIZER = 0x0D;
    internal const ushort HID_USAGE_DIGITIZER_TOUCH_SCREEN = 0x04;
    internal const ushort HID_USAGE_DIGITIZER_TIP_SWITCH = 0x42;
    internal const ushort HID_USAGE_DIGITIZER_CONTACT_ID = 0x51;
    internal const ushort HID_USAGE_DIGITIZER_CONTACT_COUNT = 0x54;

    internal const int HIDP_INPUT = 0;
    internal const int HIDP_STATUS_SUCCESS = 0x00110000;
    internal const int HIDP_CAPS_SIZE = 64;
    internal const int HIDP_CAPS_NUMBER_INPUT_VALUE_CAPS_OFFSET = 48;
    internal const int HIDP_VALUE_CAPS_SIZE = 72;

    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;
    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;

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
    internal struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint TargetWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nint WParam;
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
    internal static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint numberOfDevices,
        uint sizeOfRawInputDevice);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetRawInputData(
        nint rawInput,
        uint command,
        nint data,
        ref uint size,
        uint sizeOfRawInputHeader);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    internal static extern uint GetRawInputDeviceInfo(
        nint device,
        uint command,
        nint data,
        ref uint size);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(nint preparsedData, nint capabilities);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetValueCaps(
        int reportType,
        nint valueCapabilities,
        ref ushort valueCapabilitiesLength,
        nint preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetUsageValue(
        int reportType,
        ushort usagePage,
        ushort linkCollection,
        ushort usage,
        out uint usageValue,
        nint preparsedData,
        nint report,
        uint reportLength);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetUsages(
        int reportType,
        ushort usagePage,
        ushort linkCollection,
        [Out] ushort[] usageList,
        ref uint usageLength,
        nint preparsedData,
        nint report,
        uint reportLength);
}
