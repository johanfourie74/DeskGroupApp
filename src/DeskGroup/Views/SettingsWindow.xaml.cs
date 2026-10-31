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
        string ToggleEdgeDockHotkey,
        string ToggleEdgeDockMouseHotkey,
        Models.HotkeyMode ToggleEdgeDockHotkeyMode,
        bool EnableEdgeDockArrowHotkeys,
        bool WatchForUngroupedIcons,
        bool AllowMultiMonitor,
        bool ShowDebugMenu,
        bool DesktopShortcutInstalled,
        bool PinnedToStart,
        bool PinnedToTaskbar,
        bool AutoUpdate,
        bool AutoInstall,
        bool OptInBugReporting,
        bool EnableEdgeDock,
        Models.SideDockMonitorMode EdgeDockMonitorMode,
        Models.DockEdge PrimaryDockEdge,
        Models.SideDockLockState EdgeDockLockState,
        bool EdgeDockIsPinned,
        bool EdgeDockShowOnHover,
        bool EdgeDockShowBubble,
        int EdgeDockBubbleSize,
        int EdgeDockIconSize,
        bool EdgeLeftEnabled,
        bool EdgeTopEnabled,
        bool EdgeRightEnabled,
        bool EdgeBottomEnabled
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

        Loaded += SettingsWindow_Loaded;
        Closing += SettingsWindow_Closing;
        KeyDown += SettingsWindow_KeyDown;
        Loaded += (s, e) => SetVersionInfo();
    }

    private void SettingsWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
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
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null && VersionText != null)
            {
                VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
            else if (VersionText != null)
            {
                VersionText.Text = "Version 1.4.6";
            }

            if (CopyrightText != null)
            {
                CopyrightText.Text = $"© {DateTime.Now.Year} Johan Fourie. All rights reserved.";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetVersionInfo failed: {ex.Message}");
        }
    }

    private void SettingsTab_Click(object sender, RoutedEventArgs e) => ShowSettingsTab();
    private void AboutTab_Click(object sender, RoutedEventArgs e) => ShowAboutTab();
    private void HelpTab_Click(object sender, RoutedEventArgs e) => ShowHelpTab();
    private void UpdatesTab_Click(object sender, RoutedEventArgs e) => ShowUpdatesTab();

    private void ShowSettingsTab()
    {
        HeaderTitle.Text = "Settings";
        HeaderSubtitle.Text = "Customize DeskGroup appearance and behavior";

        SettingsPanel.Visibility = Visibility.Visible;
        AboutPanel.Visibility = Visibility.Collapsed;
        HelpPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null) SettingsTabButton.Background = accentBrush;
        }
        catch
        {
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

        BottomButtonBar.Visibility = Visibility.Visible;
    }

    private void ShowAboutTab()
    {
        HeaderTitle.Text = "About";
        HeaderSubtitle.Text = "Application information and credits";

        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Visible;
        HelpPanel.Visibility = Visibility.Collapsed;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        SettingsTabButton.ClearValue(BackgroundProperty);
        SettingsTabButton.FontWeight = FontWeights.Normal;

        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null) AboutTabButton.Background = accentBrush;
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

        BottomButtonBar.Visibility = Visibility.Collapsed;
    }

    private void ShowHelpTab()
    {
        HeaderTitle.Text = "Help";
        HeaderSubtitle.Text = "Quick reference and keyboard shortcuts";

        SettingsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;
        HelpPanel.Visibility = Visibility.Visible;
        UpdatesPanel.Visibility = Visibility.Collapsed;

        SettingsTabButton.ClearValue(BackgroundProperty);
        SettingsTabButton.FontWeight = FontWeights.Normal;
        AboutTabButton.ClearValue(BackgroundProperty);
        AboutTabButton.FontWeight = FontWeights.Normal;

        try
        {
            var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush;
            if (accentBrush != null) HelpTabButton.Background = accentBrush;
        }
        catch
        {
            HelpTabButton.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 120, 212));
        }

        HelpTabButton.FontWeight = FontWeights.SemiBold;
        UpdatesTabButton.ClearValue(BackgroundProperty);
        UpdatesTabButton.FontWeight = FontWeights.Normal;

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
        BottomButtonBar.Visibility = Visibility.Visible;

        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (ver != null && UpdateCurrentVersionRun != null)
            UpdateCurrentVersionRun.Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}";
    }

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
        catch { }
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

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e) { }

    private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) { }

    public bool WasApplied => _applied;

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ApplyCommand.ExecuteAsync(null);
            if (!_viewModel.LastApplySucceeded)
                return;

            _applied = true;
            TakeSnapshot();

            SettingsApplied?.Invoke(this, EventArgs.Empty);

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
        double change = e.Delta > 0 ? 0.05 : -0.05;
        double newValue = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value + change));
        slider.Value = newValue;
        e.Handled = true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

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
                    _viewModel.ApplyCommand.Execute(null);
                    if (_viewModel.LastApplySucceeded)
                    {
                        _applied = true;
                        SettingsApplied?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        e.Cancel = true;
                    }
                    break;

                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;

                case MessageBoxResult.No:
                    // Revert live preview changes back to snapshot!
                    RestoreFromSnapshot();
                    SettingsApplied?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }

    private void RestoreFromSnapshot()
    {
        if (_snapshot == null) return;
        _viewModel.Theme = _snapshot.Theme;
        _viewModel.GroupBackgroundOpacity = _snapshot.GroupBackgroundOpacity;
        _viewModel.DefaultDockMode = _snapshot.DefaultDockMode;
        _viewModel.DefaultSnapToGrid = _snapshot.DefaultSnapToGrid;
        _viewModel.SnapGridSize = _snapshot.SnapGridSize;
        _viewModel.HideGroupedIcons = _snapshot.HideGroupedIcons;
        _viewModel.IconSizeMode = _snapshot.IconSizeMode;
        _viewModel.CustomIconSize = _snapshot.CustomIconSize;
        _viewModel.SingleClickToLaunch = _snapshot.SingleClickToLaunch;
        _viewModel.EnableShellContextMenu = _snapshot.EnableShellContextMenu;
        _viewModel.StartWithWindows = _snapshot.StartWithWindows;
        _viewModel.StartMinimizedToTray = _snapshot.StartMinimizedToTray;
        _viewModel.ToggleAllHotkey = _snapshot.ToggleAllHotkey;
        _viewModel.ToggleAllMouseHotkey = _snapshot.ToggleAllMouseHotkey;
        _viewModel.ToggleAllHotkeyMode = _snapshot.ToggleAllHotkeyMode;
        _viewModel.FullscreenToggleHotkey = _snapshot.FullscreenToggleHotkey;
        _viewModel.FullscreenMouseHotkey = _snapshot.FullscreenMouseHotkey;
        _viewModel.FullscreenHotkeyMode = _snapshot.FullscreenHotkeyMode;
        _viewModel.ToggleEdgeDockHotkey = _snapshot.ToggleEdgeDockHotkey;
        _viewModel.ToggleEdgeDockMouseHotkey = _snapshot.ToggleEdgeDockMouseHotkey;
        _viewModel.ToggleEdgeDockHotkeyMode = _snapshot.ToggleEdgeDockHotkeyMode;
        _viewModel.EnableEdgeDockArrowHotkeys = _snapshot.EnableEdgeDockArrowHotkeys;
        _viewModel.WatchForUngroupedIcons = _snapshot.WatchForUngroupedIcons;
        _viewModel.AllowMultiMonitor = _snapshot.AllowMultiMonitor;
        _viewModel.ShowDebugMenu = _snapshot.ShowDebugMenu;
        _viewModel.DesktopShortcutInstalled = _snapshot.DesktopShortcutInstalled;
        _viewModel.PinnedToStart = _snapshot.PinnedToStart;
        _viewModel.PinnedToTaskbar = _snapshot.PinnedToTaskbar;
        _viewModel.AutoUpdate = _snapshot.AutoUpdate;
        _viewModel.AutoInstall = _snapshot.AutoInstall;
        _viewModel.OptInBugReporting = _snapshot.OptInBugReporting;
        _viewModel.EnableEdgeDock = _snapshot.EnableEdgeDock;
        _viewModel.EdgeDockMonitorMode = _snapshot.EdgeDockMonitorMode;
        _viewModel.PrimaryDockEdge = _snapshot.PrimaryDockEdge;
        _viewModel.EdgeDockLockState = _snapshot.EdgeDockLockState;
        _viewModel.EdgeDockIsPinned = _snapshot.EdgeDockIsPinned;
        _viewModel.EdgeDockShowOnHover = _snapshot.EdgeDockShowOnHover;
        _viewModel.EdgeDockShowBubble = _snapshot.EdgeDockShowBubble;
        _viewModel.EdgeDockBubbleSize = _snapshot.EdgeDockBubbleSize;
        _viewModel.EdgeDockIconSize = _snapshot.EdgeDockIconSize;
        _viewModel.EdgeLeftEnabled = _snapshot.EdgeLeftEnabled;
        _viewModel.EdgeTopEnabled = _snapshot.EdgeTopEnabled;
        _viewModel.EdgeRightEnabled = _snapshot.EdgeRightEnabled;
        _viewModel.EdgeBottomEnabled = _snapshot.EdgeBottomEnabled;
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
            _viewModel.ToggleEdgeDockHotkey,
            _viewModel.ToggleEdgeDockMouseHotkey,
            _viewModel.ToggleEdgeDockHotkeyMode,
            _viewModel.EnableEdgeDockArrowHotkeys,
            _viewModel.WatchForUngroupedIcons,
            _viewModel.AllowMultiMonitor,
            _viewModel.ShowDebugMenu,
            _viewModel.DesktopShortcutInstalled,
            _viewModel.PinnedToStart,
            _viewModel.PinnedToTaskbar,
            _viewModel.AutoUpdate,
            _viewModel.AutoInstall,
            _viewModel.OptInBugReporting,
            _viewModel.EnableEdgeDock,
            _viewModel.EdgeDockMonitorMode,
            _viewModel.PrimaryDockEdge,
            _viewModel.EdgeDockLockState,
            _viewModel.EdgeDockIsPinned,
            _viewModel.EdgeDockShowOnHover,
            _viewModel.EdgeDockShowBubble,
            _viewModel.EdgeDockBubbleSize,
            _viewModel.EdgeDockIconSize,
            _viewModel.EdgeLeftEnabled,
            _viewModel.EdgeTopEnabled,
            _viewModel.EdgeRightEnabled,
            _viewModel.EdgeBottomEnabled
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
            || _viewModel.ToggleEdgeDockHotkey   != _snapshot.ToggleEdgeDockHotkey
            || _viewModel.ToggleEdgeDockMouseHotkey != _snapshot.ToggleEdgeDockMouseHotkey
            || _viewModel.ToggleEdgeDockHotkeyMode != _snapshot.ToggleEdgeDockHotkeyMode
            || _viewModel.EnableEdgeDockArrowHotkeys != _snapshot.EnableEdgeDockArrowHotkeys
            || _viewModel.WatchForUngroupedIcons != _snapshot.WatchForUngroupedIcons
            || _viewModel.AllowMultiMonitor      != _snapshot.AllowMultiMonitor
            || _viewModel.ShowDebugMenu          != _snapshot.ShowDebugMenu
            || _viewModel.DesktopShortcutInstalled != _snapshot.DesktopShortcutInstalled
            || _viewModel.PinnedToStart          != _snapshot.PinnedToStart
            || _viewModel.PinnedToTaskbar        != _snapshot.PinnedToTaskbar
            || _viewModel.AutoUpdate             != _snapshot.AutoUpdate
            || _viewModel.AutoInstall            != _snapshot.AutoInstall
            || _viewModel.OptInBugReporting      != _snapshot.OptInBugReporting
            || _viewModel.EnableEdgeDock         != _snapshot.EnableEdgeDock
            || _viewModel.EdgeDockMonitorMode    != _snapshot.EdgeDockMonitorMode
            || _viewModel.PrimaryDockEdge        != _snapshot.PrimaryDockEdge
            || _viewModel.EdgeDockLockState      != _snapshot.EdgeDockLockState
            || _viewModel.EdgeDockIsPinned       != _snapshot.EdgeDockIsPinned
            || _viewModel.EdgeDockShowOnHover    != _snapshot.EdgeDockShowOnHover
            || _viewModel.EdgeDockShowBubble     != _snapshot.EdgeDockShowBubble
            || _viewModel.EdgeDockBubbleSize     != _snapshot.EdgeDockBubbleSize
            || _viewModel.EdgeDockIconSize       != _snapshot.EdgeDockIconSize
            || _viewModel.EdgeLeftEnabled        != _snapshot.EdgeLeftEnabled
            || _viewModel.EdgeTopEnabled         != _snapshot.EdgeTopEnabled
            || _viewModel.EdgeRightEnabled       != _snapshot.EdgeRightEnabled
            || _viewModel.EdgeBottomEnabled      != _snapshot.EdgeBottomEnabled;
    }
}
