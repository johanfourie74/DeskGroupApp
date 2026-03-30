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
    // Event to notify when icon size settings change (for live updates)
    public event EventHandler? IconSizeChanged;

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

    partial void OnIconSizeModeChanged(IconSizeMode value)
    {
        _settings.IconSizeMode = value;
        IconSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnCustomIconSizeChanged(int value)
    {
        _settings.CustomIconSize = value;
        IconSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    [ObservableProperty] private bool _singleClickToLaunch;
    [ObservableProperty] private bool _enableShellContextMenu;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimizedToTray;
    [ObservableProperty] private string _toggleAllHotkey = string.Empty;
    [ObservableProperty] private string _toggleAllMouseHotkey = string.Empty;
    [ObservableProperty] private Models.HotkeyMode _toggleAllHotkeyMode;
    [ObservableProperty] private string _fullscreenToggleHotkey = string.Empty;
    [ObservableProperty] private string _fullscreenMouseHotkey = string.Empty;
    [ObservableProperty] private Models.HotkeyMode _fullscreenHotkeyMode;
    [ObservableProperty] private bool _watchForUngroupedIcons;
    [ObservableProperty] private bool _allowMultiMonitor;
    [ObservableProperty] private bool _showDebugMenu;
    [ObservableProperty] private bool _desktopShortcutInstalled;
    [ObservableProperty] private bool _pinnedToStart;
    [ObservableProperty] private bool _pinnedToTaskbar;

    // ── Update settings ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _autoUpdate;
    [ObservableProperty] private bool _autoInstall;

    // ── Developer / feedback ──────────────────────────────────────────────────
    [ObservableProperty] private bool _optInBugReporting;

    // ── Update UI state (not persisted directly) ───────────────────────────────
    [ObservableProperty] private bool   _isCheckingForUpdate;
    [ObservableProperty] private bool   _isDownloadingUpdate;
    [ObservableProperty] private bool   _updateAvailable;
    [ObservableProperty] private string _availableVersion  = string.Empty;
    [ObservableProperty] private string _updateReleaseNotes = string.Empty;
    [ObservableProperty] private string _updateStatusText   = string.Empty;
    [ObservableProperty] private int    _downloadProgress;

    private Services.UpdateService.UpdateInfo? _pendingUpdate;
    private System.Threading.CancellationTokenSource? _updateCts;

    public List<Models.ThemeMode> ThemeModes { get; } = new() { Models.ThemeMode.Light, Models.ThemeMode.Dark, Models.ThemeMode.System };
    public List<GroupDockMode> DockModes { get; } = new() { GroupDockMode.Sticky, GroupDockMode.Float };
    public List<IconSizeMode> IconSizeModes { get; } = new() { IconSizeMode.System, IconSizeMode.Custom };
    public System.Array HotkeyModes { get; } = Enum.GetValues(typeof(Models.HotkeyMode));

    // Expose settings for window position persistence
    public AppSettings Settings => _settings;

    public Window? OwnerWindow { get; set; }

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
        AutoUpdate  = settings.AutoUpdate;
        AutoInstall = settings.AutoInstall;
        SingleClickToLaunch = settings.SingleClickToLaunch;
        EnableShellContextMenu = settings.EnableShellContextMenu;
        OptInBugReporting = settings.OptInBugReporting;
        ToggleAllMouseHotkey = settings.ToggleAllMouseHotkey;
        ToggleAllHotkeyMode = settings.ToggleAllHotkeyMode;
        FullscreenMouseHotkey = settings.FullscreenMouseHotkey;
        FullscreenHotkeyMode = settings.FullscreenHotkeyMode;

        UpdateStatusText = $"Last checked: {(settings.LastUpdateCheck == DateTime.MinValue ? "Never" : settings.LastUpdateCheck.ToLocalTime().ToString("g"))}";
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
            dialog.Owner = OwnerWindow;

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
            _settings.ToggleAllMouseHotkey != ToggleAllMouseHotkey ||
            _settings.ToggleAllHotkeyMode != ToggleAllHotkeyMode ||
            _settings.FullscreenToggleHotkey != FullscreenToggleHotkey ||
            _settings.FullscreenMouseHotkey != FullscreenMouseHotkey ||
            _settings.FullscreenHotkeyMode != FullscreenHotkeyMode;

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
        _settings.ToggleAllHotkey = ToggleAllHotkey;
        _settings.ToggleAllMouseHotkey = ToggleAllMouseHotkey;
        _settings.ToggleAllHotkeyMode = ToggleAllHotkeyMode;
        _settings.FullscreenToggleHotkey = FullscreenToggleHotkey;
        _settings.FullscreenMouseHotkey = FullscreenMouseHotkey;
        _settings.FullscreenHotkeyMode = FullscreenHotkeyMode;
        _settings.WatchForUngroupedIcons = WatchForUngroupedIcons;
        _settings.AllowMultiMonitor = AllowMultiMonitor;
        _settings.ShowDebugMenu = ShowDebugMenu;
        _settings.DesktopShortcutInstalled = DesktopShortcutInstalled;
        _settings.PinnedToStart = PinnedToStart;
        _settings.PinnedToTaskbar = PinnedToTaskbar;
        _settings.AutoUpdate  = AutoUpdate;
        _settings.AutoInstall = AutoInstall;
        _settings.SingleClickToLaunch = SingleClickToLaunch;
        _settings.EnableShellContextMenu = EnableShellContextMenu;
        _settings.OptInBugReporting = OptInBugReporting;
        _startupService.SyncWithSetting(StartWithWindows);
        await _persistenceService.SaveSettingsImmediateAsync(_settings);

        // Sync shell context menu registration
        if (EnableShellContextMenu) Helpers.ShellContextMenuHelper.RegisterContextMenu();
        else Helpers.ShellContextMenuHelper.UnregisterContextMenu();

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
            dialog.Owner = OwnerWindow;

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

    // ── Update commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private async System.Threading.Tasks.Task CheckForUpdateAsync()
    {
        if (IsCheckingForUpdate) return;
        IsCheckingForUpdate = true;
        UpdateAvailable = false;
        _pendingUpdate  = null;

        _updateCts?.Cancel();
        _updateCts = new System.Threading.CancellationTokenSource();

        try
        {
            UpdateStatusText = "Checking for updates…";
            var info = await Services.UpdateService.CheckAsync(
                _settings.SkippedUpdateVersion, _updateCts.Token).ConfigureAwait(true);

            _settings.LastUpdateCheck = DateTime.UtcNow;
            await _persistenceService.SaveSettingsImmediateAsync(_settings);

            if (info == null)
            {
                UpdateStatusText = $"✅ You're up to date (v{Services.UpdateService.GetCurrentVersion()})  —  " +
                                   $"Last checked: {DateTime.Now:g}";
            }
            else
            {
                _pendingUpdate      = info;
                AvailableVersion    = info.Version;
                UpdateReleaseNotes  = info.ReleaseNotes;
                UpdateAvailable     = true;
                UpdateStatusText    = $"🆕 Update available: v{info.Version}";

                if (AutoUpdate && AutoInstall)
                    await DownloadAndInstallAsync(_updateCts.Token);
            }
        }
        catch (System.OperationCanceledException) { UpdateStatusText = "Check cancelled."; }
        catch (Exception ex)                      { UpdateStatusText = $"Check failed: {ex.Message}"; }
        finally                                   { IsCheckingForUpdate = false; }
    }

    [RelayCommand]
    private void SkipThisUpdate()
    {
        if (_pendingUpdate == null) return;
        _settings.SkippedUpdateVersion = _pendingUpdate.Version;
        _ = _persistenceService.SaveSettingsImmediateAsync(_settings);
        UpdateAvailable  = false;
        UpdateStatusText = $"v{_pendingUpdate.Version} will be skipped.";
        _pendingUpdate   = null;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task InstallUpdateAsync()
    {
        if (_pendingUpdate == null || IsDownloadingUpdate) return;
        await DownloadAndInstallAsync(System.Threading.CancellationToken.None);
    }

    private async System.Threading.Tasks.Task DownloadAndInstallAsync(System.Threading.CancellationToken ct)
    {
        if (_pendingUpdate == null) return;

        if (string.IsNullOrEmpty(_pendingUpdate.DownloadUrl))
        {
            UpdateStatusText = "⚠️ No installer asset found in release. Visit GitHub to download manually.";
            return;
        }

        IsDownloadingUpdate = true;
        DownloadProgress    = 0;
        UpdateStatusText    = "Downloading…";

        try
        {
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                UpdateStatusText = $"Downloading… {p}%";
            });

            var path = await Services.UpdateService.DownloadAsync(
                _pendingUpdate.DownloadUrl, progress, ct).ConfigureAwait(true);

            UpdateStatusText = "Download complete — launching installer…";
            Services.UpdateService.LaunchInstallerAndShutdown(path);
        }
        catch (System.OperationCanceledException) { UpdateStatusText = "Download cancelled."; }
        catch (Exception ex)                      { UpdateStatusText = $"Download failed: {ex.Message}"; }
        finally                                   { IsDownloadingUpdate = false; }
    }

    /// <summary>
    /// Called by App.xaml.cs on startup and by the weekly background timer.
    /// Runs silently; shows a balloon tip notification if an update is found and AutoInstall is off.
    /// </summary>
    public async System.Threading.Tasks.Task RunBackgroundUpdateCheckAsync(
        Action<string, string>? showBalloon = null)
    {
        if (!AutoUpdate) return;
        try
        {
            var info = await Services.UpdateService.CheckAsync(
                _settings.SkippedUpdateVersion).ConfigureAwait(true);

            _settings.LastUpdateCheck = DateTime.UtcNow;
            await _persistenceService.SaveSettingsImmediateAsync(_settings);

            if (info == null) return;

            _pendingUpdate     = info;
            AvailableVersion   = info.Version;
            UpdateReleaseNotes = info.ReleaseNotes;
            UpdateAvailable    = true;
            UpdateStatusText   = $"🆕 Update available: v{info.Version}";

            if (AutoInstall)
            {
                await DownloadAndInstallAsync(System.Threading.CancellationToken.None);
            }
            else
            {
                showBalloon?.Invoke(
                    $"DeskGroup v{info.Version} available 🆕",
                    "Open Settings → Updates to install.");
            }
        }
        catch { /* silent — background check */ }
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
        if (!string.IsNullOrWhiteSpace(ToggleAllHotkey) && !HotkeyService.IsValidHotkeyString(ToggleAllHotkey))
            return $"'Toggle All Groups' keyboard hotkey '{ToggleAllHotkey}' is not in valid format.\n\nExamples: Ctrl+F2, XButton1, Ctrl+ScrollUp";

        if (!string.IsNullOrWhiteSpace(ToggleAllMouseHotkey))
        {
            if (!HotkeyService.IsValidHotkeyString(ToggleAllMouseHotkey))
                return $"'Toggle All Groups' mouse hotkey '{ToggleAllMouseHotkey}' is not in valid format.\n\nExamples: MiddleClick, Ctrl+XButton1";
            if (!HotkeyService.IsMouseHotkeyString(ToggleAllMouseHotkey))
                return $"'Toggle All Groups' mouse hotkey '{ToggleAllMouseHotkey}' must be a mouse button combo.\n\nExamples: MiddleClick, Ctrl+XButton1, ScrollUp";
        }

        if (!string.IsNullOrWhiteSpace(FullscreenToggleHotkey) && !HotkeyService.IsValidHotkeyString(FullscreenToggleHotkey))
            return $"'Fullscreen Toggle' keyboard hotkey '{FullscreenToggleHotkey}' is not in valid format.\n\nExamples: F11, Ctrl+XButton2";

        if (!string.IsNullOrWhiteSpace(FullscreenMouseHotkey))
        {
            if (!HotkeyService.IsValidHotkeyString(FullscreenMouseHotkey))
                return $"'Fullscreen Toggle' mouse hotkey '{FullscreenMouseHotkey}' is not in valid format.\n\nExamples: MiddleClick, Ctrl+XButton2";
            if (!HotkeyService.IsMouseHotkeyString(FullscreenMouseHotkey))
                return $"'Fullscreen Toggle' mouse hotkey '{FullscreenMouseHotkey}' must be a mouse button combo.\n\nExamples: MiddleClick, Ctrl+XButton2, ScrollDown";
        }

        // Collect all non-empty hotkeys and check for duplicates
        var allHotkeys = new[]
        {
            (Label: "Toggle All (keyboard)",  Value: ToggleAllHotkey),
            (Label: "Toggle All (mouse)",      Value: ToggleAllMouseHotkey),
            (Label: "Fullscreen (keyboard)",   Value: FullscreenToggleHotkey),
            (Label: "Fullscreen (mouse)",      Value: FullscreenMouseHotkey),
        };

        for (int i = 0; i < allHotkeys.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(allHotkeys[i].Value)) continue;
            for (int j = i + 1; j < allHotkeys.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(allHotkeys[j].Value)) continue;
                if (allHotkeys[i].Value.Equals(allHotkeys[j].Value, System.StringComparison.OrdinalIgnoreCase))
                    return $"'{allHotkeys[i].Label}' and '{allHotkeys[j].Label}' are set to the same combination.\n\nEach hotkey must be unique.";
            }
        }

        return null;
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
        ToggleAllMouseHotkey = d.ToggleAllMouseHotkey; ToggleAllHotkeyMode = d.ToggleAllHotkeyMode;
        FullscreenToggleHotkey = d.FullscreenToggleHotkey; FullscreenMouseHotkey = d.FullscreenMouseHotkey;
        FullscreenHotkeyMode = d.FullscreenHotkeyMode; WatchForUngroupedIcons = d.WatchForUngroupedIcons;
        AllowMultiMonitor = d.AllowMultiMonitor;
        ShowDebugMenu = d.ShowDebugMenu;
        AutoUpdate  = d.AutoUpdate;
        AutoInstall = d.AutoInstall;
        SingleClickToLaunch = d.SingleClickToLaunch;
        EnableShellContextMenu = d.EnableShellContextMenu;
        OptInBugReporting = d.OptInBugReporting;
    }
}