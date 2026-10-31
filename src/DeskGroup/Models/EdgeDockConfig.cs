using System;
using System.Collections.Generic;

namespace DeskGroup.Models;

public enum DockEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

public enum SideDockMonitorMode
{
    MainMonitorOnly = 0,
    SameForAllMonitors = 1,
    PerMonitorCustom = 2
}

public enum SideDockLockState
{
    AutoHide = 0,
    LockedOpen = 1,
    LockedClosed = 2
}

public class EdgeDockConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MonitorDeviceName { get; set; } = string.Empty; // Empty or "ALL" or "\\.\DISPLAY1"
    public DockEdge Edge { get; set; } = DockEdge.Left;
    public bool IsEnabled { get; set; } = false;
    public string Name { get; set; } = "Edge Dock";
    public string AccentColor { get; set; } = "#0078D4";
    public string CustomIconPath { get; set; } = string.Empty;
    public SideDockLockState LockState { get; set; } = SideDockLockState.AutoHide;
    public bool IsPinned { get; set; } = false; // When true, dock position/edge cannot be dragged
    public bool ShowOnEdgeHover { get; set; } = true;
    public bool ShowBubbleButton { get; set; } = true;
    public int BubbleSize { get; set; } = 16; // 16px x 16px default, user-selectable
    public double EdgePositionRatio { get; set; } = 0.5; // 0.0 to 1.0 along the edge for the dock panel
    public double BubblePositionRatio { get; set; } = -1.0; // -1 = follow EdgePositionRatio; 0.05..0.95 = independent bubble position
    public DockEdge? BubbleEdgeOverride { get; set; } = null; // Optional independent edge for the bubble
    public int IconColumns { get; set; } = 1; // Default 1 icon wide (snaps to 1, 2, 3, ... columns)
    public double PanelThickness { get; set; } = 100; // Snapped to IconColumns width/thickness
    public double PanelLength { get; set; } = 360;    // Height for Left/Right, Width for Top/Bottom
    public int PerDockIconSize { get; set; } = 0;     // 0 = use global setting
    public double PerDockOpacity { get; set; } = -1.0; // -1 = use global setting
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public List<OriginalIconState> OriginalIconStates { get; set; } = new();
}
