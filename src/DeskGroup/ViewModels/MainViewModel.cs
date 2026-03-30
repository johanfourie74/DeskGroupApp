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
    public AppSettings Settings => _settings;
    public bool WasFirstRun { get; private set; }

    public event EventHandler<DeskGroup.Models.ThemeMode>? ThemeChanged;
    public event EventHandler<List<string>>? UngroupedIconsDetected;
    public event EventHandler<GroupViewModel>? GroupAdded;
    public event EventHandler<GroupViewModel>? GroupRemoved;
    public event EventHandler? OpenSettingsRequested;

    public MainViewModel()
    {
        _persistenceService = new PersistenceService();
        _usageTracker = new UsageTrackingService(_persistenceService);
        _iconExtractor = new IconExtractorService();
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
        _persistenceService.SetDebounceInterval(_settings.AutoSaveDebounceMs);
        _startupService.SyncWithSetting(_settings.StartWithWindows);
        _groupModels = await _persistenceService.LoadGroupsAsync();

        foreach (var groupModel in _groupModels)
        {
            var groupVm = CreateGroupViewModel(groupModel);
            Groups.Add(groupVm);
        }

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

        _desktopIconService.CleanupOrphans(_groupModels);

        // Sync AllGroupsVisible with actual loaded state so first tray-click toggles correctly
        AllGroupsVisible = _groupModels.Count == 0 || _groupModels.Any(g => g.IsVisible);

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

    public void RegisterHotkeys(Window window)
    {
        _hotkeyService.Initialize(window);

        // Subscribe to registration failures BEFORE registering
        _hotkeyService.RegistrationFailed += OnHotkeyRegistrationFailed;

        // Register keyboard hotkey for Toggle All (if mode includes keyboard)
        if (_settings.ToggleAllHotkeyMode != Models.HotkeyMode.Mouse
            && !string.IsNullOrWhiteSpace(_settings.ToggleAllHotkey))
            _hotkeyService.RegisterHotkey(_settings.ToggleAllHotkey, ToggleAllGroupsVisibility);

        // Register mouse hotkey for Toggle All (if mode includes mouse)
        if (_settings.ToggleAllHotkeyMode != Models.HotkeyMode.Keyboard
            && !string.IsNullOrWhiteSpace(_settings.ToggleAllMouseHotkey))
            _hotkeyService.RegisterHotkey(_settings.ToggleAllMouseHotkey, ToggleAllGroupsVisibility);

        // F11 hotkey removed - per-window KeyDown handler is more reliable
        // (Users press F11 while focused on the group they want to fullscreen)
        // _hotkeyService.RegisterHotkey(_settings.FullscreenToggleHotkey, ToggleFocusedGroupFullscreen);
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

            // Use debounced save for rapid-fire changes during resize
            if (e.PropertyName == nameof(GroupViewModel.Width) ||
                e.PropertyName == nameof(GroupViewModel.Height))
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

        var ungrouped = _desktopIconService.GetUngroupedShortcuts(_groupModels)
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
        _desktopIconService.GetUngroupedShortcuts(_groupModels);

    public SettingsViewModel CreateSettingsViewModel() =>
        new SettingsViewModel(_settings, _persistenceService, _startupService);

    public Task SaveSettingsAsync() =>
        _persistenceService.SaveSettingsImmediateAsync(_settings);

    public void ApplySettingsChanges(Window hotkeyWindow)
    {
        ThemeChanged?.Invoke(this, _settings.Theme);

        // DON'T re-register hotkeys here - requires app restart
        // Reason: If hotkeys fail to register, it triggers the warning dialog
        // which creates a loop when user is in Settings

        // _hotkeyService.UnregisterAll();
        // RegisterHotkeys(hotkeyWindow);

        // DISABLED: FileSystemWatcher causes infinite loop
        // if (_settings.WatchForUngroupedIcons) _ungroupedWatcher.StartWatching();
        // else _ungroupedWatcher.StopWatching();

        _persistenceService.SetDebounceInterval(_settings.AutoSaveDebounceMs);
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
