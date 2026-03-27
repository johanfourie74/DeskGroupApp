using System;
using Microsoft.Win32;

namespace DeskGroup.Helpers;

/// <summary>
/// Manages the "Add to DeskGroup" Windows shell context menu entry for .lnk and .url files.
/// Uses HKCU so no admin rights are required.
/// </summary>
public static class ShellContextMenuHelper
{
    private const string MenuKeyName = "AddToDeskGroup";
    private const string MenuLabel   = "Add to DeskGroup";

    // File-type classes to hook into
    private static readonly string[] TargetClasses = { "lnkfile", "InternetShortcut" };

    /// <summary>Registers (or refreshes) the context menu entries pointing to the current exe.</summary>
    public static void RegisterContextMenu()
    {
        var exePath = Environment.ProcessPath
            ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskGroup.exe");

        foreach (var cls in TargetClasses)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(
                    $@"Software\Classes\{cls}\shell\{MenuKeyName}");
                key.SetValue("", MenuLabel);
                key.SetValue("Icon", $"\"{exePath}\",0");

                using var cmd = key.CreateSubKey("command");
                cmd.SetValue("", $"\"{exePath}\" --add-to-group \"%1\"");
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Removes the context menu entries from the registry.</summary>
    public static void UnregisterContextMenu()
    {
        foreach (var cls in TargetClasses)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    $@"Software\Classes\{cls}\shell\{MenuKeyName}",
                    throwOnMissingSubKey: false);
            }
            catch { }
        }
    }
}
