using System;
using System.Runtime.InteropServices;

namespace Windows.Win32
{
    // Lightweight public wrappers that mirror CsWin32 generated shapes.
    public readonly struct HWND
    {
        public IntPtr Value { get; }
        public HWND(IntPtr value) => Value = value;
        public static implicit operator HWND(IntPtr v) => new HWND(v);
        public static implicit operator IntPtr(HWND h) => h.Value;
    }

    public readonly struct HICON
    {
        public IntPtr Value { get; }
        public HICON(IntPtr value) => Value = value;
        public static implicit operator HICON(IntPtr v) => new HICON(v);
        public static implicit operator IntPtr(HICON h) => h.Value;
    }

    public readonly struct HINSTANCE
    {
        public IntPtr Value { get; }
        public HINSTANCE(IntPtr value) => Value = value;
        public static readonly HINSTANCE NULL = new HINSTANCE(IntPtr.Zero);
        public static implicit operator HINSTANCE(IntPtr v) => new HINSTANCE(v);
        public static implicit operator IntPtr(HINSTANCE h) => h.Value;
    }

    public readonly struct WPARAM
    {
        public nuint Value { get; }
        public WPARAM(nuint v) => Value = v;
        public static implicit operator WPARAM(nuint v) => new WPARAM(v);
    }

    public readonly struct LPARAM
    {
        public nint Value { get; }
        public LPARAM(nint v) => Value = v;
        public static implicit operator LPARAM(nint v) => new LPARAM(v);
    }

    public enum SMTO : uint
    {
        NORMAL = 0x0000
    }

    public enum WM : uint { }

    public static class PInvoke
    {
        // user32
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
        private static extern IntPtr _FindWindowW(string lpClassName, string lpWindowName);
        public static HWND FindWindowW(string? lpClassName, string? lpWindowName) => new HWND(_FindWindowW(lpClassName ?? string.Empty, lpWindowName ?? string.Empty));

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
        private static extern IntPtr _FindWindowExW(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        public static HWND FindWindowEx(HWND hwndParent, HWND hwndChildAfter, string? lpszClass, string? lpszWindow) => new HWND(_FindWindowExW(hwndParent, hwndChildAfter, lpszClass ?? string.Empty, lpszWindow ?? string.Empty));

        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
        private static extern IntPtr _GetDesktopWindow();
        public static HWND GetDesktopWindow() => new HWND(_GetDesktopWindow());

        [DllImport("user32.dll", EntryPoint = "GetShellWindow")]
        private static extern IntPtr _GetShellWindow();
        public static HWND GetShellWindow() => new HWND(_GetShellWindow());

        [DllImport("user32.dll", EntryPoint = "SetParent")]
        private static extern IntPtr _SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        public static HWND SetParent(HWND hWndChild, HWND hWndNewParent) => new HWND(_SetParent(hWndChild, hWndNewParent));

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        public static BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags) => new BOOL(_SetWindowPos(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags));

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int _GetWindowLong(IntPtr hWnd, int nIndex);
        public static int GetWindowLong(HWND hWnd, int nIndex) => _GetWindowLong(hWnd.Value, nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int _SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        public static int SetWindowLong(HWND hWnd, int nIndex, int dwNewLong) => _SetWindowLong(hWnd.Value, nIndex, dwNewLong);

        [DllImport("user32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _ShowWindow(IntPtr hWnd, int nCmdShow);
        public static BOOL ShowWindow(HWND hWnd, int nCmdShow) => new BOOL(_ShowWindow(hWnd.Value, nCmdShow));

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr _SendMessage(IntPtr hWnd, uint Msg, nuint wParam, nint lParam);
        public static IntPtr SendMessage(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam) => _SendMessage(hWnd.Value, Msg, wParam.Value, lParam.Value);

        [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _SendMessageTimeout(IntPtr hWnd, uint Msg, nuint wParam, nint lParam, uint fuFlags, uint uTimeout, out nuint lpdwResult);
        public static BOOL SendMessageTimeout(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam, SMTO fuFlags, uint uTimeout, out nuint lpdwResult) => new BOOL(_SendMessageTimeout(hWnd.Value, Msg, wParam.Value, lParam.Value, (uint)fuFlags, uTimeout, out lpdwResult));

        // shell32
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconW")]
        private static extern IntPtr _ExtractIconW(IntPtr hInst, string lpszExeFileName, int nIconIndex);
        public static HICON ExtractIconW(HINSTANCE hInst, string lpszExeFileName, int nIconIndex) => new HICON(_ExtractIconW(hInst.Value, lpszExeFileName, nIconIndex));

        [DllImport("user32.dll", EntryPoint = "DestroyIcon")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool _DestroyIcon(IntPtr hIcon);
        public static BOOL DestroyIcon(HICON hIcon) => new BOOL(_DestroyIcon(hIcon.Value));
    }

    // Helper BOOL wrapper
    public readonly struct BOOL
    {
        private readonly bool _v;
        public BOOL(bool v) => _v = v;
        public bool IsTrue() => _v;
        public static implicit operator BOOL(bool v) => new BOOL(v);
    }
}
