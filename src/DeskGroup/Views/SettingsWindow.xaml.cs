using System;
using System.Windows;
using DeskGroup.ViewModels;

namespace DeskGroup.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private bool _applied = false;

    /// <summary>
    /// Fired when the user clicks Apply, so the main app can refresh hotkeys, theme, etc.
    /// </summary>
    public event EventHandler? SettingsApplied;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

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

    // ====================================================================
    // WINDOW LIFECYCLE
    // ====================================================================

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = _viewModel.Settings;

        // Restore position if valid
        if (!double.IsNaN(settings.SettingsWindowLeft) && !double.IsNaN(settings.SettingsWindowTop))
        {
            Left = settings.SettingsWindowLeft;
            Top = settings.SettingsWindowTop;
            Width = settings.SettingsWindowWidth;
            Height = settings.SettingsWindowHeight;

            // Ensure window is on screen
            var workingArea = SystemParameters.WorkArea;
            if (Left < workingArea.Left || Left > workingArea.Right - Width)
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (Top < workingArea.Top || Top > workingArea.Bottom - Height)
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window position
        var settings = _viewModel.Settings;
        settings.SettingsWindowLeft = Left;
        settings.SettingsWindowTop = Top;
        settings.SettingsWindowWidth = Width;
        settings.SettingsWindowHeight = Height;

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
        try { DragMove(); } catch { }
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
            if (!Services.HotkeyService.TryParseHotkey(_viewModel.ToggleAllHotkey, out _, out _))
            {
                errors.AppendLine($"Invalid hotkey format: \"{_viewModel.ToggleAllHotkey}\"");
            }
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.FullscreenToggleHotkey))
        {
            if (!Services.HotkeyService.TryParseHotkey(_viewModel.FullscreenToggleHotkey, out _, out _))
            {
                errors.AppendLine($"Invalid hotkey format: \"{_viewModel.FullscreenToggleHotkey}\"");
            }
        }

        if (errors.Length > 0)
        {
            MessageBox.Show(
                $"Please fix the following hotkey issues:\n\n{errors}\n" +
                "Valid format examples: Ctrl+Alt+G, F11, Ctrl+Shift+F5",
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

    /// <summary>
    /// Quick check if any setting differs from its stored value.
    /// We do a simple comparison on a few key fields.
    /// </summary>
    private bool HasUnsavedChanges()
    {
        // Since the ViewModel properties are bound two-way to the UI,
        // we can't easily detect "original" values without storing them.
        // For now, always assume changes if not applied.
        // A more robust approach would snapshot the original values in the constructor.
        return true;
    }
}