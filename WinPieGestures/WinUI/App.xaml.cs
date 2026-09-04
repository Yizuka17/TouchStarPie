using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using WinPieGestures.WinUI.Services;
using WinPieGestures.WinUI.Views;

namespace WinPieGestures.WinUI;

public partial class App : Application
{
    private const string MutexName = "Local\\StarPie.WinUI3.SingleInstance.9B8A7C";
    private const string WakeEventName = "Local\\StarPie.WinUI3.Wake.9B8A7C";
    private SettingsWindow? _settingsWindow;
    private AppRuntime? _runtime;
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _wakeEvent;
    private RegisteredWaitHandle? _wakeRegistration;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            AppLog.Error("WinUI dispatcher unhandled exception", args.Exception);
            args.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using EventWaitHandle wake = EventWaitHandle.OpenExisting(WakeEventName);
                wake.Set();
            }
            catch
            {
            }
            Exit();
            return;
        }

        _runtime = new AppRuntime(dispatcherQueue);
        _settingsWindow = new SettingsWindow(_runtime);
        _runtime.AttachSettingsWindow(_settingsWindow);
        _runtime.Start();
        _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
        _wakeRegistration = ThreadPool.RegisterWaitForSingleObject(
            _wakeEvent,
            (_, _) => dispatcherQueue.TryEnqueue(() => _settingsWindow?.ShowSettings()),
            null,
            Timeout.Infinite,
            false);

        bool startMinimized = Environment.GetCommandLineArgs().Any(arg =>
            arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--autostart", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--silent", StringComparison.OrdinalIgnoreCase));

        if (!startMinimized)
        {
            _settingsWindow.Activate();
        }

        if (Environment.GetCommandLineArgs().Any(arg =>
                arg.Equals("--wheel-preview", StringComparison.OrdinalIgnoreCase)))
        {
            dispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(600);
                if (_settingsWindow is not null)
                {
                    await _settingsWindow.ShowOverlayPreviewForTestingAsync();
                }
            });
        }
    }

    public void ExitApplication()
    {
        _wakeRegistration?.Unregister(null);
        _wakeEvent?.Dispose();
        _runtime?.Dispose();
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
        }
        _singleInstanceMutex?.Dispose();
        Exit();
    }
}
