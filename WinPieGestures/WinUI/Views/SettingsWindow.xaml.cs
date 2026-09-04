using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinPieGestures.WinUI.Controls;
using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Services;
using WinRT.Interop;

namespace WinPieGestures.WinUI.Views;

public sealed partial class SettingsWindow : Window
{
    private static readonly string[] DirectionNames4 = ["上", "右", "下", "左"];
    private static readonly string[] DirectionNames8 = ["上", "右上", "右", "右下", "下", "左下", "左", "左上"];
    private readonly AppRuntime _runtime;
    private readonly RadialMenuControl _preview;
    private readonly AppWindow _appWindow;
    private readonly nint _hwnd;
    private bool _updating;
    private bool _allowClose;

    public SettingsWindow(AppRuntime runtime)
    {
        _runtime = runtime;
        _updating = true;
        InitializeComponent();
        _updating = false;
        Title = "StarPie 设置 · WinUI 3";

        _hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        double scale = Math.Max(1, GetDpiForWindow(_hwnd) / 96.0);
        int targetWidth = (int)Math.Round(1240 * scale);
        int targetHeight = (int)Math.Round(840 * scale);
        DisplayArea display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        RectInt32 workArea = display.WorkArea;
        targetWidth = Math.Min(targetWidth, workArea.Width);
        targetHeight = Math.Min(targetHeight, workArea.Height);
        _appWindow.MoveAndResize(new RectInt32(
            workArea.X + Math.Max(0, (workArea.Width - targetWidth) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - targetHeight) / 2),
            targetWidth,
            targetHeight));
        _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app_icon.ico"));
        Closed += OnClosed;

        _preview = new RadialMenuControl(_runtime.Theme) { EnableBackdropMaterial = false };
        WheelPreviewHost.Content = _preview;
        RootLayout.Loaded += (_, _) =>
        {
            _runtime.ApplyTheme(RootLayout);
            ReloadFromConfiguration();
        };
        _runtime.Touch.StatusChanged += TouchOnStatusChanged;
        _runtime.Theme.Changed += (_, _) =>
        {
            _runtime.ApplyTheme(RootLayout);
            ApplyNativeTitleBarTheme(_runtime.Configuration.Current.AppTheme);
        };
    }

    public FrameworkElement RootElement => RootLayout;

    public void ShowSettings()
    {
        _appWindow.Show();
        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    public void ReloadFromConfiguration()
    {
        if (RootLayout is null)
        {
            return;
        }

        _updating = true;
        try
        {
            AppConfig config = _runtime.Configuration.Current;
            TouchTriggerConfig touch = config.TouchTrigger;
            WheelProfile profile = _runtime.Configuration.GetGlobalProfile();

            WheelRadiusSlider.Value = config.WheelRadius;
            InnerRadiusSlider.Value = Math.Min(config.InnerRadius, Math.Max(28, config.WheelRadius - 32));
            SectorGapSlider.Value = config.SectorGap;
            IconSizeSlider.Value = config.SectorIconSize;
            FontSizeSlider.Value = config.SectorFontSize;
            WheelRadiusValueText.Text = $"{config.WheelRadius:0} px";
            InnerRadiusValueText.Text = $"{config.InnerRadius:0} px";
            SectorGapValueText.Text = $"{config.SectorGap:0.#} px";
            IconSizeValueText.Text = $"{config.SectorIconSize:0.#} px";
            FontSizeValueText.Text = $"{config.SectorFontSize:0.#} px";
            ShowTextToggle.IsOn = config.ShowText;
            CoreTitleTextBox.Text = config.CoreTitle;

            SelectByTag(ThemeComboBox, config.AppTheme);
            SelectByTag(MaterialComboBox, config.WheelMaterial);
            SystemAccentToggle.IsOn = config.UseSystemAccentColor;
            CustomColorPanel.Opacity = config.UseSystemAccentColor ? 0.45 : 1;
            CustomColorPanel.IsHitTestVisible = !config.UseSystemAccentColor;
            SectorColorTextBox.Text = config.CustomSectorBg;
            BorderColorTextBox.Text = config.CustomSectorBorder;
            HighlightColorTextBox.Text = config.CustomHighlightBg;
            TextColorTextBox.Text = config.CustomText;
            CoreColorTextBox.Text = config.CustomCoreBg;

            SelectByTag(SectorCountComboBox, NormalizeSectorCount(profile.SectorCount).ToString());
            MultiTierToggle.IsOn = config.EnableMultiTier;
            SelectByTag(SubmenuStyleComboBox, config.SubmenuStyle);
            ActionListView.ItemsSource = BuildActionRows(profile);

            SelectByTag(MouseButtonComboBox, config.Trigger.MouseButton);
            MouseLongPressToggle.IsOn = config.LongPressTrigger;
            MouseThresholdSlider.Value = config.DragThreshold;
            MouseThresholdValueText.Text = $"{config.DragThreshold:0} px";

            TouchEnabledToggle.IsOn = touch.Enabled;
            OneFingerCheckBox.IsChecked = touch.EnableOneFinger;
            TwoFingerCheckBox.IsChecked = touch.EnableTwoFinger;
            ThreeFingerCheckBox.IsChecked = touch.EnableThreeFinger;
            LongPressSlider.Value = touch.LongPressDelayMs;
            ToleranceSlider.Value = touch.HoldMovementTolerance;
            SwipeThresholdSlider.Value = touch.SwipeThreshold;
            LongPressValueText.Text = $"{touch.LongPressDelayMs:0} ms";
            ToleranceValueText.Text = $"{touch.HoldMovementTolerance:0} px";
            SwipeThresholdValueText.Text = $"{touch.SwipeThreshold:0} px";
            SelectByTag(DirectionCountComboBox, touch.DirectionCount.ToString());

            DisableFullscreenToggle.IsOn = config.DisableOnFullScreen;
            OuterEscapeToggle.IsOn = config.EnableOuterEscapeCancel;
            OuterEscapeDistanceSlider.Value = config.OuterEscapeDistance;
            OuterEscapeDistanceValueText.Text = $"{config.OuterEscapeDistance:0} px";
            SelectByTag(IsolationModeComboBox, config.IsolationMode);
            BlacklistTextBox.Text = string.Join(", ", config.BlacklistedProcesses);
            WhitelistTextBox.Text = string.Join(", ", config.WhitelistedProcesses);
            SelectByTag(LanguageComboBox, config.Language);
            AutoUpdateToggle.IsOn = config.AutoCheckUpdate;

            _runtime.Theme.Apply(RootLayout, config.AppTheme);
            ApplyNativeTitleBarTheme(config.AppTheme);
            RefreshWheelPreview(config, profile);
            UpdateTouchStatus(_runtime.Touch.Status, StatusMessage(_runtime.Touch.Status));
        }
        finally
        {
            _updating = false;
        }
    }

    private void RefreshWheelPreview(AppConfig config, WheelProfile profile)
    {
        int count = NormalizeSectorCount(profile.SectorCount);
        List<ActionItem> actions = profile.Actions.Take(count).ToList();
        while (actions.Count < count)
        {
            actions.Add(new ActionItem { Type = "None", Name = "未配置" });
        }
        _preview.Configure(config, actions, count);
    }

    private void WheelAppearanceSlider_OnValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_updating || WheelRadiusSlider is null)
        {
            return;
        }
        double wheelRadius = WheelRadiusSlider.Value;
        double innerRadius = Math.Min(InnerRadiusSlider.Value, wheelRadius - 32);
        WheelRadiusValueText.Text = $"{wheelRadius:0} px";
        InnerRadiusValueText.Text = $"{innerRadius:0} px";
        SectorGapValueText.Text = $"{SectorGapSlider.Value:0.#} px";
        IconSizeValueText.Text = $"{IconSizeSlider.Value:0.#} px";
        FontSizeValueText.Text = $"{FontSizeSlider.Value:0.#} px";
        _runtime.Configuration.Update(config =>
        {
            config.WheelRadius = wheelRadius;
            config.InnerRadius = innerRadius;
            config.SectorGap = SectorGapSlider.Value;
            config.SectorIconSize = IconSizeSlider.Value;
            config.SectorFontSize = FontSizeSlider.Value;
        });
    }

    private void ShowTextToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.ShowText = ShowTextToggle.IsOn);
        }
    }

    private void CoreTitleTextBox_OnLostFocus(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.CoreTitle = CoreTitleTextBox.Text.Trim());
        }
    }

    private void CustomColorTextBox_OnLostFocus(object sender, RoutedEventArgs args)
    {
        if (_updating)
        {
            return;
        }
        string[] colors =
        [
            SectorColorTextBox.Text,
            BorderColorTextBox.Text,
            HighlightColorTextBox.Text,
            TextColorTextBox.Text,
            CoreColorTextBox.Text
        ];
        if (colors.Any(color => !IsHexColor(color)))
        {
            ShowConfigurationMessage("颜色格式无效，请使用 #RRGGBB 或 #AARRGGBB。", InfoBarSeverity.Warning);
            ReloadFromConfiguration();
            return;
        }
        _runtime.Configuration.Update(config =>
        {
            config.CustomSectorBg = NormalizeHex(colors[0]);
            config.CustomSectorBorder = NormalizeHex(colors[1]);
            config.CustomHighlightBg = NormalizeHex(colors[2]);
            config.CustomText = NormalizeHex(colors[3]);
            config.CustomCoreBg = NormalizeHex(colors[4]);
        });
    }

    private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_updating && SelectedTag(ThemeComboBox) is string value)
        {
            _runtime.Configuration.Update(config => config.AppTheme = value);
        }
    }

    private void MaterialComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_updating && SelectedTag(MaterialComboBox) is string value)
        {
            _runtime.Configuration.Update(config => config.WheelMaterial = value);
        }
    }

    private void SystemAccentToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.UseSystemAccentColor = SystemAccentToggle.IsOn);
        }
    }

    private void SectorCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updating || SelectedTag(SectorCountComboBox) is not string value || !int.TryParse(value, out int count))
        {
            return;
        }
        _runtime.Configuration.Update(config =>
        {
            WheelProfile profile = GetGlobalProfile(config);
            profile.SectorCount = count;
            EnsureActionCount(profile.Actions, count);
        });
    }

    private void MultiTierToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.EnableMultiTier = MultiTierToggle.IsOn);
        }
    }

    private void SubmenuStyleComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_updating && SelectedTag(SubmenuStyleComboBox) is string value)
        {
            _runtime.Configuration.Update(config => config.SubmenuStyle = value);
        }
    }

    private async void EditActionButton_OnClick(object sender, RoutedEventArgs args)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out int index))
        {
            return;
        }
        WheelProfile profile = _runtime.Configuration.GetGlobalProfile();
        EnsureActionCount(profile.Actions, NormalizeSectorCount(profile.SectorCount));
        if (index < 0 || index >= profile.Actions.Count)
        {
            return;
        }

        ActionItem action = profile.Actions[index];
        TextBox nameBox = new() { Header = "显示名称", Text = action.Name };
        ComboBox typeBox = new()
        {
            Header = "动作类型",
            ItemsSource = new[] { "None", "Hotkey", "Launch", "WebUrl", "Command", "System" },
            SelectedItem = action.Type
        };
        TextBox parameterBox = new()
        {
            Header = "动作参数",
            Text = action.Parameter,
            PlaceholderText = "例如 Ctrl+C、程序路径、网址或命令"
        };
        TextBox argumentsBox = new() { Header = "附加参数", Text = action.Arguments };
        TextBox iconBox = new()
        {
            Header = "内置图标键",
            Text = action.IconKey,
            PlaceholderText = "Copy / Paste / Undo / Search / Terminal ..."
        };
        ComboBox layoutBox = new()
        {
            Header = "此扇区排版",
            ItemsSource = new[] { "Inherit", "IconAndText", "IconOnly", "TextOnly" },
            SelectedItem = action.LayoutMode ?? "Inherit"
        };
        StackPanel content = new() { Spacing = 12, MinWidth = 440 };
        content.Children.Add(nameBox);
        content.Children.Add(typeBox);
        content.Children.Add(parameterBox);
        content.Children.Add(argumentsBox);
        content.Children.Add(iconBox);
        content.Children.Add(layoutBox);
        ContentDialog dialog = new()
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = $"编辑 {DirectionName(index, NormalizeSectorCount(profile.SectorCount))} 动作",
            Content = new ScrollViewer { Content = content, MaxHeight = 520 },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _runtime.Configuration.Update(config =>
        {
            WheelProfile currentProfile = GetGlobalProfile(config);
            EnsureActionCount(currentProfile.Actions, index + 1);
            ActionItem current = currentProfile.Actions[index];
            current.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "未命名动作" : nameBox.Text.Trim();
            current.Type = typeBox.SelectedItem?.ToString() ?? "None";
            current.Parameter = parameterBox.Text.Trim();
            current.Arguments = argumentsBox.Text.Trim();
            current.IconKey = iconBox.Text.Trim();
            current.LayoutMode = layoutBox.SelectedItem?.ToString() ?? "Inherit";
        });
    }

    private void MouseButtonComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updating || SelectedTag(MouseButtonComboBox) is not string value)
        {
            return;
        }
        _runtime.Configuration.Update(config =>
        {
            config.Trigger.TriggerType = "Mouse";
            config.Trigger.MouseButton = value;
            config.TriggerButton = value;
        });
    }

    private void MouseLongPressToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.LongPressTrigger = MouseLongPressToggle.IsOn);
        }
    }

    private void MouseSlider_OnValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_updating || MouseThresholdSlider is null)
        {
            return;
        }
        MouseThresholdValueText.Text = $"{MouseThresholdSlider.Value:0} px";
        _runtime.Configuration.Update(config => config.DragThreshold = MouseThresholdSlider.Value);
    }

    private void TouchEnabledToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.TouchTrigger.Enabled = TouchEnabledToggle.IsOn);
        }
    }

    private void FingerCheckBox_OnClick(object sender, RoutedEventArgs args)
    {
        if (_updating)
        {
            return;
        }
        _runtime.Configuration.Update(config =>
        {
            config.TouchTrigger.EnableOneFinger = OneFingerCheckBox.IsChecked == true;
            config.TouchTrigger.EnableTwoFinger = TwoFingerCheckBox.IsChecked == true;
            config.TouchTrigger.EnableThreeFinger = ThreeFingerCheckBox.IsChecked == true;
        });
    }

    private void TouchSlider_OnValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_updating || LongPressSlider is null || ToleranceSlider is null || SwipeThresholdSlider is null)
        {
            return;
        }
        LongPressValueText.Text = $"{LongPressSlider.Value:0} ms";
        ToleranceValueText.Text = $"{ToleranceSlider.Value:0} px";
        SwipeThresholdValueText.Text = $"{SwipeThresholdSlider.Value:0} px";
        _runtime.Configuration.Update(config =>
        {
            config.TouchTrigger.LongPressDelayMs = LongPressSlider.Value;
            config.TouchTrigger.HoldMovementTolerance = ToleranceSlider.Value;
            config.TouchTrigger.SwipeThreshold = SwipeThresholdSlider.Value;
        });
    }

    private void DirectionCountComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_updating || SelectedTag(DirectionCountComboBox) is not string value || !int.TryParse(value, out int count))
        {
            return;
        }
        _runtime.Configuration.Update(config => config.TouchTrigger.DirectionCount = count);
    }

    private void SceneToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (_updating)
        {
            return;
        }
        _runtime.Configuration.Update(config =>
        {
            config.DisableOnFullScreen = DisableFullscreenToggle.IsOn;
            config.EnableOuterEscapeCancel = OuterEscapeToggle.IsOn;
        });
    }

    private void SceneSlider_OnValueChanged(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_updating || OuterEscapeDistanceSlider is null)
        {
            return;
        }
        OuterEscapeDistanceValueText.Text = $"{OuterEscapeDistanceSlider.Value:0} px";
        _runtime.Configuration.Update(config => config.OuterEscapeDistance = OuterEscapeDistanceSlider.Value);
    }

    private void IsolationModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_updating && SelectedTag(IsolationModeComboBox) is string value)
        {
            _runtime.Configuration.Update(config => config.IsolationMode = value);
        }
    }

    private void SceneTextBox_OnLostFocus(object sender, RoutedEventArgs args)
    {
        if (_updating)
        {
            return;
        }
        _runtime.Configuration.Update(config =>
        {
            config.BlacklistedProcesses = SplitProcessList(BlacklistTextBox.Text);
            config.WhitelistedProcesses = SplitProcessList(WhitelistTextBox.Text);
        });
    }

    private async void ExportConfigButton_OnClick(object sender, RoutedEventArgs args)
    {
        try
        {
            FileSavePicker picker = new();
            InitializeWithWindow.Initialize(picker, _hwnd);
            picker.SuggestedFileName = $"StarPie-config-{DateTime.Now:yyyyMMdd}";
            picker.FileTypeChoices.Add("StarPie JSON 配置", [".json"]);
            StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }
            _runtime.Configuration.ExportTo(file.Path);
            ShowConfigurationMessage("配置已完整导出。", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            AppLog.Error("Unable to export configuration", exception);
            ShowConfigurationMessage($"导出失败：{exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportConfigButton_OnClick(object sender, RoutedEventArgs args)
    {
        try
        {
            FileOpenPicker picker = new();
            InitializeWithWindow.Initialize(picker, _hwnd);
            picker.FileTypeFilter.Add(".json");
            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }
            _runtime.Configuration.ImportFrom(file.Path);
            ReloadFromConfiguration();
            ShowConfigurationMessage("配置已导入并立即应用。", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            AppLog.Error("Unable to import configuration", exception);
            ShowConfigurationMessage($"导入失败：{exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void OpenLogFolderButton_OnClick(object sender, RoutedEventArgs args)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarPie");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    private void LanguageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_updating && SelectedTag(LanguageComboBox) is string value)
        {
            _runtime.Configuration.Update(config => config.Language = value);
        }
    }

    private void AdvancedToggle_OnToggled(object sender, RoutedEventArgs args)
    {
        if (!_updating)
        {
            _runtime.Configuration.Update(config => config.AutoCheckUpdate = AutoUpdateToggle.IsOn);
        }
    }

    private async void ShowOverlayPreviewButton_OnClick(object sender, RoutedEventArgs args) =>
        await ShowOverlayPreviewForTestingAsync();

    public async Task ShowOverlayPreviewForTestingAsync()
    {
        PointInt32 position = _appWindow.Position;
        SizeInt32 size = _appWindow.Size;
        _runtime.Wheel.ShowPreview(new TouchPoint(position.X + size.Width / 2.0, position.Y + size.Height / 2.0));
        await Task.Delay(3000);
        _runtime.Wheel.Hide();
    }

    private void TouchOnStatusChanged(object? sender, GlobalTouchStatusChangedEventArgs args) =>
        UpdateTouchStatus(args.Status, args.Message);

    private void UpdateTouchStatus(GlobalTouchStatus status, string message)
    {
        TouchStatusInfoBar.Severity = status switch
        {
            GlobalTouchStatus.Active => InfoBarSeverity.Success,
            GlobalTouchStatus.Disabled => InfoBarSeverity.Informational,
            GlobalTouchStatus.UiAccessRequired => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Error
        };
        TouchStatusInfoBar.Message = message;
    }

    private void ShowConfigurationMessage(string message, InfoBarSeverity severity)
    {
        ConfigOperationInfoBar.Message = message;
        ConfigOperationInfoBar.Severity = severity;
        ConfigOperationInfoBar.IsOpen = true;
    }

    private static string StatusMessage(GlobalTouchStatus status) => status switch
    {
        GlobalTouchStatus.Active => "全局触摸触发正常；非手势触摸实时透传",
        GlobalTouchStatus.UiAccessRequired => "开发构建未启用 UIAccess；请使用签名发布包测试全局触摸。",
        GlobalTouchStatus.RegistrationConflict => "系统中已有另一个全局触摸捕获目标。",
        GlobalTouchStatus.Error => "触摸服务启动失败，请查看日志。",
        _ => "全局触摸触发未启用"
    };

    private static List<ActionSlotRow> BuildActionRows(WheelProfile profile)
    {
        int count = NormalizeSectorCount(profile.SectorCount);
        List<ActionSlotRow> rows = new(count);
        for (int index = 0; index < count; index++)
        {
            ActionItem action = index < profile.Actions.Count
                ? profile.Actions[index]
                : new ActionItem { Type = "None", Name = "未配置" };
            string summary = string.IsNullOrWhiteSpace(action.Parameter)
                ? action.Type
                : $"{action.Type} · {action.Parameter}";
            rows.Add(new ActionSlotRow(index, DirectionName(index, count), action.Name, summary));
        }
        return rows;
    }

    private static string DirectionName(int index, int count)
    {
        if (count == 4)
        {
            return DirectionNames4[index % 4];
        }
        if (count == 8)
        {
            return DirectionNames8[index % 8];
        }
        int degrees = index * 30;
        return degrees == 0 ? "上" : $"{degrees}°";
    }

    private static WheelProfile GetGlobalProfile(AppConfig config) =>
        config.Profiles.FirstOrDefault(profile =>
            profile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase)) ?? config.Profiles[0];

    private static void EnsureActionCount(List<ActionItem> actions, int count)
    {
        while (actions.Count < count)
        {
            actions.Add(new ActionItem { Type = "None", Name = "未配置" });
        }
    }

    private static List<string> SplitProcessList(string value) => value
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static bool IsHexColor(string value)
    {
        string text = value.Trim().TrimStart('#');
        return text.Length is 6 or 8 && text.All(Uri.IsHexDigit);
    }

    private static string NormalizeHex(string value)
    {
        string text = value.Trim().TrimStart('#').ToUpperInvariant();
        return text.Length == 6 ? $"#FF{text}" : $"#{text}";
    }

    private static int NormalizeSectorCount(int count) => count switch
    {
        4 => 4,
        12 => 12,
        _ => 8
    };

    private static void SelectByTag(ComboBox comboBox, string? tag)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            ?? comboBox.Items[0];
    }

    private static object? SelectedTag(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Tag;

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }
        args.Handled = true;
        _appWindow.Hide();
    }

    private void ApplyNativeTitleBarTheme(string mode)
    {
        int dark = string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase) && _runtime.Theme.IsSystemDark()
            ? 1
            : 0;
        DwmSetWindowAttribute(_hwnd, 20, ref dark, sizeof(int));
    }

    public sealed record ActionSlotRow(int Index, string Direction, string Name, string Summary);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
