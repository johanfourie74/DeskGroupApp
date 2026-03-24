using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly PersistenceService _persistenceService;
    private readonly StartupService _startupService;

    // Event to notify when opacity changes (for live updates)
    public event EventHandler? OpacityChanged;

    [ObservableProperty] private Models.ThemeMode _theme;
    [ObservableProperty] private double _groupBackgroundOpacity;

    partial void OnGroupBackgroundOpacityChanged(double value)
    {
        // Immediately update the settings model so group windows see the change in real-time
        _settings.GroupBackgroundOpacity = value;

        // Notify subscribers (MainViewModel) to refresh all groups
        OpacityChanged?.Invoke(this, EventArgs.Empty);
    }

    [ObservableProperty] private GroupDockMode _defaultDockMode;
    [ObservableProperty] private bool _defaultSnapToGrid;
    [ObservableProperty] private int _snapGridSize;
    [ObservableProperty] private bool _hideGroupedIcons;
    [ObservableProperty] private IconSizeMode _iconSizeMode;
    [ObservableProperty] private int _customIconSize;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimizedToTray;
    [ObservableProperty] private string _toggleAllHotkey = string.Empty;
    [ObservableProperty] private string _fullscreenToggleHotkey = string.Empty;
    [ObservableProperty] private bool _watchForUngroupedIcons;
    [ObservableProperty] private bool _allowMultiMonitor;
    [ObservableProperty] private bool _showDebugMenu;
    [ObservableProperty] private bool _desktopShortcutInstalled;
    [ObservableProperty] private bool _pinnedToStart;
    [ObservableProperty] private bool _pinnedToTaskbar;

    public List<Models.ThemeMode> ThemeModes { get; } = new() { Models.ThemeMode.Light, Models.ThemeMode.Dark, Models.ThemeMode.System };
    public List<GroupDockMode> DockModes { get; } = new() { GroupDockMode.Sticky, GroupDockMode.Float };
    public List<IconSizeMode> IconSizeModes { get; } = new() { IconSizeMode.System, IconSizeMode.Custom };

    // Expose settings for window position persistence
    public AppSettings Settings => _settings;

    public SettingsViewModel(AppSettings settings, PersistenceService persistenceService, StartupService startupService)
    {
        _settings = settings; _persistenceService = persistenceService; _startupService = startupService;
        Theme = settings.Theme; 
        GroupBackgroundOpacity = settings.GroupBackgroundOpacity;
        DefaultDockMode = settings.DefaultDockMode;
        DefaultSnapToGrid = settings.DefaultSnapToGrid; SnapGridSize = settings.SnapGridSize;
        HideGroupedIcons = settings.HideGroupedIcons; IconSizeMode = settings.IconSizeMode;
        CustomIconSize = settings.CustomIconSize; StartWithWindows = settings.StartWithWindows;
        StartMinimizedToTray = settings.StartMinimizedToTray; ToggleAllHotkey = settings.ToggleAllHotkey;
        FullscreenToggleHotkey = settings.FullscreenToggleHotkey;
        WatchForUngroupedIcons = settings.WatchForUngroupedIcons;
        AllowMultiMonitor = settings.AllowMultiMonitor;
        ShowDebugMenu = settings.ShowDebugMenu;
        DesktopShortcutInstalled = settings.DesktopShortcutInstalled;
        PinnedToStart = settings.PinnedToStart;
        PinnedToTaskbar = settings.PinnedToTaskbar;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Apply()
    {
        // Validate hotkeys before saving
        string? validationError = ValidateHotkeys();
        if (validationError != null)
        {
            MessageBox.Show(
                $"⚠️ Invalid Hotkey Configuration\n\n{validationError}\n\nPlease change the hotkey and try again.",
                "Invalid Hotkey",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return; // Don't save
        }

        // Check for obviously problematic hotkeys
        var warnings = GetHotkeyWarnings();
        if (warnings.Count > 0)
        {
            var message = 
                $"{string.Join("\n\n", warnings)}\n\n" +
                $"⚠️ IMPORTANT WARNINGS:\n\n" +
                $"• This hotkey is used by Windows or other applications\n" +
                $"• Registering it may PREVENT other programs from working correctly\n" +
                $"• It may cause UNPREDICTABLE BEHAVIOR in other applications\n" +
                $"• The hotkey will likely NOT work in DeskGroup\n\n" +
                $"👉 You should REALLY choose a different hotkey combination!\n\n" +
                $"Recommended: Use Ctrl+Shift or Ctrl+Alt with a letter not commonly used.\n" +
                $"Examples: Ctrl+Shift+D, Ctrl+Alt+G, Ctrl+Shift+K";

            var dialog = new DeskGroup.Views.HotkeyWarningDialog(
                "CRITICAL: Hotkey Conflict Detected",
                message);

            var result = dialog.ShowDialog();

            if (result != true || !dialog.ForcedAnyway)
            {
                return; // User cancelled - don't save
            }
            // User clicked "FORCE ANYWAY" - proceed with save
        }

        // Check if hotkeys changed
        bool hotkeysChanged = 
            _settings.ToggleAllHotkey != ToggleAllHotkey ||
            _settings.FullscreenToggleHotkey != FullscreenToggleHotkey;

        // Capture shortcut states before overwriting
        bool wasDesktopShortcut = _settings.DesktopShortcutInstalled;
        bool wasStart = _settings.PinnedToStart;
        bool wasTaskbar = _settings.PinnedToTaskbar;

        _settings.Theme = Theme; 
        _settings.GroupBackgroundOpacity = GroupBackgroundOpacity;
        _settings.DefaultDockMode = DefaultDockMode;
        _settings.DefaultSnapToGrid = DefaultSnapToGrid; _settings.SnapGridSize = SnapGridSize;
        _settings.HideGroupedIcons = HideGroupedIcons; _settings.IconSizeMode = IconSizeMode;
        _settings.CustomIconSize = CustomIconSize; _settings.StartWithWindows = StartWithWindows;
        _settings.StartMinimizedToTray = StartMinimizedToTray; _settings.ToggleAllHotkey = ToggleAllHotkey;
        _settings.FullscreenToggleHotkey = FullscreenToggleHotkey;
        _settings.WatchForUngroupedIcons = WatchForUngroupedIcons;
        _settings.AllowMultiMonitor = AllowMultiMonitor;
        _settings.ShowDebugMenu = ShowDebugMenu;
        _settings.DesktopShortcutInstalled = DesktopShortcutInstalled;
        _settings.PinnedToStart = PinnedToStart;
        _settings.PinnedToTaskbar = PinnedToTaskbar;
        _startupService.SyncWithSetting(StartWithWindows);
        await _persistenceService.SaveSettingsImmediateAsync(_settings);

        // Handle shortcut/pin changes
        if (wasDesktopShortcut != DesktopShortcutInstalled)
        {
            if (DesktopShortcutInstalled) Helpers.ShortcutInstallHelper.InstallDesktopShortcut();
            else Helpers.ShortcutInstallHelper.RemoveDesktopShortcut();
        }
        if (!wasStart && PinnedToStart) HandlePinToStart();
        if (!wasTaskbar && PinnedToTaskbar) HandlePinToTaskbar();
        if (hotkeysChanged)
        {
            var dialog = new DeskGroup.Views.HotkeyWarningDialog(
                "Restart Required",
                "✅ Settings Saved Successfully!\n\n" +
                "⚠️ IMPORTANT: Hotkey changes require an app restart.\n\n" +
                "The new hotkeys will take effect after restarting DeskGroup.\n\n" +
                "Would you like to restart now?");

            // Customize buttons for restart scenario
            dialog.CancelButtonControl.Content = "RESTART LATER";
            dialog.ForceButtonControl.Content = "RESTART NOW";

            // RESTART LATER = gray (default), RESTART NOW = green (recommended)
            dialog.CancelButtonControl.Style = (System.Windows.Style)dialog.FindResource("DefaultButtonStyle");
            dialog.ForceButtonControl.Style = (System.Windows.Style)dialog.FindResource("GreenButtonStyle");

            var result = dialog.ShowDialog();

            if (result == true && dialog.ForcedAnyway)
            {
                // User chose to restart now
                RestartApplication();
            }
        }
    }

    private void HandlePinToStart()
    {
        var result = Helpers.ShortcutInstallHelper.PinToStartMenu();
        if (result == Helpers.ShortcutInstallHelper.PinResult.Success) return;
        MessageBox.Show(
            "Automatic Start menu pinning isn't supported on Windows 11.\n\n" +
            "To pin DeskGroup manually:\n" +
            "1. Find the DeskGroup shortcut on your desktop\n" +
            "2. Right-click it and choose 'Pin to Start'\n\n" +
            "Your preference has been saved.",
            "Pin to Start — Manual Action Needed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void HandlePinToTaskbar()
    {
        var result = Helpers.ShortcutInstallHelper.PinToTaskbar();
        if (result == Helpers.ShortcutInstallHelper.PinResult.Success) return;
        MessageBox.Show(
            "Automatic taskbar pinning isn't supported on Windows 11.\n\n" +
            "To pin DeskGroup manually:\n" +
            "1. Find the DeskGroup shortcut on your desktop\n" +
            "2. Right-click it and choose 'Pin to taskbar'\n\n" +
            "Your preference has been saved.",
            "Pin to Taskbar — Manual Action Needed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RestartApplication()
    {
        try
        {
            // Get current executable path
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if (!string.IsNullOrEmpty(exePath))
            {
                // Start new instance
                System.Diagnostics.Process.Start(exePath);

                // Shutdown current instance
                System.Windows.Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restart application: {ex.Message}");
            MessageBox.Show(
                $"Failed to restart automatically.\n\nPlease restart DeskGroup manually.\n\nError: {ex.Message}",
                "Restart Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private List<string> GetHotkeyWarnings()
    {
        var warnings = new List<string>();

        // Common system hotkeys that will definitely conflict
        var systemHotkeys = new[] 
        { 
            "CTRL+C", "CTRL+V", "CTRL+X", "CTRL+Z", "CTRL+A", "CTRL+S",
            "CTRL+P", "CTRL+O", "CTRL+N", "CTRL+F", "CTRL+H",
            "ALT+F4", "ALT+TAB", "WIN+D", "WIN+E", "WIN+L",
            "CTRL+ALT+DELETE", "CTRL+SHIFT+ESC"
        };

        if (!string.IsNullOrWhiteSpace(ToggleAllHotkey))
        {
            var upper = ToggleAllHotkey.ToUpperInvariant().Replace(" ", "");
            if (System.Array.Exists(systemHotkeys, h => h == upper))
            {
                warnings.Add($"'{ToggleAllHotkey}' is a common system shortcut and will likely not work.");
            }
        }

        if (!string.IsNullOrWhiteSpace(FullscreenToggleHotkey))
        {
            var upper = FullscreenToggleHotkey.ToUpperInvariant().Replace(" ", "");
            if (System.Array.Exists(systemHotkeys, h => h == upper))
            {
                warnings.Add($"'{FullscreenToggleHotkey}' is a common system shortcut and will likely not work.");
            }
        }

        return warnings;
    }

    private string? ValidateHotkeys()
    {
        // Check if hotkeys can be parsed
        if (!string.IsNullOrWhiteSpace(ToggleAllHotkey))
        {
            if (!HotkeyService.TryParseHotkey(ToggleAllHotkey, out _, out _))
            {
                return $"'Toggle All Groups' hotkey '{ToggleAllHotkey}' is not in valid format.\n\nExample: Ctrl+Shift+D";
            }
        }

        if (!string.IsNullOrWhiteSpace(FullscreenToggleHotkey))
        {
            if (!HotkeyService.TryParseHotkey(FullscreenToggleHotkey, out _, out _))
            {
                return $"'Fullscreen Toggle' hotkey '{FullscreenToggleHotkey}' is not in valid format.\n\nExample: Ctrl+Shift+F";
            }
        }

        // Check if both hotkeys are the same
        if (!string.IsNullOrWhiteSpace(ToggleAllHotkey) && 
            !string.IsNullOrWhiteSpace(FullscreenToggleHotkey) &&
            ToggleAllHotkey.Equals(FullscreenToggleHotkey, System.StringComparison.OrdinalIgnoreCase))
        {
            return "Both hotkeys are set to the same combination.\n\nEach hotkey must be unique.";
        }

        return null; // Valid
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var d = new AppSettings();
        Theme = d.Theme; 
        GroupBackgroundOpacity = d.GroupBackgroundOpacity;
        DefaultDockMode = d.DefaultDockMode; DefaultSnapToGrid = d.DefaultSnapToGrid;
        SnapGridSize = d.SnapGridSize; HideGroupedIcons = d.HideGroupedIcons; IconSizeMode = d.IconSizeMode;
        CustomIconSize = d.CustomIconSize; StartWithWindows = d.StartWithWindows;
        StartMinimizedToTray = d.StartMinimizedToTray; ToggleAllHotkey = d.ToggleAllHotkey;
        FullscreenToggleHotkey = d.FullscreenToggleHotkey; WatchForUngroupedIcons = d.WatchForUngroupedIcons;
        AllowMultiMonitor = d.AllowMultiMonitor;
        ShowDebugMenu = d.ShowDebugMenu;
    }
}