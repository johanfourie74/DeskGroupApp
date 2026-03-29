using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace DeskGroup.Services;

public class MonitorService
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
        MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType,
        out uint dpiX, out uint dpiY);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor,
        ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int Size;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    public List<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                var info = new MONITORINFOEX();
                info.Size = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (GetMonitorInfo(hMonitor, ref info))
                {
                    uint dpiX = 96, dpiY = 96;
                    try { GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY); } catch { }
                    monitors.Add(new MonitorInfo
                    {
                        Handle = hMonitor,
                        DeviceName = info.DeviceName,
                        IsPrimary = (info.Flags & 1) != 0,
                        Bounds = new Rect(info.Monitor.Left, info.Monitor.Top,
                            info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top),
                        WorkingArea = new Rect(info.WorkArea.Left, info.WorkArea.Top,
                            info.WorkArea.Right - info.WorkArea.Left, info.WorkArea.Bottom - info.WorkArea.Top),
                        DpiX = dpiX, DpiY = dpiY, ScaleFactor = dpiX / 96.0
                    });
                }
                return true;
            }, IntPtr.Zero);
        return monitors;
    }

    public MonitorInfo? GetPrimaryMonitor() =>
        GetAllMonitors().FirstOrDefault(m => m.IsPrimary);

    public MonitorInfo? GetMonitorFromPoint(double x, double y) =>
        GetAllMonitors().FirstOrDefault(m => m.Bounds.Contains(x, y));

    public MonitorInfo? GetMonitorByName(string deviceName) =>
        GetAllMonitors().FirstOrDefault(m => m.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));

    public Rect ClampToWorkingArea(Rect rect, MonitorInfo monitor)
    {
        double x = rect.X, y = rect.Y;
        double w = Math.Min(rect.Width, monitor.WorkingArea.Width);
        double h = Math.Min(rect.Height, monitor.WorkingArea.Height);
        if (x < monitor.WorkingArea.Left) x = monitor.WorkingArea.Left;
        if (y < monitor.WorkingArea.Top) y = monitor.WorkingArea.Top;
        if (x + w > monitor.WorkingArea.Right) x = monitor.WorkingArea.Right - w;
        if (y + h > monitor.WorkingArea.Bottom) y = monitor.WorkingArea.Bottom - h;
        return new Rect(x, y, w, h);
    }
}

public class MonitorInfo
{
    public IntPtr Handle { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Rect Bounds { get; set; }
    public Rect WorkingArea { get; set; }
    public uint DpiX { get; set; } = 96;
    public uint DpiY { get; set; } = 96;
    public double ScaleFactor { get; set; } = 1.0;
}