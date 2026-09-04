using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace WinPieGestures.WinUI.Services;

public readonly record struct WheelPalette(
    Color Accent,
    Color AccentSoft,
    Color Sector,
    Color SectorBorder,
    Color Text,
    Color Core);

public sealed class SystemThemeService : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly UISettings _settings = new();

    public SystemThemeService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _settings.ColorValuesChanged += SettingsOnColorValuesChanged;
    }

    public event EventHandler? Changed;

    public void Apply(FrameworkElement root, string mode)
    {
        root.RequestedTheme = mode switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    public WheelPalette CreateWheelPalette(ElementTheme actualTheme, AppConfig config)
    {
        bool dark = actualTheme == ElementTheme.Dark ||
                    actualTheme == ElementTheme.Default && IsSystemDark();
        Color accent = config.UseSystemAccentColor
            ? _settings.GetColorValue(UIColorType.Accent)
            : ParseColor(config.CustomHighlightBg, Color.FromArgb(224, 108, 77, 255));
        Color sector = config.UseSystemAccentColor
            ? dark ? Color.FromArgb(224, 24, 24, 27) : Color.FromArgb(232, 249, 250, 251)
            : ParseColor(config.CustomSectorBg, Color.FromArgb(224, 24, 24, 27));
        Color border = config.UseSystemAccentColor
            ? dark ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(52, 0, 0, 0)
            : ParseColor(config.CustomSectorBorder, Color.FromArgb(90, 255, 255, 255));
        Color text = config.UseSystemAccentColor
            ? dark ? Color.FromArgb(255, 248, 250, 252) : Color.FromArgb(255, 20, 24, 32)
            : ParseColor(config.CustomText, Color.FromArgb(255, 248, 250, 252));
        Color core = config.UseSystemAccentColor
            ? dark ? Color.FromArgb(242, 15, 23, 42) : Color.FromArgb(245, 255, 255, 255)
            : ParseColor(config.CustomCoreBg, Color.FromArgb(242, 15, 23, 42));

        return new WheelPalette(
            accent,
            Mix(accent, dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0), dark ? 0.12 : 0.06),
            sector,
            border,
            text,
            core);
    }

    public WheelPalette CreateWheelPalette(string themeMode, AppConfig config)
    {
        ElementTheme theme = themeMode switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => IsSystemDark() ? ElementTheme.Dark : ElementTheme.Light
        };
        return CreateWheelPalette(theme, config);
    }

    public bool IsSystemDark()
    {
        Color background = _settings.GetColorValue(UIColorType.Background);
        return RelativeLuminance(background) < 0.5;
    }

    public void Dispose()
    {
        _settings.ColorValuesChanged -= SettingsOnColorValuesChanged;
        GC.SuppressFinalize(this);
    }

    private void SettingsOnColorValuesChanged(UISettings sender, object args)
    {
        _dispatcherQueue.TryEnqueue(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    private static double RelativeLuminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;

    private static Color Mix(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Blend(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return Color.FromArgb(Blend(from.A, to.A), Blend(from.R, to.R), Blend(from.G, to.G), Blend(from.B, to.B));
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        string text = value.Trim().TrimStart('#');
        if (text.Length == 6)
        {
            text = "FF" + text;
        }
        return text.Length == 8 && uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out uint argb)
            ? Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)
            : fallback;
    }
}
