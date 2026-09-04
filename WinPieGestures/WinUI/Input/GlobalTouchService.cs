using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using WinPieGestures.WinUI.Services;

namespace WinPieGestures.WinUI.Input;

public enum GlobalTouchStatus
{
    Disabled,
    Active,
    UiAccessRequired,
    RegistrationConflict,
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
/// Receives globally redirected PT_TOUCH pointer messages in a non-activating message-only
/// HWND. Windows permits registration only for a signed UIAccess process.
/// </summary>
public sealed class GlobalTouchService : IDisposable
{
    private const int ErrorAccessDenied = 5;
    private readonly DispatcherQueueTimer _holdTimer;
    private readonly TouchGestureRecognizer _recognizer = new();
    private readonly TouchPassthroughInjector _injector = new();
    private NativeTouchMethods.WindowProc? _windowProcedure;
    private nint _window;
    private string? _windowClassName;
    private bool _gestureOwnsSequence;
    private AppConfig _sceneConfig = new();

    public GlobalTouchService(DispatcherQueue dispatcherQueue)
    {
        _holdTimer = dispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(16);
        _holdTimer.IsRepeating = true;
        _holdTimer.Tick += (_, _) =>
        {
            _recognizer.Tick(DateTimeOffset.UtcNow);
        };
        _recognizer.Activated += RecognizerOnActivated;
        _recognizer.Updated += (_, update) => Updated?.Invoke(this, update);
        _recognizer.Completed += (_, completion) => Completed?.Invoke(this, completion);
        _recognizer.SessionEnded += (_, _) =>
        {
            if (_gestureOwnsSequence)
            {
                _gestureOwnsSequence = false;
                _injector.Reset();
            }
        };
    }

    public GlobalTouchStatus Status { get; private set; } = GlobalTouchStatus.Disabled;

    public event EventHandler<GlobalTouchStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<TouchGestureActivation>? Activated;
    public event EventHandler<TouchGestureUpdate>? Updated;
    public event EventHandler<TouchGestureCompletion>? Completed;

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
        // Global PT_TOUCH registration redirects the whole pointer type to this process.
        // Streaming reinjection is therefore a safety invariant, not an optional mode.
        config.PassThroughUnhandledTouch = true;
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
            if (!NativeTouchMethods.RegisterPointerInputTarget(_window, NativeTouchMethods.PT_TOUCH))
            {
                int error = Marshal.GetLastWin32Error();
                SetStatus(
                    error == ErrorAccessDenied ? GlobalTouchStatus.UiAccessRequired : GlobalTouchStatus.RegistrationConflict,
                    error == ErrorAccessDenied
                        ? "系统级触摸需要已签名并安装到 Program Files 的 UIAccess 构建。"
                        : $"触摸重定向注册失败（Win32 {error}），桌面上可能已有其他全局触摸目标。");
                return Status;
            }

            _holdTimer.Start();
            SetStatus(GlobalTouchStatus.Active, "单指/双指/三指全局触摸触发已启用");
        }
        catch (Exception exception)
        {
            AppLog.Error("Unable to start global touch service", exception);
            SetStatus(GlobalTouchStatus.Error, exception.Message);
        }
        return Status;
    }

    public void Stop()
    {
        _holdTimer.Stop();
        _injector.CancelAll(canceled: false);
        _recognizer.Cancel();
        _injector.Reset();
        if (_window != 0)
        {
            NativeTouchMethods.UnregisterPointerInputTarget(_window, NativeTouchMethods.PT_TOUCH);
        }
        SetStatus(GlobalTouchStatus.Disabled, "全局触摸触发未启用");
    }

    public void Dispose()
    {
        Stop();
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
        _windowClassName = $"StarPie.TouchTarget.{Environment.ProcessId}";
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
            "StarPie touch target",
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
        if (message is NativeTouchMethods.WM_POINTERDOWN or NativeTouchMethods.WM_POINTERUPDATE or NativeTouchMethods.WM_POINTERUP)
        {
            HandlePointerMessage(message, NativeTouchMethods.PointerIdFromWParam(wParam));
            return 0;
        }
        if (message == NativeTouchMethods.WM_POINTERCAPTURECHANGED)
        {
            _injector.CancelAll();
            _recognizer.Cancel();
            _injector.Reset();
            _gestureOwnsSequence = false;
            return 0;
        }
        return NativeTouchMethods.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void HandlePointerMessage(uint message, uint pointerId)
    {
        if (!NativeTouchMethods.GetPointerInfo(pointerId, out NativeTouchMethods.PointerInfo info))
        {
            return;
        }

        TouchPoint point = new(info.PixelLocation.X, info.PixelLocation.Y);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (message == NativeTouchMethods.WM_POINTERDOWN)
        {
            bool beginsSequence = _recognizer.Phase == TouchGesturePhase.Idle;
            _recognizer.PointerDown(pointerId, point, now);
            if (beginsSequence && !ScenePolicy.IsGestureAllowed(_sceneConfig))
            {
                _recognizer.Cancel();
            }
            if (!_gestureOwnsSequence)
            {
                EnsureInjected(_injector.Sync(_recognizer.Contacts));
            }
            return;
        }

        if (message == NativeTouchMethods.WM_POINTERUPDATE)
        {
            _recognizer.PointerMove(pointerId, point, now);
            if (!_gestureOwnsSequence)
            {
                EnsureInjected(_injector.Sync(_recognizer.Contacts));
            }
            return;
        }

        // Capture the final physical position before the recognizer removes the contact.
        _recognizer.PointerMove(pointerId, point, now);
        IReadOnlyList<TouchContact> beforeUp = _recognizer.Contacts;
        TouchGesturePhase phaseBeforeUp = _recognizer.Phase;
        _recognizer.PointerUp(pointerId, point, now);

        if (_gestureOwnsSequence || phaseBeforeUp == TouchGesturePhase.Armed)
        {
            return;
        }

        if (!_injector.IsActive)
        {
            if (!EnsureInjected(_injector.Sync(beforeUp)))
            {
                return;
            }
        }
        if (!EnsureInjected(_injector.EndContact(pointerId, point, _recognizer.Contacts)))
        {
            return;
        }
        if (_recognizer.Phase == TouchGesturePhase.Idle)
        {
            _injector.Reset();
        }
    }

    private void RecognizerOnActivated(object? sender, TouchGestureActivation activation)
    {
        // Normal touch has been mirrored since POINTERDOWN. Only now, after the complete
        // long-press chord is recognized, cancel that mirrored stream and let StarPie own
        // the remainder. This keeps ordinary taps, scrolling and pinch gestures live.
        _gestureOwnsSequence = true;
        _injector.CancelAll();
        Activated?.Invoke(this, activation);
    }

    private bool EnsureInjected(bool succeeded)
    {
        if (succeeded)
        {
            return true;
        }

        // Never leave PT_TOUCH redirected when passthrough cannot be guaranteed.
        _holdTimer.Stop();
        if (_window != 0)
        {
            NativeTouchMethods.UnregisterPointerInputTarget(_window, NativeTouchMethods.PT_TOUCH);
        }
        _recognizer.Cancel();
        _injector.Reset();
        _gestureOwnsSequence = false;
        SetStatus(GlobalTouchStatus.Error, "触摸透传失败，已自动关闭全局捕获以恢复系统触摸。");
        return false;
    }

    private void SetStatus(GlobalTouchStatus status, string message)
    {
        Status = status;
        StatusChanged?.Invoke(this, new GlobalTouchStatusChangedEventArgs(status, message));
        AppLog.Info($"Global touch status: {status} - {message}");
    }
}
