using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DeskGroup.Models;

namespace DeskGroup.Services;

public class DesktopIconService
{
    private readonly string _desktopPath;
    private readonly string _publicDesktopPath;
    private readonly string _storagePath;

    public DesktopIconService(string appDataDirectory)
    {
        _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        _publicDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        _storagePath = Path.Combine(appDataDirectory, "HiddenIcons");
        Directory.CreateDirectory(_storagePath);
    }

    public List<string> GetAllDesktopShortcuts()
    {
        var shortcuts = new List<string>();
        var extensions = new[] { "*.lnk", "*.url" };
        foreach (var ext in extensions)
        {
            try { shortcuts.AddRange(Directory.GetFiles(_desktopPath, ext, SearchOption.TopDirectoryOnly)); } catch { }
            try { shortcuts.AddRange(Directory.GetFiles(_publicDesktopPath, ext, SearchOption.TopDirectoryOnly)); } catch { }
        }
        try { shortcuts.AddRange(Directory.GetFiles(_desktopPath, "*.exe", SearchOption.TopDirectoryOnly)); } catch { }
        return shortcuts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public List<string> GetUngroupedShortcuts(List<IconGroup> existingGroups)
    {
        var allDesktop = GetAllDesktopShortcuts();
        var groupedPaths = existingGroups
            .SelectMany(g => g.Shortcuts)
            .Select(s => s.OriginalShortcutPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return allDesktop.Where(path => !groupedPaths.Contains(path)).ToList();
    }

    public OriginalIconState? HideDesktopIcon(ShortcutItem shortcut)
    {
        try
        {
            var originalPath = shortcut.OriginalShortcutPath;
            if (string.IsNullOrEmpty(originalPath) || !File.Exists(originalPath)) return null;
            if (originalPath.StartsWith(_publicDesktopPath, StringComparison.OrdinalIgnoreCase)) return null;

            var fileName = Path.GetFileName(originalPath);
            var storedPath = Path.Combine(_storagePath, $"{shortcut.Id}_{fileName}");
            if (File.Exists(storedPath))
                storedPath = Path.Combine(_storagePath, $"{shortcut.Id}_{Guid.NewGuid():N}_{fileName}");

            File.Move(originalPath, storedPath);
            shortcut.StoredFilePath = storedPath;
            shortcut.OriginalIconHidden = true;
            NotifyShellDeleted(originalPath);
            NotifyShellUpdateDir(_desktopPath);

            return new OriginalIconState
            {
                ShortcutItemId = shortcut.Id,
                OriginalFilePath = originalPath,
                OriginalX = 0, OriginalY = 0,
                CapturedAt = DateTime.UtcNow,
                WasMoved = true,
                StoredFilePath = storedPath
            };
        }
        catch { return null; }
    }

    public bool RestoreDesktopIcon(OriginalIconState state)
    {
        try
        {
            if (!state.WasMoved || string.IsNullOrEmpty(state.StoredFilePath)) return false;
            if (!File.Exists(state.StoredFilePath)) return false;

            var dir = Path.GetDirectoryName(state.OriginalFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(state.OriginalFilePath))
            {
                File.Delete(state.StoredFilePath);
                return true;
            }
            File.Move(state.StoredFilePath, state.OriginalFilePath);
            NotifyShellCreated(state.OriginalFilePath);
            NotifyShellUpdateDir(_desktopPath);
            return true;
        }
        catch { return false; }
    }

    public void RestoreAllGroupIcons(IconGroup group)
    {
        foreach (var state in group.OriginalIconStates)
            RestoreDesktopIcon(state);
    }

    public void CleanupOrphans(List<IconGroup> allGroups)
    {
        try
        {
            var allStoredPaths = allGroups
                .SelectMany(g => g.OriginalIconStates)
                .Select(s => s.StoredFilePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(_storagePath))
            {
                if (!allStoredPaths.Contains(file))
                {
                    try
                    {
                        var desktopPath = Path.Combine(_desktopPath, ExtractOriginalFileName(file));
                        if (!File.Exists(desktopPath)) { File.Move(file, desktopPath); NotifyShellCreated(desktopPath); NotifyShellUpdateDir(_desktopPath); }
                        else File.Delete(file);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static string ExtractOriginalFileName(string storedPath)
    {
        var fileName = Path.GetFileName(storedPath);
        var underscoreIndex = fileName.IndexOf('_');
        if (underscoreIndex > 0 && underscoreIndex < fileName.Length - 1)
            return fileName[(underscoreIndex + 1)..];
        return fileName;
    }

    // Shell notification — forces Windows Explorer to refresh immediately (no F5 needed)
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_CREATE    = 0x00000002;
    private const int SHCNE_DELETE    = 0x00000004;
    private const int SHCNE_UPDATEDIR = 0x00001000;
    private const uint SHCNF_PATH     = 0x0005;
    private const uint SHCNF_FLUSH    = 0x1000;

    private static void NotifyShellDeleted(string path)
    {
        try
        {
            var ptr = Marshal.StringToCoTaskMemUni(path);
            SHChangeNotify(SHCNE_DELETE, SHCNF_PATH | SHCNF_FLUSH, ptr, IntPtr.Zero);
            Marshal.FreeCoTaskMem(ptr);
        }
        catch { }
    }

    private static void NotifyShellCreated(string path)
    {
        try
        {
            var ptr = Marshal.StringToCoTaskMemUni(path);
            SHChangeNotify(SHCNE_CREATE, SHCNF_PATH | SHCNF_FLUSH, ptr, IntPtr.Zero);
            Marshal.FreeCoTaskMem(ptr);
        }
        catch { }
    }

    // Signals Explorer to refresh the folder view — Win 11 doesn't always repaint on
    // per-file SHCNE_CREATE/DELETE alone; a SHCNE_UPDATEDIR on the parent folder fixes it.
    private static void NotifyShellUpdateDir(string folderPath)
    {
        try
        {
            var ptr = Marshal.StringToCoTaskMemUni(folderPath);
            SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATH | SHCNF_FLUSH, ptr, IntPtr.Zero);
            Marshal.FreeCoTaskMem(ptr);
        }
        catch { }
    }
}