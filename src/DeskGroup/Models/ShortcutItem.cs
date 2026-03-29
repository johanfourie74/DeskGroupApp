using System;
using System.Text.Json.Serialization;

namespace DeskGroup.Models;

public class ShortcutItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string OriginalShortcutPath { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public int IconIndex { get; set; } = 0;
    public int LaunchCount { get; set; } = 0;
    public DateTime? LastLaunched { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; } = 0;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool OriginalIconHidden { get; set; } = false;
    // Path where the .lnk/.url was stored when HideGroupedIcons moved it off the desktop.
    // Empty when HideGroupedIcons=false (file stays on desktop).
    public string StoredFilePath { get; set; } = string.Empty;
}