using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public sealed class MonitorOptionItem
{
    public string DeviceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public override string ToString() => DisplayName;
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly PersistenceService _persistenceService;
    private readonly StartupService _startupService;
    private readonly MonitorService _monitorService;
    private bool _isLoadingMonitorEdges;
    private bool _isInitializing = true;
    private bool _isSyncingHotkeyMode;

    // Events for live preview updates while Settings window is open
    public event EventHandler? OpacityChanged;
    public event EventHandler? IconSizeChanged;
    public event EventHandler? LiveSettingsChanged;
    public event EventHandler? HotkeysChanged;
    public event EventHandler? HideGroupedIconsChanged;

    public bool LastApplySucceeded { get; private set; } = true;

    [ObservableProperty] private Models.ThemeMode _theme;
    [ObservableProperty] private double _groupBackgroundOpacity;

    partial void OnThemeChanged(Models.ThemeMode value)
    {
        if (_isInitializing) return;
        _settings.Theme = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnGroupBackgroundOpacityChanged(double value)
    {
        if (_isInitializing) return;
        _settings.GroupBackgroundOpacity = value;
        OpacityChanged?.Invoke(this, EventArgs.Empty);
    }

    [ObservableProperty] private GroupDockMode _defaultDockMode;
    [ObservableProperty] private bool _defaultSnapToGrid;
    [ObservableProperty] private int _snapGridSize;
    [ObservableProperty] private bool _hideGroupedIcons;
    [ObservableProperty] private IconSizeMode _iconSizeMode;
    [ObservableProperty] private int _customIconSize;

    partial void OnDefaultDockModeChanged(GroupDockMode value)
    {
        if (_isInitializing) return;
        _settings.DefaultDockMode = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDefaultSnapToGridChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.DefaultSnapToGrid = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSnapGridSizeChanged(int value)
    {
        if (_isInitializing) return;
        _settings.SnapGridSize = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnHideGroupedIconsChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.HideGroupedIcons = value;
        HideGroupedIconsChanged?.Invoke(this, EventArgs.Empty);
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIconSizeModeChanged(IconSizeMode value)
    {
        if (_isInitializing) return;
        _settings.IconSizeMode = value;
        IconSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnCustomIconSizeChanged(int value)
    {
        if (_isInitializing) return;
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
    [ObservableProperty] private string _toggleEdgeDockHotkey = "Ctrl+Alt+Left";
    [ObservableProperty] private string _toggleEdgeDockMouseHotkey = string.Empty;
    [ObservableProperty] private Models.HotkeyMode _toggleEdgeDockHotkeyMode = Models.HotkeyMode.Keyboard;
    [ObservableProperty] private bool _enableEdgeDockArrowHotkeys = true;
    [ObservableProperty] private bool _watchForUngroupedIcons;
    [ObservableProperty] private bool _allowMultiMonitor;
    [ObservableProperty] private bool _showDebugMenu;
    [ObservableProperty] private bool _desktopShortcutInstalled;
    [ObservableProperty] private bool _pinnedToStart;
    [ObservableProperty] private bool _pinnedToTaskbar;

    partial void OnSingleClickToLaunchChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.SingleClickToLaunch = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEnableShellContextMenuChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.EnableShellContextMenu = value;
        if (value) Helpers.ShellContextMenuHelper.RegisterContextMenu();
        else Helpers.ShellContextMenuHelper.UnregisterContextMenu();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.StartWithWindows = value;
        _startupService.SyncWithSetting(value);
    }

    partial void OnStartMinimizedToTrayChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.StartMinimizedToTray = value;
    }

    partial void OnWatchForUngroupedIconsChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.WatchForUngroupedIcons = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnAllowMultiMonitorChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.AllowMultiMonitor = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnShowDebugMenuChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.ShowDebugMenu = value;
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDesktopShortcutInstalledChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.DesktopShortcutInstalled = value;
        if (value) Helpers.ShortcutInstallHelper.InstallDesktopShortcut();
        else Helpers.ShortcutInstallHelper.RemoveDesktopShortcut();
    }

    partial void OnPinnedToStartChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.PinnedToStart = value;
        if (value) HandlePinToStart();
        else Helpers.ShortcutInstallHelper.RemoveStartMenuShortcut();
    }

    partial void OnPinnedToTaskbarChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.PinnedToTaskbar = value;
        if (value) HandlePinToTaskbar();
    }

    // ── Live Hotkey & Mouse Shortcut Handlers ─────────────────────────────────

    partial void OnToggleAllHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(value) && ToggleAllHotkeyMode == Models.HotkeyMode.Mouse && !_isSyncingHotkeyMode)
        {
            _isSyncingHotkeyMode = true;
            ToggleAllHotkeyMode = string.IsNullOrWhiteSpace(ToggleAllMouseHotkey) ? Models.HotkeyMode.Keyboard : Models.HotkeyMode.Both;
            _settings.ToggleAllHotkeyMode = ToggleAllHotkeyMode;
            _isSyncingHotkeyMode = false;
        }
        if (HotkeyService.IsValidHotkeyString(value))
        {
            _settings.ToggleAllHotkey = value;
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnToggleAllMouseHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(value) && ToggleAllHotkeyMode == Models.HotkeyMode.Keyboard && !_isSyncingHotkeyMode)
        {
            _isSyncingHotkeyMode = true;
            ToggleAllHotkeyMode = string.IsNullOrWhiteSpace(ToggleAllHotkey) ? Models.HotkeyMode.Mouse : Models.HotkeyMode.Both;
            _settings.ToggleAllHotkeyMode = ToggleAllHotkeyMode;
            _isSyncingHotkeyMode = false;
        }
        if (string.IsNullOrWhiteSpace(value) || HotkeyService.IsMouseHotkeyString(value))
        {
            _settings.ToggleAllMouseHotkey = value;
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnToggleAllHotkeyModeChanged(Models.HotkeyMode value)
    {
        if (_isInitializing) return;
        _settings.ToggleAllHotkeyMode = value;
        if (!_isSyncingHotkeyMode)
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnFullscreenToggleHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(value) && FullscreenHotkeyMode == Models.HotkeyMode.Mouse && !_isSyncingHotkeyMode)
        {
            _isSyncingHotkeyMode = true;
            FullscreenHotkeyMode = string.IsNullOrWhiteSpace(FullscreenMouseHotkey) ? Models.HotkeyMode.Keyboard : Models.HotkeyMode.Both;
            _settings.FullscreenHotkeyMode = FullscreenHotkeyMode;
            _isSyncingHotkeyMode = false;
        }
        if (HotkeyService.IsValidHotkeyString(value))
        {
            _settings.FullscreenToggleHotkey = value;
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnFullscreenMouseHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(value) && FullscreenHotkeyMode == Models.HotkeyMode.Keyboard && !_isSyncingHotkeyMode)
        {
            _isSyncingHotkeyMode = true;
            FullscreenHotkeyMode = string.IsNullOrWhiteSpace(FullscreenToggleHotkey) ? Models.HotkeyMode.Mouse : Models.HotkeyMode.Both;
            _settings.FullscreenHotkeyMode = FullscreenHotkeyMode;
            _isSyncingHotkeyMode = false;
        }
        if (string.IsNullOrWhiteSpace(value) || HotkeyService.IsMouseHotkeyString(value))
        {
            _settings.FullscreenMouseHotkey = value;
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnFullscreenHotkeyModeChanged(Models.HotkeyMode value)
    {
        if (_isInitializing) return;
        _settings.FullscreenHotkeyMode = value;
        if (!_isSyncingHotkeyMode)
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnToggleEdgeDockHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(value) && ToggleEdgeDockHotkeyMode == Models.HotkeyMode.Mouse && !_isSyncingHotkeyMode)
        {
            _isSyncingHotkeyMode = true;
            ToggleEdgeDockHotkeyMode = string.IsNullOrWhiteSpace(ToggleEdgeDockMouseHotkey) ? Models.HotkeyMode.Keyboard : Models.HotkeyMode.Both;
            _settings.ToggleEdgeDockHotkeyMode = ToggleEdgeDockHotkeyMode;
            _isSyncingHotkeyMode = false;
        }
        if (HotkeyService.IsValidHotkeyString(value))
        {
            _settings.ToggleEdgeDockHotkey = value;
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnToggleEdgeDockMouseHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(value) && ToggleEdgeDockHotkeyMode == Models.HotkeyMode.Keyboard && !_isSyncingHotkeyMode)
        {
            _isSyncingHotkeyMode = true;
            ToggleEdgeDockHotkeyMode = string.IsNullOrWhiteSpace(ToggleEdgeDockHotkey) ? Models.HotkeyMode.Mouse : Models.HotkeyMode.Both;
            _settings.ToggleEdgeDockHotkeyMode = ToggleEdgeDockHotkeyMode;
            _isSyncingHotkeyMode = false;
        }
        if (string.IsNullOrWhiteSpace(value) || HotkeyService.IsMouseHotkeyString(value))
        {
            _settings.ToggleEdgeDockMouseHotkey = value;
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnToggleEdgeDockHotkeyModeChanged(Models.HotkeyMode value)
    {
        if (_isInitializing) return;
        _settings.ToggleEdgeDockHotkeyMode = value;
        if (!_isSyncingHotkeyMode)
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEnableEdgeDockArrowHotkeysChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.EnableEdgeDockArrowHotkeys = value;
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Edge Dock (Side Panel) settings ───────────────────────────────────────
    [ObservableProperty] private bool _enableEdgeDock;
    [ObservableProperty] private SideDockMonitorMode _edgeDockMonitorMode;
    [ObservableProperty] private DockEdge _primaryDockEdge = DockEdge.Left;
    [ObservableProperty] private SideDockLockState _edgeDockLockState = SideDockLockState.AutoHide;
    [ObservableProperty] private bool _edgeDockIsPinned;
    [ObservableProperty] private bool _edgeDockShowOnHover = true;
    [ObservableProperty] private bool _edgeDockShowBubble = true;
    [ObservableProperty] private int _edgeDockBubbleSize = 16;
    [ObservableProperty] private int _edgeDockIconSize = 48;

    public string EdgeDockIconSizeDisplayText =>
        _settings.DefaultEdgeDockIconSize <= 0 && _settings.EdgeDocks.All(d => d.PerDockIconSize <= 0)
            ? $"{EdgeDockIconSize}px (Global)"
            : $"{EdgeDockIconSize}px";

    // Per-monitor / multi-edge (1, 2, 3, or all 4 edges) controls
    public ObservableCollection<MonitorOptionItem> AvailableMonitors { get; } = new();
    [ObservableProperty] private MonitorOptionItem? _selectedMonitorOption;
    [ObservableProperty] private bool _edgeLeftEnabled = true;
    [ObservableProperty] private bool _edgeTopEnabled;
    [ObservableProperty] private bool _edgeRightEnabled;
    [ObservableProperty] private bool _edgeBottomEnabled;

    partial void OnEnableEdgeDockChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.EnableEdgeDock = value;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockMonitorModeChanged(SideDockMonitorMode value)
    {
        if (_isInitializing) return;
        _settings.EdgeDockMonitorMode = value;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockLockStateChanged(SideDockLockState value)
    {
        if (_isInitializing || _isLoadingMonitorEdges) return;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockIsPinnedChanged(bool value)
    {
        if (_isInitializing || _isLoadingMonitorEdges) return;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockShowOnHoverChanged(bool value)
    {
        if (_isInitializing || _isLoadingMonitorEdges) return;
        _settings.DefaultEdgeDockHoverShow = value;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockShowBubbleChanged(bool value)
    {
        if (_isInitializing || _isLoadingMonitorEdges) return;
        _settings.DefaultEdgeDockShowBubble = value;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockBubbleSizeChanged(int value)
    {
        if (_isInitializing || _isLoadingMonitorEdges) return;
        _settings.DefaultEdgeDockBubbleSize = Math.Clamp(value, 12, 48);
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeDockIconSizeChanged(int value)
    {
        if (_isInitializing || _isLoadingMonitorEdges) return;
        int clamped = Math.Clamp(value, 16, 96);
        _settings.DefaultEdgeDockIconSize = clamped;
        foreach (var dock in _settings.EdgeDocks)
        {
            if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom ||
                SelectedMonitorOption == null ||
                string.Equals(dock.MonitorDeviceName, SelectedMonitorOption.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                (SelectedMonitorOption.IsPrimary && string.IsNullOrEmpty(dock.MonitorDeviceName)))
            {
                dock.PerDockIconSize = clamped;
            }
        }
        OnPropertyChanged(nameof(EdgeDockIconSizeDisplayText));
        IconSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ResetEdgeDockIconSize()
    {
        _isLoadingMonitorEdges = true;
        _settings.DefaultEdgeDockIconSize = 0;
        foreach (var dock in _settings.EdgeDocks)
            dock.PerDockIconSize = 0;
        EdgeDockIconSize = IconSizeMode == IconSizeMode.Custom ? CustomIconSize : 48;
        _isLoadingMonitorEdges = false;
        OnPropertyChanged(nameof(EdgeDockIconSizeDisplayText));
        IconSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLiveEdgeDockChanges()
    {
        if (_isInitializing) return;

        SyncSelectedMonitorEdgesToSettings();

        if (EnableEdgeDock && !_settings.EdgeDocks.Any(c => c.IsEnabled))
        {
            var first = _settings.EdgeDocks.FirstOrDefault();
            if (first != null)
            {
                first.Edge = PrimaryDockEdge;
                first.IsEnabled = true;
            }
        }

        foreach (var dock in _settings.EdgeDocks)
        {
            if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom ||
                SelectedMonitorOption == null ||
                string.Equals(dock.MonitorDeviceName, SelectedMonitorOption.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                (SelectedMonitorOption.IsPrimary && string.IsNullOrEmpty(dock.MonitorDeviceName)))
            {
                dock.LockState = EdgeDockLockState;
                dock.IsPinned = EdgeDockIsPinned;
                dock.ShowOnEdgeHover = EdgeDockShowOnHover;
                dock.ShowBubbleButton = EdgeDockShowBubble;
                dock.BubbleSize = Math.Clamp(EdgeDockBubbleSize, 12, 48);
            }
        }

        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Update settings ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _autoUpdate;
    [ObservableProperty] private bool _autoInstall;

    partial void OnAutoUpdateChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.AutoUpdate = value;
    }

    partial void OnAutoInstallChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.AutoInstall = value;
    }

    // ── Developer / feedback ──────────────────────────────────────────────────
    [ObservableProperty] private bool _optInBugReporting;

    partial void OnOptInBugReportingChanged(bool value)
    {
        if (_isInitializing) return;
        _settings.OptInBugReporting = value;
    }

    // ── Update UI state (not persisted directly) ───────────────────────────────
    [ObservableProperty] private bool   _isCheckingForUpdate;
    [ObservableProperty] private bool   _isDownloadingUpdate;
    [ObservableProperty] private bool   _updateAvailable;
    [ObservableProperty] private string _availableVersion  = string.Empty;
    [ObservableProperty] private string _updateReleaseNotes = string.Empty;
    [ObservableProperty] private string _updateStatusText   = string.Empty;
    [ObservableProperty] private int    _downloadProgress;
    [ObservableProperty] private bool   _canReinstallLatest;
    [ObservableProperty] private string _reinstallButtonText = "🔄 Reinstall Latest";

    private Services.UpdateService.UpdateInfo? _pendingUpdate;
    private Services.UpdateService.UpdateInfo? _latestKnownRelease;
    private System.Threading.CancellationTokenSource? _updateCts;

    public List<Models.ThemeMode> ThemeModes { get; } = new() { Models.ThemeMode.Light, Models.ThemeMode.Dark, Models.ThemeMode.System };
    public List<GroupDockMode> DockModes { get; } = new() { GroupDockMode.Sticky, GroupDockMode.Float };
    public List<IconSizeMode> IconSizeModes { get; } = new() { IconSizeMode.System, IconSizeMode.Custom };
    public System.Array HotkeyModes { get; } = Enum.GetValues(typeof(Models.HotkeyMode));
    public List<SideDockMonitorMode> EdgeDockMonitorModes { get; } = new()
    {
        SideDockMonitorMode.MainMonitorOnly,
        SideDockMonitorMode.SameForAllMonitors,
        SideDockMonitorMode.PerMonitorCustom
    };
    public List<DockEdge> DockEdges { get; } = new()
    {
        DockEdge.Left,
        DockEdge.Top,
        DockEdge.Right,
        DockEdge.Bottom
    };
    public List<SideDockLockState> EdgeDockLockStates { get; } = new()
    {
        SideDockLockState.AutoHide,
        SideDockLockState.LockedOpen,
        SideDockLockState.LockedClosed
    };
    public List<int> BubbleSizePresets { get; } = new() { 12, 16, 20, 24, 32, 40, 48 };

    // Expose settings for window position persistence
    public AppSettings Settings => _settings;

    public Window? OwnerWindow { get; set; }

    public SettingsViewModel(
        AppSettings settings,
        PersistenceService persistenceService,
        StartupService startupService,
        MonitorService? monitorService = null)
    {
        _settings = settings;
        _persistenceService = persistenceService;
        _startupService = startupService;
        _monitorService = monitorService ?? new MonitorService();

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
        DesktopShortcutInstalled = Helpers.ShortcutInstallHelper.IsDesktopShortcutInstalled();
        if (_settings.DesktopShortcutInstalled != DesktopShortcutInstalled)
            _settings.DesktopShortcutInstalled = DesktopShortcutInstalled;
        PinnedToStart = settings.PinnedToStart || Helpers.ShortcutInstallHelper.IsStartMenuShortcutInstalled();
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
        ToggleEdgeDockHotkey = settings.ToggleEdgeDockHotkey;
        ToggleEdgeDockMouseHotkey = settings.ToggleEdgeDockMouseHotkey;
        ToggleEdgeDockHotkeyMode = settings.ToggleEdgeDockHotkeyMode;
        EnableEdgeDockArrowHotkeys = settings.EnableEdgeDockArrowHotkeys;

        // Initialize Edge Dock properties
        EnableEdgeDock = settings.EnableEdgeDock;
        EdgeDockMonitorMode = settings.EdgeDockMonitorMode;
        EdgeDockBubbleSize = Math.Clamp(settings.DefaultEdgeDockBubbleSize, 12, 48);
        EdgeDockShowBubble = settings.DefaultEdgeDockShowBubble;
        EdgeDockShowOnHover = settings.DefaultEdgeDockHoverShow;

        PopulateMonitorsAndEdgeDockState();

        UpdateStatusText = $"Last checked: {(settings.LastUpdateCheck == DateTime.MinValue ? "Never" : settings.LastUpdateCheck.ToLocalTime().ToString("g"))}";
        _isInitializing = false;
    }

    private void PopulateMonitorsAndEdgeDockState()
    {
        _settings.EdgeDocks ??= new List<EdgeDockConfig>();
        var monitors = _monitorService.GetAllMonitors();
        AvailableMonitors.Clear();

        if (monitors.Count == 0)
        {
            AvailableMonitors.Add(new MonitorOptionItem
            {
                DeviceName = "\\\\.\\DISPLAY1",
                DisplayName = "Primary Monitor (Default)",
                IsPrimary = true
            });
        }
        else
        {
            int idx = 1;
            foreach (var m in monitors)
            {
                AvailableMonitors.Add(new MonitorOptionItem
                {
                    DeviceName = m.DeviceName,
                    DisplayName = $"Monitor {idx}: {(m.IsPrimary ? "Primary " : "")}({(int)m.Bounds.Width}×{(int)m.Bounds.Height})",
                    IsPrimary = m.IsPrimary
                });
                idx++;
            }
        }

        var primaryDev = AvailableMonitors.FirstOrDefault(m => m.IsPrimary)?.DeviceName
                         ?? AvailableMonitors.First().DeviceName;

        _settings.ConsolidateEdgeDocksForPrimaryMonitor(primaryDev);

        if (_settings.EdgeDocks.Count == 0)
        {
            _settings.EdgeDocks.Add(new EdgeDockConfig
            {
                MonitorDeviceName = primaryDev,
                Edge = DockEdge.Left,
                IsEnabled = true,
                Name = "Side Panel",
                LockState = SideDockLockState.AutoHide,
                IsPinned = false,
                ShowOnEdgeHover = EdgeDockShowOnHover,
                ShowBubbleButton = EdgeDockShowBubble,
                BubbleSize = EdgeDockBubbleSize,
                IconColumns = 1
            });
        }

        SelectedMonitorOption = AvailableMonitors.FirstOrDefault(m => m.IsPrimary) ?? AvailableMonitors.FirstOrDefault();
        LoadSelectedMonitorEdgeState();
    }

    partial void OnSelectedMonitorOptionChanged(MonitorOptionItem? value)
    {
        LoadSelectedMonitorEdgeState();
    }

    private void LoadSelectedMonitorEdgeState()
    {
        if (SelectedMonitorOption == null) return;
        _isLoadingMonitorEdges = true;
        try
        {
            var dev = SelectedMonitorOption.DeviceName;
            var isPrimary = SelectedMonitorOption.IsPrimary;

            if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom && isPrimary)
            {
                _settings.ConsolidateEdgeDocksForPrimaryMonitor(dev);
            }

            var monConfigs = EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom
                ? _settings.EdgeDocks.ToList()
                : _settings.EdgeDocks
                    .Where(c => string.Equals(c.MonitorDeviceName, dev, StringComparison.OrdinalIgnoreCase)
                             || (isPrimary && string.IsNullOrEmpty(c.MonitorDeviceName)))
                    .ToList();

            EdgeLeftEnabled = monConfigs.Any(c => c.Edge == DockEdge.Left && c.IsEnabled);
            EdgeTopEnabled = monConfigs.Any(c => c.Edge == DockEdge.Top && c.IsEnabled);
            EdgeRightEnabled = monConfigs.Any(c => c.Edge == DockEdge.Right && c.IsEnabled);
            EdgeBottomEnabled = monConfigs.Any(c => c.Edge == DockEdge.Bottom && c.IsEnabled);

            var activeCfg = monConfigs.FirstOrDefault(c => c.IsEnabled) ?? monConfigs.FirstOrDefault();
            if (activeCfg != null)
            {
                PrimaryDockEdge = activeCfg.Edge;
                EdgeDockLockState = activeCfg.LockState;
                EdgeDockIsPinned = activeCfg.IsPinned;
                EdgeDockShowOnHover = activeCfg.ShowOnEdgeHover;
                EdgeDockShowBubble = activeCfg.ShowBubbleButton;
                EdgeDockBubbleSize = Math.Clamp(activeCfg.BubbleSize, 12, 48);
                int effectiveIconSz = activeCfg.PerDockIconSize > 0
                    ? activeCfg.PerDockIconSize
                    : (_settings.DefaultEdgeDockIconSize > 0
                        ? _settings.DefaultEdgeDockIconSize
                        : (IconSizeMode == IconSizeMode.Custom ? CustomIconSize : 48));
                EdgeDockIconSize = Math.Clamp(effectiveIconSz, 16, 96);
                OnPropertyChanged(nameof(EdgeDockIconSizeDisplayText));
            }
        }
        finally
        {
            _isLoadingMonitorEdges = false;
        }
    }

    partial void OnPrimaryDockEdgeChanged(DockEdge value)
    {
        if (_isLoadingMonitorEdges || SelectedMonitorOption == null) return;

        int enabledCount = (EdgeLeftEnabled ? 1 : 0) + (EdgeTopEnabled ? 1 : 0) + (EdgeRightEnabled ? 1 : 0) + (EdgeBottomEnabled ? 1 : 0);
        if (enabledCount <= 1)
        {
            _isLoadingMonitorEdges = true;
            EdgeLeftEnabled = value == DockEdge.Left;
            EdgeTopEnabled = value == DockEdge.Top;
            EdgeRightEnabled = value == DockEdge.Right;
            EdgeBottomEnabled = value == DockEdge.Bottom;
            _isLoadingMonitorEdges = false;
            ApplyLiveEdgeDockChanges();
        }
    }

    partial void OnEdgeLeftEnabledChanged(bool value)
    {
        if (_isLoadingMonitorEdges) return;
        if (value) PrimaryDockEdge = DockEdge.Left;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeTopEnabledChanged(bool value)
    {
        if (_isLoadingMonitorEdges) return;
        if (value) PrimaryDockEdge = DockEdge.Top;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeRightEnabledChanged(bool value)
    {
        if (_isLoadingMonitorEdges) return;
        if (value) PrimaryDockEdge = DockEdge.Right;
        ApplyLiveEdgeDockChanges();
    }

    partial void OnEdgeBottomEnabledChanged(bool value)
    {
        if (_isLoadingMonitorEdges) return;
        if (value) PrimaryDockEdge = DockEdge.Bottom;
        ApplyLiveEdgeDockChanges();
    }

    private void SyncSelectedMonitorEdgesToSettings()
    {
        if (SelectedMonitorOption == null) return;
        var dev = SelectedMonitorOption.DeviceName;
        var isPrimary = SelectedMonitorOption.IsPrimary;

        SetMonitorEdgeState(dev, isPrimary, DockEdge.Left, EdgeLeftEnabled);
        SetMonitorEdgeState(dev, isPrimary, DockEdge.Top, EdgeTopEnabled);
        SetMonitorEdgeState(dev, isPrimary, DockEdge.Right, EdgeRightEnabled);
        SetMonitorEdgeState(dev, isPrimary, DockEdge.Bottom, EdgeBottomEnabled);
    }

    private void SetMonitorEdgeState(string dev, bool isPrimary, DockEdge edge, bool enabled)
    {
        if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom && isPrimary)
        {
            _settings.ConsolidateEdgeDocksForPrimaryMonitor(dev);
        }

        var cfg = EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom
            ? _settings.EdgeDocks.FirstOrDefault(c => c.Edge == edge)
            : _settings.EdgeDocks.FirstOrDefault(c =>
                c.Edge == edge &&
                (string.Equals(c.MonitorDeviceName, dev, StringComparison.OrdinalIgnoreCase) ||
                 (isPrimary && string.IsNullOrEmpty(c.MonitorDeviceName))));

        if (cfg == null)
        {
            if (!enabled) return;
            var reusable = _settings.EdgeDocks.FirstOrDefault(c =>
                !c.IsEnabled &&
                c.Shortcuts.Count == 0 &&
                (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom ||
                 string.Equals(c.MonitorDeviceName, dev, StringComparison.OrdinalIgnoreCase) ||
                 (isPrimary && string.IsNullOrEmpty(c.MonitorDeviceName))));

            if (reusable != null)
            {
                reusable.MonitorDeviceName = dev;
                reusable.Edge = edge;
                reusable.IsEnabled = true;
                return;
            }

            _settings.EdgeDocks.Add(new EdgeDockConfig
            {
                MonitorDeviceName = dev,
                Edge = edge,
                IsEnabled = true,
                Name = $"Side Panel ({edge})",
                LockState = EdgeDockLockState,
                IsPinned = EdgeDockIsPinned,
                ShowOnEdgeHover = EdgeDockShowOnHover,
                ShowBubbleButton = EdgeDockShowBubble,
                BubbleSize = EdgeDockBubbleSize,
                IconColumns = 1
            });
        }
        else
        {
            if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom || string.IsNullOrEmpty(cfg.MonitorDeviceName))
                cfg.MonitorDeviceName = dev;
            cfg.IsEnabled = enabled;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Apply()
    {
        LastApplySucceeded = false;

        // Validate hotkeys before saving
        string? validationError = ValidateHotkeys();
        if (validationError != null)
        {
            MessageBox.Show(
                $"⚠️ Invalid Hotkey Configuration\n\n{validationError}\n\nPlease change the hotkey and try again.",
                "Invalid Hotkey",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
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
                $"Recommended: Use Ctrl+Shift or Ctrl+Alt with a letter or arrow key.\n" +
                $"Examples: Ctrl+Shift+D, Ctrl+Alt+Left, Ctrl+Shift+K";

            var dialog = new DeskGroup.Views.HotkeyWarningDialog(
                "CRITICAL: Hotkey Conflict Detected",
                message);
            dialog.Owner = OwnerWindow;

            var result = dialog.ShowDialog();

            if (result != true || !dialog.ForcedAnyway)
            {
                return;
            }
        }

        _settings.Theme = Theme; 
        _settings.GroupBackgroundOpacity = GroupBackgroundOpacity;
        _settings.DefaultDockMode = DefaultDockMode;
        _settings.DefaultSnapToGrid = DefaultSnapToGrid; _settings.SnapGridSize = SnapGridSize;
        _settings.HideGroupedIcons = HideGroupedIcons; _settings.IconSizeMode = IconSizeMode;
        _settings.CustomIconSize = CustomIconSize; _settings.StartWithWindows = StartWithWindows;
        _settings.StartMinimizedToTray = StartMinimizedToTray;
        _settings.ToggleAllHotkey = ToggleAllHotkey;
        _settings.ToggleAllMouseHotkey = ToggleAllMouseHotkey;
        _settings.ToggleAllHotkeyMode = ToggleAllHotkeyMode;
        _settings.FullscreenToggleHotkey = FullscreenToggleHotkey;
        _settings.FullscreenMouseHotkey = FullscreenMouseHotkey;
        _settings.FullscreenHotkeyMode = FullscreenHotkeyMode;
        _settings.ToggleEdgeDockHotkey = ToggleEdgeDockHotkey;
        _settings.ToggleEdgeDockMouseHotkey = ToggleEdgeDockMouseHotkey;
        _settings.ToggleEdgeDockHotkeyMode = ToggleEdgeDockHotkeyMode;
        _settings.EnableEdgeDockArrowHotkeys = EnableEdgeDockArrowHotkeys;
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

        // Save Edge Dock settings
        _settings.EnableEdgeDock = EnableEdgeDock;
        _settings.EdgeDockMonitorMode = EdgeDockMonitorMode;
        _settings.DefaultEdgeDockBubbleSize = Math.Clamp(EdgeDockBubbleSize, 12, 48);
        _settings.DefaultEdgeDockShowBubble = EdgeDockShowBubble;
        _settings.DefaultEdgeDockHoverShow = EdgeDockShowOnHover;

        SyncSelectedMonitorEdgesToSettings();

        if (EnableEdgeDock && !_settings.EdgeDocks.Any(c => c.IsEnabled))
        {
            var first = _settings.EdgeDocks.FirstOrDefault();
            if (first != null)
            {
                first.Edge = PrimaryDockEdge;
                first.IsEnabled = true;
            }
        }

        foreach (var dock in _settings.EdgeDocks)
        {
            if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom ||
                SelectedMonitorOption == null ||
                string.Equals(dock.MonitorDeviceName, SelectedMonitorOption.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                (SelectedMonitorOption.IsPrimary && string.IsNullOrEmpty(dock.MonitorDeviceName)))
            {
                dock.LockState = EdgeDockLockState;
                dock.IsPinned = EdgeDockIsPinned;
                dock.ShowOnEdgeHover = EdgeDockShowOnHover;
                dock.ShowBubbleButton = EdgeDockShowBubble;
                dock.BubbleSize = Math.Clamp(EdgeDockBubbleSize, 12, 48);
            }
        }

        _startupService.SyncWithSetting(StartWithWindows);
        if (EnableShellContextMenu) Helpers.ShellContextMenuHelper.RegisterContextMenu();
        else Helpers.ShellContextMenuHelper.UnregisterContextMenu();

        if (DesktopShortcutInstalled) Helpers.ShortcutInstallHelper.InstallDesktopShortcut();
        else Helpers.ShortcutInstallHelper.RemoveDesktopShortcut();

        if (PinnedToStart) Helpers.ShortcutInstallHelper.PinToStartMenu();
        else Helpers.ShortcutInstallHelper.RemoveStartMenuShortcut();

        await _persistenceService.SaveSettingsImmediateAsync(_settings);

        LastApplySucceeded = true;
        HideGroupedIconsChanged?.Invoke(this, EventArgs.Empty);
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        LiveSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HandlePinToStart()
    {
        var result = Helpers.ShortcutInstallHelper.PinToStartMenu();
        if (result == Helpers.ShortcutInstallHelper.PinResult.Success) return;
        MessageBox.Show(
            "Automatic Start menu pinning isn't supported on Windows 11.\n\n" +
            "To pin DeskGroup manually:\n" +
            "1. Find the DeskGroup shortcut on your desktop or in Start → All apps\n" +
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
            "1. Find the DeskGroup shortcut on your desktop or in Start → All apps\n" +
            "2. Right-click it and choose 'Pin to taskbar'\n\n" +
            "Your preference has been saved.",
            "Pin to Taskbar — Manual Action Needed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ── Update commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private async System.Threading.Tasks.Task CheckForUpdateAsync()
    {
        if (IsCheckingForUpdate) return;
        IsCheckingForUpdate = true;
        UpdateAvailable = false;
        CanReinstallLatest = false;
        _pendingUpdate  = null;

        _updateCts?.Cancel();
        _updateCts = new System.Threading.CancellationTokenSource();

        try
        {
            UpdateStatusText = "Checking for updates…";
            var detailed = await Services.UpdateService.CheckDetailedAsync(
                _settings.SkippedUpdateVersion,
                ignoreSkipped: true,
                _updateCts.Token).ConfigureAwait(true);

            _settings.LastUpdateCheck = DateTime.UtcNow;
            await _persistenceService.SaveSettingsImmediateAsync(_settings);

            _latestKnownRelease = detailed.LatestRelease;
            var info = detailed.UpdateAvailable;

            if (info == null)
            {
                if (detailed.LatestRelease != null)
                {
                    CanReinstallLatest = !string.IsNullOrEmpty(detailed.LatestRelease.DownloadUrl);
                    ReinstallButtonText = $"🔄 Reinstall / Repair v{detailed.LatestRelease.Version}";
                    UpdateStatusText = $"✅ You're up to date (v{Services.UpdateService.GetCurrentVersion()})  —  " +
                                       $"Last checked: {DateTime.Now:g}";
                }
                else
                {
                    UpdateStatusText = !string.IsNullOrEmpty(detailed.DiagnosticMessage)
                        ? $"⚠️ {detailed.DiagnosticMessage}"
                        : $"✅ You're up to date (v{Services.UpdateService.GetCurrentVersion()})  —  Last checked: {DateTime.Now:g}";
                }
            }
            else
            {
                _pendingUpdate      = info;
                AvailableVersion    = info.Version;
                UpdateReleaseNotes  = info.ReleaseNotes;
                UpdateAvailable     = true;
                UpdateStatusText    = info.IsSameVersionRebuild
                    ? $"🆕 Newer build of v{info.Version} available"
                    : $"🆕 Update available: v{info.Version}";

                if (AutoUpdate && AutoInstall)
                    await DownloadAndInstallAsync(_updateCts.Token);
            }
        }
        catch (System.OperationCanceledException) { UpdateStatusText = "Check cancelled."; }
        catch (Exception ex)                      { UpdateStatusText = $"Check failed: {ex.Message}"; }
        finally                                   { IsCheckingForUpdate = false; }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ReinstallLatestAsync()
    {
        if (_latestKnownRelease == null || IsDownloadingUpdate) return;
        _pendingUpdate = _latestKnownRelease;
        await DownloadAndInstallAsync(System.Threading.CancellationToken.None);
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
                warnings.Add($"'{ToggleAllHotkey}' is a common system shortcut and will likely not work.");
        }

        if (!string.IsNullOrWhiteSpace(FullscreenToggleHotkey))
        {
            var upper = FullscreenToggleHotkey.ToUpperInvariant().Replace(" ", "");
            if (System.Array.Exists(systemHotkeys, h => h == upper))
                warnings.Add($"'{FullscreenToggleHotkey}' is a common system shortcut and will likely not work.");
        }

        if (!string.IsNullOrWhiteSpace(ToggleEdgeDockHotkey))
        {
            var upper = ToggleEdgeDockHotkey.ToUpperInvariant().Replace(" ", "");
            if (System.Array.Exists(systemHotkeys, h => h == upper))
                warnings.Add($"'{ToggleEdgeDockHotkey}' is a common system shortcut and will likely not work.");
        }

        return warnings;
    }

    public string? ValidateHotkeys()
    {
        if (!string.IsNullOrWhiteSpace(ToggleAllHotkey) && !HotkeyService.IsValidHotkeyString(ToggleAllHotkey))
            return $"'Toggle All Groups' keyboard hotkey '{ToggleAllHotkey}' is not in valid format.\n\nExamples: Ctrl+F2, XButton1, Ctrl+ScrollUp";

        if (!string.IsNullOrWhiteSpace(ToggleAllMouseHotkey))
        {
            if (!HotkeyService.IsValidHotkeyString(ToggleAllMouseHotkey) || !HotkeyService.IsMouseHotkeyString(ToggleAllMouseHotkey))
                return $"'Toggle All Groups' mouse hotkey '{ToggleAllMouseHotkey}' must be a valid mouse shortcut.\n\nExamples: MiddleClick, XButton1, ScrollUp, Ctrl+RightClick";
        }

        if (!string.IsNullOrWhiteSpace(FullscreenToggleHotkey) && !HotkeyService.IsValidHotkeyString(FullscreenToggleHotkey))
            return $"'Fullscreen Toggle' keyboard hotkey '{FullscreenToggleHotkey}' is not in valid format.\n\nExamples: F11, Ctrl+XButton2";

        if (!string.IsNullOrWhiteSpace(FullscreenMouseHotkey))
        {
            if (!HotkeyService.IsValidHotkeyString(FullscreenMouseHotkey) || !HotkeyService.IsMouseHotkeyString(FullscreenMouseHotkey))
                return $"'Fullscreen Toggle' mouse hotkey '{FullscreenMouseHotkey}' must be a valid mouse shortcut.\n\nExamples: MiddleClick, XButton2, ScrollDown, Ctrl+RightClick";
        }

        if (!string.IsNullOrWhiteSpace(ToggleEdgeDockHotkey) && !HotkeyService.IsValidHotkeyString(ToggleEdgeDockHotkey))
            return $"'Show/Hide Side Panel' keyboard hotkey '{ToggleEdgeDockHotkey}' is not in valid format.\n\nExamples: Ctrl+Alt+Left, Ctrl+Shift+S";

        if (!string.IsNullOrWhiteSpace(ToggleEdgeDockMouseHotkey))
        {
            if (!HotkeyService.IsValidHotkeyString(ToggleEdgeDockMouseHotkey) || !HotkeyService.IsMouseHotkeyString(ToggleEdgeDockMouseHotkey))
                return $"'Show/Hide Side Panel' mouse hotkey '{ToggleEdgeDockMouseHotkey}' must be a valid mouse shortcut.\n\nExamples: MiddleClick, Ctrl+XButton1, Ctrl+RightClick";
        }

        var activeHotkeys = new List<(string Label, string Value)>();
        if (ToggleAllHotkeyMode != Models.HotkeyMode.Mouse && !string.IsNullOrWhiteSpace(ToggleAllHotkey))
            activeHotkeys.Add(("Toggle All (keyboard)", ToggleAllHotkey));
        if (ToggleAllHotkeyMode != Models.HotkeyMode.Keyboard && !string.IsNullOrWhiteSpace(ToggleAllMouseHotkey))
            activeHotkeys.Add(("Toggle All (mouse)", ToggleAllMouseHotkey));

        if (FullscreenHotkeyMode != Models.HotkeyMode.Mouse && !string.IsNullOrWhiteSpace(FullscreenToggleHotkey))
            activeHotkeys.Add(("Fullscreen (keyboard)", FullscreenToggleHotkey));
        if (FullscreenHotkeyMode != Models.HotkeyMode.Keyboard && !string.IsNullOrWhiteSpace(FullscreenMouseHotkey))
            activeHotkeys.Add(("Fullscreen (mouse)", FullscreenMouseHotkey));

        if (ToggleEdgeDockHotkeyMode != Models.HotkeyMode.Mouse && !string.IsNullOrWhiteSpace(ToggleEdgeDockHotkey))
            activeHotkeys.Add(("Side Panel (keyboard)", ToggleEdgeDockHotkey));
        if (ToggleEdgeDockHotkeyMode != Models.HotkeyMode.Keyboard && !string.IsNullOrWhiteSpace(ToggleEdgeDockMouseHotkey))
            activeHotkeys.Add(("Side Panel (mouse)", ToggleEdgeDockMouseHotkey));

        for (int i = 0; i < activeHotkeys.Count; i++)
        {
            for (int j = i + 1; j < activeHotkeys.Count; j++)
            {
                if (activeHotkeys[i].Value.Equals(activeHotkeys[j].Value, System.StringComparison.OrdinalIgnoreCase))
                    return $"'{activeHotkeys[i].Label}' and '{activeHotkeys[j].Label}' are set to the same combination ('{activeHotkeys[i].Value}').\n\nEach active shortcut must be unique.";
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
        FullscreenHotkeyMode = d.FullscreenHotkeyMode;
        ToggleEdgeDockHotkey = d.ToggleEdgeDockHotkey;
        ToggleEdgeDockMouseHotkey = d.ToggleEdgeDockMouseHotkey;
        ToggleEdgeDockHotkeyMode = d.ToggleEdgeDockHotkeyMode;
        EnableEdgeDockArrowHotkeys = d.EnableEdgeDockArrowHotkeys;
        WatchForUngroupedIcons = d.WatchForUngroupedIcons;
        AllowMultiMonitor = d.AllowMultiMonitor;
        ShowDebugMenu = d.ShowDebugMenu;
        AutoUpdate  = d.AutoUpdate;
        AutoInstall = d.AutoInstall;
        SingleClickToLaunch = d.SingleClickToLaunch;
        EnableShellContextMenu = d.EnableShellContextMenu;
        OptInBugReporting = d.OptInBugReporting;
        EnableEdgeDock = d.EnableEdgeDock;
        EdgeDockMonitorMode = d.EdgeDockMonitorMode;
        PrimaryDockEdge = DockEdge.Left;
        EdgeDockLockState = SideDockLockState.AutoHide;
        EdgeDockIsPinned = false;
        EdgeDockShowOnHover = d.DefaultEdgeDockHoverShow;
        EdgeDockShowBubble = d.DefaultEdgeDockShowBubble;
        EdgeDockBubbleSize = d.DefaultEdgeDockBubbleSize;
        ResetEdgeDockIconSize();
        EdgeLeftEnabled = true;
        EdgeTopEnabled = false;
        EdgeRightEnabled = false;
        EdgeBottomEnabled = false;
    }
}
