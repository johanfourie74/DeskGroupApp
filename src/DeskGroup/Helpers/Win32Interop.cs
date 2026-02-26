using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DeskGroup.Helpers;

public static class Win32Interop
{
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOPMOST = 0x00000008;

    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public static readonly IntPtr HWND_TOP = new IntPtr(0);

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOZORDER = 0x0004;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;
    public const int SW_SHOWNOACTIVATE = 4;

    public static IntPtr FindWindow(string? lpClassName, string? lpWindowName)
        => PInvoke.FindWindowW(lpClassName, lpWindowName).Value;

    public static IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow)
        => PInvoke.FindWindowEx(new HWND(hwndParent), new HWND(hwndChildAfter), lpszClass, lpszWindow).Value;

    public static IntPtr GetDesktopWindow()
        => PInvoke.GetDesktopWindow().Value;

    public static IntPtr GetShellWindow()
        => PInvoke.GetShellWindow().Value;

    public static IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent)
        => PInvoke.SetParent(new HWND(hWndChild), new HWND(hWndNewParent)).Value;

    public static bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags)
        => PInvoke.SetWindowPos(new HWND(hWnd), new HWND(hWndInsertAfter), X, Y, cx, cy, uFlags).IsTrue();

    public static int GetWindowLong(IntPtr hWnd, int nIndex)
        => PInvoke.GetWindowLong(new HWND(hWnd), nIndex);

    public static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
        => PInvoke.SetWindowLong(new HWND(hWnd), nIndex, dwNewLong);

    public static bool ShowWindow(IntPtr hWnd, int nCmdShow)
        => PInvoke.ShowWindow(new HWND(hWnd), nCmdShow).IsTrue();

    public static IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam)
        => PInvoke.SendMessage(new HWND(hWnd), Msg, new Windows.Win32.WPARAM((nuint)wParam.ToInt64()), new Windows.Win32.LPARAM((nint)lParam.ToInt64()));

    public static bool SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult)
    {
        var ok = PInvoke.SendMessageTimeout(new HWND(hWnd), Msg, new Windows.Win32.WPARAM((nuint)wParam.ToInt64()), new Windows.Win32.LPARAM((nint)lParam.ToInt64()), (Windows.Win32.SMTO)fuFlags, uTimeout, out nuint res);
        lpdwResult = new IntPtr((long)res);
        return ok.IsTrue();
    }

    // Icon helpers
    public static IntPtr ExtractIcon(string filePath, int index)
        => PInvoke.ExtractIconW(HINSTANCE.NULL, filePath, index).Value;

    public static bool DestroyIcon(IntPtr hIcon)
        => PInvoke.DestroyIcon(new HICON(hIcon)).IsTrue();

    public static IntPtr GetDesktopWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
            0x0000, 1000, out _);

        IntPtr workerW = IntPtr.Zero;
        IntPtr currentWorker = IntPtr.Zero;
        do
        {
            currentWorker = FindWindowEx(IntPtr.Zero, currentWorker, "WorkerW", null);
            if (currentWorker == IntPtr.Zero) break;

            var defView = FindWindowEx(currentWorker, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
            {
                workerW = currentWorker;
                break;
            }
        } while (true);

        return workerW;
    }

    public static bool AttachToDesktop(IntPtr windowHandle)
    {
        IntPtr workerW = GetDesktopWorkerW();
        if (workerW == IntPtr.Zero) return false;
        SetParent(windowHandle, workerW);
        return true;
    }

    public static void DetachFromDesktop(IntPtr windowHandle)
    {
        SetParent(windowHandle, IntPtr.Zero);
    }

    public static void SetToolWindow(IntPtr windowHandle)
    {
        int exStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        SetWindowLong(windowHandle, GWL_EXSTYLE, exStyle);
    }

    public static void SetTopmost(IntPtr windowHandle, bool topmost)
    {
        SetWindowPos(windowHandle,
            topmost ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
