using System;
using System.IO;

namespace DeskGroup.Helpers;

public static class ShortcutInstallHelper
{
    public enum PinResult { Success, NotSupported, Failed }

    private static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DeskGroup.lnk");

    private static string StartMenuShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "DeskGroup.lnk");

    private static string ExePath =>
        Environment.ProcessPath
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskGroup.exe");

    public static bool IsDesktopShortcutInstalled() => File.Exists(ShortcutPath);

    public static bool IsStartMenuShortcutInstalled() => File.Exists(StartMenuShortcutPath);

    public static bool InstallDesktopShortcut() => CreateShortcutFile(ShortcutPath);

    public static bool RemoveDesktopShortcut()
    {
        try
        {
            if (File.Exists(ShortcutPath))
                File.Delete(ShortcutPath);
            return true;
        }
        catch { return false; }
    }

    public static PinResult PinToStartMenu()
    {
        try
        {
            // Ensure Start Menu Programs shortcut exists (works 100% reliably on both Windows 10 and Windows 11)
            bool startMenuCreated = CreateShortcutFile(StartMenuShortcutPath);

            // Also attempt shell verb if available (Windows 10)
            var verbTarget = File.Exists(ShortcutPath) ? ShortcutPath : StartMenuShortcutPath;
            var verbResult = PinUsingVerb(verbTarget, "Pin to Start");

            if (verbResult == PinResult.Success || startMenuCreated)
                return PinResult.Success;

            return PinResult.NotSupported;
        }
        catch
        {
            return PinResult.Failed;
        }
    }

    public static bool RemoveStartMenuShortcut()
    {
        try
        {
            if (File.Exists(StartMenuShortcutPath))
                File.Delete(StartMenuShortcutPath);
            return true;
        }
        catch { return false; }
    }

    public static PinResult PinToTaskbar()
    {
        // Ensure a .lnk file exists to invoke the verb on
        string targetLnk = File.Exists(ShortcutPath)
            ? ShortcutPath
            : (CreateShortcutFile(StartMenuShortcutPath) ? StartMenuShortcutPath : ShortcutPath);
        return PinUsingVerb(targetLnk, "Pin to taskbar");
    }

    private static bool CreateShortcutFile(string linkPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return false;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = ExePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(ExePath) ?? "";
            shortcut.Description = "DeskGroup - Desktop Icon Organizer";
            shortcut.IconLocation = $"{ExePath},0";
            shortcut.Save();
            return File.Exists(linkPath);
        }
        catch { return false; }
    }

    private static PinResult PinUsingVerb(string filePath, string verb)
    {
        if (!File.Exists(filePath)) return PinResult.Failed;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return PinResult.NotSupported;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace(Path.GetDirectoryName(filePath)!);
            dynamic? item = folder?.ParseName(Path.GetFileName(filePath));
            dynamic? verbs = item?.Verbs();
            if (verbs == null) return PinResult.NotSupported;
            for (int i = 0; i < verbs.Count; i++)
            {
                dynamic v = verbs.Item(i);
                string? name = v?.Name;
                if (!string.IsNullOrEmpty(name) &&
                    name.Replace("&", "").Equals(verb, StringComparison.OrdinalIgnoreCase))
                {
                    v!.DoIt();
                    return PinResult.Success;
                }
            }
            return PinResult.NotSupported;
        }
        catch { return PinResult.Failed; }
    }
}
