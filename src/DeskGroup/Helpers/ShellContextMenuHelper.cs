using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeskGroup.Helpers;

/// <summary>
/// Manages the "Add to DeskGroup" Windows shell context menu entry for .lnk, .url, .exe, and folder items.
/// Uses HKCU so no admin rights are required.
/// </summary>
public static class ShellContextMenuHelper
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int  SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST       = 0x0000;
    private const uint SHCNF_FLUSHNOWAIT  = 0x2000;

    private const string MenuKeyName = "AddToDeskGroup";
    private const string MenuLabel   = "Add to DeskGroup";

    // ProgID-based classes (work on Windows 10 and in Win11 context menu)
    private static readonly string[] ProgIdClasses =
        { "lnkfile", "InternetShortcut", "exefile", "Directory" };

    // SystemFileAssociations paths — reliable on Windows 11 context menu
    private static readonly string[] SysFileAssocPaths =
        { @"SystemFileAssociations\.lnk", @"SystemFileAssociations\.url", @"SystemFileAssociations\.exe" };

    /// <summary>Registers (or refreshes) the context menu entries pointing to the current exe.</summary>
    public static void RegisterContextMenu()
    {
        var exePath = Environment.ProcessPath
            ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskGroup.exe");

        foreach (var cls in ProgIdClasses)
            WriteEntry(cls, exePath);

        foreach (var path in SysFileAssocPaths)
            WriteEntry(path, exePath);

        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSHNOWAIT, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    /// <summary>Removes the context menu entries from the registry.</summary>
    public static void UnregisterContextMenu()
    {
        foreach (var cls in ProgIdClasses)
            DeleteEntry(cls);

        foreach (var path in SysFileAssocPaths)
            DeleteEntry(path);

        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSHNOWAIT, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    private static void WriteEntry(string classPath, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{classPath}\shell\{MenuKeyName}");
            key.SetValue("", MenuLabel);
            key.SetValue("Icon", $"\"{exePath}\",0");

            using var cmd = key.CreateSubKey("command");
            cmd.SetValue("", $"\"{exePath}\" --add-to-group \"%1\"");
        }
        catch { /* best effort */ }
    }

    private static void DeleteEntry(string classPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\{classPath}\shell\{MenuKeyName}",
                throwOnMissingSubKey: false);
        }
        catch { }
    }
}
