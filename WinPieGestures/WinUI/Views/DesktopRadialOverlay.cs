using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using WinPieGestures.WinUI.Input;
using WinPieGestures.WinUI.Services;
using WindowsColor = Windows.UI.Color;

namespace WinPieGestures.WinUI.Views;

/// <summary>
/// No-activate, fully hit-test-transparent desktop overlay. Selection is driven by the
/// global mouse/raw-touch side channels, so the overlay must never become an input target.
/// </summary>
internal sealed class DesktopRadialOverlay : IDisposable
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopMost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExLayered = 0x00080000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const uint WmNcHitTest = 0x0084;
    private const uint WmMouseActivate = 0x0021;
    private const nint HtTransparent = -1;
    private const nint MaNoActivate = 3;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const uint UlwAlpha = 0x00000002;
    private const byte AcSrcAlpha = 0x01;

    private readonly SystemThemeService _themeService;
    private readonly WindowProc _windowProc;
    private nint _window;
    private string? _className;
    private AppConfig _config = new();
    private IReadOnlyList<ActionItem> _actions = [];
    private int _sectorCount = 8;
    private int _mainIndex = -1;
    private int _subIndex = -1;
    private bool _showSubTier;
    private TouchPoint _screenCenter;
    private bool _visible;

    public DesktopRadialOverlay(SystemThemeService themeService)
    {
        _themeService = themeService;
        _windowProc = WindowProcedure;
        _themeService.Changed += ThemeServiceOnChanged;
        CreateWindow();
    }

    public void Configure(AppConfig config, IReadOnlyList<ActionItem> actions, int sectorCount)
    {
        _config = config;
        _actions = actions;
        _sectorCount = sectorCount is 4 or 8 or 12 ? sectorCount : 8;
        _mainIndex = -1;
        _subIndex = -1;
        _showSubTier = false;
        if (_visible)
        {
            Render();
        }
    }

    public void ShowAt(TouchPoint center)
    {
        _screenCenter = center;
        _visible = true;
        Render();
        ShowWindow(_window, SwShowNoActivate);
    }

    public void UpdateSelection(int mainIndex, int subIndex, bool showSubTier)
    {
        if (_mainIndex == mainIndex && _subIndex == subIndex && _showSubTier == showSubTier)
        {
            return;
        }
        _mainIndex = mainIndex;
        _subIndex = subIndex;
        _showSubTier = showSubTier;
        if (_visible)
        {
            Render();
        }
    }

    public void Hide()
    {
        _visible = false;
        _mainIndex = -1;
        _subIndex = -1;
        _showSubTier = false;
        if (_window != 0)
        {
            ShowWindow(_window, SwHide);
        }
    }

    public void Dispose()
    {
        _themeService.Changed -= ThemeServiceOnChanged;
        if (_window != 0)
        {
            DestroyWindow(_window);
            _window = 0;
        }
        if (!string.IsNullOrWhiteSpace(_className))
        {
            UnregisterClass(_className, GetModuleHandle(null));
            _className = null;
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

    private void CreateWindow()
    {
        _className = $"StarPie.DesktopOverlay.{Environment.ProcessId}.{Guid.NewGuid():N}";
        nint instance = GetModuleHandle(null);
        WndClassEx windowClass = new()
        {
            Size = (uint)Marshal.SizeOf<WndClassEx>(),
            WindowProcedure = _windowProc,
            Instance = instance,
            ClassName = _className
        };
        if (RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException($"Unable to register radial overlay class: {Marshal.GetLastWin32Error()}");
        }

        int exStyle = WsExTopMost | WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate;
        _window = CreateWindowEx(
            exStyle,
            _className,
            "StarPie Radial Overlay",
            WsPopup,
            0,
            0,
            1,
            1,
            0,
            0,
            instance,
            0);
        if (_window == 0)
        {
            throw new InvalidOperationException($"Unable to create radial overlay: {Marshal.GetLastWin32Error()}");
        }
        ShowWindow(_window, SwHide);
    }

    private nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        // Explicitly opt out of hit testing even if Windows changes layered-window behavior.
        if (message == WmNcHitTest)
        {
            return HtTransparent;
        }
        if (message == WmMouseActivate)
        {
            return MaNoActivate;
        }
        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void Render()
    {
        if (_window == 0)
        {
            return;
        }

        double primaryOuter = Math.Clamp(_config.WheelRadius, 92, 240);
        double primaryInner = Math.Clamp(_config.InnerRadius, 28, primaryOuter - 32);
        double subInner = primaryOuter + Math.Max(0, _config.SubWheelInnerGap) + 2;
        double configuredSubOuter = _config.SubWheelOuterRadius > subInner + 16
            ? _config.SubWheelOuterRadius
            : Math.Max(primaryOuter * Math.Max(1.25, _config.SubWheelRadiusRatio), subInner + 56);
        bool canShowSub = _showSubTier &&
                          _config.EnableMultiTier &&
                          _mainIndex >= 0 &&
                          _mainIndex < _actions.Count &&
                          _actions[_mainIndex].SubActions is { Count: > 0 };
        double renderOuter = canShowSub ? Math.Max(primaryOuter, configuredSubOuter) : primaryOuter;
        double padding = Math.Max(28, Math.Max(_config.HighlightGlowRadius, _config.SubWheelHighlightGlowRadius) * 0.6);
        int side = (int)Math.Ceiling((renderOuter + padding) * 2);
        side = Math.Clamp(side, 256, 1200);

        using Bitmap bitmap = new(side, side, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(System.Drawing.Color.Transparent);

        WheelPalette palette = _themeService.CreateWheelPalette(_config.AppTheme, _config);
        System.Drawing.Color normal = ToDrawing(palette.Sector);
        System.Drawing.Color accent = ToDrawing(palette.Accent);
        System.Drawing.Color border = ToDrawing(palette.SectorBorder);
        System.Drawing.Color text = ToDrawing(palette.Text);
        System.Drawing.Color core = ToDrawing(palette.Core);
        float center = side / 2f;
        double mainStep = Math.Tau / _sectorCount;
        double angularGap = Math.Min(mainStep * 0.18, Math.Max(0.006, _config.SectorGap / primaryOuter));

        for (int index = 0; index < _sectorCount; index++)
        {
            double centerAngle = -Math.PI / 2 + index * mainStep;
            double start = centerAngle - mainStep / 2 + angularGap / 2;
            double end = centerAngle + mainStep / 2 - angularGap / 2;
            bool selected = index == _mainIndex;
            DrawSector(graphics, center, primaryInner, primaryOuter, start, end,
                selected ? accent : normal, border, selected ? 1.6f : 1f);
            ActionItem? action = index < _actions.Count ? _actions[index] : null;
            DrawActionLabel(graphics, center, (primaryInner + primaryOuter) / 2, centerAngle, action,
                text, selected ? accent : normal, isSub: false);
        }

        if (canShowSub)
        {
            DrawSubTier(graphics, center, subInner, configuredSubOuter, palette, mainStep);
        }

        using SolidBrush coreBrush = new(core);
        using Pen corePen = new(border, 1f);
        float coreDiameter = (float)(primaryInner * 2 - 5);
        graphics.FillEllipse(coreBrush, center - coreDiameter / 2, center - coreDiameter / 2, coreDiameter, coreDiameter);
        graphics.DrawEllipse(corePen, center - coreDiameter / 2, center - coreDiameter / 2, coreDiameter, coreDiameter);

        string title = ResolveCenterTitle();
        using Font titleFont = new("Segoe UI Variable Display", (float)Math.Clamp(_config.CoreFontSize, 10, 20), FontStyle.Bold, GraphicsUnit.Pixel);
        using SolidBrush textBrush = new(text);
        StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        RectangleF titleRect = new(center - (float)primaryInner * .78f, center - (float)primaryInner * .55f,
            (float)primaryInner * 1.56f, (float)primaryInner * 1.1f);
        graphics.DrawString(title, titleFont, textBrush, titleRect, centered);

        Present(bitmap, side);
    }

    private void DrawSubTier(Graphics graphics, float center, double inner, double outer, WheelPalette palette, double mainStep)
    {
        ActionItem parent = _actions[_mainIndex];
        IReadOnlyList<ActionItem> children = parent.SubActions;
        int count = children.Count;
        if (count == 0)
        {
            return;
        }

        double parentCenter = -Math.PI / 2 + _mainIndex * mainStep;
        double parentStart = parentCenter - mainStep / 2;
        double childStep = mainStep / count;
        double gap = Math.Min(childStep * .14, Math.Max(.004, _config.SubWheelInnerGap / Math.Max(outer, 1)));
        System.Drawing.Color normal = ToDrawing(palette.Sector);
        System.Drawing.Color accent = ToDrawing(palette.Accent);
        System.Drawing.Color border = ToDrawing(palette.SectorBorder);
        System.Drawing.Color text = ToDrawing(palette.Text);

        for (int index = 0; index < count; index++)
        {
            double childCenter = parentStart + (index + .5) * childStep;
            double start = parentStart + index * childStep + gap / 2;
            double end = parentStart + (index + 1) * childStep - gap / 2;
            bool selected = index == _subIndex;
            DrawSector(graphics, center, inner, outer, start, end,
                selected ? accent : WithAlpha(normal, 235), border, selected ? 1.6f : 1f);
            DrawActionLabel(graphics, center, (inner + outer) / 2, childCenter, children[index],
                text, selected ? accent : normal, isSub: true);
        }
    }

    private void DrawActionLabel(
        Graphics graphics,
        float center,
        double radius,
        double angle,
        ActionItem? action,
        System.Drawing.Color textColor,
        System.Drawing.Color background,
        bool isSub)
    {
        if (action is null)
        {
            return;
        }

        float x = center + (float)(Math.Cos(angle) * radius);
        float y = center + (float)(Math.Sin(angle) * radius);
        float iconSize = (float)Math.Clamp(action.CustomIconSize ?? (isSub ? _config.SubWheelIconSize : _config.SectorIconSize), 12, 34);
        float textSize = (float)Math.Clamp(action.CustomFontSize ?? (isSub ? _config.SubWheelFontSize : _config.SectorFontSize), 8, 18);
        string layout = string.IsNullOrWhiteSpace(action.LayoutMode) || action.LayoutMode == "Inherit"
            ? _config.IconLayoutMode
            : action.LayoutMode;
        bool showIcon = !layout.Equals("TextOnly", StringComparison.OrdinalIgnoreCase);
        bool showText = !layout.Equals("IconOnly", StringComparison.OrdinalIgnoreCase) && _config.ShowText;
        using SolidBrush brush = new(textColor);
        using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        if (showIcon)
        {
            using Font iconFont = new(ActionIconCatalog.FontFamilyName, iconSize, FontStyle.Regular, GraphicsUnit.Pixel);
            RectangleF iconRect = new(x - 28, y - (showText ? 25 : 16), 56, 34);
            graphics.DrawString(ActionIconCatalog.Resolve(action), iconFont, brush, iconRect, centered);
        }
        if (showText)
        {
            using Font labelFont = new("Segoe UI Variable Text", textSize, FontStyle.Regular, GraphicsUnit.Pixel);
            RectangleF textRect = new(x - (isSub ? 46 : 42), y + (showIcon ? 5 : -10), isSub ? 92 : 84, 28);
            graphics.DrawString(action.Name ?? string.Empty, labelFont, brush, textRect, centered);
        }
    }

    private string ResolveCenterTitle()
    {
        if (_mainIndex >= 0 && _mainIndex < _actions.Count)
        {
            ActionItem main = _actions[_mainIndex];
            if (_showSubTier && _subIndex >= 0 && _subIndex < main.SubActions.Count)
            {
                return main.SubActions[_subIndex].Name;
            }
            return main.Name;
        }
        return string.IsNullOrWhiteSpace(_config.CoreTitle) ? "StarPie" : _config.CoreTitle;
    }

    private static void DrawSector(
        Graphics graphics,
        float center,
        double inner,
        double outer,
        double start,
        double end,
        System.Drawing.Color fill,
        System.Drawing.Color border,
        float borderWidth)
    {
        using GraphicsPath path = CreateSectorPath(center, inner, outer, start, end);
        using SolidBrush brush = new(fill);
        using Pen pen = new(border, borderWidth);
        graphics.FillPath(brush, path);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateSectorPath(float center, double inner, double outer, double start, double end)
    {
        float startDeg = (float)(start * 180 / Math.PI);
        float sweepDeg = (float)((end - start) * 180 / Math.PI);
        RectangleF outerRect = new(center - (float)outer, center - (float)outer, (float)outer * 2, (float)outer * 2);
        RectangleF innerRect = new(center - (float)inner, center - (float)inner, (float)inner * 2, (float)inner * 2);
        GraphicsPath path = new();
        path.AddArc(outerRect, startDeg, sweepDeg);
        path.AddArc(innerRect, startDeg + sweepDeg, -sweepDeg);
        path.CloseFigure();
        return path;
    }

    private void Present(Bitmap bitmap, int side)
    {
        nint screenDc = GetDC(0);
        nint memoryDc = CreateCompatibleDC(screenDc);
        nint bitmapHandle = bitmap.GetHbitmap(System.Drawing.Color.FromArgb(0));
        nint previous = SelectObject(memoryDc, bitmapHandle);
        try
        {
            NativePoint source = new(0, 0);
            NativeSize size = new(side, side);
            NativePoint destination = new(
                (int)Math.Round(_screenCenter.X - side / 2.0),
                (int)Math.Round(_screenCenter.Y - side / 2.0));
            BlendFunction blend = new()
            {
                BlendOp = 0,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };
            if (!UpdateLayeredWindow(_window, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha))
            {
                AppLog.Info($"UpdateLayeredWindow failed: {Marshal.GetLastWin32Error()}");
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

    private static System.Drawing.Color ToDrawing(WindowsColor value) =>
        System.Drawing.Color.FromArgb(value.A, value.R, value.G, value.B);

    private static System.Drawing.Color WithAlpha(System.Drawing.Color value, byte alpha) =>
        System.Drawing.Color.FromArgb(alpha, value.R, value.G, value.B);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public WindowProc WindowProcedure;
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
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
        public NativePoint(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public readonly int Width;
        public readonly int Height;
        public NativeSize(int width, int height) { Width = width; Height = height; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);
    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string className, nint instance);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hwnd, int command);
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint obj);
}
