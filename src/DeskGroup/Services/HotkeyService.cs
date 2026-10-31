using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DeskGroup.Services;

public class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int nVirtKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>
    /// Set to true while a HotkeyBox is actively recording a shortcut so bound global
    /// keyboard/mouse hotkeys don't trigger and steal focus mid-capture.
    /// </summary>
    public static bool SuppressGlobalHotkeys { get; set; }

    private const int WM_HOTKEY = 0x0312;
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_SHIFT = 0x10;

    private static readonly HashSet<string> _mouseTokens = new(StringComparer.OrdinalIgnoreCase)
        { "XButton1", "XButton2", "MiddleClick", "ScrollUp", "ScrollDown", "RightClick", "LeftClick" };

    [Flags]
    private enum HotkeyModifiers : uint
    {
        None = 0x0000, Alt = 0x0001, Ctrl = 0x0002,
        Shift = 0x0004, Win = 0x0008, NoRepeat = 0x4000
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private sealed class MouseHotkeyEntry
    {
        public required string Token { get; init; }
        public uint Modifiers { get; init; }
        public required Action Callback { get; init; }
        public int Id { get; init; }
    }

    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _nextId = 9000;
    private bool _disposed = false;

    private IntPtr _mouseHookHandle = IntPtr.Zero;
    private LowLevelMouseProc? _mouseProcDelegate;
    private readonly Dictionary<int, MouseHotkeyEntry> _mouseHotkeys = new();
    private int _nextMouseId = 8000;

    public event EventHandler<string>? RegistrationFailed;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (_windowHandle == helper.Handle && _hwndSource != null) return;
        _hwndSource?.RemoveHook(WndProc);
        _windowHandle = helper.Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    public int RegisterHotkey(string hotkeyString, Action callback)
    {
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("HotkeyService not initialized. Call Initialize() first.");

        if (IsMouseHotkeyString(hotkeyString))
            return RegisterMouseHotkey(hotkeyString, callback);

        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            RegistrationFailed?.Invoke(this, $"Could not parse hotkey: {hotkeyString}");
            return -1;
        }

        int id = _nextId++;
        bool success = RegisterHotKey(_windowHandle, id, modifiers | (uint)HotkeyModifiers.NoRepeat, vk);
        if (!success)
        {
            System.Diagnostics.Debug.WriteLine($"❌ RegisterHotKey FAILED for '{hotkeyString}' (ID: {id})");
            int errorCode = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"   Win32 Error Code: {errorCode}");
            RegistrationFailed?.Invoke(this, $"Failed to register hotkey '{hotkeyString}'. It may be in use by another application.");
            return -1;
        }
        System.Diagnostics.Debug.WriteLine($"✅ RegisterHotKey SUCCESS for '{hotkeyString}' (ID: {id})");
        _hotkeyActions[id] = callback;
        return id;
    }

    public void UnregisterHotkeyById(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            if (_windowHandle != IntPtr.Zero) UnregisterHotKey(_windowHandle, id);
            _hotkeyActions.Remove(id);
        }
        else if (_mouseHotkeys.ContainsKey(id))
        {
            _mouseHotkeys.Remove(id);
            if (_mouseHotkeys.Count == 0) StopMouseHook();
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys)
            UnregisterHotKey(_windowHandle, id);
        _hotkeyActions.Clear();
        _mouseHotkeys.Clear();
        StopMouseHook();
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    public static bool IsValidHotkeyString(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString)) return true;
        return TryParseHotkey(hotkeyString, out _, out _) || IsMouseHotkeyString(hotkeyString);
    }

    public static bool IsMouseHotkeyString(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;
        return TryParseMouseHotkey(hotkeyString, out _, out _);
    }

    public static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].ToUpperInvariant();
            if (i < parts.Length - 1)
            {
                switch (part)
                {
                    case "CTRL": case "CONTROL": modifiers |= (uint)HotkeyModifiers.Ctrl; break;
                    case "ALT": modifiers |= (uint)HotkeyModifiers.Alt; break;
                    case "SHIFT": modifiers |= (uint)HotkeyModifiers.Shift; break;
                    case "WIN": case "WINDOWS": modifiers |= (uint)HotkeyModifiers.Win; break;
                    default: return false;
                }
            }
            else
            {
                if (part is "LEFTARROW" or "ARROWLEFT") part = "LEFT";
                else if (part is "RIGHTARROW" or "ARROWRIGHT") part = "RIGHT";
                else if (part is "UPARROW" or "ARROWUP") part = "UP";
                else if (part is "DOWNARROW" or "ARROWDOWN") part = "DOWN";
                if (Enum.TryParse<Key>(part, ignoreCase: true, out Key key))
                    virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                else if (part.StartsWith("F") && int.TryParse(part[1..], out int fNum) && fNum >= 1 && fNum <= 24)
                    virtualKey = (uint)(0x70 + fNum - 1);
                else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                    virtualKey = (uint)char.ToUpper(part[0]);
                else
                    return false;
            }
        }
        return virtualKey != 0;
    }

    // ── Mouse hotkey registration ─────────────────────────────────────────────

    private int RegisterMouseHotkey(string hotkeyString, Action callback)
    {
        if (!TryParseMouseHotkey(hotkeyString, out string token, out uint modifiers))
        {
            RegistrationFailed?.Invoke(this, $"Could not parse mouse hotkey: {hotkeyString}");
            return -1;
        }

        int id = _nextMouseId++;
        _mouseHotkeys[id] = new MouseHotkeyEntry { Token = token, Modifiers = modifiers, Callback = callback, Id = id };

        if (_mouseHookHandle == IntPtr.Zero)
            StartMouseHook();

        System.Diagnostics.Debug.WriteLine($"✅ RegisterMouseHotkey SUCCESS for '{hotkeyString}' (ID: {id})");
        return id;
    }

    private static bool TryParseMouseHotkey(string hotkeyString, out string token, out uint modifiers)
    {
        token = string.Empty;
        modifiers = 0;
        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": modifiers |= (uint)HotkeyModifiers.Ctrl; break;
                case "ALT": modifiers |= (uint)HotkeyModifiers.Alt; break;
                case "SHIFT": modifiers |= (uint)HotkeyModifiers.Shift; break;
                default: return false;
            }
        }
        token = parts[^1];
        if (!_mouseTokens.Contains(token)) return false;

        // Bare LeftClick or RightClick without modifiers cannot be a global hotkey
        if ((token.Equals("LeftClick", StringComparison.OrdinalIgnoreCase) ||
             token.Equals("RightClick", StringComparison.OrdinalIgnoreCase)) && modifiers == 0)
        {
            return false;
        }

        return true;
    }

    // ── WH_MOUSE_LL hook ──────────────────────────────────────────────────────

    private void StartMouseHook()
    {
        _mouseProcDelegate = MouseHookProc;
        IntPtr hMod = GetModuleHandle(null);
        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProcDelegate, hMod, 0);
        if (_mouseHookHandle == IntPtr.Zero && hMod != IntPtr.Zero)
            _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProcDelegate, IntPtr.Zero, 0);

        if (_mouseHookHandle == IntPtr.Zero)
            System.Diagnostics.Debug.WriteLine($"❌ SetWindowsHookEx(WH_MOUSE_LL) FAILED: {Marshal.GetLastWin32Error()}");
        else
            System.Diagnostics.Debug.WriteLine("✅ WH_MOUSE_LL hook installed");
    }

    private void StopMouseHook()
    {
        if (_mouseHookHandle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_mouseHookHandle);
        _mouseHookHandle = IntPtr.Zero;
        _mouseProcDelegate = null;
        System.Diagnostics.Debug.WriteLine("WH_MOUSE_LL hook removed");
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _mouseHotkeys.Count > 0 && !SuppressGlobalHotkeys)
        {
            int msg = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            string? token = msg switch
            {
                WM_MBUTTONDOWN  => "MiddleClick",
                WM_XBUTTONDOWN  => ((data.mouseData >> 16) & 0xFFFF) == 2 ? "XButton2" : "XButton1",
                WM_MOUSEWHEEL   => (short)((data.mouseData >> 16) & 0xFFFF) > 0 ? "ScrollUp" : "ScrollDown",
                WM_RBUTTONDOWN  => "RightClick",
                WM_LBUTTONDOWN  => "LeftClick",
                _               => null
            };

            if (token != null)
            {
                uint mods = GetCurrentKeyModifiers();
                // Require at least one modifier for LeftClick / RightClick
                if ((msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN) && mods == 0)
                    return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

                foreach (var entry in _mouseHotkeys.Values)
                {
                    if (string.Equals(entry.Token, token, StringComparison.OrdinalIgnoreCase) &&
                        entry.Modifiers == mods)
                    {
                        var cb = entry.Callback;
                        Application.Current?.Dispatcher.BeginInvoke(cb);
                        break;
                    }
                }
            }
        }
        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private static uint GetCurrentKeyModifiers()
    {
        uint m = 0;
        if (((GetAsyncKeyState(VK_CONTROL) | GetKeyState(VK_CONTROL)) & 0x8000) != 0) m |= (uint)HotkeyModifiers.Ctrl;
        if (((GetAsyncKeyState(VK_MENU)    | GetKeyState(VK_MENU))    & 0x8000) != 0) m |= (uint)HotkeyModifiers.Alt;
        if (((GetAsyncKeyState(VK_SHIFT)   | GetKeyState(VK_SHIFT))   & 0x8000) != 0) m |= (uint)HotkeyModifiers.Shift;
        return m;
    }

    // ── WM_HOTKEY WndProc ─────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && !SuppressGlobalHotkeys)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
    }
}
