using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Views;

namespace WinPieGestures.WinUI.Services;

public sealed class WheelCoordinator : IDisposable
{
    private readonly ConfigurationService _configuration;
    private readonly ActionExecutionService _actionExecution;
    private readonly DesktopRadialOverlay _window;
    private IReadOnlyList<ActionItem> _activeActions = [];
    private int _directionCount = 8;
    private int _selectedIndex = -1;
    private int _selectedSubIndex = -1;
    private bool _showSubTier;
    private bool _cancelled;

    public WheelCoordinator(
        ConfigurationService configuration,
        SystemThemeService themeService,
        ActionExecutionService actionExecution)
    {
        _configuration = configuration;
        _actionExecution = actionExecution;
        _window = new DesktopRadialOverlay(themeService);
    }

    public void OnTouchActivated(object? sender, TouchGestureActivation activation)
    {
        AppConfig config = _configuration.Current;
        _directionCount = activation.FingerCount == 0
            ? NormalizeSectorCount(_configuration.GetGlobalProfile().SectorCount)
            : config.TouchTrigger.DirectionCount == 4 ? 4 : 8;
        WheelProfile profile = _configuration.GetGlobalProfile();
        _activeActions = BuildDirectionalActions(profile.Actions, _directionCount);
        ResetSelection();
        _window.Configure(config, _activeActions, _directionCount);
        _window.ShowAt(activation.Center);
    }

    public void OnTouchUpdated(object? sender, TouchGestureUpdate update)
    {
        AppConfig config = _configuration.Current;
        if (config.EnableOuterEscapeCancel && update.Distance >= config.OuterEscapeDistance)
        {
            _cancelled = true;
            _selectedIndex = -1;
            _selectedSubIndex = -1;
            _showSubTier = false;
            _window.Hide();
            return;
        }
        if (_cancelled)
        {
            return;
        }

        int mainIndex = update.HasDirection ? update.DirectionIndex : -1;
        int subIndex = -1;
        bool showSubTier = false;

        if (mainIndex >= 0 && mainIndex < _activeActions.Count && config.EnableMultiTier)
        {
            ActionItem parent = _activeActions[mainIndex];
            int subCount = parent.SubActions?.Count ?? 0;
            if (subCount > 0)
            {
                double triggerDistance = config.SubWheelTriggerDistance > 20
                    ? config.SubWheelTriggerDistance
                    : 95;
                showSubTier = update.Distance >= triggerDistance;
                double selectionDistance = Math.Max(
                    triggerDistance,
                    Math.Clamp(config.WheelRadius, 92, 240) + Math.Max(0, config.SubWheelInnerGap));
                if (showSubTier && update.Distance >= selectionDistance)
                {
                    subIndex = RadialSelectionMath.QuantizeSub(
                        update.Angle,
                        mainIndex,
                        _directionCount,
                        subCount);
                }
            }
        }

        _selectedIndex = mainIndex;
        _selectedSubIndex = subIndex;
        _showSubTier = showSubTier;
        _window.UpdateSelection(mainIndex, subIndex, showSubTier);
    }

    public async void OnTouchCompleted(object? sender, TouchGestureCompletion completion)
    {
        ActionItem? action = ResolveSelectedAction();
        _window.Hide();
        bool cancelled = _cancelled;
        ResetSelection();
        if (cancelled || action is null)
        {
            return;
        }
        await _actionExecution.ExecuteAsync(action);
    }

    public void OnTouchCanceled(object? sender, EventArgs args)
    {
        _window.Hide();
        ResetSelection();
    }

    public void Hide()
    {
        _window.Hide();
        ResetSelection();
    }

    public void Dispose()
    {
        _window.Dispose();
        GC.SuppressFinalize(this);
    }

    public void ShowPreview(TouchPoint center)
    {
        AppConfig config = _configuration.Current;
        WheelProfile profile = _configuration.GetGlobalProfile();
        _directionCount = NormalizeSectorCount(profile.SectorCount);
        _activeActions = BuildDirectionalActions(profile.Actions, _directionCount);
        ResetSelection();
        _window.Configure(config, _activeActions, _directionCount);
        _window.ShowAt(center);
    }

    private ActionItem? ResolveSelectedAction()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _activeActions.Count)
        {
            return null;
        }
        ActionItem main = _activeActions[_selectedIndex];
        if (_showSubTier && _selectedSubIndex >= 0 && _selectedSubIndex < main.SubActions.Count)
        {
            return main.SubActions[_selectedSubIndex];
        }
        return main;
    }

    private void ResetSelection()
    {
        _selectedIndex = -1;
        _selectedSubIndex = -1;
        _showSubTier = false;
        _cancelled = false;
    }

    private static IReadOnlyList<ActionItem> BuildDirectionalActions(IReadOnlyList<ActionItem> source, int count)
    {
        List<ActionItem> actions = new(count);
        for (int index = 0; index < count; index++)
        {
            actions.Add(index < source.Count
                ? source[index]
                : new ActionItem { Type = "None", Name = "未配置", IconKey = "None" });
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
