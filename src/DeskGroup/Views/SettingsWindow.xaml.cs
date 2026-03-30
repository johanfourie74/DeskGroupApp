using System;
using System.Windows;
using DeskGroup.ViewModels;

namespace DeskGroup.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private bool _applied = false;

    private sealed record SettingsSnapshot(
        Models.ThemeMode Theme,
        double GroupBackgroundOpacity,
        Models.GroupDockMode DefaultDockMode,
        bool DefaultSnapToGrid,
        int SnapGridSize,
        bool HideGroupedIcons,
        Models.IconSizeMode IconSizeMode,
        int CustomIconSize,
        bool SingleClickToLaunch,
        bool EnableShellContextMenu,
        bool StartWithWindows,
        bool StartMinimizedToTray,
        string ToggleAllHotkey,
        string ToggleAllMouseHotkey,
        Models.HotkeyMode ToggleAllHotkeyMode,
        string FullscreenToggleHotkey,
        string FullscreenMouseHotkey,
        Models.HotkeyMode FullscreenHotkeyMode,
        bool WatchForUngroupedIcons,
        bool AllowMultiMonitor,
        bool ShowDebugMenu,
        bool DesktopShortcutInstalled,
        bool PinnedToStart,
        bool PinnedToTaskbar,
        bool AutoUpdate,
        bool AutoInstall,
        bool OptInBugReporting
    );

    private SettingsSnapshot? _snapshot;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const uint WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 2;

    /// <summary>
    /// Fired when the user clicks Apply, so the main app can refresh hotkeys, theme, etc.
    /// </summary>
    public event EventHandler? SettingsApplied;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.OwnerWindow = this;
        TakeSnapshot();

        // Restore window position if previously saved
        Loaded += SettingsWindow_Loaded;
        Closing += SettingsWindow_Closing;

        // Enable F1 hotkey for Help
        KeyDown += SettingsWindow_KeyDown;

        // Set app version dynamically (after window loads)
        Loaded += (s, e) => SetVersionInfo();
    }

    private void SettingsWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // F1 = Show Help tab
        if (e.Key == System.Windows.Input.Key.F1)
        {
            ShowHelpTab();
            e.Handled = true;
        }
    }

    private void SetVersionInfo()
    {
        try
        {
            // Safely get version
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null && VersionText != null)
            {
                VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
            else if (VersionText != null)
            {
                VersionText.Text = "Version 1.3.0";
            }

            // Safely set copyright
            if (CopyrightText != null)
            {
                CopyrightText.Text = $"© {DateTime.Now.Year} Johan Fourie. All rights reserved.";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetVersionInfo failed: {ex.Message}");
            // Silent fail - not critical
        }
    }

    // ====================================================================
    // TAB SWITCHING
    // ====================================================================

    private void SettingsTab_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsTab();
    }

    private void AboutTab_Click(object sender, RoutedEventArgs e)
    {
        ShowAboutTab();
    }

    private void HelpTab_Click(object sender, RoutedEventArgs e)
    {
        ShowHelpTab();
    }

    private void UpdatesTab_Click(object sender, RoutedEventArgs e)
    {
        ShowUpdatesTab();
    }

    private void ShowSettingsTab()
    {
        // Update header
        HeaderTitle.Text = "Settings";
        HeaderSubtitle.Text = "Customize DeskGroup appearance and behavior";

        // Show Settings panel, hide others
        SettingsPanel.Visibility = Visibility.Visible;
        AboutPanel.Visibility = Visibility.Collapsed;
        HelpPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        // Update tab button styles (highlight active tab)
        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null)
            {
                SettingsTabButton.Background = accentBrush;
            }
        }
        catch
        {
            // Fallback to light blue if resource not found
            SettingsTabButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));
        }

        SettingsTabButton.FontWeight = FontWeights.SemiBold;
        AboutTabButton.ClearValue(BackgroundProperty);
        AboutTabButton.FontWeight = FontWeights.Normal;
        HelpTabButton.ClearValue(BackgroundProperty);
        HelpTabButton.FontWeight = FontWeights.Normal;
        UpdatesTabButton.ClearValue(BackgroundProperty);
        UpdatesTabButton.FontWeight = FontWeights.Normal;

        // Show bottom buttons (Apply/Reset)
        BottomButtonBar.Visibility = Visibility.Visible;
    }

    private void ShowAboutTab()
    {
        // Update header
        HeaderTitle.Text = "About";
        HeaderSubtitle.Text = "Application information and credits";

        // Show About panel, hide others
        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Visible;
        HelpPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        // Update tab button styles
        SettingsTabButton.ClearValue(BackgroundProperty);
        SettingsTabButton.FontWeight = FontWeights.Normal;

        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null)
            {
                AboutTabButton.Background = accentBrush;
            }
        }
        catch
        {
            AboutTabButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));
        }

        AboutTabButton.FontWeight = FontWeights.SemiBold;
        HelpTabButton.ClearValue(BackgroundProperty);
        HelpTabButton.FontWeight = FontWeights.Normal;
        UpdatesTabButton.ClearValue(BackgroundProperty);
        UpdatesTabButton.FontWeight = FontWeights.Normal;

        // Hide bottom buttons (not needed on About)
        BottomButtonBar.Visibility = Visibility.Collapsed;
    }

    private void ShowHelpTab()
    {
        // Update header
        HeaderTitle.Text = "Help";
        HeaderSubtitle.Text = "Quick reference and keyboard shortcuts";

        // Show Help panel, hide others
        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;
        HelpPanel.Visibility = Visibility.Visible;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        // Update tab button styles
        SettingsTabButton.ClearValue(BackgroundProperty);
        SettingsTabButton.FontWeight = FontWeights.Normal;
        AboutTabButton.ClearValue(BackgroundProperty);
        AboutTabButton.FontWeight = FontWeights.Normal;

        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null)
            {
                HelpTabButton.Background = accentBrush;
            }
        }
        catch
        {
            HelpTabButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));
        }

        HelpTabButton.FontWeight = FontWeights.SemiBold;
        UpdatesTabButton.ClearValue(BackgroundProperty);
        UpdatesTabButton.FontWeight = FontWeights.Normal;

        // Hide bottom buttons (not needed on Help)
        BottomButtonBar.Visibility = Visibility.Collapsed;
    }

    private void ShowUpdatesTab()
    {
        HeaderTitle.Text    = "Updates";
        HeaderSubtitle.Text = "Check for new versions of DeskGroup";

        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility    = Visibility.Collapsed;
        HelpPanel.Visibility     = Visibility.Collapsed;
        UpdatesPanel.Visibility  = Visibility.Visible;

        SettingsTabButton.ClearValue(BackgroundProperty);
        SettingsTabButton.FontWeight = FontWeights.Normal;
        AboutTabButton.ClearValue(BackgroundProperty);
        AboutTabButton.FontWeight = FontWeights.Normal;
        HelpTabButton.ClearValue(BackgroundProperty);
        HelpTabButton.FontWeight = FontWeights.Normal;

        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null) UpdatesTabButton.Background = accentBrush;
        }
        catch
        {
            UpdatesTabButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));
        }

        UpdatesTabButton.FontWeight = FontWeights.SemiBold;

        // Show Apply/Reset so AutoUpdate/AutoInstall can be saved
        BottomButtonBar.Visibility = Visibility.Visible;

        // Populate current version in the Updates panel
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (ver != null && UpdateCurrentVersionRun != null)
            UpdateCurrentVersionRun.Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    // ====================================================================
    // ABOUT TAB ACTIONS
    // ====================================================================

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/johanfourie74/DeskGroup",
                UseShellExecute = true
            });
        }
        catch
        {
            // Silent fail if browser won't open
        }
    }

    private void Contact_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/johanfourie74/DeskGroup/issues",
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Could not open browser.\n\n" +
                "Please visit: https://github.com/johanfourie74/DeskGroup/issues",
                "DeskGroup Contact",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = Helpers.CrashReporter.BuildManualIssueUrl();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Could not open browser.\n\n" +
                "Please visit: https://github.com/johanfourie74/DeskGroupApp/issues/new",
                "DeskGroup Report Bug",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    // ====================================================================
    // WINDOW LIFECYCLE
    // ====================================================================

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Always opens at CenterScreen (set in XAML); no position restore.
    }

    private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // ViewModel will auto-save via debounce
    }

    /// <summary>
    /// Whether settings were applied during this session.
    /// </summary>
    public bool WasApplied => _applied;

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate hotkey format before applying
            if (!ValidateHotkeys())
                return;

            // Execute the Apply command on the ViewModel
            _viewModel.ApplyCommand.Execute(null);
            _applied = true;

            SettingsApplied?.Invoke(this, EventArgs.Empty);

            // Show brief confirmation
            ApplyButton.Content = "✓ Applied";
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200)
            };
            timer.Tick += (s, args) =>
            {
                ApplyButton.Content = "Apply";
                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to apply settings:\n\n{ex.Message}",
                "Settings Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SettingsHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            e.Handled = true;
        }
    }

    private void TransparencySlider_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var slider = (System.Windows.Controls.Slider)sender;

        // Scroll up = increase, scroll down = decrease
        double change = e.Delta > 0 ? 0.05 : -0.05;
        double newValue = slider.Value + change;

        // Clamp to min/max
        newValue = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
        slider.Value = newValue;

        e.Handled = true;
    }

    /// <summary>
    /// Validate that the hotkey strings are in a parseable format.
    /// </summary>
    private bool ValidateHotkeys()
    {
        var errors = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(_viewModel.ToggleAllHotkey))
        {
            if (!Services.HotkeyService.IsValidHotkeyString(_viewModel.ToggleAllHotkey))
            {
                errors.AppendLine($"Invalid hotkey format: \"{_viewModel.ToggleAllHotkey}\"");
            }
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.FullscreenToggleHotkey))
        {
            if (!Services.HotkeyService.IsValidHotkeyString(_viewModel.FullscreenToggleHotkey))
            {
                errors.AppendLine($"Invalid hotkey format: \"{_viewModel.FullscreenToggleHotkey}\"");
            }
        }

        if (errors.Length > 0)
        {
            MessageBox.Show(
                $"Please fix the following hotkey issues:\n\n{errors}\n" +
                "Valid format examples: Ctrl+Alt+G, F11, Ctrl+Shift+F5, MiddleClick, XButton1",
                "Invalid Hotkey",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Prompt to save if closing without applying.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        // If settings were modified but not applied, ask the user
        if (!_applied && HasUnsavedChanges())
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Apply them before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    if (ValidateHotkeys())
                    {
                        _viewModel.ApplyCommand.Execute(null);
                        _applied = true;
                        SettingsApplied?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        e.Cancel = true; // Don't close, let them fix the hotkeys
                    }
                    break;

                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;

                case MessageBoxResult.No:
                    // Discard changes - just close
                    break;
            }
        }
    }

    private void TakeSnapshot()
    {
        _snapshot = new SettingsSnapshot(
            _viewModel.Theme,
            _viewModel.GroupBackgroundOpacity,
            _viewModel.DefaultDockMode,
            _viewModel.DefaultSnapToGrid,
            _viewModel.SnapGridSize,
            _viewModel.HideGroupedIcons,
            _viewModel.IconSizeMode,
            _viewModel.CustomIconSize,
            _viewModel.SingleClickToLaunch,
            _viewModel.EnableShellContextMenu,
            _viewModel.StartWithWindows,
            _viewModel.StartMinimizedToTray,
            _viewModel.ToggleAllHotkey,
            _viewModel.ToggleAllMouseHotkey,
            _viewModel.ToggleAllHotkeyMode,
            _viewModel.FullscreenToggleHotkey,
            _viewModel.FullscreenMouseHotkey,
            _viewModel.FullscreenHotkeyMode,
            _viewModel.WatchForUngroupedIcons,
            _viewModel.AllowMultiMonitor,
            _viewModel.ShowDebugMenu,
            _viewModel.DesktopShortcutInstalled,
            _viewModel.PinnedToStart,
            _viewModel.PinnedToTaskbar,
            _viewModel.AutoUpdate,
            _viewModel.AutoInstall,
            _viewModel.OptInBugReporting
        );
    }

    private bool HasUnsavedChanges()
    {
        if (_snapshot == null) return false;
        return _viewModel.Theme                 != _snapshot.Theme
            || _viewModel.GroupBackgroundOpacity != _snapshot.GroupBackgroundOpacity
            || _viewModel.DefaultDockMode        != _snapshot.DefaultDockMode
            || _viewModel.DefaultSnapToGrid      != _snapshot.DefaultSnapToGrid
            || _viewModel.SnapGridSize           != _snapshot.SnapGridSize
            || _viewModel.HideGroupedIcons       != _snapshot.HideGroupedIcons
            || _viewModel.IconSizeMode           != _snapshot.IconSizeMode
            || _viewModel.CustomIconSize         != _snapshot.CustomIconSize
            || _viewModel.SingleClickToLaunch    != _snapshot.SingleClickToLaunch
            || _viewModel.EnableShellContextMenu != _snapshot.EnableShellContextMenu
            || _viewModel.StartWithWindows       != _snapshot.StartWithWindows
            || _viewModel.StartMinimizedToTray   != _snapshot.StartMinimizedToTray
            || _viewModel.ToggleAllHotkey        != _snapshot.ToggleAllHotkey
            || _viewModel.ToggleAllMouseHotkey   != _snapshot.ToggleAllMouseHotkey
            || _viewModel.ToggleAllHotkeyMode    != _snapshot.ToggleAllHotkeyMode
            || _viewModel.FullscreenToggleHotkey != _snapshot.FullscreenToggleHotkey
            || _viewModel.FullscreenMouseHotkey  != _snapshot.FullscreenMouseHotkey
            || _viewModel.FullscreenHotkeyMode   != _snapshot.FullscreenHotkeyMode
            || _viewModel.WatchForUngroupedIcons != _snapshot.WatchForUngroupedIcons
            || _viewModel.AllowMultiMonitor      != _snapshot.AllowMultiMonitor
            || _viewModel.ShowDebugMenu          != _snapshot.ShowDebugMenu
            || _viewModel.DesktopShortcutInstalled != _snapshot.DesktopShortcutInstalled
            || _viewModel.PinnedToStart          != _snapshot.PinnedToStart
            || _viewModel.PinnedToTaskbar        != _snapshot.PinnedToTaskbar
            || _viewModel.AutoUpdate             != _snapshot.AutoUpdate
            || _viewModel.AutoInstall            != _snapshot.AutoInstall
            || _viewModel.OptInBugReporting      != _snapshot.OptInBugReporting;
    }
}