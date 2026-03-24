using System;
using System.IO;

namespace DeskGroup.Helpers;

public static class ShortcutInstallHelper
{
    public enum PinResult { Success, NotSupported, Failed }

    private static string ShortcutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DeskGroup.lnk");

    private static string ExePath =>
        Environment.ProcessPath
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskGroup.exe");

    public static bool IsDesktopShortcutInstalled() => File.Exists(ShortcutPath);

    public static bool InstallDesktopShortcut()
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return false;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = ExePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(ExePath) ?? "";
            shortcut.Description = "DeskGroup - Desktop Icon Organizer";
            shortcut.Save();
            return File.Exists(ShortcutPath);
        }
        catch { return false; }
    }

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

    public static PinResult PinToStartMenu() => PinUsingVerb(ShortcutPath, "Pin to Start");

    public static PinResult PinToTaskbar() => PinUsingVerb(ShortcutPath, "Pin to taskbar");

    private static PinResult PinUsingVerb(string filePath, string verb)
    {
        if (!File.Exists(filePath)) return PinResult.Failed;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return PinResult.NotSupported;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace(Path.GetDirectoryName(filePath)!);
            dynamic item = folder.ParseName(Path.GetFileName(filePath));
            dynamic? verbs = item?.Verbs();
            if (verbs == null) return PinResult.NotSupported;
            for (int i = 0; i < verbs.Count; i++)
            {
                dynamic v = verbs.Item(i);
                if (v.Name.Replace("&", "").Equals(verb, StringComparison.OrdinalIgnoreCase))
                {
                    v.DoIt();
                    return PinResult.Success;
                }
            }
            return PinResult.NotSupported;
        }
        catch { return PinResult.Failed; }
    }
}
