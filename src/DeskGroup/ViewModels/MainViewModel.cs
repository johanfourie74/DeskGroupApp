using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PersistenceService _persistenceService;
    private readonly UsageTrackingService _usageTracker;
    private readonly IconExtractorService _iconExtractor;
    private readonly ShortcutResolverService _shortcutResolver;
    private readonly DesktopIconService _desktopIconService;
    private readonly MonitorService _monitorService;
    private readonly StartupService _startupService;
    private readonly HotkeyService _hotkeyService;
    private readonly UngroupedIconWatcherService _ungroupedWatcher;
    private readonly WindowPositionHelper _positionHelper;

    private AppSettings _settings = new();
    private List<IconGroup> _groupModels = new();
    private bool _suppressHotkeyWarnings;

    [ObservableProperty] private bool _allGroupsVisible = true;
    [ObservableProperty] private bool _isLoaded;

    public ObservableCollection<GroupViewModel> Groups { get; } = new();
    public ObservableCollection<EdgeDockViewModel> ActiveEdgeDocks { get; } = new();
    public AppSettings Settings => _settings;
    public MonitorService MonitorService => _monitorService;
    public bool WasFirstRun { get; private set; }

    public event EventHandler<DeskGroup.Models.ThemeMode>? ThemeChanged;
    public event EventHandler<List<string>>? UngroupedIconsDetected;
    public event EventHandler<GroupViewModel>? GroupAdded;
    public event EventHandler<GroupViewModel>? GroupRemoved;
    public event EventHandler? EdgeDocksRebuilt;
    public event EventHandler? OpenSettingsRequested;

    public MainViewModel()
    {
        _persistenceService = new PersistenceService();
        _usageTracker = new UsageTrackingService(_persistenceService);
        _iconExtractor = new IconExtractorService(_persistenceService.DataDirectory);
        _shortcutResolver = new ShortcutResolverService(_iconExtractor);
        _monitorService = new MonitorService();
        _startupService = new StartupService();
        _hotkeyService = new HotkeyService();
        _positionHelper = new WindowPositionHelper(_monitorService);
        _desktopIconService = new DesktopIconService(_persistenceService.DataDirectory);
        _ungroupedWatcher = new UngroupedIconWatcherService();
        _ungroupedWatcher.NewUngroupedIconsDetected += OnNewUngroupedIconsDetected;
    }

    public async Task InitializeAsync()
    {
        _settings = await _persistenceService.LoadSettingsAsync();
        EnsureDefaultEdgeDockConfig();
        AutoHealHotkeyModes();
        _persistenceService.SetDebounceInterval(_settings.AutoSaveDebounceMs);
        _startupService.SyncWithSetting(_settings.StartWithWindows);
        _groupModels = await _persistenceService.LoadGroupsAsync();

        foreach (var groupModel in _groupModels)
        {
            var groupVm = CreateGroupViewModel(groupModel);
            Groups.Add(groupVm);
        }

        // Build active Edge Dock instances (independent of Groups visibility)
        RebuildEdgeDocks();

        // RE-ENABLED with improved throttling and filtering
        if (_settings.WatchForUngroupedIcons)
        {
            System.Diagnostics.Debug.WriteLine("MainViewModel: Starting UngroupedIconWatcher with 5s cooldown");
            _ungroupedWatcher.CooldownSeconds = 5;
            _ungroupedWatcher.StartWatching();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("MainViewModel: UngroupedIconWatcher disabled in settings");
        }

        // Perform orphan cleanup asynchronously on a background thread so it never slows startup
        var groupsCopy = _groupModels.ToList();
        var docksCopy = _settings.EdgeDocks.ToList();
        _ = Task.Run(() => _desktopIconService.CleanupOrphans(groupsCopy, docksCopy));

        // Honor StartMinimizedToTray if enabled
        if (_settings.StartMinimizedToTray && _groupModels.Count > 0)
        {
            foreach (var g in Groups)
                g.IsVisible = false;
            AllGroupsVisible = false;
        }
        else
        {
            // Sync AllGroupsVisible with actual loaded state so first tray-click toggles correctly
            AllGroupsVisible = _groupModels.Count == 0 || _groupModels.Any(g => g.IsVisible);
        }

        if (_settings.IsFirstRun || !_settings.HasSeenSetupWizard)
        {
            WasFirstRun = true;
            _suppressHotkeyWarnings = true;
            _settings.IsFirstRun = false;
            // HasSeenSetupWizard is set to true only after the wizard completes successfully.
            await _persistenceService.SaveSettingsImmediateAsync(_settings);
        }
        IsLoaded = true;
    }

    private void AutoHealHotkeyModes()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ToggleAllMouseHotkey) &&
            _settings.ToggleAllHotkeyMode == Models.HotkeyMode.Keyboard)
        {
            _settings.ToggleAllHotkeyMode = string.IsNullOrWhiteSpace(_settings.ToggleAllHotkey)
                ? Models.HotkeyMode.Mouse
                : Models.HotkeyMode.Both;
        }

        if (!string.IsNullOrWhiteSpace(_settings.FullscreenMouseHotkey) &&
            _settings.FullscreenHotkeyMode == Models.HotkeyMode.Keyboard)
        {
            _settings.FullscreenHotkeyMode = string.IsNullOrWhiteSpace(_settings.FullscreenToggleHotkey)
                ? Models.HotkeyMode.Mouse
                : Models.HotkeyMode.Both;
        }

        if (!string.IsNullOrWhiteSpace(_settings.ToggleEdgeDockMouseHotkey) &&
            _settings.ToggleEdgeDockHotkeyMode == Models.HotkeyMode.Keyboard)
        {
            _settings.ToggleEdgeDockHotkeyMode = string.IsNullOrWhiteSpace(_settings.ToggleEdgeDockHotkey)
                ? Models.HotkeyMode.Mouse
                : Models.HotkeyMode.Both;
        }
    }

    public void SyncGroupedDesktopIconsVisibility()
    {
        try
        {
            if (_settings.HideGroupedIcons)
            {
                foreach (var group in _groupModels)
                {
                    foreach (var sc in group.Shortcuts)
                    {
                        bool alreadyTracked = group.OriginalIconStates.Any(s => s.ShortcutItemId == sc.Id);
                        if (!alreadyTracked && !string.IsNullOrEmpty(sc.OriginalShortcutPath) && File.Exists(sc.OriginalShortcutPath))
                        {
                            var state = _desktopIconService.HideDesktopIcon(sc);
                            if (state != null)
                                group.OriginalIconStates.Add(state);
                        }
                    }
                }

                if (_settings.EdgeDocks != null)
                {
                    foreach (var dock in _settings.EdgeDocks)
                    {
                        foreach (var sc in dock.Shortcuts)
                        {
                            bool alreadyTracked = dock.OriginalIconStates.Any(s => s.ShortcutItemId == sc.Id);
                            if (!alreadyTracked && !string.IsNullOrEmpty(sc.OriginalShortcutPath) && File.Exists(sc.OriginalShortcutPath))
                            {
                                var state = _desktopIconService.HideDesktopIcon(sc);
                                if (state != null)
                                    dock.OriginalIconStates.Add(state);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var group in _groupModels)
                {
                    _desktopIconService.RestoreAllGroupIcons(group);
                    group.OriginalIconStates.Clear();
                    foreach (var sc in group.Shortcuts)
                        sc.OriginalIconHidden = false;
                }

                if (_settings.EdgeDocks != null)
                {
                    foreach (var dock in _settings.EdgeDocks)
                    {
                        _desktopIconService.RestoreAllDockIcons(dock);
                        dock.OriginalIconStates.Clear();
                        foreach (var sc in dock.Shortcuts)
                            sc.OriginalIconHidden = false;
                    }
                }
            }

            _persistenceService.SaveGroupsDebounced(_groupModels);
            _ = _persistenceService.SaveSettingsImmediateAsync(_settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SyncGroupedDesktopIconsVisibility error: {ex.Message}");
        }
    }

    public void EnsureDefaultEdgeDockConfig()
    {
        _settings.EdgeDocks ??= new List<EdgeDockConfig>();
        if (_settings.EdgeDocks.Count == 0)
        {
            var primary = _monitorService.GetPrimaryMonitor();
            _settings.EdgeDocks.Add(new EdgeDockConfig
            {
                MonitorDeviceName = primary?.DeviceName ?? string.Empty,
                Edge = DockEdge.Left,
                IsEnabled = true,
                Name = "Side Panel",
                AccentColor = "#0078D4",
                LockState = SideDockLockState.AutoHide,
                IsPinned = false,
                ShowOnEdgeHover = _settings.DefaultEdgeDockHoverShow,
                ShowBubbleButton = _settings.DefaultEdgeDockShowBubble,
                BubbleSize = Math.Clamp(_settings.DefaultEdgeDockBubbleSize, 12, 48),
                EdgePositionRatio = 0.5,
                IconColumns = 1
            });
        }
    }

    public void RebuildEdgeDocks()
    {
        EnsureDefaultEdgeDockConfig();
        var previouslyOpenIds = ActiveEdgeDocks.Where(d => d.IsPanelOpen).Select(d => d.Id).ToHashSet();
        ActiveEdgeDocks.Clear();

        if (!_settings.EnableEdgeDock)
        {
            EdgeDocksRebuilt?.Invoke(this, EventArgs.Empty);
            return;
        }

        var allMonitors = _monitorService.GetAllMonitors();
        var primaryMonitor = _monitorService.GetPrimaryMonitor() ?? allMonitors.FirstOrDefault() ?? new MonitorInfo
        {
            DeviceName = "\\\\.\\DISPLAY1",
            IsPrimary = true,
            Bounds = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
            WorkingArea = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight)
        };

        if (allMonitors.Count == 0)
            allMonitors.Add(primaryMonitor);

        switch (_settings.EdgeDockMonitorMode)
        {
            case SideDockMonitorMode.MainMonitorOnly:
            {
                var mainConfigs = GetOrCreateMainMonitorConfigs(primaryMonitor.DeviceName);
                foreach (var cfg in mainConfigs.Where(c => c.IsEnabled))
                {
                    var vm = CreateEdgeDockViewModel(cfg, primaryMonitor);
                    ActiveEdgeDocks.Add(vm);
                }
                break;
            }

            case SideDockMonitorMode.SameForAllMonitors:
            {
                var mainConfigs = GetOrCreateMainMonitorConfigs(primaryMonitor.DeviceName);
                foreach (var cfg in mainConfigs.Where(c => c.IsEnabled))
                {
                    foreach (var mon in allMonitors)
                    {
                        var vm = CreateEdgeDockViewModel(cfg, mon);
                        ActiveEdgeDocks.Add(vm);
                    }
                }
                break;
            }

            case SideDockMonitorMode.PerMonitorCustom:
            {
                _settings.ConsolidateEdgeDocksForPrimaryMonitor(primaryMonitor.DeviceName);

                // If any enabled dock is bound to a monitor device name not currently connected, fall back to primary monitor
                foreach (var orphan in _settings.EdgeDocks.Where(c =>
                    !string.IsNullOrEmpty(c.MonitorDeviceName) &&
                    !allMonitors.Any(m => string.Equals(m.DeviceName, c.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))))
                {
                    orphan.MonitorDeviceName = primaryMonitor.DeviceName;
                }
                _settings.ConsolidateEdgeDocksForPrimaryMonitor(primaryMonitor.DeviceName);

                foreach (var mon in allMonitors)
                {
                    var monConfigs = _settings.EdgeDocks
                        .Where(c => string.Equals(c.MonitorDeviceName, mon.DeviceName, StringComparison.OrdinalIgnoreCase)
                                 || (mon.IsPrimary && string.IsNullOrEmpty(c.MonitorDeviceName)))
                        .ToList();

                    if (monConfigs.Count == 0 && mon.IsPrimary)
                    {
                        var first = _settings.EdgeDocks.First();
                        first.MonitorDeviceName = mon.DeviceName;
                        first.IsEnabled = true;
                        monConfigs.Add(first);
                    }

                    foreach (var cfg in monConfigs.Where(c => c.IsEnabled))
                    {
                        if (string.IsNullOrEmpty(cfg.MonitorDeviceName))
                            cfg.MonitorDeviceName = mon.DeviceName;
                        var vm = CreateEdgeDockViewModel(cfg, mon);
                        ActiveEdgeDocks.Add(vm);
                    }
                }
                break;
            }
        }

        foreach (var dockVm in ActiveEdgeDocks)
        {
            if (previouslyOpenIds.Contains(dockVm.Id) && dockVm.LockState != SideDockLockState.LockedClosed)
                dockVm.IsPanelOpen = true;
        }

        EdgeDocksRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private List<EdgeDockConfig> GetOrCreateMainMonitorConfigs(string primaryDeviceName)
    {
        if (_settings.ConsolidateEdgeDocksForPrimaryMonitor(primaryDeviceName))
        {
            _ = _persistenceService.SaveSettingsImmediateAsync(_settings);
        }

        var mainConfigs = _settings.EdgeDocks
            .Where(c => string.IsNullOrEmpty(c.MonitorDeviceName)
                     || string.Equals(c.MonitorDeviceName, primaryDeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mainConfigs.Count == 0)
        {
            var first = _settings.EdgeDocks.First();
            first.MonitorDeviceName = primaryDeviceName;
            first.IsEnabled = true;
            mainConfigs.Add(first);
        }
        else if (!mainConfigs.Any(c => c.IsEnabled))
        {
            mainConfigs[0].IsEnabled = true;
        }

        return mainConfigs;
    }

    private EdgeDockViewModel CreateEdgeDockViewModel(EdgeDockConfig cfg, MonitorInfo monitor)
    {
        var vm = new EdgeDockViewModel(
            cfg,
            monitor,
            _usageTracker,
            _iconExtractor,
            _desktopIconService,
            _persistenceService,
            _settings);

        vm.SaveRequested += (s, e) =>
        {
            // Sync any sibling mirror docks in SameForAllMonitors mode
            foreach (var sibling in ActiveEdgeDocks.Where(d => d.Id == vm.Id && !ReferenceEquals(d, vm)))
            {
                sibling.SyncFromSharedConfig();
            }
            _ = _persistenceService.SaveSettingsImmediateAsync(_settings);
            _persistenceService.SaveGroupsDebounced(_groupModels);
        };

        return vm;
    }

    public EdgeDockConfig GetOrCreateEdgeDockConfig(string monitorDeviceName, DockEdge edge)
    {
        EnsureDefaultEdgeDockConfig();
        var primary = _monitorService.GetPrimaryMonitor();
        bool isPrimary = primary != null && string.Equals(primary.DeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase);

        if (_settings.EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom)
        {
            _settings.ConsolidateEdgeDocksForPrimaryMonitor(primary?.DeviceName ?? monitorDeviceName);
            var singleExisting = _settings.EdgeDocks.FirstOrDefault(c => c.Edge == edge);
            if (singleExisting != null)
            {
                singleExisting.MonitorDeviceName = primary?.DeviceName ?? monitorDeviceName;
                return singleExisting;
            }
        }

        var existing = _settings.EdgeDocks.FirstOrDefault(c =>
            c.Edge == edge &&
            (string.Equals(c.MonitorDeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase) ||
             (isPrimary && string.IsNullOrEmpty(c.MonitorDeviceName))));

        if (existing != null)
        {
            if (string.IsNullOrEmpty(existing.MonitorDeviceName))
                existing.MonitorDeviceName = monitorDeviceName;
            return existing;
        }

        var created = new EdgeDockConfig
        {
            MonitorDeviceName = monitorDeviceName,
            Edge = edge,
            IsEnabled = false,
            Name = $"Side Panel ({edge})",
            AccentColor = "#0078D4",
            LockState = SideDockLockState.AutoHide,
            IsPinned = false,
            ShowOnEdgeHover = _settings.DefaultEdgeDockHoverShow,
            ShowBubbleButton = _settings.DefaultEdgeDockShowBubble,
            BubbleSize = Math.Clamp(_settings.DefaultEdgeDockBubbleSize, 12, 48),
            EdgePositionRatio = 0.5,
            IconColumns = 1
        };
        _settings.EdgeDocks.Add(created);
        return created;
    }

    public async Task SetEdgeDockEnabledOnMonitorAsync(string monitorDeviceName, DockEdge edge, bool enabled)
    {
        var cfg = GetOrCreateEdgeDockConfig(monitorDeviceName, edge);
        cfg.IsEnabled = enabled;
        await _persistenceService.SaveSettingsImmediateAsync(_settings);
        RebuildEdgeDocks();
    }

    public async Task ToggleEdgeDockFeatureAsync()
    {
        _settings.EnableEdgeDock = !_settings.EnableEdgeDock;
        await _persistenceService.SaveSettingsImmediateAsync(_settings);
        RebuildEdgeDocks();
    }

    public void ToggleAllEdgeDocksVisibility()
    {
        if (!_settings.EnableEdgeDock || ActiveEdgeDocks.Count == 0)
        {
            _settings.EnableEdgeDock = true;
            RebuildEdgeDocks();
            foreach (var d in ActiveEdgeDocks) d.IsPanelOpen = true;
            return;
        }

        bool anyOpen = ActiveEdgeDocks.Any(d => d.IsPanelOpen);
        foreach (var dock in ActiveEdgeDocks)
        {
            dock.IsPanelOpen = !anyOpen;
        }
    }

    public void ToggleEdgeDockByDirection(DockEdge edge)
    {
        if (!_settings.EnableEdgeDock)
            return;

        var matching = ActiveEdgeDocks.Where(d => d.Edge == edge).ToList();
        if (matching.Count > 0)
        {
            bool anyOpen = matching.Any(d => d.IsPanelOpen);
            foreach (var dock in matching)
                dock.IsPanelOpen = !anyOpen;
        }
        else if (ActiveEdgeDocks.Count == 1)
        {
            // If only 1 side panel is active on the desktop, allow any directional shortcut to toggle it
            ActiveEdgeDocks[0].TogglePanel();
        }
    }

    public void RegisterHotkeys(Window window)
    {
        AutoHealHotkeyModes();
        _hotkeyService.Initialize(window);
        _hotkeyService.UnregisterAll();

        // Ensure single subscription to RegistrationFailed
        _hotkeyService.RegistrationFailed -= OnHotkeyRegistrationFailed;
        _hotkeyService.RegistrationFailed += OnHotkeyRegistrationFailed;

        // Register keyboard hotkey for Toggle All (if mode includes keyboard)
        if (_settings.ToggleAllHotkeyMode != Models.HotkeyMode.Mouse
            && !string.IsNullOrWhiteSpace(_settings.ToggleAllHotkey))
            _hotkeyService.RegisterHotkey(_settings.ToggleAllHotkey, ToggleAllGroupsVisibility);

        // Register mouse hotkey for Toggle All (if mode includes mouse)
        if (_settings.ToggleAllHotkeyMode != Models.HotkeyMode.Keyboard
            && !string.IsNullOrWhiteSpace(_settings.ToggleAllMouseHotkey))
            _hotkeyService.RegisterHotkey(_settings.ToggleAllMouseHotkey, ToggleAllGroupsVisibility);

        // Register keyboard hotkey for Fullscreen Toggle (if mode includes keyboard and not default per-window F11)
        if (_settings.FullscreenHotkeyMode != Models.HotkeyMode.Mouse
            && !string.IsNullOrWhiteSpace(_settings.FullscreenToggleHotkey)
            && !string.Equals(_settings.FullscreenToggleHotkey.Trim(), "F11", StringComparison.OrdinalIgnoreCase))
        {
            _hotkeyService.RegisterHotkey(_settings.FullscreenToggleHotkey, ToggleFocusedGroupFullscreen);
        }

        // Register mouse hotkey for Fullscreen Toggle (if mode includes mouse)
        if (_settings.FullscreenHotkeyMode != Models.HotkeyMode.Keyboard
            && !string.IsNullOrWhiteSpace(_settings.FullscreenMouseHotkey))
        {
            _hotkeyService.RegisterHotkey(_settings.FullscreenMouseHotkey, ToggleFocusedGroupFullscreen);
        }

        // Register keyboard hotkey(s) for Show/Hide Side Panel (Edge Dock)
        if (_settings.ToggleEdgeDockHotkeyMode != Models.HotkeyMode.Mouse
            && !string.IsNullOrWhiteSpace(_settings.ToggleEdgeDockHotkey))
        {
            RegisterEdgeDockKeyboardHotkeys(_settings.ToggleEdgeDockHotkey);
        }

        // Register mouse hotkey for Show/Hide Side Panel (Edge Dock)
        if (_settings.ToggleEdgeDockHotkeyMode != Models.HotkeyMode.Keyboard
            && !string.IsNullOrWhiteSpace(_settings.ToggleEdgeDockMouseHotkey))
        {
            _hotkeyService.RegisterHotkey(_settings.ToggleEdgeDockMouseHotkey, ToggleAllEdgeDocksVisibility);
        }
    }

    private void RegisterEdgeDockKeyboardHotkeys(string hotkeyString)
    {
        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string lastToken = parts[^1].ToUpperInvariant();
        bool isArrowKey = lastToken is "LEFT" or "RIGHT" or "UP" or "DOWN";

        if (isArrowKey && _settings.EnableEdgeDockArrowHotkeys && parts.Length >= 2)
        {
            // Extract modifier prefix (e.g. "Ctrl+Alt") and register all 4 directional arrows!
            string modPrefix = string.Join("+", parts[..^1]);
            _hotkeyService.RegisterHotkey($"{modPrefix}+Left", () => ToggleEdgeDockByDirection(DockEdge.Left));
            _hotkeyService.RegisterHotkey($"{modPrefix}+Right", () => ToggleEdgeDockByDirection(DockEdge.Right));
            _hotkeyService.RegisterHotkey($"{modPrefix}+Up", () => ToggleEdgeDockByDirection(DockEdge.Top));
            _hotkeyService.RegisterHotkey($"{modPrefix}+Down", () => ToggleEdgeDockByDirection(DockEdge.Bottom));
        }
        else if (isArrowKey)
        {
            DockEdge targetEdge = lastToken switch
            {
                "LEFT" => DockEdge.Left,
                "RIGHT" => DockEdge.Right,
                "UP" => DockEdge.Top,
                "DOWN" => DockEdge.Bottom,
                _ => DockEdge.Left
            };
            _hotkeyService.RegisterHotkey(hotkeyString, () => ToggleEdgeDockByDirection(targetEdge));
        }
        else
        {
            _hotkeyService.RegisterHotkey(hotkeyString, ToggleAllEdgeDocksVisibility);
        }
    }

    private void OnHotkeyRegistrationFailed(object? sender, string errorMessage)
    {
        System.Diagnostics.Debug.WriteLine($"🔥🔥🔥 OnHotkeyRegistrationFailed CALLED: {errorMessage}");

        // Show user-friendly warning on UI thread
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_suppressHotkeyWarnings) return;
            System.Diagnostics.Debug.WriteLine($"🔥 About to show HotkeyWarningDialog...");

            var message =
                $"{errorMessage}\n\n" +
                $"💡 WHY THIS HAPPENED:\n" +
                $"This hotkey is already in use by Windows or another application.\n\n" +
                $"⚠️ IMPORTANT:\n" +
                $"• If you FORCED this hotkey to register, it may have BROKEN that other application\n" +
                $"• That application may now behave unpredictably or not respond to its shortcuts\n" +
                $"• The hotkey will NOT work in DeskGroup\n\n" +
                $"👉 You should choose a DIFFERENT hotkey combination!\n\n" +
                $"Recommended combinations:\n" +
                $"• Ctrl+Shift+D, Ctrl+Alt+G, Ctrl+Shift+K (rarely used by other apps)\n\n" +
                $"Would you like to open Settings to change the hotkey?";

            var dialog = new DeskGroup.Views.HotkeyWarningDialog(
                "Hotkey Registration Failed",
                message);

            // Set owner to the active window so the dialog appears on top
            dialog.Owner = Application.Current?.Windows.OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.MainWindow;

            // Change button texts and styles for this scenario
            dialog.CancelButtonControl.Content = "IGNORE";
            dialog.ForceButtonControl.Content = "OPEN SETTINGS";

            // Swap the styles - "OPEN SETTINGS" should be green (recommended action)
            dialog.CancelButtonControl.Style = (System.Windows.Style)dialog.FindResource("RedButtonStyle");
            dialog.ForceButtonControl.Style = (System.Windows.Style)dialog.FindResource("GreenButtonStyle");

            System.Diagnostics.Debug.WriteLine($"🔥 Showing dialog now...");
            var result = dialog.ShowDialog();
            System.Diagnostics.Debug.WriteLine($"🔥 Dialog closed with result: {result}, ForcedAnyway: {dialog.ForcedAnyway}");

            if (result == true && dialog.ForcedAnyway)
            {
                // User chose to open Settings
                System.Diagnostics.Debug.WriteLine($"🔥 User selected 'OPEN SETTINGS', invoking OpenSettingsRequested event");

                // Delay slightly to let dialog close
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ => 
                {
                    Application.Current?.Dispatcher.Invoke(() => 
                    {
                        System.Diagnostics.Debug.WriteLine($"🔥 Invoking OpenSettingsRequested NOW");
                        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
                        System.Diagnostics.Debug.WriteLine($"🔥 OpenSettingsRequested invoked, has subscribers: {OpenSettingsRequested != null}");
                    });
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔥 User selected 'IGNORE' or closed dialog");
            }
        });

        System.Diagnostics.Debug.WriteLine($"🔥 OnHotkeyRegistrationFailed COMPLETE");
    }

    private GroupViewModel CreateGroupViewModel(IconGroup model)
    {
        var vm = new GroupViewModel(model, _usageTracker, _iconExtractor,
            _desktopIconService, _persistenceService, _positionHelper, _settings);

        // Subscribe to property changes to trigger auto-save
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GroupViewModel.IsVisible) ||
                e.PropertyName == nameof(GroupViewModel.IsDragging))
                return;

            // Use debounced save for rapid-fire changes during resize (both Normal and Grid view!)
            if (e.PropertyName == nameof(GroupViewModel.Width) ||
                e.PropertyName == nameof(GroupViewModel.Height) ||
                e.PropertyName == nameof(GroupViewModel.GridViewWidth) ||
                e.PropertyName == nameof(GroupViewModel.GridViewHeight))
            {
                _persistenceService.SaveGroupsDebounced(_groupModels);
                return;
            }

            _ = _persistenceService.SaveGroupsImmediateAsync(_groupModels);
        };

        return vm;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CreateGroup()
    {
        try
        {
            // Calculate staggered position for new group
            int groupIndex = _groupModels.Count;
            double baseX = 100;
            double baseY = 100;
            double offsetX = (groupIndex % 5) * 220; // 5 groups per row, 220px apart
            double offsetY = (groupIndex / 5) * 120; // New row every 5 groups, 120px apart

            var model = new IconGroup
            {
                Name = $"Group {groupIndex + 1}",
                DockMode = _settings.DefaultDockMode,
                SnapToGrid = _settings.DefaultSnapToGrid,
                SortOrder = groupIndex,
                PositionX = baseX + offsetX,
                PositionY = baseY + offsetY,
                ViewMode = GroupViewMode.Normal
            };

            System.Diagnostics.Debug.WriteLine($"CreateGroup: {model.Name} at ({model.PositionX}, {model.PositionY}), DockMode: {model.DockMode}");

            _groupModels.Add(model);
            var vm = CreateGroupViewModel(model);
            Groups.Add(vm);

            System.Diagnostics.Debug.WriteLine($"CreateGroup: Invoking GroupAdded event");
            GroupAdded?.Invoke(this, vm);

            System.Diagnostics.Debug.WriteLine($"CreateGroup: Saving groups");
            await _persistenceService.SaveGroupsImmediateAsync(_groupModels);

            System.Diagnostics.Debug.WriteLine($"CreateGroup: COMPLETE for {model.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateGroup: FAILED with exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"CreateGroup: Stack: {ex.StackTrace}");

            MessageBox.Show(
                $"Failed to create group:\n\n{ex.Message}",
                "Create Group Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DeleteGroup(GroupViewModel groupVm)
    {
        _desktopIconService.RestoreAllGroupIcons(groupVm.Model);
        _groupModels.Remove(groupVm.Model);
        Groups.Remove(groupVm);
        GroupRemoved?.Invoke(this, groupVm);
        await _persistenceService.SaveGroupsImmediateAsync(_groupModels);
    }

    public void AddShortcutToGroup(GroupViewModel group, string filePath)
    {
        group.AddShortcutCommand.Execute(filePath);
        _persistenceService.SaveGroupsDebounced(_groupModels);
    }

    public void RemoveShortcutFromGroup(GroupViewModel group, ShortcutItemViewModel shortcut)
    {
        group.RemoveShortcutCommand.Execute(shortcut);
        _persistenceService.SaveGroupsDebounced(_groupModels);
    }

    public void ToggleAllGroupsVisibility()
    {
        // Any group with SavedDockModeBeforeFloat set means ShowAllGroups() floated them —
        // they are already visible on top, so the toggle should hide them.
        bool anyTempFloated = Groups.Any(g => g.SavedDockModeBeforeFloat.HasValue);

        if (anyTempFloated)
        {
            HideAllGroups();
        }
        else if (!AllGroupsVisible || !Groups.Any(g => g.IsVisible))
        {
            // Groups are off-screen / hidden — bring them to front.
            ShowAllGroups();
        }
        else if (Groups.Any(g => g.IsVisible && g.DockMode == GroupDockMode.Float))
        {
            // Groups are visible in permanent Float mode — hide them.
            HideAllGroups();
        }
        else
        {
            // Groups are visible but in Sticky mode (on the desktop layer, behind other windows).
            // The user can't see them; bring them to the front by floating them temporarily.
            ShowAllGroups();
        }
    }

    private void ToggleFocusedGroupFullscreen()
    {
        var target = Groups.FirstOrDefault(g => g.IsVisible && g.ViewMode != GroupViewMode.Minimized)
                     ?? Groups.FirstOrDefault(g => g.IsVisible);
        target?.ToggleFullscreenCommand.Execute(null);
    }

    private void OnNewUngroupedIconsDetected(object? sender, List<string> newFiles)
    {
        System.Diagnostics.Debug.WriteLine($"OnNewUngroupedIconsDetected: Received {newFiles.Count} file(s)");

        var undismissed = newFiles
            .Where(f =>
            {
                var fileName = System.IO.Path.GetFileName(f);
                var isDismissed = _settings.DismissedUngroupedIcons.Contains(fileName, StringComparer.OrdinalIgnoreCase);
                if (isDismissed)
                {
                    System.Diagnostics.Debug.WriteLine($"  Skipping dismissed: {fileName}");
                }
                return !isDismissed;
            }).ToList();

        if (undismissed.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("  All files were dismissed, nothing to do");
            return;
        }

        var ungrouped = _desktopIconService.GetUngroupedShortcuts(_groupModels, _settings.EdgeDocks)
            .Intersect(undismissed, StringComparer.OrdinalIgnoreCase).ToList();

        if (ungrouped.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"  Found {ungrouped.Count} ungrouped icon(s), invoking UngroupedIconsDetected event");
            Application.Current?.Dispatcher.Invoke(() => UngroupedIconsDetected?.Invoke(this, ungrouped));
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("  No ungrouped icons found (all are already in groups)");
        }
    }

    public void ProcessUngroupedAssignments(Dictionary<string, IconGroup?> assignments)
    {
        foreach (var (filePath, targetGroup) in assignments)
        {
            if (targetGroup == null) continue;
            var groupVm = Groups.FirstOrDefault(g => g.Id == targetGroup.Id);
            if (groupVm != null) AddShortcutToGroup(groupVm, filePath);
        }
        _persistenceService.SaveGroupsDebounced(_groupModels);
    }

    public List<string> GetUngroupedShortcuts() =>
        _desktopIconService.GetUngroupedShortcuts(_groupModels, _settings.EdgeDocks);

    public SettingsViewModel CreateSettingsViewModel() =>
        new SettingsViewModel(_settings, _persistenceService, _startupService, _monitorService);

    public Task SaveSettingsAsync() =>
        _persistenceService.SaveSettingsImmediateAsync(_settings);

    public void ApplySettingsChanges(Window hotkeyWindow)
    {
        ThemeChanged?.Invoke(this, _settings.Theme);

        if (_settings.WatchForUngroupedIcons)
        {
            _ungroupedWatcher.CooldownSeconds = 5;
            _ungroupedWatcher.StartWatching();
        }
        else
        {
            _ungroupedWatcher.StopWatching();
        }

        _persistenceService.SetDebounceInterval(_settings.AutoSaveDebounceMs);
        RebuildEdgeDocks();

        bool prevSuppress = _suppressHotkeyWarnings;
        _suppressHotkeyWarnings = true;
        try
        {
            RegisterHotkeys(hotkeyWindow);
        }
        finally
        {
            _suppressHotkeyWarnings = prevSuppress;
        }
    }

    public async Task SaveAllAsync()
    {
        foreach (var group in Groups)
        {
            group.Model.Name = group.Name; group.Model.ViewMode = group.ViewMode;
            group.Model.DockMode = group.DockMode; group.Model.IsPinned = group.IsPinned;
            group.Model.SnapToGrid = group.SnapToGrid; group.Model.PositionX = group.PositionX;
            group.Model.PositionY = group.PositionY; group.Model.Width = group.Width;
            group.Model.Height = group.Height; group.Model.MonitorDeviceName = group.MonitorDeviceName;
            group.Model.IsVisible = group.IsVisible;
            group.Model.AccentColor = group.AccentColorHex;
        }
        foreach (var dock in ActiveEdgeDocks)
        {
            dock.SyncToConfig();
        }
        await _persistenceService.SaveGroupsImmediateAsync(_groupModels);
        await _persistenceService.SaveSettingsImmediateAsync(_settings);
    }

    public void Shutdown()
    {
        _ungroupedWatcher.Dispose();
        _hotkeyService.Dispose();
    }

    [RelayCommand] 
    private void ShowAllGroups()
    {
        AllGroupsVisible = true;
        foreach (var group in Groups)
        {
            group.IsVisible = true;
            if (group.DockMode == GroupDockMode.Sticky)
            {
                group.SavedDockModeBeforeFloat = GroupDockMode.Sticky;
                group.DockMode = GroupDockMode.Float;
            }
        }
        DeskGroup.Views.GroupWidget.SetLastTempFloatTime();
    }

    [RelayCommand] 
    private void HideAllGroups()
    {
        AllGroupsVisible = false;
        foreach (var group in Groups)
        {
            if (group.SavedDockModeBeforeFloat.HasValue)
            {
                group.DockMode = group.SavedDockModeBeforeFloat.Value;
                group.SavedDockModeBeforeFloat = null;
            }
            group.IsVisible = false;
        }
    }

    [RelayCommand] private void ExitApplication() => Application.Current?.Shutdown();

    /// <summary>
    /// Restore ALL groups that were temporarily floated (from Show All) back to their saved dock mode.
    /// Called when user clicks any shortcut in any group.
    /// </summary>
    public void RestoreAllTemporaryFloats()
    {
        foreach (var group in Groups)
        {
            if (group.SavedDockModeBeforeFloat.HasValue)
            {
                group.DockMode = group.SavedDockModeBeforeFloat.Value;
                group.SavedDockModeBeforeFloat = null;
            }
        }
    }
}
