using System;
using System.Collections.Generic;

namespace DeskGroup.Models;

public class IconGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Group";
    public string CustomIconPath { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#0078D4";
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public List<OriginalIconState> OriginalIconStates { get; set; } = new();
    public GroupViewMode ViewMode { get; set; } = GroupViewMode.Normal;
    public GroupDockMode DockMode { get; set; } = GroupDockMode.Sticky;
    public bool IsPinned { get; set; } = false;
    public bool SnapToGrid { get; set; } = false;
    public double PositionX { get; set; } = 100;
    public double PositionY { get; set; } = 100;
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 250;
    public double GridViewWidth { get; set; } = 400;
    public double GridViewHeight { get; set; } = 300;
    public string MonitorDeviceName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public int PerGroupIconSize { get; set; } = 0;       // 0 = use global setting
    public double PerGroupOpacity { get; set; } = -1.0;  // -1 = use global setting
}

public enum GroupViewMode
{
    Minimized = 0,
    Hover = 1,
    Normal = 2,      // Keep original values!
    Grid = 3,
    Fullscreen = 4,
    Compact = 5      // Add new mode at end to avoid breaking saved data
}

public enum GroupDockMode
{
    Sticky = 0,
    Float = 1
}