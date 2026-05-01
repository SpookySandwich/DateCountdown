using System;
using System.Runtime.InteropServices;

namespace DateCountdown.Services;

internal readonly record struct DesktopWorkerWindowHost(IntPtr Parent, IntPtr InsertAfter, bool UsesRaisedDesktop);

internal static partial class DesktopWorkerWindowHostResolver
{
    private const int GwlExStyle = -20;
    private const long WsExNoRedirectionBitmap = 0x00200000L;
    private const uint WorkerWMessage = 0x052C;
    private const uint SmtoNormal = 0x0000;

    public static bool TryResolve(out DesktopWorkerWindowHost host)
    {
        host = default;

        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return false;
        }

        bool usesRaisedDesktop = HasExtendedStyle(progman, WsExNoRedirectionBitmap);
        _ = SendMessageTimeout(
            progman,
            WorkerWMessage,
            new IntPtr(0xD),
            new IntPtr(0x1),
            SmtoNormal,
            1000,
            out _);

        IntPtr workerW = IntPtr.Zero;
        IntPtr shellView = IntPtr.Zero;
        _ = EnumWindows((topHandle, _) =>
        {
            IntPtr candidateShellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (candidateShellView != IntPtr.Zero)
            {
                workerW = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                shellView = candidateShellView;
            }

            return true;
        }, IntPtr.Zero);

        if (usesRaisedDesktop)
        {
            shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            workerW = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        }

        if (workerW == IntPtr.Zero && !usesRaisedDesktop)
        {
            workerW = progman;
        }

        IntPtr parent = usesRaisedDesktop ? progman : workerW;
        if (parent == IntPtr.Zero)
        {
            return false;
        }

        host = new DesktopWorkerWindowHost(parent, usesRaisedDesktop ? shellView : IntPtr.Zero, usesRaisedDesktop);
        return true;
    }

    public static bool IsWindowAlive(IntPtr window)
    {
        return window != IntPtr.Zero && IsWindow(window);
    }

    private static bool HasExtendedStyle(IntPtr hwnd, long style)
    {
        long value = IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, GwlExStyle).ToInt64()
            : GetWindowLong32(hwnd, GwlExStyle);

        return (value & style) == style;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
