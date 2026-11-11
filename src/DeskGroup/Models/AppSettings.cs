using System;
using System.Collections.Generic;
using System.Linq;

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
    public string ToggleEdgeDockHotkey { get; set; } = "Ctrl+Alt+Left";
    public string ToggleEdgeDockMouseHotkey { get; set; } = string.Empty;
    public HotkeyMode ToggleEdgeDockHotkeyMode { get; set; } = HotkeyMode.Keyboard;
    public bool EnableEdgeDockArrowHotkeys { get; set; } = true;
    public bool WatchForUngroupedIcons { get; set; } = true;
    public List<string> DismissedUngroupedIcons { get; set; } = new();
    public bool IsPortableMode { get; set; } = false;
    public bool AllowMultiMonitor { get; set; } = true;
    public bool IsFirstRun { get; set; } = true;
    public bool HasSeenSetupWizard { get; set; } = false;
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

    // Optional Side Panel / Edge Docker
    public bool EnableEdgeDock { get; set; } = false;
    public SideDockMonitorMode EdgeDockMonitorMode { get; set; } = SideDockMonitorMode.MainMonitorOnly;
    public int DefaultEdgeDockBubbleSize { get; set; } = 16;
    public int DefaultEdgeDockIconSize { get; set; } = 0; // 0 = follow global icon size; 16-96 = custom px
    public bool DefaultEdgeDockShowBubble { get; set; } = true;
    public bool DefaultEdgeDockHoverShow { get; set; } = true;
    public List<EdgeDockConfig> EdgeDocks { get; set; } = new();

    // Settings Window Position
    public double SettingsWindowLeft { get; set; } = double.NaN;
    public double SettingsWindowTop { get; set; } = double.NaN;
    public double SettingsWindowWidth { get; set; } = 520;
    public double SettingsWindowHeight { get; set; } = 600;

    /// <summary>
    /// Consolidates and self-heals EdgeDock configs so that Windows monitor re-enumeration
    /// (e.g., \\.\DISPLAY1 swapping with \\.\DISPLAY2 after a power failure or monitor wake)
    /// never strands populated panels on an old device name or leaves empty duplicate panels.
    /// </summary>
    public bool ConsolidateEdgeDocksForPrimaryMonitor(string? primaryDeviceName)
    {
        EdgeDocks ??= new List<EdgeDockConfig>();
        if (EdgeDocks.Count == 0)
            return false;

        bool changed = false;

        if (EdgeDockMonitorMode != SideDockMonitorMode.PerMonitorCustom)
        {
            var consolidated = new List<EdgeDockConfig>();
            foreach (var edgeGroup in EdgeDocks.GroupBy(c => c.Edge))
            {
                var ordered = edgeGroup
                    .OrderByDescending(c => c.Shortcuts?.Count ?? 0)
                    .ThenByDescending(c => c.IsEnabled)
                    .ThenByDescending(c => !string.IsNullOrEmpty(primaryDeviceName) &&
                                           string.Equals(c.MonitorDeviceName, primaryDeviceName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var winner = ordered[0];
                winner.Shortcuts ??= new List<ShortcutItem>();
                winner.OriginalIconStates ??= new List<OriginalIconState>();

                for (int i = 1; i < ordered.Count; i++)
                {
                    var dup = ordered[i];
                    changed = true;
                    if (dup.IsEnabled && !winner.IsEnabled)
                        winner.IsEnabled = true;

                    if (dup.Shortcuts != null)
                    {
                        foreach (var sc in dup.Shortcuts)
                        {
                            bool exists = winner.Shortcuts.Any(w =>
                                w.Id == sc.Id ||
                                (string.Equals(w.TargetPath, sc.TargetPath, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(w.Arguments, sc.Arguments, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(w.DisplayName, sc.DisplayName, StringComparison.OrdinalIgnoreCase)));
                            if (!exists)
                            {
                                sc.SortOrder = winner.Shortcuts.Count;
                                winner.Shortcuts.Add(sc);
                            }
                        }
                    }

                    if (dup.OriginalIconStates != null)
                    {
                        foreach (var st in dup.OriginalIconStates)
                        {
                            if (winner.OriginalIconStates.All(w => w.ShortcutItemId != st.ShortcutItemId))
                                winner.OriginalIconStates.Add(st);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(primaryDeviceName) &&
                    !string.Equals(winner.MonitorDeviceName, primaryDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    winner.MonitorDeviceName = primaryDeviceName;
                    changed = true;
                }

                consolidated.Add(winner);
            }

            if (changed || consolidated.Count != EdgeDocks.Count)
            {
                EdgeDocks.Clear();
                EdgeDocks.AddRange(consolidated);
                return true;
            }

            return false;
        }
        else
        {
            // PerMonitorCustom: deduplicate any identical (MonitorDeviceName, Edge) entries
            var consolidated = new List<EdgeDockConfig>();
            foreach (var group in EdgeDocks.GroupBy(c => (
                Device: (string.IsNullOrEmpty(c.MonitorDeviceName) && !string.IsNullOrEmpty(primaryDeviceName)
                    ? primaryDeviceName
                    : c.MonitorDeviceName ?? string.Empty).ToUpperInvariant(),
                c.Edge)))
            {
                var ordered = group
                    .OrderByDescending(c => c.Shortcuts?.Count ?? 0)
                    .ThenByDescending(c => c.IsEnabled)
                    .ToList();

                var winner = ordered[0];
                winner.Shortcuts ??= new List<ShortcutItem>();
                winner.OriginalIconStates ??= new List<OriginalIconState>();

                if (string.IsNullOrEmpty(winner.MonitorDeviceName) && !string.IsNullOrEmpty(primaryDeviceName))
                {
                    winner.MonitorDeviceName = primaryDeviceName;
                    changed = true;
                }

                for (int i = 1; i < ordered.Count; i++)
                {
                    var dup = ordered[i];
                    changed = true;
                    if (dup.IsEnabled && !winner.IsEnabled)
                        winner.IsEnabled = true;

                    if (dup.Shortcuts != null)
                    {
                        foreach (var sc in dup.Shortcuts)
                        {
                            if (winner.Shortcuts.All(w => w.Id != sc.Id))
                            {
                                sc.SortOrder = winner.Shortcuts.Count;
                                winner.Shortcuts.Add(sc);
                            }
                        }
                    }

                    if (dup.OriginalIconStates != null)
                    {
                        foreach (var st in dup.OriginalIconStates)
                        {
                            if (winner.OriginalIconStates.All(w => w.ShortcutItemId != st.ShortcutItemId))
                                winner.OriginalIconStates.Add(st);
                        }
                    }
                }

                consolidated.Add(winner);
            }

            if (changed || consolidated.Count != EdgeDocks.Count)
            {
                EdgeDocks.Clear();
                EdgeDocks.AddRange(consolidated);
                return true;
            }

            return false;
        }
    }
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
