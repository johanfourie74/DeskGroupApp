using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeskGroup.Controls;

public enum HotkeyBoxCaptureMode { All, KeyboardOnly, MouseOnly }

public partial class HotkeyBox : UserControl
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int nVirtKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

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

    private const int WH_MOUSE_LL = 14;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_SHIFT = 0x10;

    // ── Dependency Property ────────────────────────────────────────────────────

    public static readonly DependencyProperty HotkeyValueProperty =
        DependencyProperty.Register(nameof(HotkeyValue), typeof(string), typeof(HotkeyBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHotkeyValueChanged));

    public string HotkeyValue
    {
        get => (string)GetValue(HotkeyValueProperty);
        set => SetValue(HotkeyValueProperty, value);
    }

    private static void OnHotkeyValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyBox box && !box._updatingText)
        {
            box._updatingText = true;
            box.HotkeyTextBox.Text = (string)(e.NewValue ?? string.Empty);
            box.HotkeyTextBox.FontStyle = FontStyles.Normal;
            box._updatingText = false;
        }
    }

    public static readonly DependencyProperty CaptureModeProperty =
        DependencyProperty.Register(nameof(CaptureMode), typeof(HotkeyBoxCaptureMode), typeof(HotkeyBox),
            new PropertyMetadata(HotkeyBoxCaptureMode.All));

    public HotkeyBoxCaptureMode CaptureMode
    {
        get => (HotkeyBoxCaptureMode)GetValue(CaptureModeProperty);
        set => SetValue(CaptureModeProperty, value);
    }

    // ── State ──────────────────────────────────────────────────────────────────

    private static HotkeyBox? s_activeDetectBox;
    private bool _isDetecting;
    private bool _updatingText;
    private Window? _captureWindow;
    private IntPtr _detectMouseHookHandle = IntPtr.Zero;
    private LowLevelMouseProc? _detectMouseHookDelegate;

    public HotkeyBox()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _updatingText = true;
        HotkeyTextBox.Text = HotkeyValue ?? string.Empty;
        _updatingText = false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopDetecting();

    // ── Manual edit ────────────────────────────────────────────────────────────

    private void HotkeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingText || _isDetecting) return;
        HotkeyValue = HotkeyTextBox.Text;
        HideStatus();
    }

    private void HotkeyTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (_isDetecting) e.Handled = true;
    }

    private void HotkeyTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // In MouseOnly mode (or if user Ctrl/Alt/Shift-clicks the textbox while detecting),
        // clicking directly inside the box starts mouse detection automatically!
        if (!_isDetecting && CaptureMode == HotkeyBoxCaptureMode.MouseOnly)
        {
            StartDetecting();
            e.Handled = true;
        }
    }

    // ── Detect & Clear buttons ─────────────────────────────────────────────────

    private void DetectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting)
            StopDetecting();
        else
            StartDetecting();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        StopDetecting();
        HotkeyValue = string.Empty;
        _updatingText = true;
        HotkeyTextBox.Text = string.Empty;
        HotkeyTextBox.FontStyle = FontStyles.Normal;
        _updatingText = false;
        HideStatus();
    }

    private void StartDetecting()
    {
        if (s_activeDetectBox != null && !ReferenceEquals(s_activeDetectBox, this))
            s_activeDetectBox.StopDetecting();

        s_activeDetectBox = this;
        _isDetecting = true;
        Services.HotkeyService.SuppressGlobalHotkeys = true;
        DetectButton.IsChecked = true;
        HideStatus();

        _updatingText = true;
        HotkeyTextBox.Text = CaptureMode switch
        {
            HotkeyBoxCaptureMode.KeyboardOnly => "Press key combo (e.g. Ctrl+F2)\u2026",
            HotkeyBoxCaptureMode.MouseOnly    => "Click Middle/Side button, Scroll, or Ctrl+Click\u2026",
            _                                 => "Press key combo or mouse button\u2026"
        };
        HotkeyTextBox.FontStyle = FontStyles.Italic;
        _updatingText = false;

        _captureWindow = Window.GetWindow(this);
        if (_captureWindow != null)
        {
            _captureWindow.Activate();
            HotkeyTextBox.Focus();
            Keyboard.Focus(HotkeyTextBox);

            _captureWindow.PreviewKeyDown += OnWindowKeyDown;
            _captureWindow.Deactivated    += OnWindowDeactivated;
            if (CaptureMode != HotkeyBoxCaptureMode.KeyboardOnly)
            {
                _captureWindow.PreviewMouseDown  += OnWindowMouseDown;
                _captureWindow.PreviewMouseWheel += OnWindowMouseWheel;
                StartDetectMouseHook();
            }
        }
    }

    private void StopDetecting()
    {
        if (!_isDetecting) return;
        _isDetecting = false;
        if (ReferenceEquals(s_activeDetectBox, this))
        {
            s_activeDetectBox = null;
            Services.HotkeyService.SuppressGlobalHotkeys = false;
        }
        DetectButton.IsChecked = false;
        UnhookEvents();

        _updatingText = true;
        HotkeyTextBox.Text = HotkeyValue ?? string.Empty;
        HotkeyTextBox.FontStyle = FontStyles.Normal;
        _updatingText = false;
    }

    private void CaptureValue(string value)
    {
        if (string.Equals(value, HotkeyValue, StringComparison.OrdinalIgnoreCase))
        {
            ShowHint("\u2139\uFE0F Already your active hotkey \u2014 no change made.");
            StopDetecting();
            return;
        }

        _isDetecting = false;
        if (ReferenceEquals(s_activeDetectBox, this))
        {
            s_activeDetectBox = null;
            Services.HotkeyService.SuppressGlobalHotkeys = false;
        }
        DetectButton.IsChecked = false;
        UnhookEvents();

        HotkeyValue = value;
        _updatingText = true;
        HotkeyTextBox.Text = value;
        HotkeyTextBox.FontStyle = FontStyles.Normal;
        _updatingText = false;
        HideStatus();
    }

    private void UnhookEvents()
    {
        StopDetectMouseHook();
        if (_captureWindow == null) return;
        _captureWindow.PreviewKeyDown    -= OnWindowKeyDown;
        _captureWindow.Deactivated       -= OnWindowDeactivated;
        _captureWindow.PreviewMouseDown  -= OnWindowMouseDown;
        _captureWindow.PreviewMouseWheel -= OnWindowMouseWheel;
        _captureWindow = null;
    }

    private void StartDetectMouseHook()
    {
        if (_detectMouseHookHandle != IntPtr.Zero) return;
        _detectMouseHookDelegate = DetectMouseHookProc;
        IntPtr hMod = GetModuleHandle(null);
        _detectMouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _detectMouseHookDelegate, hMod, 0);
        if (_detectMouseHookHandle == IntPtr.Zero && hMod != IntPtr.Zero)
            _detectMouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _detectMouseHookDelegate, IntPtr.Zero, 0);
    }

    private void StopDetectMouseHook()
    {
        if (_detectMouseHookHandle == IntPtr.Zero) return;
        UnhookWindowsHookEx(_detectMouseHookHandle);
        _detectMouseHookHandle = IntPtr.Zero;
        _detectMouseHookDelegate = null;
    }

    private IntPtr DetectMouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isDetecting && CaptureMode != HotkeyBoxCaptureMode.KeyboardOnly)
        {
            int msg = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            string? token = msg switch
            {
                WM_MBUTTONDOWN => "MiddleClick",
                WM_XBUTTONDOWN => ((data.mouseData >> 16) & 0xFFFF) == 2 ? "XButton2" : "XButton1",
                WM_MOUSEWHEEL  => (short)((data.mouseData >> 16) & 0xFFFF) > 0 ? "ScrollUp" : "ScrollDown",
                _              => null
            };

            if (token != null)
            {
                string combo = BuildMouseCombo(token);
                Dispatcher.BeginInvoke(() =>
                {
                    if (_isDetecting)
                        CaptureValue(combo);
                });
                // Swallow the middle/side/scroll input during detection so it doesn't scroll Settings or navigate back
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(_detectMouseHookHandle, nCode, wParam, lParam);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => StopDetecting();

    // ── Keyboard capture ────────────────────────────────────────────────────────

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // In MouseOnly mode only modifiers are tracked (for BuildMouseCombo); do NOT mark modifiers handled
        if (CaptureMode == HotkeyBoxCaptureMode.MouseOnly)
        {
            if (key == Key.Escape)
            {
                StopDetecting();
                e.Handled = true;
                return;
            }
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                    or Key.LeftShift or Key.RightShift)
            {
                ShowHint("Modifier held \u2014 now press a mouse button or scroll wheel\u2026");
                return;
            }
            ShowHint("This box is for mouse shortcuts (e.g. MiddleClick, XButton1, ScrollUp, Ctrl+RightClick).");
            e.Handled = true;
            return;
        }

        // Show hint when a standalone modifier is pressed
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            ShowHint("Hold modifier(s) then press a key \u2014 e.g. Ctrl+F2");
            e.Handled = true;
            return;
        }

        // ESC cancels detect without capturing
        if (key == Key.Escape)
        {
            StopDetecting();
            e.Handled = true;
            return;
        }

        var mods = e.KeyboardDevice.Modifiers;
        var sb = new StringBuilder();
        if ((mods & ModifierKeys.Control) != 0 || IsKeyDownAsync(VK_CONTROL)) sb.Append("Ctrl+");
        if ((mods & ModifierKeys.Alt)     != 0 || IsKeyDownAsync(VK_MENU))    sb.Append("Alt+");
        if ((mods & ModifierKeys.Shift)   != 0 || IsKeyDownAsync(VK_SHIFT))   sb.Append("Shift+");
        sb.Append(KeyToString(key));

        CaptureValue(sb.ToString());
        e.Handled = true;
    }

    // ── Mouse capture ───────────────────────────────────────────────────────────

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        bool hasModifier = HasAnyModifierDown();

        switch (e.ChangedButton)
        {
            case MouseButton.Left:
                // If clicking our own detect or clear button, let it through
                if (IsDescendant(e.OriginalSource as DependencyObject, DetectButton) ||
                    IsDescendant(e.OriginalSource as DependencyObject, ClearButton))
                    return;

                // Allow Ctrl/Alt/Shift + LeftClick as a valid mouse shortcut!
                if (hasModifier)
                {
                    CaptureValue(BuildMouseCombo("LeftClick"));
                    e.Handled = true;
                    return;
                }

                // If clicking inside our own HotkeyTextBox without modifiers, stay in detect mode and guide the user
                if (IsDescendant(e.OriginalSource as DependencyObject, HotkeyTextBox))
                {
                    ShowHint("\u2139\uFE0F Press MiddleClick, Side button (XButton1/2), Scroll wheel, or hold Ctrl/Alt/Shift + Click.");
                    e.Handled = true;
                    return;
                }

                // Clicked outside this HotkeyBox — cancel detect cleanly
                StopDetecting();
                return;

            case MouseButton.Right:
                // Allow Ctrl/Alt/Shift + RightClick as a valid mouse shortcut!
                if (hasModifier)
                {
                    CaptureValue(BuildMouseCombo("RightClick"));
                    e.Handled = true;
                    return;
                }
                ShowWarning("\u2139\uFE0F Hold Ctrl, Alt, or Shift with RightClick, or use MiddleClick / XButton1 / Scroll.");
                e.Handled = true; // prevent context menu
                return;

            case MouseButton.Middle:
                CaptureValue(BuildMouseCombo("MiddleClick"));
                e.Handled = true;
                return;

            case MouseButton.XButton1:
                CaptureValue(BuildMouseCombo("XButton1"));
                e.Handled = true;
                return;

            case MouseButton.XButton2:
                CaptureValue(BuildMouseCombo("XButton2"));
                e.Handled = true;
                return;
        }
    }

    private void OnWindowMouseWheel(object sender, MouseWheelEventArgs e)
    {
        CaptureValue(BuildMouseCombo(e.Delta > 0 ? "ScrollUp" : "ScrollDown"));
        e.Handled = true;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static bool IsKeyDownAsync(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool HasAnyModifierDown() =>
        Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || IsKeyDownAsync(VK_CONTROL) ||
        Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) || IsKeyDownAsync(VK_MENU) ||
        Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || IsKeyDownAsync(VK_SHIFT);

    private string BuildMouseCombo(string token)
    {
        var sb = new StringBuilder();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) || IsKeyDownAsync(VK_CONTROL)) sb.Append("Ctrl+");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) || IsKeyDownAsync(VK_MENU)) sb.Append("Alt+");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || IsKeyDownAsync(VK_SHIFT)) sb.Append("Shift+");
        sb.Append(token);
        return sb.ToString();
    }

    private void ShowWarning(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFF, 0xA5, 0x00));
        StatusText.Visibility = Visibility.Visible;
    }

    private void ShowHint(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        StatusText.Visibility = Visibility.Visible;
    }

    private void HideStatus() => StatusText.Visibility = Visibility.Collapsed;

    private static string KeyToString(Key key) => key switch
    {
        Key.D0 or Key.NumPad0 => "0",
        Key.D1 or Key.NumPad1 => "1",
        Key.D2 or Key.NumPad2 => "2",
        Key.D3 or Key.NumPad3 => "3",
        Key.D4 or Key.NumPad4 => "4",
        Key.D5 or Key.NumPad5 => "5",
        Key.D6 or Key.NumPad6 => "6",
        Key.D7 or Key.NumPad7 => "7",
        Key.D8 or Key.NumPad8 => "8",
        Key.D9 or Key.NumPad9 => "9",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.Up => "Up",
        Key.Down => "Down",
        _ => key.ToString()
    };

    private static bool IsDescendant(DependencyObject? child, DependencyObject? ancestor)
    {
        var current = child;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
