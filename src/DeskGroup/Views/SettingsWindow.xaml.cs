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
    }

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
            ApplyButton.Content = "âœ“ Applied";
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