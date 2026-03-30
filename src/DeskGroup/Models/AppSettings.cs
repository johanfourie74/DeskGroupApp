using System;
using System.Collections.Generic;

namespace DeskGroup.Models;

public class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public WindowThemeStyle WindowStyle { get; set; } = WindowThemeStyle.Default;
    public double GroupBackgroundOpacity { get; set; } = 1.0;
    public GroupDockMode DefaultDockMode { get; set; } = GroupDockMode.Sticky;
    public bool DefaultSnapToGrid { get; set; } = false;
    public int SnapGridSize { get; set; } = 20;
    public bool HideGroupedIcons { get; set; } = true;
    public IconSizeMode IconSizeMode { get; set; } = IconSizeMode.System;
    public int CustomIconSize { get; set; } = 48;
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimizedToTray { get; set; } = false;
    public string ToggleAllHotkey { get; set; } = "Ctrl+F2";
    public string ToggleAllMouseHotkey { get; set; } = string.Empty;
    public HotkeyMode ToggleAllHotkeyMode { get; set; } = HotkeyMode.Keyboard;
    public string FullscreenToggleHotkey { get; set; } = "F11";
    public string FullscreenMouseHotkey { get; set; } = string.Empty;
    public HotkeyMode FullscreenHotkeyMode { get; set; } = HotkeyMode.Keyboard;
    public bool WatchForUngroupedIcons { get; set; } = true;
    public List<string> DismissedUngroupedIcons { get; set; } = new();
    public bool IsPortableMode { get; set; } = false;
    public bool AllowMultiMonitor { get; set; } = true;
    public bool IsFirstRun { get; set; } = true;
    public bool DesktopShortcutInstalled { get; set; } = false;
    public bool PinnedToStart { get; set; } = false;
    public bool PinnedToTaskbar { get; set; } = false;
    public string LastAppVersion { get; set; } = "1.0.0";
    public int AutoSaveDebounceMs { get; set; } = 2000;
    public bool ShowDebugMenu { get; set; } = false;

    // Auto-Update
    public bool AutoUpdate { get; set; } = false;
    public bool AutoInstall { get; set; } = false;
    public string SkippedUpdateVersion { get; set; } = string.Empty;
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;

    // Launch behavior
    public bool SingleClickToLaunch { get; set; } = false;

    // Shell integration
    public bool EnableShellContextMenu { get; set; } = false;

    // Bug reporting (explicit opt-in — nothing is sent automatically)
    public bool OptInBugReporting { get; set; } = false;

    // Settings Window Position
    public double SettingsWindowLeft { get; set; } = double.NaN;
    public double SettingsWindowTop { get; set; } = double.NaN;
    public double SettingsWindowWidth { get; set; } = 520;
    public double SettingsWindowHeight { get; set; } = 600;
}

public enum ThemeMode
{
    Light = 0,
    Dark = 1,
    System = 2
}

public enum WindowThemeStyle
{
    Default = 0,
    StandardLight = 1,
    StandardDark = 2,
    FollowSystem = 3
}

public enum IconSizeMode
{
    System = 0,
    Custom = 1
}