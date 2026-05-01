using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Windows.Storage;

namespace DateCountdown.Services;

internal sealed class DesktopCountdownWindow : IDisposable
{
    private const string WindowClassName = "DateCountdownDesktopTextWindow";
    private const int ErrorClassAlreadyExists = 1410;
    private const uint WsChild = 0x40000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint LwaColorKey = 0x00000001;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwShowNoActivate = 4;
    private const int WmPaint = 0x000F;
    private const int WmNcDestroy = 0x0082;
    private const int TransparentColor = 0x00030201;
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

    private static readonly WndProc s_wndProc = WindowProcedure;
    private static readonly Dictionary<IntPtr, DesktopCountdownWindow> s_windows = new();
    private static bool s_isClassRegistered;

    private IntPtr _hwnd;
    private IntPtr _insertAfter;
    private IntPtr _parent;
    private bool _usesRaisedDesktop;
    private IReadOnlyList<DesktopCountdownContent> _contents = Array.Empty<DesktopCountdownContent>();
    private CoreWebView2Controller? _webViewController;
    private CoreWebView2? _webView;
    private bool _useGdiFallback;
    private bool _isInitializingWebView;

    public bool IsVisible => DesktopWorkerWindowHostResolver.IsWindowAlive(_hwnd);

    public bool ShowOrUpdate(IReadOnlyList<DesktopCountdownContent> contents)
    {
        _contents = new ReadOnlyCollection<DesktopCountdownContent>(contents.ToList());
        if (_contents.Count == 0)
        {
            Hide();
            return true;
        }

        if (!EnsureWindow())
        {
            return false;
        }

        (int x, int y, int width, int height) = CalculateBounds();
        IntPtr insertAfter = _usesRaisedDesktop && _insertAfter != IntPtr.Zero
            ? _insertAfter
            : IntPtr.Zero;
        _ = SetWindowPos(_hwnd, insertAfter, x, y, width, height, SwpNoActivate | SwpShowWindow);
        _ = ShowWindow(_hwnd, SwShowNoActivate);
        UpdateWebViewBounds(width, height);
        _ = EnsureWebViewAsync(width, height);
        return true;
    }

    public void Hide()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        s_windows.Remove(_hwnd);
        _webViewController?.Close();
        _webViewController = null;
        _webView = null;
        _useGdiFallback = false;
        _isInitializingWebView = false;
        _ = DestroyWindow(_hwnd);
        _hwnd = IntPtr.Zero;
        _insertAfter = IntPtr.Zero;
        _parent = IntPtr.Zero;
        _usesRaisedDesktop = false;
    }

    private async Task EnsureWebViewAsync(int width, int height)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_webView is not null)
        {
            UpdateWebViewBounds(width, height);
            _webView.NavigateToString(DesktopCountdownHtmlDocument.Create(_contents));
            return;
        }

        if (_isInitializingWebView)
        {
            return;
        }

        _isInitializingWebView = true;
        try
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00FFFFFF");
            string userDataFolder = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "DesktopCountdownWebView2");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, null);
            CoreWebView2ControllerWindowReference parentWindow =
                CoreWebView2ControllerWindowReference.CreateFromWindowHandle((ulong)_hwnd.ToInt64());
            _webViewController = await environment.CreateCoreWebView2ControllerAsync(parentWindow);
            _webViewController.DefaultBackgroundColor = Windows.UI.Color.FromArgb(0, 255, 255, 255);
            _webViewController.BoundsMode = CoreWebView2BoundsMode.UseRawPixels;
            _webViewController.Bounds = new Windows.Foundation.Rect(0, 0, width, height);
            _webViewController.IsVisible = true;
            _webView = _webViewController.CoreWebView2;
            _webView.Settings.AreDefaultContextMenusEnabled = false;
            _webView.Settings.AreDevToolsEnabled = false;
            _webView.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _webView.NavigateToString(DesktopCountdownHtmlDocument.Create(_contents));
            _useGdiFallback = false;
        }
        catch
        {
            _webViewController?.Close();
            _webViewController = null;
            _webView = null;
            _useGdiFallback = true;
            _ = InvalidateRect(_hwnd, IntPtr.Zero, false);
            _ = UpdateWindow(_hwnd);
        }
        finally
        {
            _isInitializingWebView = false;
        }
    }

    private void UpdateWebViewBounds(int width, int height)
    {
        if (_webViewController is not null)
        {
            _webViewController.Bounds = new Windows.Foundation.Rect(0, 0, width, height);
        }
    }

    public void Dispose()
    {
        Hide();
    }

    private bool EnsureWindow()
    {
        if (!DesktopWorkerWindowHostResolver.TryResolve(out DesktopWorkerWindowHost host))
        {
            Hide();
            return false;
        }

        if (IsVisible && _parent == host.Parent)
        {
            return true;
        }

        Hide();
        if (!RegisterWindowClass())
        {
            return false;
        }

        _parent = host.Parent;
        _insertAfter = host.InsertAfter;
        _usesRaisedDesktop = host.UsesRaisedDesktop;
        _hwnd = CreateWindowEx(
            WsExLayered | WsExNoActivate | WsExTransparent | WsExToolWindow,
            WindowClassName,
            string.Empty,
            WsChild | WsVisible,
            0,
            0,
            1,
            1,
            host.Parent,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            _parent = IntPtr.Zero;
            return false;
        }

        s_windows[_hwnd] = this;
        _ = SetLayeredWindowAttributes(_hwnd, TransparentColor, 255, LwaColorKey);

        if (host.UsesRaisedDesktop && host.InsertAfter != IntPtr.Zero)
        {
            _ = SetWindowPos(_hwnd, host.InsertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

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

    private (int X, int Y, int Width, int Height) CalculateBounds()
    {
        DesktopBounds bounds = GetVirtualDesktopBounds();
        int x = bounds.X;
        int y = bounds.Y;
        if (_parent != IntPtr.Zero && GetWindowRect(_parent, out Rect parentRect))
        {
            x -= parentRect.Left;
            y -= parentRect.Top;
        }

        return (x, y, bounds.Width, bounds.Height);
    }

    private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmPaint && s_windows.TryGetValue(hwnd, out DesktopCountdownWindow? window))
        {
            window.Paint();
            return IntPtr.Zero;
        }

        if (message == WmNcDestroy)
        {
            s_windows.Remove(hwnd);
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
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
            _ = SetTextColor(hdc, 0x00FFFFFF);

            if (!_useGdiFallback)
            {
                return;
            }

            foreach (DesktopCountdownContent content in _contents)
            {
                DrawContentFallback(hdc, rect, content);
            }
        }
        finally
        {
            _ = EndPaint(_hwnd, ref paint);
        }
    }

    private static void DrawContentFallback(IntPtr hdc, Rect rect, DesktopCountdownContent content)
    {
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        double daysFontSize = content.DaysTextSize * content.Scale;
        double titleFontSize = content.TitleTextSize * content.Scale;
        int daysHeight = (int)Math.Ceiling(daysFontSize * 1.25);
        int titleHeight = (int)Math.Ceiling(titleFontSize * 1.8);
        int totalHeight = daysHeight + titleHeight + 10;
        int contentWidth = CalculateContentWidth(width, content, daysFontSize, titleFontSize);
        int left = ClampToRange(
            (int)Math.Round((content.PositionX * width) - (contentWidth / 2.0)),
            16,
            Math.Max(16, width - contentWidth - 16));
        int top = ClampToRange(
            (int)Math.Round((content.PositionY * height) - (totalHeight / 2.0)),
            16,
            Math.Max(16, height - totalHeight - 16));

        Rect daysRect = new()
        {
            Left = left,
            Top = top,
            Right = left + contentWidth,
            Bottom = top + daysHeight
        };
        DrawTextWithShadow(hdc, content.DaysLeft, daysRect, daysFontSize, FwLight, "Segoe UI Variable Display");

        Rect titleRect = new()
        {
            Left = left,
            Top = daysRect.Bottom + 8,
            Right = left + contentWidth,
            Bottom = daysRect.Bottom + 8 + titleHeight
        };
        DrawTextWithShadow(hdc, content.Title, titleRect, titleFontSize, FwSemiBold, "Segoe UI");
    }

    private static int CalculateContentWidth(int availableWidth, DesktopCountdownContent content, double daysFontSize, double titleFontSize)
    {
        double daysWidth = Math.Max(280, content.DaysLeft.Length * daysFontSize * 0.58);
        double titleWidth = Math.Max(220, content.Title.Length * titleFontSize * 0.72);
        return ClampToRange((int)Math.Ceiling(Math.Max(daysWidth, titleWidth) + 48), 280, Math.Max(280, availableWidth - 32));
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

    private readonly record struct DesktopBounds(int X, int Y, int Width, int Height);

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
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

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
}
