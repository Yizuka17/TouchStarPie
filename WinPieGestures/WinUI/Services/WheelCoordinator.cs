using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Views;

namespace WinPieGestures.WinUI.Services;

public sealed class WheelCoordinator : IDisposable
{
    private readonly ConfigurationService _configuration;
    private readonly ActionExecutionService _actionExecution;
    private readonly RadialMenuWindow _window;
    private IReadOnlyList<ActionItem> _activeActions = [];
    private int _selectedIndex = -1;
    private bool _cancelled;

    public WheelCoordinator(
        ConfigurationService configuration,
        SystemThemeService themeService,
        ActionExecutionService actionExecution)
    {
        _configuration = configuration;
        _actionExecution = actionExecution;
        _window = new RadialMenuWindow(themeService);
    }

    public void OnTouchActivated(object? sender, TouchGestureActivation activation)
    {
        AppConfig config = _configuration.Current;
        int directionCount = activation.FingerCount == 0
            ? NormalizeSectorCount(_configuration.GetGlobalProfile().SectorCount)
            : config.TouchTrigger.DirectionCount == 4 ? 4 : 8;
        WheelProfile profile = _configuration.GetGlobalProfile();
        _activeActions = BuildDirectionalActions(profile.Actions, directionCount);
        _selectedIndex = -1;
        _cancelled = false;
        _window.Configure(config, _activeActions, directionCount);
        _window.ShowAt(activation.Center);
    }

    public void OnTouchUpdated(object? sender, TouchGestureUpdate update)
    {
        if (_configuration.Current.EnableOuterEscapeCancel &&
            update.Distance >= _configuration.Current.OuterEscapeDistance)
        {
            _cancelled = true;
            _selectedIndex = -1;
            _window.Hide();
            return;
        }
        if (_cancelled)
        {
            return;
        }
        _selectedIndex = update.HasDirection ? update.DirectionIndex : -1;
        _window.UpdateDirection(_selectedIndex);
    }

    public async void OnTouchCompleted(object? sender, TouchGestureCompletion completion)
    {
        int selected = completion.HasDirection ? completion.DirectionIndex : _selectedIndex;
        _window.Hide();
        _selectedIndex = -1;
        if (_cancelled)
        {
            _cancelled = false;
            return;
        }
        if (selected >= 0 && selected < _activeActions.Count)
        {
            await _actionExecution.ExecuteAsync(_activeActions[selected]);
        }
    }

    public void OnTouchCanceled(object? sender, EventArgs args)
    {
        _window.Hide();
        _selectedIndex = -1;
        _cancelled = false;
    }

    public void Hide() => _window.Hide();

    public void Dispose()
    {
        _window.Dispose();
        GC.SuppressFinalize(this);
    }

    public void ShowPreview(TouchPoint center)
    {
        AppConfig config = _configuration.Current;
        WheelProfile profile = _configuration.GetGlobalProfile();
        int directionCount = NormalizeSectorCount(profile.SectorCount);
        _activeActions = BuildDirectionalActions(profile.Actions, directionCount);
        _selectedIndex = -1;
        _cancelled = false;
        _window.Configure(config, _activeActions, directionCount);
        _window.ShowAt(center);
    }

    private static IReadOnlyList<ActionItem> BuildDirectionalActions(IReadOnlyList<ActionItem> source, int count)
    {
        List<ActionItem> actions = new(count);
        for (int index = 0; index < count; index++)
        {
            actions.Add(index < source.Count
                ? source[index]
                : new ActionItem { Type = "None", Name = "未配置", IconKey = "" });
        }
        return actions;
    }

    private static int NormalizeSectorCount(int count) => count switch
    {
        4 => 4,
        12 => 12,
        _ => 8
    };
}
