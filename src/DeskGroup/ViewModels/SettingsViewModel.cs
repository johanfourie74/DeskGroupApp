using System.Collections.Generic;
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

    [ObservableProperty] private ThemeMode _theme;
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

    public List<ThemeMode> ThemeModes { get; } = new() { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System };
    public List<GroupDockMode> DockModes { get; } = new() { GroupDockMode.Sticky, GroupDockMode.Float };
    public List<IconSizeMode> IconSizeModes { get; } = new() { IconSizeMode.System, IconSizeMode.Custom };

    public SettingsViewModel(AppSettings settings, PersistenceService persistenceService, StartupService startupService)
    {
        _settings = settings; _persistenceService = persistenceService; _startupService = startupService;
        Theme = settings.Theme; DefaultDockMode = settings.DefaultDockMode;
        DefaultSnapToGrid = settings.DefaultSnapToGrid; SnapGridSize = settings.SnapGridSize;
        HideGroupedIcons = settings.HideGroupedIcons; IconSizeMode = settings.IconSizeMode;
        CustomIconSize = settings.CustomIconSize; StartWithWindows = settings.StartWithWindows;
        StartMinimizedToTray = settings.StartMinimizedToTray; ToggleAllHotkey = settings.ToggleAllHotkey;
        FullscreenToggleHotkey = settings.FullscreenToggleHotkey;
        WatchForUngroupedIcons = settings.WatchForUngroupedIcons;
        AllowMultiMonitor = settings.AllowMultiMonitor;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Apply()
    {
        _settings.Theme = Theme; _settings.DefaultDockMode = DefaultDockMode;
        _settings.DefaultSnapToGrid = DefaultSnapToGrid; _settings.SnapGridSize = SnapGridSize;
        _settings.HideGroupedIcons = HideGroupedIcons; _settings.IconSizeMode = IconSizeMode;
        _settings.CustomIconSize = CustomIconSize; _settings.StartWithWindows = StartWithWindows;
        _settings.StartMinimizedToTray = StartMinimizedToTray; _settings.ToggleAllHotkey = ToggleAllHotkey;
        _settings.FullscreenToggleHotkey = FullscreenToggleHotkey;
        _settings.WatchForUngroupedIcons = WatchForUngroupedIcons;
        _settings.AllowMultiMonitor = AllowMultiMonitor;
        _startupService.SyncWithSetting(StartWithWindows);
        await _persistenceService.SaveSettingsImmediateAsync(_settings);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var d = new AppSettings();
        Theme = d.Theme; DefaultDockMode = d.DefaultDockMode; DefaultSnapToGrid = d.DefaultSnapToGrid;
        SnapGridSize = d.SnapGridSize; HideGroupedIcons = d.HideGroupedIcons; IconSizeMode = d.IconSizeMode;
        CustomIconSize = d.CustomIconSize; StartWithWindows = d.StartWithWindows;
        StartMinimizedToTray = d.StartMinimizedToTray; ToggleAllHotkey = d.ToggleAllHotkey;
        FullscreenToggleHotkey = d.FullscreenToggleHotkey; WatchForUngroupedIcons = d.WatchForUngroupedIcons;
        AllowMultiMonitor = d.AllowMultiMonitor;
    }
}