using System;
using System.IO;
using System.Threading.Tasks;
using DeskGroup.Services;

namespace DeskGroup.Helpers;

/// <summary>
/// Called by the Inno Setup uninstaller via <c>--uninstall-cleanup</c> before files are deleted.
/// Restores hidden desktop icons, removes shell/registry integrations, and deletes user data.
/// </summary>
public static class UninstallHelper
{
    public static async Task RunAsync()
    {
        try
        {
            var persistence = new PersistenceService();
            var settings    = await persistence.LoadSettingsAsync();
            var groups      = await persistence.LoadGroupsAsync();

            // Restore all desktop icons that were hidden by DeskGroup
            if (settings.HideGroupedIcons)
            {
                var desktopService = new DesktopIconService(persistence.DataDirectory);
                foreach (var group in groups)
                    desktopService.RestoreAllGroupIcons(group);
            }

            // Remove user data directory (%LOCALAPPDATA%\DeskGroup)
            try
            {
                if (Directory.Exists(persistence.DataDirectory))
                    Directory.Delete(persistence.DataDirectory, recursive: true);
            }
            catch { /* best effort */ }
        }
        catch { /* best effort — uninstall must proceed regardless */ }

        // Remove shell context menu registry entries
        ShellContextMenuHelper.UnregisterContextMenu();

        // Remove startup registry entry
        try { new StartupService().SyncWithSetting(false); } catch { }
    }
}
