using System;

namespace DeskGroup.Models;

public class OriginalIconState
{
    public Guid ShortcutItemId { get; set; }
    public string OriginalFilePath { get; set; } = string.Empty;
    public double OriginalX { get; set; }
    public double OriginalY { get; set; }
    public string MonitorDeviceName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public bool WasMoved { get; set; } = false;
    public string StoredFilePath { get; set; } = string.Empty;
}