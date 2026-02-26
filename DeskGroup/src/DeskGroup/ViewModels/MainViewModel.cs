using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public event EventHandler<ThemeMode>? ThemeChanged;
    public event EventHandler<List<string>>? UngroupedIconsDetected;
    public event EventHandler<GroupViewModel>? GroupDockModeChanged;
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

        if (_settings.WatchForUngroupedIcons) _ungroupedWatcher.StartWatching();
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

    private GroupViewModel CreateGroupViewModel(IconGroup model) =>
                new GroupViewModel(model, _usageTracker, _iconExtractor,
            _desktopIconService, _persistenceService, _positionHelper);

    [RelayCommand]
    private async void CreateGroup()
    {
        var model = new IconGroup
        {
            Name = $"Group {_groupModels.Count + 1}",
            DockMode = _settings.DefaultDockMode,
            SnapToGrid = _settings.DefaultSnapToGrid,
            SortOrder = _groupModels.Count
        };
        _groupModels.Add(model);
        var vm = CreateGroupViewModel(model);
        Groups.Add(vm);
        GroupAdded?.Invoke(this, vm);
        await _persistenceService.SaveGroupsImmediateAsync(_groupModels);
    }

    [RelayCommand]
    private async void DeleteGroup(GroupViewModel groupVm)
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
        var undismissed = newFiles
            .Where(f =>
            {
                var fileName = System.IO.Path.GetFileName(f);
                return !_settings.DismissedUngroupedIcons.Contains(fileName, StringComparer.OrdinalIgnoreCase);
            }).ToList();

        if (undismissed.Count == 0) return;

        var ungrouped = _desktopIconService.GetUngroupedShortcuts(_groupModels)
            .Intersect(undismissed, StringComparer.OrdinalIgnoreCase).ToList();

        if (ungrouped.Count > 0)
            Application.Current?.Dispatcher.Invoke(() => UngroupedIconsDetected?.Invoke(this, ungrouped));
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
        if (_settings.WatchForUngroupedIcons) _ungroupedWatcher.StartWatching();
        else _ungroupedWatcher.StopWatching();
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

    [RelayCommand] private void ShowAllGroups()
    {
        AllGroupsVisible = true;
        foreach (var group in Groups) group.IsVisible = true;
    }

    [RelayCommand] private void HideAllGroups()
    {
        AllGroupsVisible = false;
        foreach (var group in Groups) group.IsVisible = false;
    }

    [RelayCommand] private void ExitApplication() => Application.Current?.Shutdown();
}