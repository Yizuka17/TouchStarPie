using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using WinPieGestures.WinUI.Services;

namespace WinPieGestures.WinUI.Input;

public enum GlobalTouchStatus
{
    Disabled,
    Active,
    // Kept for settings/UI compatibility with the earlier pointer-redirection preview.
    UiAccessRequired,
    RegistrationConflict,
    RegistrationFailed,
    Error
}

public sealed class GlobalTouchStatusChangedEventArgs : EventArgs
{
    public GlobalTouchStatusChangedEventArgs(GlobalTouchStatus status, string message)
    {
        Status = status;
        Message = message;
    }

    public GlobalTouchStatus Status { get; }
    public string Message { get; }
}

/// <summary>
/// Passive global touchscreen observer. Raw Input delivers HID reports to a message-only HWND
/// while the original Windows pointer stream continues to its normal target untouched.
/// </summary>
public sealed class GlobalTouchService : IDisposable
{
    private readonly record struct RawContactKey(nint Device, uint ContactId);

    private readonly DispatcherQueueTimer _holdTimer;
    private readonly TouchGestureRecognizer _recognizer = new();
    private readonly RawTouchHidParser _parser = new();
    private readonly Dictionary<nint, Dictionary<uint, TouchPoint>> _deviceContacts = [];
    private readonly Dictionary<RawContactKey, uint> _syntheticIds = [];
    private NativeTouchMethods.WindowProc? _windowProcedure;
    private nint _window;
    private string? _windowClassName;
    private uint _nextSyntheticId = 1;
    private AppConfig _sceneConfig = new();

    public GlobalTouchService(DispatcherQueue dispatcherQueue)
    {
        _holdTimer = dispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(16);
        _holdTimer.IsRepeating = true;
        _holdTimer.Tick += (_, _) => _recognizer.Tick(DateTimeOffset.UtcNow);
        _recognizer.Activated += (_, activation) => Activated?.Invoke(this, activation);
        _recognizer.Updated += (_, update) => Updated?.Invoke(this, update);
        _recognizer.Completed += (_, completion) => Completed?.Invoke(this, completion);
        _recognizer.Canceled += (_, _) => Canceled?.Invoke(this, EventArgs.Empty);
    }

    public GlobalTouchStatus Status { get; private set; } = GlobalTouchStatus.Disabled;

    public event EventHandler<GlobalTouchStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<TouchGestureActivation>? Activated;
    public event EventHandler<TouchGestureUpdate>? Updated;
    public event EventHandler<TouchGestureCompletion>? Completed;
    public event EventHandler? Canceled;

    public void Configure(AppConfig appConfig)
    {
        TouchTriggerConfig config = appConfig.TouchTrigger;
        _sceneConfig = appConfig;
        _recognizer.LongPressDelayMs = config.LongPressDelayMs;
        _recognizer.HoldMovementTolerance = config.HoldMovementTolerance;
        _recognizer.SwipeThreshold = config.SwipeThreshold;
        _recognizer.DirectionCount = config.DirectionCount;
        _recognizer.EnableOneFinger = config.EnableOneFinger;
        _recognizer.EnableTwoFinger = config.EnableTwoFinger;
        _recognizer.EnableThreeFinger = config.EnableThreeFinger;
    }

    public GlobalTouchStatus SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            Stop();
            return Status;
        }
        return Start();
    }

    public GlobalTouchStatus Start()
    {
        if (Status == GlobalTouchStatus.Active)
        {
            return Status;
        }

        try
        {
            EnsureMessageWindow();
            NativeTouchMethods.RawInputDevice[] devices =
            [
                new NativeTouchMethods.RawInputDevice
                {
                    UsagePage = NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER,
                    Usage = NativeTouchMethods.HID_USAGE_DIGITIZER_TOUCH_SCREEN,
                    Flags = NativeTouchMethods.RIDEV_INPUTSINK | NativeTouchMethods.RIDEV_DEVNOTIFY,
                    TargetWindow = _window
                }
            ];
            if (!NativeTouchMethods.RegisterRawInputDevices(
                    devices,
                    (uint)devices.Length,
                    (uint)Marshal.SizeOf<NativeTouchMethods.RawInputDevice>()))
            {
                int error = Marshal.GetLastWin32Error();
                SetStatus(GlobalTouchStatus.RegistrationFailed, $"Raw Input 触摸注册失败（Win32 {error}）。");
                return Status;
            }

            _holdTimer.Start();
            SetStatus(GlobalTouchStatus.Active, "Raw Input/HID 旁路监听已启用（单指/双指/三指）");
        }
        catch (Exception exception)
        {
            AppLog.Error("Unable to start passive raw touch service", exception);
            SetStatus(GlobalTouchStatus.Error, exception.Message);
        }
        return Status;
    }

    public void Stop()
    {
        _holdTimer.Stop();
        if (_window != 0)
        {
            NativeTouchMethods.RawInputDevice[] devices =
            [
                new NativeTouchMethods.RawInputDevice
                {
                    UsagePage = NativeTouchMethods.HID_USAGE_PAGE_DIGITIZER,
                    Usage = NativeTouchMethods.HID_USAGE_DIGITIZER_TOUCH_SCREEN,
                    Flags = NativeTouchMethods.RIDEV_REMOVE,
                    TargetWindow = 0
                }
            ];
            NativeTouchMethods.RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<NativeTouchMethods.RawInputDevice>());
        }

        _recognizer.Cancel();
        _deviceContacts.Clear();
        _syntheticIds.Clear();
        _parser.Reset();
        SetStatus(GlobalTouchStatus.Disabled, "全局触摸触发未启用");
    }

    public void Dispose()
    {
        Stop();
        _parser.Dispose();
        if (_window != 0)
        {
            NativeTouchMethods.DestroyWindow(_window);
            _window = 0;
        }
        GC.SuppressFinalize(this);
    }

    private void EnsureMessageWindow()
    {
        if (_window != 0)
        {
            return;
        }

        _windowProcedure = WindowProcedure;
        _windowClassName = $"StarPie.RawTouchSink.{Environment.ProcessId}";
        NativeTouchMethods.WndClassEx windowClass = new()
        {
            Size = (uint)Marshal.SizeOf<NativeTouchMethods.WndClassEx>(),
            Instance = NativeTouchMethods.GetModuleHandle(null),
            WindowProcedure = _windowProcedure,
            ClassName = _windowClassName
        };

        ushort atom = NativeTouchMethods.RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
        }

        _window = NativeTouchMethods.CreateWindowEx(
            0x08000000, // WS_EX_NOACTIVATE
            _windowClassName,
            "StarPie raw touch sink",
            0,
            0,
            0,
            0,
            0,
            NativeTouchMethods.HwndMessage,
            0,
            windowClass.Instance,
            0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        if (message == NativeTouchMethods.WM_INPUT)
        {
            if (_parser.TryParse(lParam, out RawTouchFrame frame))
            {
                HandleFrame(frame);
            }
            // WM_INPUT still goes through DefWindowProc so foreground raw-input cleanup rules
            // remain correct even though StarPie normally receives it as RIM_INPUTSINK.
            return NativeTouchMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }

        if (message == NativeTouchMethods.WM_INPUT_DEVICE_CHANGE && wParam == NativeTouchMethods.GIDC_REMOVAL)
        {
            HandleDeviceRemoval(lParam);
            return 0;
        }
        return NativeTouchMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void HandleFrame(RawTouchFrame frame)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!_deviceContacts.TryGetValue(frame.Device, out Dictionary<uint, TouchPoint>? previous))
        {
            previous = [];
            _deviceContacts[frame.Device] = previous;
        }
        Dictionary<uint, TouchPoint> current = frame.Contacts
            .GroupBy(contact => contact.Id)
            .ToDictionary(group => group.Key, group => group.Last().Point);

        // Update contacts that remain present before applying contact-set changes. This makes
        // a newly added second/third finger reset the hold baseline at the latest centroid.
        foreach ((uint rawId, TouchPoint point) in current)
        {
            if (previous.ContainsKey(rawId) && TryGetSyntheticId(frame.Device, rawId, out uint syntheticId))
            {
                _recognizer.PointerMove(syntheticId, point, now);
            }
        }

        foreach ((uint rawId, TouchPoint oldPoint) in previous.ToArray())
        {
            if (current.ContainsKey(rawId))
            {
                continue;
            }
            if (TryGetSyntheticId(frame.Device, rawId, out uint syntheticId))
            {
                _recognizer.PointerUp(syntheticId, oldPoint, now);
            }
            _syntheticIds.Remove(new RawContactKey(frame.Device, rawId));
        }

        foreach ((uint rawId, TouchPoint point) in current)
        {
            if (previous.ContainsKey(rawId))
            {
                continue;
            }

            uint syntheticId = AllocateSyntheticId(frame.Device, rawId);
            bool beginsSequence = _recognizer.Phase == TouchGesturePhase.Idle;
            _recognizer.PointerDown(syntheticId, point, now);
            if (beginsSequence && !ScenePolicy.IsGestureAllowed(_sceneConfig))
            {
                _recognizer.Suppress();
            }
        }

        previous.Clear();
        foreach ((uint rawId, TouchPoint point) in current)
        {
            previous[rawId] = point;
        }
        if (previous.Count == 0)
        {
            _deviceContacts.Remove(frame.Device);
        }
    }

    private void HandleDeviceRemoval(nint device)
    {
        bool hadContacts = _deviceContacts.Remove(device);
        foreach (RawContactKey key in _syntheticIds.Keys.Where(key => key.Device == device).ToArray())
        {
            _syntheticIds.Remove(key);
        }
        _parser.ForgetDevice(device);
        if (hadContacts)
        {
            _recognizer.Cancel();
        }
    }

    private uint AllocateSyntheticId(nint device, uint rawId)
    {
        RawContactKey key = new(device, rawId);
        if (_syntheticIds.TryGetValue(key, out uint existing))
        {
            return existing;
        }

        uint id = _nextSyntheticId++;
        if (id == 0)
        {
            id = _nextSyntheticId++;
        }
        _syntheticIds[key] = id;
        return id;
    }

    private bool TryGetSyntheticId(nint device, uint rawId, out uint id) =>
        _syntheticIds.TryGetValue(new RawContactKey(device, rawId), out id);

    private void SetStatus(GlobalTouchStatus status, string message)
    {
        Status = status;
        StatusChanged?.Invoke(this, new GlobalTouchStatusChangedEventArgs(status, message));
        AppLog.Info($"Global touch status: {status} - {message}");
    }
}
