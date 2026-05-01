using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace DateCountdown.Services;

internal sealed class DesktopTrayService : IDisposable
{
    private const uint SubclassId = 2;
    private const uint WmNull = 0x0000;
    private const uint WmClose = 0x0010;
    private const uint WmApp = 0x8000;
    private const uint WmTrayIcon = WmApp + 73;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NinSelect = 0x0400;
    private const uint NinKeySelect = 0x0401;
    private const uint NotifyIconVersion4 = 4;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmReturnCommand = 0x0100;
    private const uint TpmRightButton = 0x0002;
    private const int TrayIconId = 1;
    private const int OpenCommandId = 100;
    private const int ExitCommandId = 101;
    private const int SwHide = 0;
    private const int SwShow = 5;

    private readonly IntPtr _hwnd;
    private readonly Func<bool> _shouldHideToTray;
    private readonly Action _exitApplication;
    private readonly string _tooltip;
    private readonly string _openText;
    private readonly string _exitText;
    private readonly SubclassProc _subclassProc;
    private IntPtr _icon;
    private bool _isTrayIconVisible;
    private bool _isDisposed;

    public DesktopTrayService(
        Window window,
        Func<bool> shouldHideToTray,
        Action exitApplication,
        string tooltip,
        string openText,
        string exitText)
    {
        _hwnd = WindowNative.GetWindowHandle(window);
        _shouldHideToTray = shouldHideToTray;
        _exitApplication = exitApplication;
        _tooltip = tooltip;
        _openText = openText;
        _exitText = exitText;
        _subclassProc = WindowSubclassProc;
        _ = SetWindowSubclass(_hwnd, _subclassProc, SubclassId, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        HideTrayIcon();
        _ = RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);
    }

    private IntPtr WindowSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint subclassId, IntPtr referenceData)
    {
        if (message == WmClose && _shouldHideToTray())
        {
            HideWindowToTray();
            return IntPtr.Zero;
        }

        if (message == WmTrayIcon)
        {
            HandleTrayMessage(GetTrayNotification(lParam));
            return IntPtr.Zero;
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void HandleTrayMessage(uint trayMessage)
    {
        switch (trayMessage)
        {
            case NinSelect:
            case NinKeySelect:
            case WmLButtonUp:
            case WmLButtonDoubleClick:
                RestoreWindow();
                break;
            case WmRButtonUp:
            case WmContextMenu:
                ShowContextMenu();
                break;
        }
    }

    private void HideWindowToTray()
    {
        ShowTrayIcon();
        _ = ShowWindow(_hwnd, SwHide);
    }

    private void RestoreWindow()
    {
        _ = ShowWindow(_hwnd, SwShow);
        _ = SetForegroundWindow(_hwnd);
        HideTrayIcon();
    }

    private void ShowContextMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _ = AppendMenu(menu, MfString, OpenCommandId, _openText);
            _ = AppendMenu(menu, MfSeparator, 0, null);
            _ = AppendMenu(menu, MfString, ExitCommandId, _exitText);
            _ = GetCursorPos(out Point point);
            _ = SetForegroundWindow(_hwnd);
            int command = TrackPopupMenu(menu, TpmReturnCommand | TpmRightButton, point.X, point.Y, 0, _hwnd, IntPtr.Zero);
            _ = PostMessage(_hwnd, WmNull, IntPtr.Zero, IntPtr.Zero);
            switch (command)
            {
                case OpenCommandId:
                    RestoreWindow();
                    break;
                case ExitCommandId:
                    HideTrayIcon();
                    _exitApplication();
                    break;
            }
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    private static uint GetTrayNotification(IntPtr lParam)
    {
        // NOTIFYICON_VERSION_4 packs the mouse/key notification into the low word.
        // Older shell versions pass the window message directly. The low-word form
        // works for both, and keeps right-click context menus reliable.
        return unchecked((uint)(lParam.ToInt64() & 0xFFFF));
    }

    private void ShowTrayIcon()
    {
        if (_isTrayIconVisible)
        {
            return;
        }

        _icon = LoadImage(
            IntPtr.Zero,
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
            ImageIcon,
            16,
            16,
            LrLoadFromFile);

        NotifyIconData data = CreateNotifyIconData();
        if (Shell_NotifyIcon(NimAdd, ref data))
        {
            data.uVersion = NotifyIconVersion4;
            _ = Shell_NotifyIcon(NimSetVersion, ref data);
            _isTrayIconVisible = true;
        }
    }

    private void HideTrayIcon()
    {
        if (_isTrayIconVisible)
        {
            NotifyIconData data = CreateNotifyIconData();
            _ = Shell_NotifyIcon(NimDelete, ref data);
            _isTrayIconVisible = false;
        }

        if (_icon != IntPtr.Zero)
        {
            _ = DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }

    private NotifyIconData CreateNotifyIconData()
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = TrayIconId,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmTrayIcon,
            hIcon = _icon,
            szTip = _tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, uint subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc subclassProc, uint subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint load);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, int newItemId, string? newItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, uint subclassId, IntPtr referenceData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
