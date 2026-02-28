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

    private const int WM_HOTKEY = 0x0312;

    [Flags]
    private enum HotkeyModifiers : uint
    {
        None = 0x0000, Alt = 0x0001, Ctrl = 0x0002,
        Shift = 0x0004, Win = 0x0008, NoRepeat = 0x4000
    }

    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _nextId = 9000;
    private bool _disposed = false;

    public event EventHandler<string>? RegistrationFailed;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    public int RegisterHotkey(string hotkeyString, Action callback)
    {
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("HotkeyService not initialized. Call Initialize() first.");

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
            int errorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
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
        if (_windowHandle != IntPtr.Zero && _hotkeyActions.ContainsKey(id))
        {
            UnregisterHotKey(_windowHandle, id);
            _hotkeyActions.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys)
            UnregisterHotKey(_windowHandle, id);
        _hotkeyActions.Clear();
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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
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