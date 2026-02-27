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
    public GroupDockMode DockMode { get; set; } = GroupDockMode.Float;
    public bool IsPinned { get; set; } = false;
    public bool SnapToGrid { get; set; } = true;
    public double PositionX { get; set; } = 100;
    public double PositionY { get; set; } = 100;
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 250;
    public string MonitorDeviceName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public enum GroupViewMode
{
    Minimized = 0,
    Hover = 1,
    Normal = 2,
    Fullscreen = 3
}

public enum GroupDockMode
{
    Sticky = 0,
    Float = 1
}