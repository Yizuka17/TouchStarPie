using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Views;

namespace WinPieGestures.WinUI.Services;

public sealed class AppRuntime : IDisposable
{
    private SettingsWindow? _settingsWindow;

    public AppRuntime(DispatcherQueue dispatcherQueue)
    {
        Configuration = new ConfigurationService();
        Theme = new SystemThemeService(dispatcherQueue);
        Actions = new ActionExecutionService();
        Touch = new GlobalTouchService(dispatcherQueue);
        Mouse = new GlobalMouseGestureService(dispatcherQueue);
        Tray = new TrayIconService();
        Wheel = new WheelCoordinator(Configuration, Theme, Actions);

        Touch.Activated += Wheel.OnTouchActivated;
        Touch.Updated += Wheel.OnTouchUpdated;
        Touch.Completed += Wheel.OnTouchCompleted;
        Touch.Canceled += Wheel.OnTouchCanceled;
        Mouse.Activated += Wheel.OnTouchActivated;
        Mouse.Updated += Wheel.OnTouchUpdated;
        Mouse.Completed += Wheel.OnTouchCompleted;
        Configuration.Changed += OnConfigurationChanged;
    }

    public ConfigurationService Configuration { get; }
    public SystemThemeService Theme { get; }
    public ActionExecutionService Actions { get; }
    public GlobalTouchService Touch { get; }
    public GlobalMouseGestureService Mouse { get; }
    public TrayIconService Tray { get; }
    public WheelCoordinator Wheel { get; }

    public void Start()
    {
        Wheel.Hide();
        ApplyTouchConfiguration();
        Mouse.Start();
        Tray.ShowRequested += TrayOnShowRequested;
        Tray.ExitRequested += TrayOnExitRequested;
        Tray.Start();
        AppLog.Info("StarPie v2 WinUI 3 runtime started");
    }

    public void AttachSettingsWindow(SettingsWindow settingsWindow)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= SettingsWindowOnClosed;
        }
        _settingsWindow = settingsWindow;
        _settingsWindow.Closed += SettingsWindowOnClosed;
    }

    public void ApplyTheme(FrameworkElement root) => Theme.Apply(root, Configuration.Current.AppTheme);

    public void ApplyTouchConfiguration()
    {
        Touch.Configure(Configuration.Current);
        Touch.SetEnabled(Configuration.Current.TouchTrigger.Enabled);
        Mouse.Configure(Configuration.Current, Configuration.GetGlobalProfile().SectorCount);
    }

    public void Dispose()
    {
        Touch.Activated -= Wheel.OnTouchActivated;
        Touch.Updated -= Wheel.OnTouchUpdated;
        Touch.Completed -= Wheel.OnTouchCompleted;
        Touch.Canceled -= Wheel.OnTouchCanceled;
        Mouse.Activated -= Wheel.OnTouchActivated;
        Mouse.Updated -= Wheel.OnTouchUpdated;
        Mouse.Completed -= Wheel.OnTouchCompleted;
        Configuration.Changed -= OnConfigurationChanged;
        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= SettingsWindowOnClosed;
        }
        Wheel.Dispose();
        Touch.Dispose();
        Mouse.Dispose();
        Tray.ShowRequested -= TrayOnShowRequested;
        Tray.ExitRequested -= TrayOnExitRequested;
        Tray.Dispose();
        Theme.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnConfigurationChanged(object? sender, EventArgs args)
    {
        ApplyTouchConfiguration();
        _settingsWindow?.ReloadFromConfiguration();
    }

    private void SettingsWindowOnClosed(object sender, WindowEventArgs args) => Wheel.Hide();

    private void TrayOnShowRequested(object? sender, EventArgs args) => _settingsWindow?.ShowSettings();

    private void TrayOnExitRequested(object? sender, EventArgs args)
    {
        Wheel.Hide();
        _settingsWindow?.CloseForExit();
        if (Application.Current is App app)
        {
            app.ExitApplication();
        }
    }
}
