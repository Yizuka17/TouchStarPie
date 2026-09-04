using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Services;
using DrawingColor = System.Drawing.Color;
using UiColor = Windows.UI.Color;

namespace WinPieGestures.WinUI.Views;

/// <summary>
/// Per-pixel-alpha desktop surface for the live wheel. WinUI 3 owns the application and
/// its reusable wheel control; the top-level overlay uses UpdateLayeredWindow because a
/// WinUI desktop swapchain is opaque and otherwise exposes a black rectangular HWND.
/// </summary>
public sealed class RadialMenuWindow : IDisposable
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopMost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint UlwAlpha = 0x00000002;
    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
    private static readonly nint HwndTopMost = new(-1);

    private readonly SystemThemeService _themeService;
    private readonly NativeWindowProc _windowProcedure;
    private readonly string _windowClassName;
    private readonly nint _window;
    private AppConfig _config = new();
    private IReadOnlyList<ActionItem> _actions = [];
    private int _sectorCount = 8;
    private int _selectedIndex = -1;
    private int _left;
    private int _top;
    private int _width;
    private int _height;
    private double _scale = 1;
    private bool _visible;

    public RadialMenuWindow(SystemThemeService themeService)
    {
        _themeService = themeService;
        _themeService.Changed += ThemeServiceOnChanged;
        _windowProcedure = WindowProcedure;
        _windowClassName = $"StarPie.LayeredWheel.{Environment.ProcessId}";
        NativeWindowClass windowClass = new()
        {
            Size = (uint)Marshal.SizeOf<NativeWindowClass>(),
            Instance = GetModuleHandle(null),
            WindowProcedure = _windowProcedure,
            ClassName = _windowClassName
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException($"Unable to register wheel window: {Marshal.GetLastWin32Error()}");
        }

        _window = CreateWindowEx(
            WsExTopMost | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate,
            _windowClassName,
            "StarPie Wheel",
            WsPopup,
            0,
            0,
            0,
            0,
            0,
            0,
            windowClass.Instance,
            0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"Unable to create wheel window: {Marshal.GetLastWin32Error()}");
        }
    }

    public int SelectedIndex => _selectedIndex;

    public void Configure(AppConfig config, IReadOnlyList<ActionItem> actions, int sectorCount)
    {
        _config = config;
        _actions = actions;
        _sectorCount = sectorCount is 4 or 8 or 12 ? sectorCount : 8;
        _selectedIndex = -1;
    }

    public void ShowAt(TouchPoint center)
    {
        SetWindowPos(
            _window,
            HwndTopMost,
            (int)Math.Round(center.X),
            (int)Math.Round(center.Y),
            0,
            0,
            SwpNoSize | SwpNoActivate);
        _scale = Math.Max(1, GetDpiForWindow(_window) / 96.0);
        double padding = Math.Max(26, _config.HighlightGlowRadius * 0.55);
        _width = _height = (int)Math.Ceiling((_config.WheelRadius + padding) * 2 * _scale);
        _left = (int)Math.Round(center.X - _width / 2.0);
        _top = (int)Math.Round(center.Y - _height / 2.0);
        _visible = true;
        Render();
        SetWindowPos(
            _window,
            HwndTopMost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    public void UpdateDirection(int directionIndex)
    {
        int next = directionIndex >= 0 && directionIndex < _sectorCount ? directionIndex : -1;
        if (next == _selectedIndex)
        {
            return;
        }
        _selectedIndex = next;
        if (_visible)
        {
            Render();
        }
    }

    public void Hide()
    {
        _visible = false;
        ShowWindow(_window, 0);
    }

    public void Dispose()
    {
        _themeService.Changed -= ThemeServiceOnChanged;
        if (_window != 0)
        {
            DestroyWindow(_window);
        }
        GC.SuppressFinalize(this);
    }

    private void ThemeServiceOnChanged(object? sender, EventArgs args)
    {
        if (_visible)
        {
            Render();
        }
    }

    private void Render()
    {
        if (_width <= 0 || _height <= 0)
        {
            return;
        }

        using Bitmap bitmap = new(_width, _height, PixelFormat.Format32bppPArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        WheelPalette palette = _themeService.CreateWheelPalette(_config.AppTheme, _config);
        DrawingColor sectorColor = ToDrawingColor(palette.Sector);
        DrawingColor highlightColor = ToDrawingColor(palette.Accent);
        DrawingColor borderColor = ToDrawingColor(palette.SectorBorder);
        DrawingColor textColor = ToDrawingColor(palette.Text);
        DrawingColor coreColor = ToDrawingColor(palette.Core);
        float outerRadius = (float)(Math.Clamp(_config.WheelRadius, 92, 240) * _scale);
        float innerRadius = (float)(Math.Clamp(_config.InnerRadius, 28, _config.WheelRadius - 32) * _scale);
        float centerX = _width / 2f;
        float centerY = _height / 2f;
        float step = 360f / _sectorCount;
        float angularGap = (float)Math.Min(step * 0.18, Math.Max(0.35, _config.SectorGap / _config.WheelRadius * 180 / Math.PI));

        using SolidBrush normalBrush = new(sectorColor);
        using SolidBrush highlightBrush = new(highlightColor);
        using Pen borderPen = new(borderColor, Math.Max(1, (float)_scale));
        for (int index = 0; index < _sectorCount; index++)
        {
            float centerAngle = -90 + index * step;
            float startAngle = centerAngle - step / 2 + angularGap / 2;
            float sweepAngle = step - angularGap;
            using GraphicsPath path = CreateAnnularSector(centerX, centerY, innerRadius, outerRadius, startAngle, sweepAngle);
            graphics.FillPath(index == _selectedIndex ? highlightBrush : normalBrush, path);
            graphics.DrawPath(borderPen, path);
            DrawAction(graphics, index, centerAngle, centerX, centerY, innerRadius, outerRadius, textColor);
        }

        using SolidBrush coreBrush = new(coreColor);
        RectangleF coreRect = new(centerX - innerRadius + 2, centerY - innerRadius + 2, innerRadius * 2 - 4, innerRadius * 2 - 4);
        graphics.FillEllipse(coreBrush, coreRect);
        graphics.DrawEllipse(borderPen, coreRect);
        string title = _selectedIndex >= 0 && _selectedIndex < _actions.Count
            ? _actions[_selectedIndex].Name
            : string.IsNullOrWhiteSpace(_config.CoreTitle) ? "StarPie" : _config.CoreTitle;
        using Font coreFont = CreateFont(_config.CoreFontFamily, (float)(Math.Clamp(_config.CoreFontSize, 11, 22) * _scale), FontStyle.Bold);
        using SolidBrush textBrush = new(textColor);
        using StringFormat centered = CreateCenteredFormat();
        graphics.DrawString(title, coreFont, textBrush, coreRect, centered);

        Present(bitmap);
    }

    private void DrawAction(
        Graphics graphics,
        int index,
        float angleDegrees,
        float centerX,
        float centerY,
        float innerRadius,
        float outerRadius,
        DrawingColor defaultTextColor)
    {
        ActionItem? action = index < _actions.Count ? _actions[index] : null;
        string layout = action?.LayoutMode ?? "Inherit";
        bool showIcon = !layout.Equals("TextOnly", StringComparison.OrdinalIgnoreCase);
        bool showText = !layout.Equals("IconOnly", StringComparison.OrdinalIgnoreCase) &&
                        (_config.ShowText || layout.Equals("TextOnly", StringComparison.OrdinalIgnoreCase));
        double angle = angleDegrees * Math.PI / 180;
        float radius = (innerRadius + outerRadius) / 2;
        float x = centerX + (float)Math.Cos(angle) * radius + (float)((action?.CustomTextOffsetX ?? _config.SectorTextOffsetX) * _scale);
        float y = centerY + (float)Math.Sin(angle) * radius + (float)((action?.CustomTextOffsetY ?? _config.SectorTextOffsetY) * _scale);
        DrawingColor actionTextColor = ParseColor(action?.CustomTextColor, defaultTextColor);
        using SolidBrush textBrush = new(actionTextColor);
        using StringFormat centered = CreateCenteredFormat();

        float iconSize = (float)(Math.Clamp(action?.CustomIconSize ?? _config.SectorIconSize, 14, 32) * _scale);
        float fontSize = (float)(Math.Clamp(action?.CustomFontSize ?? _config.SectorFontSize, 9, 18) * _scale);
        float separation = showIcon && showText ? (iconSize + fontSize) * 0.34f : 0;
        bool textAbove = string.Equals(
            action?.CustomTextPlacement is null or "" or "Inherit" ? _config.SectorTextPlacement : action.CustomTextPlacement,
            "Above",
            StringComparison.OrdinalIgnoreCase);

        if (showIcon)
        {
            using Font iconFont = CreateFont("Segoe Fluent Icons", iconSize, FontStyle.Regular);
            RectangleF iconRect = new(x - 42 * (float)_scale, y - 20 * (float)_scale - (textAbove ? -separation : separation), 84 * (float)_scale, 40 * (float)_scale);
            graphics.DrawString(GlyphFor(action?.IconKey), iconFont, textBrush, iconRect, centered);
        }
        if (showText)
        {
            using Font textFont = CreateFont(
                string.IsNullOrWhiteSpace(action?.CustomFontFamily) ? _config.WheelFontFamily : action.CustomFontFamily,
                fontSize,
                FontStyle.Regular);
            RectangleF textRect = new(x - 48 * (float)_scale, y - 13 * (float)_scale + (textAbove ? -separation : separation), 96 * (float)_scale, 26 * (float)_scale);
            graphics.DrawString(action?.Name ?? string.Empty, textFont, textBrush, textRect, centered);
        }
    }

    private void Present(Bitmap bitmap)
    {
        nint screenDc = GetDC(0);
        nint memoryDc = CreateCompatibleDC(screenDc);
        nint bitmapHandle = bitmap.GetHbitmap(DrawingColor.FromArgb(0));
        nint previous = SelectObject(memoryDc, bitmapHandle);
        try
        {
            NativePoint destination = new() { X = _left, Y = _top };
            NativeSize size = new() { Width = _width, Height = _height };
            NativePoint source = default;
            BlendFunction blend = new()
            {
                BlendOp = AcSrcOver,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };
            if (!UpdateLayeredWindow(
                    _window,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    UlwAlpha))
            {
                AppLog.Error($"UpdateLayeredWindow failed: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(bitmapHandle);
            DeleteDC(memoryDc);
            ReleaseDC(0, screenDc);
        }
    }

    private static GraphicsPath CreateAnnularSector(
        float centerX,
        float centerY,
        float innerRadius,
        float outerRadius,
        float startAngle,
        float sweepAngle)
    {
        RectangleF outer = new(centerX - outerRadius, centerY - outerRadius, outerRadius * 2, outerRadius * 2);
        RectangleF inner = new(centerX - innerRadius, centerY - innerRadius, innerRadius * 2, innerRadius * 2);
        GraphicsPath path = new();
        path.AddArc(outer, startAngle, sweepAngle);
        path.AddArc(inner, startAngle + sweepAngle, -sweepAngle);
        path.CloseFigure();
        return path;
    }

    private static Font CreateFont(string? familyNames, float size, FontStyle style)
    {
        string family = familyNames?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
                        ?? "Segoe UI";
        try
        {
            return new Font(family, size, style, GraphicsUnit.Pixel);
        }
        catch
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
        }
    }

    private static StringFormat CreateCenteredFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private static DrawingColor ToDrawingColor(UiColor color) =>
        DrawingColor.FromArgb(color.A, color.R, color.G, color.B);

    private static DrawingColor ParseColor(string? value, DrawingColor fallback)
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
            ? DrawingColor.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb)
            : fallback;
    }

    private static string GlyphFor(string? iconKey) => iconKey?.ToLowerInvariant() switch
    {
        "copy" => "\uE8C8",
        "paste" => "\uE77F",
        "cut" => "\uE8C6",
        "undo" => "\uE7A7",
        "redo" => "\uE7A6",
        "search" => "\uE721",
        "save" => "\uE74E",
        "lock" => "\uE72E",
        "screenshot" => "\uE722",
        "volumeup" => "\uE995",
        "volumedown" => "\uE993",
        "showdesktop" => "\uE7C4",
        "terminal" => "\uE756",
        "code" => "\uE943",
        _ => "\uE945"
    };

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam) =>
        DefWindowProc(hwnd, message, wParam, lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint NativeWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeWindowClass
    {
        public uint Size;
        public uint Style;
        public NativeWindowProc WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize { public int Width; public int Height; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref NativeWindowClass windowClass);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(nint hwnd, nint destinationDc, ref NativePoint destination, ref NativeSize size, nint sourceDc, ref NativePoint source, uint colorKey, ref BlendFunction blend, uint flags);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hwnd, nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint CreateCompatibleDC(nint dc);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint dc, nint value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);

}
