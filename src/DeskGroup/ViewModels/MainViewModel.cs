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

    [ObservableProperty] private bool _allGroupsVisible = true;
    [ObservableProperty] private bool _isLoaded;

    public ObservableCollection<GroupViewModel> Groups { get; } = new();
    public AppSettings Settings => _settings;

    public event EventHandler<DeskGroup.Models.ThemeMode>? ThemeChanged;
    public event EventHandler<List<string>>? UngroupedIconsDetected;
    public event EventHandler<GroupViewModel>? GroupAdded;
    public event EventHandler<GroupViewModel>? GroupRemoved;

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

        if (_settings.IsFirstRun)
        {
            _settings.IsFirstRun = false;
            await _persistenceService.SaveSettingsImmediateAsync(_settings);
        }
        IsLoaded = true;
    }

    public void RegisterHotkeys(Window window)
    {
        _hotkeyService.Initialize(window);
        _hotkeyService.RegisterHotkey(_settings.ToggleAllHotkey, ToggleAllGroupsVisibility);
        _hotkeyService.RegisterHotkey(_settings.FullscreenToggleHotkey, ToggleFocusedGroupFullscreen);
        _hotkeyService.RegistrationFailed += (s, msg) =>
            System.Diagnostics.Debug.WriteLine($"Hotkey registration failed: {msg}");
    }

    private GroupViewModel CreateGroupViewModel(IconGroup model)
    {
        var vm = new GroupViewModel(model, _usageTracker, _iconExtractor,
            _desktopIconService, _persistenceService, _positionHelper);

        // Subscribe to property changes to trigger auto-save
        vm.PropertyChanged += async (s, e) =>
        {
            // Save whenever any important property changes (but not during drag or visibility toggles)
            if (e.PropertyName != nameof(GroupViewModel.IsVisible) && 
                e.PropertyName != nameof(GroupViewModel.IsDragging))
            {
                System.Diagnostics.Debug.WriteLine($"MainViewModel: GroupVM.{e.PropertyName} changed, triggering save");
                await _persistenceService.SaveGroupsImmediateAsync(_groupModels);
            }
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
                ViewMode = GroupViewMode.Minimized // Start minimized for better visibility
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
        AllGroupsVisible = !AllGroupsVisible;
        foreach (var group in Groups) group.IsVisible = AllGroupsVisible;
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

    public void ApplySettingsChanges(Window hotkeyWindow)
    {
        ThemeChanged?.Invoke(this, _settings.Theme);
        _hotkeyService.UnregisterAll();
        RegisterHotkeys(hotkeyWindow);

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
            group.Model.Height = group.Height; group.Model.IsVisible = group.IsVisible;
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
        try
        {
            System.Diagnostics.Debug.WriteLine($"ShowAllGroups: START - showing {Groups.Count} groups");

            AllGroupsVisible = true;

            int count = 0;
            foreach (var group in Groups)
            {
                count++;
                System.Diagnostics.Debug.WriteLine($"  Showing group {count}: {group.Name}");
                group.IsVisible = true;
            }

            System.Diagnostics.Debug.WriteLine($"ShowAllGroups: COMPLETE");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowAllGroups: FAILED - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"  Stack: {ex.StackTrace}");

            MessageBox.Show(
                $"Failed to show groups:\n\n{ex.Message}",
                "Show Groups Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand] 
    private void HideAllGroups()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"HideAllGroups: START - hiding {Groups.Count} groups");

            AllGroupsVisible = false;

            int count = 0;
            foreach (var group in Groups)
            {
                count++;
                System.Diagnostics.Debug.WriteLine($"  Hiding group {count}: {group.Name}");
                group.IsVisible = false;
            }

            System.Diagnostics.Debug.WriteLine($"HideAllGroups: COMPLETE");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HideAllGroups: FAILED - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"  Stack: {ex.StackTrace}");

            MessageBox.Show(
                $"Failed to hide groups:\n\n{ex.Message}",
                "Hide Groups Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand] private void ExitApplication() => Application.Current?.Shutdown();
}
