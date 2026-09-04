using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using WinPieGestures.WinUI.Services;

namespace WinPieGestures.WinUI.Input;

public sealed class GlobalMouseGestureService : IDisposable
{
    private const int WhMouseLl = 14;
    private const uint WmQuit = 0x0012;
    private const uint WmMouseMove = 0x0200;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmXButtonUp = 0x020C;
    private const uint InputMouse = 0;
    private static readonly nuint InjectedMarker = unchecked((nuint)0x53545049454D4F55UL);

    private readonly object _sync = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _longPressTimer;
    private Thread? _hookThread;
    private HookProc? _hookProcedure;
    private nint _hook;
    private uint _hookThreadId;
    private string _triggerButton = "RightButton";
    private bool _enabled = true;
    private bool _longPressEnabled;
    private double _longPressDelayMs = 450;
    private double _dragThreshold = 25;
    private double _selectionThreshold = 35;
    private int _directionCount = 8;
    private bool _pressed;
    private bool _gestureActive;
    private TouchPoint _pressPoint;
    private TouchPoint _lastPoint;
    private long _pressedTimestamp;
    private TouchGestureUpdate _pendingUpdate;
    private int _updateQueued;
    private AppConfig _sceneConfig = new();

    public GlobalMouseGestureService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _longPressTimer = dispatcherQueue.CreateTimer();
        _longPressTimer.Interval = TimeSpan.FromMilliseconds(16);
        _longPressTimer.IsRepeating = true;
        _longPressTimer.Tick += (_, _) => TryActivateLongPress();
    }

    public event EventHandler<TouchGestureActivation>? Activated;
    public event EventHandler<TouchGestureUpdate>? Updated;
    public event EventHandler<TouchGestureCompletion>? Completed;

    public void Configure(AppConfig config, int directionCount)
    {
        lock (_sync)
        {
            _sceneConfig = config;
            _enabled = string.Equals(config.Trigger?.TriggerType, "Mouse", StringComparison.OrdinalIgnoreCase);
            _triggerButton = config.Trigger?.MouseButton ?? config.TriggerButton ?? "RightButton";
            _longPressEnabled = config.LongPressTrigger;
            _longPressDelayMs = Math.Clamp(config.LongPressDelayMs, 200, 1200);
            _dragThreshold = Math.Clamp(config.DragThreshold, 8, 120);
            _selectionThreshold = Math.Max(_dragThreshold, config.CoreDeadzoneRadius);
            _directionCount = directionCount is 4 or 8 or 12 ? directionCount : 8;
        }
    }

    public void Start()
    {
        if (_hookThread is not null)
        {
            return;
        }
        _longPressTimer.Start();
        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "StarPie.MouseHook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    public void Stop()
    {
        _longPressTimer.Stop();
        if (_hookThreadId != 0)
        {
            PostThreadMessage(_hookThreadId, WmQuit, 0, 0);
        }
        _hookThread?.Join(1000);
        _hookThread = null;
        _hookThreadId = 0;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    private void HookThreadMain()
    {
        _hookThreadId = GetCurrentThreadId();
        _hookProcedure = HookCallback;
        _hook = SetWindowsHookEx(WhMouseLl, _hookProcedure, GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            AppLog.Error($"SetWindowsHookEx(WH_MOUSE_LL) failed: {Marshal.GetLastWin32Error()}");
            return;
        }
        while (GetMessage(out NativeMessage message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
        UnhookWindowsHookEx(_hook);
        _hook = 0;
    }

    private nint HookCallback(int code, nuint wParam, nint lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(_hook, code, wParam, lParam);
        }
        LowLevelMouse data = Marshal.PtrToStructure<LowLevelMouse>(lParam);
        if (data.ExtraInfo == InjectedMarker)
        {
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        uint message = (uint)wParam;
        TouchPoint point = new(data.Point.X, data.Point.Y);
        bool suppress = false;
        lock (_sync)
        {
            if (!_enabled)
            {
                return CallNextHookEx(_hook, code, wParam, lParam);
            }
            if (IsTriggerDown(message, data.MouseData))
            {
                if (!_pressed)
                {
                    if (!ScenePolicy.IsGestureAllowed(_sceneConfig))
                    {
                        return CallNextHookEx(_hook, code, wParam, lParam);
                    }
                    _pressed = true;
                    _gestureActive = false;
                    _pressPoint = point;
                    _lastPoint = point;
                    _pressedTimestamp = Stopwatch.GetTimestamp();
                }
                suppress = true;
            }
            else if (message == WmMouseMove && _pressed)
            {
                _lastPoint = point;
                double dx = point.X - _pressPoint.X;
                double dy = point.Y - _pressPoint.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (!_gestureActive && distance >= _dragThreshold)
                {
                    _gestureActive = true;
                    PostActivated(_pressPoint);
                }
                if (_gestureActive)
                {
                    PostUpdated(point, dx, dy, distance);
                }
            }
            else if (IsTriggerUp(message, data.MouseData) && _pressed)
            {
                suppress = true;
                _lastPoint = point;
                if (_gestureActive)
                {
                    double dx = point.X - _pressPoint.X;
                    double dy = point.Y - _pressPoint.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    PostCompleted(point, dx, dy, distance);
                }
                else
                {
                    ReplayClick();
                }
                _pressed = false;
                _gestureActive = false;
            }
        }
        return suppress ? 1 : CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void TryActivateLongPress()
    {
        lock (_sync)
        {
            if (!_enabled || !_longPressEnabled || !_pressed || _gestureActive)
            {
                return;
            }
            if (Stopwatch.GetElapsedTime(_pressedTimestamp).TotalMilliseconds >= _longPressDelayMs)
            {
                _gestureActive = true;
                PostActivated(_pressPoint);
            }
        }
    }

    private void PostActivated(TouchPoint point) => _dispatcherQueue.TryEnqueue(() =>
        Activated?.Invoke(this, new TouchGestureActivation(0, point)));

    private void PostUpdated(TouchPoint point, double dx, double dy, double distance)
    {
        double angle = Math.Atan2(dy, dx);
        bool selected = distance >= _selectionThreshold;
        int index = selected ? Quantize(angle, _directionCount) : -1;
        _pendingUpdate = new TouchGestureUpdate(0, point, angle, distance, index, selected);
        if (Interlocked.Exchange(ref _updateQueued, 1) != 0)
        {
            return;
        }
        _dispatcherQueue.TryEnqueue(() =>
        {
            TouchGestureUpdate latest;
            lock (_sync)
            {
                latest = _pendingUpdate;
                Interlocked.Exchange(ref _updateQueued, 0);
            }
            Updated?.Invoke(this, latest);
        });
    }

    private void PostCompleted(TouchPoint point, double dx, double dy, double distance)
    {
        double angle = Math.Atan2(dy, dx);
        bool selected = distance >= _selectionThreshold;
        int index = selected ? Quantize(angle, _directionCount) : -1;
        TouchGestureCompletion completion = new(0, point, angle, distance, index, selected);
        _dispatcherQueue.TryEnqueue(() => Completed?.Invoke(this, completion));
    }

    private static int Quantize(double angle, int count)
    {
        double step = Math.Tau / count;
        int index = (int)Math.Round((angle + Math.PI / 2) / step, MidpointRounding.AwayFromZero);
        return ((index % count) + count) % count;
    }

    private bool IsTriggerDown(uint message, uint mouseData) => _triggerButton.ToLowerInvariant() switch
    {
        "middlebutton" => message == WmMButtonDown,
        "xbutton1" => message == WmXButtonDown && ((mouseData >> 16) & 0xFFFF) == 1,
        "xbutton2" => message == WmXButtonDown && ((mouseData >> 16) & 0xFFFF) == 2,
        _ => message == WmRButtonDown
    };

    private bool IsTriggerUp(uint message, uint mouseData) => _triggerButton.ToLowerInvariant() switch
    {
        "middlebutton" => message == WmMButtonUp,
        "xbutton1" => message == WmXButtonUp && ((mouseData >> 16) & 0xFFFF) == 1,
        "xbutton2" => message == WmXButtonUp && ((mouseData >> 16) & 0xFFFF) == 2,
        _ => message == WmRButtonUp
    };

    private void ReplayClick()
    {
        (uint down, uint up, uint data) = _triggerButton.ToLowerInvariant() switch
        {
            "middlebutton" => (0x0020u, 0x0040u, 0u),
            "xbutton1" => (0x0080u, 0x0100u, 1u),
            "xbutton2" => (0x0080u, 0x0100u, 2u),
            _ => (0x0008u, 0x0010u, 0u)
        };
        NativeInput[] inputs = [NativeInput.CreateMouse(down, data), NativeInput.CreateMouse(up, data)];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint HookProc(int code, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelMouse
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Data;

        public static NativeInput CreateMouse(uint flags, uint mouseData) => new()
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    MouseData = mouseData,
                    Flags = flags,
                    ExtraInfo = InjectedMarker
                }
            }
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProc procedure, nint module, uint threadId);
    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, nint window, uint minimum, uint maximum);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref NativeMessage message);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
}
