using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DateCountdown.Core;

namespace DateCountdown.Services;

internal sealed class DesktopCountdownAdjustmentWindow : IDisposable
{
    private const string WindowClassName = "DateCountdownDesktopAdjustmentWindow";
    private const int ErrorClassAlreadyExists = 1410;
    private const uint WsPopup = 0x80000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint LwaColorKey = 0x00000001;
    private const uint LwaAlpha = 0x00000002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwShowNoActivate = 4;
    private const int WmPaint = 0x000F;
    private const int WmNcDestroy = 0x0082;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmMouseMove = 0x0200;
    private const int TransparentColor = 0x00030201;
    private const byte OverlayOpacity = 155;
    private const int TransparentBkMode = 1;
    private const int DtCenter = 0x00000001;
    private const int DtVCenter = 0x00000004;
    private const int DtSingleLine = 0x00000020;
    private const int DtEndEllipsis = 0x00008000;
    private const int DtNoPrefix = 0x00000800;
    private const int DefaultCharset = 1;
    private const int OutDefaultPrecision = 0;
    private const int ClipDefaultPrecision = 0;
    private const int ClearTypeQuality = 5;
    private const int DefaultPitch = 0;
    private const int FwLight = 300;
    private const int FwSemiBold = 600;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int PsSolid = 0;
    private const int CornerHitTargetSize = 46;
    private const int CornerVisualSize = 34;

    private static readonly WndProc s_wndProc = WindowProcedure;
    private static readonly Dictionary<IntPtr, DesktopCountdownAdjustmentWindow> s_windows = new();
    private static bool s_isClassRegistered;

    private IntPtr _hwnd;
    private DesktopCountdownContent _content = new(string.Empty, string.Empty, string.Empty, 72, 14, 0.5, 0.5, 1, string.Empty);
    private Action<double, double, double>? _placementChanged;
    private Action<double, double, double>? _placementCommitted;
    private TextBounds _textBounds;
    private TextBounds _dragStartBounds;
    private DesktopCountdownContent _dragStartContent = new(string.Empty, string.Empty, string.Empty, 72, 14, 0.5, 0.5, 1, string.Empty);
    private DragMode _dragMode = DragMode.None;
    private bool _isDragging;
    private double _dragOffsetX;
    private double _dragOffsetY;

    public bool IsVisible => DesktopWorkerWindowHostResolver.IsWindowAlive(_hwnd);

    public bool ShowOrUpdate(
        DesktopCountdownContent content,
        Action<double, double, double> placementChanged,
        Action<double, double, double> placementCommitted)
    {
        _content = content;
        _placementChanged = placementChanged;
        _placementCommitted = placementCommitted;
        if (!EnsureWindow())
        {
            return false;
        }

        DesktopBounds bounds = GetVirtualDesktopBounds();
        _ = SetWindowPos(_hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
        _ = ShowWindow(_hwnd, SwShowNoActivate);
        _ = InvalidateRect(_hwnd, IntPtr.Zero, true);
        _ = UpdateWindow(_hwnd);
        return true;
    }

    public void Hide()
    {
        if (_hwnd != IntPtr.Zero)
        {
            s_windows.Remove(_hwnd);
            _ = DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        _isDragging = false;
        _placementChanged = null;
        _placementCommitted = null;
    }

    public void Dispose()
    {
        Hide();
    }

    private bool EnsureWindow()
    {
        if (IsVisible)
        {
            return true;
        }

        if (!RegisterWindowClass())
        {
            return false;
        }

        _hwnd = CreateWindowEx(
            WsExLayered | WsExNoActivate | WsExToolWindow,
            WindowClassName,
            string.Empty,
            WsPopup | WsVisible,
            0,
            0,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        s_windows[_hwnd] = this;
        _ = SetLayeredWindowAttributes(_hwnd, TransparentColor, OverlayOpacity, LwaColorKey | LwaAlpha);
        return true;
    }

    private static bool RegisterWindowClass()
    {
        if (s_isClassRegistered)
        {
            return true;
        }

        WndClassEx windowClass = new()
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = s_wndProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = WindowClassName
        };

        ushort atom = RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorClassAlreadyExists)
            {
                return false;
            }
        }

        s_isClassRegistered = true;
        return true;
    }

    private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (s_windows.TryGetValue(hwnd, out DesktopCountdownAdjustmentWindow? window))
        {
            switch (message)
            {
                case WmPaint:
                    window.Paint();
                    return IntPtr.Zero;
                case WmLButtonDown:
                    window.BeginDrag(GetPointX(lParam), GetPointY(lParam));
                    return IntPtr.Zero;
                case WmMouseMove:
                    window.DragTo(GetPointX(lParam), GetPointY(lParam));
                    return IntPtr.Zero;
                case WmLButtonUp:
                    window.EndDrag();
                    return IntPtr.Zero;
            }
        }

        if (message == WmNcDestroy)
        {
            s_windows.Remove(hwnd);
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void BeginDrag(int x, int y)
    {
        DragMode dragMode = _textBounds.HitTest(x, y);
        if (dragMode == DragMode.None)
        {
            return;
        }

        _isDragging = true;
        _dragMode = dragMode;
        _dragStartBounds = _textBounds;
        _dragStartContent = _content;
        _dragOffsetX = x - _textBounds.CenterX;
        _dragOffsetY = y - _textBounds.CenterY;
        _ = SetCapture(_hwnd);
    }

    private void DragTo(int x, int y)
    {
        if (!_isDragging || !GetClientRect(_hwnd, out Rect rect))
        {
            return;
        }

        if (_dragMode == DragMode.Move)
        {
            MoveTo(x, y, rect);
        }
        else
        {
            ResizeTo(x, y, rect);
        }

        _ = InvalidateRect(_hwnd, IntPtr.Zero, true);
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        _dragMode = DragMode.None;
        _ = ReleaseCapture();
        _placementCommitted?.Invoke(_content.PositionX, _content.PositionY, _content.Scale);
    }

    private void MoveTo(int x, int y, Rect clientRect)
    {
        int width = Math.Max(1, clientRect.Right - clientRect.Left);
        int height = Math.Max(1, clientRect.Bottom - clientRect.Top);
        double nextX = Math.Clamp((x - _dragOffsetX) / width, 0, 1);
        double nextY = Math.Clamp((y - _dragOffsetY) / height, 0, 1);
        _content = _content with { PositionX = nextX, PositionY = nextY };
        _placementChanged?.Invoke(nextX, nextY, _content.Scale);
    }

    private void ResizeTo(int x, int y, Rect clientRect)
    {
        int width = Math.Max(1, clientRect.Right - clientRect.Left);
        int height = Math.Max(1, clientRect.Bottom - clientRect.Top);
        double left = _dragStartBounds.Left;
        double top = _dragStartBounds.Top;
        double right = _dragStartBounds.Right;
        double bottom = _dragStartBounds.Bottom;

        if (_dragMode.HasFlag(DragMode.ResizeLeft))
        {
            left = Math.Clamp(x, 8, right - DesktopOverlayLayout.MinimumAdjustmentBoxSize);
        }

        if (_dragMode.HasFlag(DragMode.ResizeRight))
        {
            right = Math.Clamp(x, left + DesktopOverlayLayout.MinimumAdjustmentBoxSize, width - 8);
        }

        if (_dragMode.HasFlag(DragMode.ResizeTop))
        {
            top = Math.Clamp(y, 8, bottom - DesktopOverlayLayout.MinimumAdjustmentBoxSize);
        }

        if (_dragMode.HasFlag(DragMode.ResizeBottom))
        {
            bottom = Math.Clamp(y, top + DesktopOverlayLayout.MinimumAdjustmentBoxSize, height - 8);
        }

        double widthRatio = Math.Max(0.1, (right - left) / Math.Max(1, _dragStartBounds.Width));
        double heightRatio = Math.Max(0.1, (bottom - top) / Math.Max(1, _dragStartBounds.Height));
        double scaleRatio = GetResizeScaleRatio(widthRatio, heightRatio);
        double nextScale = DateCountdown.Core.CountdownPreferences.CoerceDesktopCountdownScale(_dragStartContent.Scale * scaleRatio);
        double nextX = Math.Clamp(((left + right) / 2) / width, 0, 1);
        double nextY = Math.Clamp(((top + bottom) / 2) / height, 0, 1);

        _content = _dragStartContent with
        {
            PositionX = nextX,
            PositionY = nextY,
            Scale = nextScale
        };
        _placementChanged?.Invoke(nextX, nextY, nextScale);
    }

    private double GetResizeScaleRatio(double widthRatio, double heightRatio)
    {
        bool horizontal = _dragMode.HasFlag(DragMode.ResizeLeft) || _dragMode.HasFlag(DragMode.ResizeRight);
        bool vertical = _dragMode.HasFlag(DragMode.ResizeTop) || _dragMode.HasFlag(DragMode.ResizeBottom);
        return DesktopOverlayLayout.SelectResizeScaleRatio(horizontal, vertical, widthRatio, heightRatio);
    }

    private void Paint()
    {
        IntPtr hdc = BeginPaint(_hwnd, out PaintStruct paint);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _ = GetClientRect(_hwnd, out Rect rect);
            IntPtr transparentBrush = CreateSolidBrush(TransparentColor);
            try
            {
                _ = FillRect(hdc, ref rect, transparentBrush);
            }
            finally
            {
                _ = DeleteObject(transparentBrush);
            }

            _ = SetBkMode(hdc, TransparentBkMode);
            _textBounds = CalculateTextBounds(rect, _content);
            DrawGuide(hdc, _textBounds);
        }
        finally
        {
            _ = EndPaint(_hwnd, ref paint);
        }
    }

    private static TextBounds CalculateTextBounds(Rect clientRect, DesktopCountdownContent content)
    {
        int width = clientRect.Right - clientRect.Left;
        int height = clientRect.Bottom - clientRect.Top;
        double daysFontSize = content.DaysTextSize * content.Scale;
        double titleFontSize = content.TitleTextSize * content.Scale;
        int daysHeight = (int)Math.Ceiling(daysFontSize * 1.25);
        int titleHeight = (int)Math.Ceiling(titleFontSize * 1.8);
        int totalHeight = daysHeight + titleHeight + 10;
        int contentWidth = CalculateContentWidth(width, content);
        int left = ClampToRange(
            (int)Math.Round((content.PositionX * width) - (contentWidth / 2.0)),
            16,
            Math.Max(16, width - contentWidth - 16));
        int top = ClampToRange(
            (int)Math.Round((content.PositionY * height) - (totalHeight / 2.0)),
            16,
            Math.Max(16, height - totalHeight - 16));

        return new TextBounds(left, top, left + contentWidth, top + totalHeight);
    }

    private static void DrawCountdownText(IntPtr hdc, DesktopCountdownContent content, TextBounds bounds)
    {
        double daysFontSize = content.DaysTextSize * content.Scale;
        double titleFontSize = content.TitleTextSize * content.Scale;
        int daysHeight = (int)Math.Ceiling(daysFontSize * 1.25);
        int titleHeight = (int)Math.Ceiling(titleFontSize * 1.8);

        Rect daysRect = new()
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Right = bounds.Right,
            Bottom = bounds.Top + daysHeight
        };
        DrawTextWithShadow(hdc, content.DaysLeft, daysRect, daysFontSize, FwLight, "Segoe UI Variable Display");

        Rect titleRect = new()
        {
            Left = bounds.Left,
            Top = daysRect.Bottom + 8,
            Right = bounds.Right,
            Bottom = daysRect.Bottom + 8 + titleHeight
        };
        DrawTextWithShadow(hdc, content.Title, titleRect, titleFontSize, FwSemiBold, "Segoe UI");
    }

    private static void DrawGuide(IntPtr hdc, TextBounds bounds)
    {
        Rect guideRect = new()
        {
            Left = bounds.Left - 16,
            Top = bounds.Top - 12,
            Right = bounds.Right + 16,
            Bottom = bounds.Bottom + 12
        };

        IntPtr guideBrush = CreateSolidBrush(0x00181818);
        try
        {
            _ = FillRect(hdc, ref guideRect, guideBrush);
        }
        finally
        {
            _ = DeleteObject(guideBrush);
        }

        IntPtr pen = CreatePen(PsSolid, 2, 0x00D8D8D8);
        IntPtr oldPen = SelectObject(hdc, pen);
        IntPtr oldBrush = SelectObject(hdc, GetStockObject(5));
        try
        {
            _ = Rectangle(hdc, guideRect.Left, guideRect.Top, guideRect.Right, guideRect.Bottom);
            DrawCornerHandles(hdc, guideRect);
        }
        finally
        {
            _ = SelectObject(hdc, oldBrush);
            _ = SelectObject(hdc, oldPen);
            _ = DeleteObject(pen);
        }
    }

    private static void DrawCornerHandles(IntPtr hdc, Rect guideRect)
    {
        int radius = CornerVisualSize / 2;
        DrawHandle(hdc, guideRect.Left, guideRect.Top, radius);
        DrawHandle(hdc, guideRect.Right, guideRect.Top, radius);
        DrawHandle(hdc, guideRect.Left, guideRect.Bottom, radius);
        DrawHandle(hdc, guideRect.Right, guideRect.Bottom, radius);
    }

    private static void DrawHandle(IntPtr hdc, int centerX, int centerY, int radius)
    {
        IntPtr handleBrush = CreateSolidBrush(0x002A2A2A);
        IntPtr oldBrush = SelectObject(hdc, handleBrush);
        IntPtr handlePen = CreatePen(PsSolid, 4, 0x00FFFFFF);
        IntPtr oldPen = SelectObject(hdc, handlePen);
        try
        {
            _ = Ellipse(hdc, centerX - radius, centerY - radius, centerX + radius, centerY + radius);
        }
        finally
        {
            _ = SelectObject(hdc, oldPen);
            _ = SelectObject(hdc, oldBrush);
            _ = DeleteObject(handlePen);
            _ = DeleteObject(handleBrush);
        }
    }

    private static int CalculateContentWidth(int availableWidth, DesktopCountdownContent content)
    {
        return DesktopOverlayLayout.EstimateTextBoundsWidth(
            availableWidth,
            content.DaysLeft,
            content.Title,
            content.DaysTextSize,
            content.TitleTextSize,
            content.Scale);
    }

    private static int ClampToRange(int value, int minimum, int maximum)
    {
        return Math.Clamp(value, Math.Min(minimum, maximum), Math.Max(minimum, maximum));
    }

    private static DesktopBounds GetVirtualDesktopBounds()
    {
        return new DesktopBounds(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            Math.Max(GetSystemMetrics(SmCxVirtualScreen), 900),
            Math.Max(GetSystemMetrics(SmCyVirtualScreen), 480));
    }

    private static void DrawTextWithShadow(IntPtr hdc, string text, Rect bounds, double fontSize, int weight, string faceName)
    {
        Rect shadowBounds = bounds;
        shadowBounds.Left += 2;
        shadowBounds.Top += 2;
        shadowBounds.Right += 2;
        shadowBounds.Bottom += 2;

        DrawTextWithFont(hdc, text, shadowBounds, fontSize, weight, faceName, 0x00000000);
        DrawTextWithFont(hdc, text, bounds, fontSize, weight, faceName, 0x00FFFFFF);
    }

    private static void DrawTextWithFont(IntPtr hdc, string text, Rect bounds, double fontSize, int weight, string faceName, int color)
    {
        int fontHeight = -Math.Max(1, (int)Math.Round(fontSize));
        IntPtr font = CreateFont(
            fontHeight,
            0,
            0,
            0,
            weight,
            0,
            0,
            0,
            DefaultCharset,
            OutDefaultPrecision,
            ClipDefaultPrecision,
            ClearTypeQuality,
            DefaultPitch,
            faceName);

        IntPtr oldFont = SelectObject(hdc, font);
        try
        {
            _ = SetTextColor(hdc, color);
            _ = DrawText(hdc, text, -1, ref bounds, DtCenter | DtVCenter | DtSingleLine | DtEndEllipsis | DtNoPrefix);
        }
        finally
        {
            _ = SelectObject(hdc, oldFont);
            _ = DeleteObject(font);
        }
    }

    private static int GetPointX(IntPtr lParam)
    {
        return unchecked((short)(long)lParam);
    }

    private static int GetPointY(IntPtr lParam)
    {
        return unchecked((short)((long)lParam >> 16));
    }

    private readonly record struct TextBounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public double CenterX => Left + ((Right - Left) / 2.0);

        public double CenterY => Top + ((Bottom - Top) / 2.0);

        public DragMode HitTest(int x, int y)
        {
            bool insideOuter = x >= Left - 20 &&
                x <= Right + 20 &&
                y >= Top - 20 &&
                y <= Bottom + 20;
            if (!insideOuter)
            {
                return DragMode.None;
            }

            DragMode cornerMode = HitTestCorner(x, y);
            return cornerMode == DragMode.None ? DragMode.Move : cornerMode;
        }

        private DragMode HitTestCorner(int x, int y)
        {
            int halfTarget = CornerHitTargetSize / 2;
            bool nearLeft = Math.Abs(x - Left) <= halfTarget;
            bool nearRight = Math.Abs(x - Right) <= halfTarget;
            bool nearTop = Math.Abs(y - Top) <= halfTarget;
            bool nearBottom = Math.Abs(y - Bottom) <= halfTarget;

            if (nearLeft && nearTop)
            {
                return DragMode.ResizeLeft | DragMode.ResizeTop;
            }

            if (nearRight && nearTop)
            {
                return DragMode.ResizeRight | DragMode.ResizeTop;
            }

            if (nearLeft && nearBottom)
            {
                return DragMode.ResizeLeft | DragMode.ResizeBottom;
            }

            if (nearRight && nearBottom)
            {
                return DragMode.ResizeRight | DragMode.ResizeBottom;
            }

            return DragMode.None;
        }
    }

    private readonly record struct DesktopBounds(int X, int Y, int Width, int Height);

    [Flags]
    private enum DragMode
    {
        None = 0,
        Move = 1,
        ResizeLeft = 2,
        ResizeRight = 4,
        ResizeTop = 8,
        ResizeBottom = 16
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public WndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr hdc;
        public bool fErase;
        public Rect rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        public byte rgbReserved1;
        public byte rgbReserved2;
        public byte rgbReserved3;
        public byte rgbReserved4;
        public byte rgbReserved5;
        public byte rgbReserved6;
        public byte rgbReserved7;
        public byte rgbReserved8;
        public byte rgbReserved9;
        public byte rgbReserved10;
        public byte rgbReserved11;
        public byte rgbReserved12;
        public byte rgbReserved13;
        public byte rgbReserved14;
        public byte rgbReserved15;
        public byte rgbReserved16;
        public byte rgbReserved17;
        public byte rgbReserved18;
        public byte rgbReserved19;
        public byte rgbReserved20;
        public byte rgbReserved21;
        public byte rgbReserved22;
        public byte rgbReserved23;
        public byte rgbReserved24;
        public byte rgbReserved25;
        public byte rgbReserved26;
        public byte rgbReserved27;
        public byte rgbReserved28;
        public byte rgbReserved29;
        public byte rgbReserved30;
        public byte rgbReserved31;
        public byte rgbReserved32;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WndClassEx windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, int colorKey, byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr BeginPaint(IntPtr hwnd, out PaintStruct paint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct paint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetCapture(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ReleaseCapture();

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateSolidBrush(int color);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int FillRect(IntPtr hdc, ref Rect rect, IntPtr brush);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int SetTextColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFont(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint charSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DrawText(IntPtr hdc, string text, int count, ref Rect rect, int format);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreatePen(int style, int width, int color);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr GetStockObject(int objectType);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool Ellipse(IntPtr hdc, int left, int top, int right, int bottom);
}
