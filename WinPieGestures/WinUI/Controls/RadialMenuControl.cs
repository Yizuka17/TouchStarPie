using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Services;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace WinPieGestures.WinUI.Controls;

public sealed class RadialMenuControl : Grid
{
    private readonly Canvas _canvas = new();
    private readonly List<XamlPath> _sectorPaths = [];
    private readonly List<FrameworkElement> _labels = [];
    private readonly Border _core = new();
    private readonly TextBlock _coreTitle = new();
    private readonly SystemThemeService _themeService;
    private AppConfig _config = new();
    private IReadOnlyList<ActionItem> _actions = [];
    private Brush? _normalBrush;
    private Brush? _highlightBrush;
    private Brush? _borderBrush;
    private int _sectorCount = 8;
    private int _selectedIndex = -1;
    private double _outerRadius = 138;
    private double _innerRadius = 52;
    private double _padding = 28;

    public RadialMenuControl(SystemThemeService themeService)
    {
        _themeService = themeService;
        Children.Add(_canvas);
        IsHitTestVisible = true;
        Background = null;
        PointerMoved += OnPointerMoved;
        PointerExited += (_, _) => SelectSector(-1);
        ActualThemeChanged += (_, _) => Rebuild();
        _themeService.Changed += (_, _) => Rebuild();
    }

    public int SelectedIndex => _selectedIndex;

    /// <summary>Disable desktop backdrop sampling for the in-window preview surface.</summary>
    public bool EnableBackdropMaterial { get; set; } = true;

    public event EventHandler<int>? SelectionChanged;

    public void Configure(AppConfig config, IReadOnlyList<ActionItem> actions, int? sectorCount = null)
    {
        _config = config;
        _actions = actions;
        _sectorCount = Math.Clamp(sectorCount ?? actions.Count, 4, 12);
        if (_sectorCount is not (4 or 8 or 12))
        {
            _sectorCount = _sectorCount < 7 ? 4 : 8;
        }
        _outerRadius = Math.Clamp(config.WheelRadius, 92, 240);
        _innerRadius = Math.Clamp(config.InnerRadius, 28, _outerRadius - 32);
        _padding = Math.Max(26, config.HighlightGlowRadius * 0.55);
        Width = Height = (_outerRadius + _padding) * 2;
        Rebuild();
    }

    public void SelectSector(int index)
    {
        int next = index >= 0 && index < _sectorCount ? index : -1;
        if (next == _selectedIndex)
        {
            return;
        }

        int previous = _selectedIndex;
        _selectedIndex = next;
        if (previous >= 0 && previous < _sectorPaths.Count)
        {
            _sectorPaths[previous].Fill = _normalBrush;
            _sectorPaths[previous].Opacity = 1;
        }
        if (next >= 0 && next < _sectorPaths.Count)
        {
            _sectorPaths[next].Fill = _highlightBrush;
            _sectorPaths[next].Opacity = 1;
        }
        _coreTitle.Text = next >= 0 && next < _actions.Count ? _actions[next].Name : _config.CoreTitle;
        SelectionChanged?.Invoke(this, next);
    }

    private void Rebuild()
    {
        _canvas.Children.Clear();
        _sectorPaths.Clear();
        _labels.Clear();
        _selectedIndex = -1;

        WheelPalette palette = _themeService.CreateWheelPalette(ActualTheme, _config);
        _normalBrush = CreateMaterialBrush(palette.Sector, _config.WheelMaterial, 0.76);
        _highlightBrush = CreateMaterialBrush(palette.Accent, _config.WheelMaterial, 0.9);
        _borderBrush = new SolidColorBrush(palette.SectorBorder);

        double total = (_outerRadius + _padding) * 2;
        Width = Height = total;
        _canvas.Width = _canvas.Height = total;
        double center = total / 2;
        double step = Math.Tau / _sectorCount;
        double angularGap = Math.Min(step * 0.18, Math.Max(0.006, _config.SectorGap / _outerRadius));

        for (int index = 0; index < _sectorCount; index++)
        {
            double centerAngle = -Math.PI / 2 + index * step;
            double start = centerAngle - step / 2 + angularGap / 2;
            double end = centerAngle + step / 2 - angularGap / 2;
            XamlPath path = new()
            {
                Data = CreateAnnularSector(center, center, _innerRadius, _outerRadius, start, end),
                Fill = _normalBrush,
                Stroke = _borderBrush,
                StrokeThickness = 1,
                UseLayoutRounding = true
            };
            _sectorPaths.Add(path);
            _canvas.Children.Add(path);

            FrameworkElement label = CreateLabel(index, palette.Text);
            double labelRadius = (_innerRadius + _outerRadius) / 2;
            double x = center + Math.Cos(centerAngle) * labelRadius;
            double y = center + Math.Sin(centerAngle) * labelRadius;
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            _labels.Add(label);
            _canvas.Children.Add(label);
        }

        _core.Width = _core.Height = _innerRadius * 2 - 5;
        _core.CornerRadius = new CornerRadius(_innerRadius);
        _core.Background = CreateMaterialBrush(palette.Core, _config.WheelMaterial, 0.88);
        _core.BorderBrush = new SolidColorBrush(palette.SectorBorder);
        _core.BorderThickness = new Thickness(1);
        _core.Child = _coreTitle;
        _coreTitle.Text = string.IsNullOrWhiteSpace(_config.CoreTitle) ? "StarPie" : _config.CoreTitle;
        _coreTitle.Foreground = new SolidColorBrush(palette.Text);
        _coreTitle.FontFamily = new FontFamily("Segoe UI Variable Display, Microsoft YaHei UI");
        _coreTitle.FontSize = Math.Clamp(_config.CoreFontSize, 11, 22);
        _coreTitle.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _coreTitle.TextAlignment = TextAlignment.Center;
        _coreTitle.TextWrapping = TextWrapping.Wrap;
        _coreTitle.VerticalAlignment = VerticalAlignment.Center;
        _coreTitle.HorizontalAlignment = HorizontalAlignment.Center;
        Canvas.SetLeft(_core, center - _core.Width / 2);
        Canvas.SetTop(_core, center - _core.Height / 2);
        _canvas.Children.Add(_core);
    }

    private FrameworkElement CreateLabel(int index, Color foreground)
    {
        ActionItem? action = index < _actions.Count ? _actions[index] : null;
        string layout = action?.LayoutMode ?? "Inherit";
        StackPanel panel = new()
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false,
            Spacing = 2,
            Translation = new System.Numerics.Vector3(
                (float)(action?.CustomTextOffsetX ?? _config.SectorTextOffsetX),
                (float)(action?.CustomTextOffsetY ?? _config.SectorTextOffsetY),
                0)
        };
        FontIcon icon = new()
        {
            Glyph = ActionIconCatalog.Resolve(action),
            FontFamily = new FontFamily(ActionIconCatalog.FontFamilyName),
            FontSize = Math.Clamp(action?.CustomIconSize ?? _config.SectorIconSize, 14, 32),
            Foreground = new SolidColorBrush(ParseColor(action?.CustomTextColor, foreground)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        bool showIcon = !string.Equals(layout, "TextOnly", StringComparison.OrdinalIgnoreCase);
        bool showText = !string.Equals(layout, "IconOnly", StringComparison.OrdinalIgnoreCase) &&
                        (_config.ShowText || string.Equals(layout, "TextOnly", StringComparison.OrdinalIgnoreCase));
        TextBlock text = new()
        {
            Text = action?.Name ?? string.Empty,
            FontSize = Math.Clamp(action?.CustomFontSize ?? _config.SectorFontSize, 9, 18),
            FontFamily = new FontFamily(string.IsNullOrWhiteSpace(action?.CustomFontFamily)
                ? _config.WheelFontFamily
                : action.CustomFontFamily),
            Foreground = new SolidColorBrush(ParseColor(action?.CustomTextColor, foreground)),
            MaxWidth = 78,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center
        };
        bool textAbove = string.Equals(
            action?.CustomTextPlacement is null or "" or "Inherit"
                ? _config.SectorTextPlacement
                : action.CustomTextPlacement,
            "Above",
            StringComparison.OrdinalIgnoreCase);
        if (showText && textAbove)
        {
            panel.Children.Add(text);
        }
        if (showIcon)
        {
            panel.Children.Add(icon);
        }
        if (showText && !textAbove)
        {
            panel.Children.Add(text);
        }
        return panel;
    }

    private Brush CreateMaterialBrush(Color color, string material, double tintOpacity)
    {
        if (EnableBackdropMaterial && string.Equals(material, "Acrylic", StringComparison.OrdinalIgnoreCase))
        {
            return new AcrylicBrush
            {
                TintColor = color,
                TintOpacity = tintOpacity,
                TintLuminosityOpacity = 0.74,
                FallbackColor = color
            };
        }
        return new SolidColorBrush(color);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        Point point = args.GetCurrentPoint(this).Position;
        double center = ActualWidth / 2;
        double dx = point.X - center;
        double dy = point.Y - center;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < _innerRadius || distance > _outerRadius)
        {
            SelectSector(-1);
            return;
        }
        SelectSector(RadialSelectionMath.QuantizeMain(Math.Atan2(dy, dx), _sectorCount));
    }

    private static PathGeometry CreateAnnularSector(
        double centerX,
        double centerY,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle)
    {
        Point outerStart = Polar(centerX, centerY, outerRadius, startAngle);
        Point outerEnd = Polar(centerX, centerY, outerRadius, endAngle);
        Point innerEnd = Polar(centerX, centerY, innerRadius, endAngle);
        Point innerStart = Polar(centerX, centerY, innerRadius, startAngle);
        bool largeArc = endAngle - startAngle > Math.PI;

        PathFigure figure = new() { StartPoint = outerStart, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment
        {
            Point = outerEnd,
            Size = new Size(outerRadius, outerRadius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = largeArc
        });
        figure.Segments.Add(new LineSegment { Point = innerEnd });
        figure.Segments.Add(new ArcSegment
        {
            Point = innerStart,
            Size = new Size(innerRadius, innerRadius),
            SweepDirection = SweepDirection.Counterclockwise,
            IsLargeArc = largeArc
        });
        figure.Segments.Add(new LineSegment { Point = outerStart });

        PathGeometry geometry = new();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Point Polar(double centerX, double centerY, double radius, double angle) =>
        new(centerX + Math.Cos(angle) * radius, centerY + Math.Sin(angle) * radius);

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
