using System;
using System.Windows;
using DeskGroup.Services;

namespace DeskGroup.Helpers;

public class WindowPositionHelper
{
    private readonly MonitorService _monitorService;

    public WindowPositionHelper(MonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public Point SnapToGrid(Point position, int gridSize)
    {
        double x = Math.Round(position.X / gridSize) * gridSize;
        double y = Math.Round(position.Y / gridSize) * gridSize;
        return new Point(x, y);
    }

    public Rect ClampToScreen(Rect widgetRect)
    {
        var monitor = _monitorService.GetMonitorFromPoint(
            widgetRect.X + widgetRect.Width / 2,
            widgetRect.Y + widgetRect.Height / 2);

        if (monitor == null)
            monitor = _monitorService.GetPrimaryMonitor();

        if (monitor == null) return widgetRect;

        return _monitorService.ClampToWorkingArea(widgetRect, monitor);
    }

    public Rect FinalizePosition(Rect widgetRect, bool snapEnabled, int gridSize)
    {
        var position = new Point(widgetRect.X, widgetRect.Y);
        if (snapEnabled)
            position = SnapToGrid(position, gridSize);

        var snapped = new Rect(position.X, position.Y, widgetRect.Width, widgetRect.Height);
        return ClampToScreen(snapped);
    }

    public Rect GetFullscreenRect(Point widgetCenter)
    {
        var monitor = _monitorService.GetMonitorFromPoint(widgetCenter.X, widgetCenter.Y)
                      ?? _monitorService.GetPrimaryMonitor();

        if (monitor == null)
            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        return monitor.WorkingArea;
    }
}