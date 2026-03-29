using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeskGroup.Controls;

public enum HotkeyBoxCaptureMode { All, KeyboardOnly, MouseOnly }

public partial class HotkeyBox : UserControl
{
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

    private bool _isDetecting;
    private bool _updatingText;
    private Window? _captureWindow;

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

    // ── Detect toggle ──────────────────────────────────────────────────────────

    private void DetectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting)
            StopDetecting();
        else
            StartDetecting();
    }

    private void StartDetecting()
    {
        _isDetecting = true;
        DetectButton.IsChecked = true;
        HideStatus();

        _updatingText = true;
        HotkeyTextBox.Text = CaptureMode switch
        {
            HotkeyBoxCaptureMode.KeyboardOnly => "Press key combo (e.g. Ctrl+F2)\u2026",
            HotkeyBoxCaptureMode.MouseOnly    => "Press modifier(s) + mouse button\u2026",
            _                                 => "Press key combo or mouse button\u2026"
        };
        HotkeyTextBox.FontStyle = FontStyles.Italic;
        _updatingText = false;

        _captureWindow = Window.GetWindow(this);
        if (_captureWindow != null)
        {
            _captureWindow.PreviewKeyDown += OnWindowKeyDown;
            _captureWindow.Deactivated    += OnWindowDeactivated;
            if (CaptureMode != HotkeyBoxCaptureMode.KeyboardOnly)
            {
                _captureWindow.PreviewMouseDown  += OnWindowMouseDown;
                _captureWindow.PreviewMouseWheel += OnWindowMouseWheel;
            }
        }
    }

    private void StopDetecting()
    {
        if (!_isDetecting) return;
        _isDetecting = false;
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
        if (_captureWindow == null) return;
        _captureWindow.PreviewKeyDown    -= OnWindowKeyDown;
        _captureWindow.Deactivated       -= OnWindowDeactivated;
        _captureWindow.PreviewMouseDown  -= OnWindowMouseDown;
        _captureWindow.PreviewMouseWheel -= OnWindowMouseWheel;
        _captureWindow = null;
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => StopDetecting();

    // ── Keyboard capture ────────────────────────────────────────────────────────

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // In MouseOnly mode only modifiers are tracked (for BuildMouseCombo); ignore key captures
        if (CaptureMode == HotkeyBoxCaptureMode.MouseOnly)
        {
            if (key == Key.Escape) { StopDetecting(); }
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
        if ((mods & ModifierKeys.Control) != 0) sb.Append("Ctrl+");
        if ((mods & ModifierKeys.Alt)     != 0) sb.Append("Alt+");
        if ((mods & ModifierKeys.Shift)   != 0) sb.Append("Shift+");
        sb.Append(KeyToString(key));

        CaptureValue(sb.ToString());
        e.Handled = true;
    }

    // ── Mouse capture ───────────────────────────────────────────────────────────

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        switch (e.ChangedButton)
        {
            case MouseButton.Left:
                // If clicking our own detect button, let it through
                if (IsDescendant(e.OriginalSource as DependencyObject, DetectButton))
                    return;
                // Otherwise cancel detect and let the click reach its target
                ShowWarning("ℹ️ Left / Right click cannot be used as a hotkey.");
                StopDetecting();
                return;

            case MouseButton.Right:
                ShowWarning("ℹ️ Left / Right click cannot be used as a hotkey.");
                StopDetecting();
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

    private string BuildMouseCombo(string token)
    {
        var sb = new StringBuilder();
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) sb.Append("Ctrl+");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) sb.Append("Alt+");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) sb.Append("Shift+");
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
