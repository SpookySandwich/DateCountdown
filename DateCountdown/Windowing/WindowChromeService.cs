using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace DateCountdown.Windowing;

internal sealed class WindowChromeService
{
    private const int CompactWindowHeight = 460;
    private const int CompactWindowWidth = 420;
    private const uint MinimumWindowSizeSubclassId = 1;
    private const uint WmGetMinMaxInfo = 0x0024;

    private SubclassProc? _minimumWindowSizeProc;

    public void Initialize(Window window, UIElement titleBar, string title, bool isWindows11OrGreater)
    {
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(titleBar);
        window.Title = title;
        window.SystemBackdrop = CreateSystemBackdrop(isWindows11OrGreater);

        InstallMinimumWindowSizeHook(window);
        SetDefaultWindowSize(window);
        window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
    }

    private static SystemBackdrop CreateSystemBackdrop(bool isWindows11OrGreater)
    {
        return isWindows11OrGreater
            ? new MicaBackdrop()
            : new DesktopAcrylicBackdrop();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, uint subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint subclassId, IntPtr referenceData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    private void InstallMinimumWindowSizeHook(Window window)
    {
        _minimumWindowSizeProc = MinimumWindowSizeProc;
        SetWindowSubclass(WindowNative.GetWindowHandle(window), _minimumWindowSizeProc, MinimumWindowSizeSubclassId, IntPtr.Zero);
    }

    private static IntPtr MinimumWindowSizeProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint subclassId, IntPtr referenceData)
    {
        if (message == WmGetMinMaxInfo)
        {
            MinMaxInfo minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            double scale = GetWindowScale(hwnd);
            minMaxInfo.MinTrackSize.X = (int)Math.Round(CompactWindowWidth * scale);
            minMaxInfo.MinTrackSize.Y = (int)Math.Round(CompactWindowHeight * scale);
            Marshal.StructureToPtr(minMaxInfo, lParam, false);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private static void SetDefaultWindowSize(Window window)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(window);
        double scale = GetWindowScale(hwnd);
        int width = (int)Math.Round(CompactWindowWidth * scale);
        int height = (int)Math.Round(CompactWindowHeight * scale);

        DisplayArea displayArea = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 workArea = displayArea.WorkArea;
        int x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        int y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private static double GetWindowScale(IntPtr hwnd)
    {
        uint dpi = GetDpiForWindow(hwnd);
        return Math.Max(dpi, 96U) / 96.0;
    }
}
