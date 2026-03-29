using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DeskGroup.Models;

namespace DeskGroup.Services;

public class ShortcutResolverService
{
    private readonly IconExtractorService _iconExtractor;

    public ShortcutResolverService(IconExtractorService iconExtractor)
    {
        _iconExtractor = iconExtractor;
    }

    public ShortcutItem Resolve(string filePath)
    {
        var item = new ShortcutItem
        {
            OriginalShortcutPath = filePath,
            DateAdded = DateTime.UtcNow
        };

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (extension)
        {
            case ".lnk": ResolveLnk(filePath, item); break;
            case ".url": ResolveUrl(filePath, item); break;
            default:
                item.TargetPath = filePath;
                item.DisplayName = _iconExtractor.GetShellDisplayName(filePath);
                item.IconPath = filePath;
                break;
        }

        if (string.IsNullOrEmpty(item.DisplayName))
            item.DisplayName = Path.GetFileNameWithoutExtension(filePath);

        return item;
    }

    private void ResolveLnk(string lnkPath, ShortcutItem item)
    {
        IShellLink? shellLink = null;
        try
        {
            shellLink = (IShellLink)new ShellLinkCoClass();
            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(lnkPath, 0);

            var targetBuilder = new StringBuilder(260);
            var findData = new WIN32_FIND_DATAW();
            shellLink.GetPath(targetBuilder, targetBuilder.Capacity, out findData, 0x4);
            item.TargetPath = targetBuilder.ToString();

            var argsBuilder = new StringBuilder(1024);
            shellLink.GetArguments(argsBuilder, argsBuilder.Capacity);
            item.Arguments = argsBuilder.ToString();

            var workDirBuilder = new StringBuilder(260);
            shellLink.GetWorkingDirectory(workDirBuilder, workDirBuilder.Capacity);
            item.WorkingDirectory = workDirBuilder.ToString();

            var iconPathBuilder = new StringBuilder(260);
            shellLink.GetIconLocation(iconPathBuilder, iconPathBuilder.Capacity, out int iconIndex);
            item.IconPath = iconPathBuilder.ToString();
            item.IconIndex = iconIndex;

            // Use filename like desktop does (clean and consistent)
            // Description field is often too verbose
            item.DisplayName = Path.GetFileNameWithoutExtension(lnkPath);
            System.Diagnostics.Debug.WriteLine($"ResolveLnk: Using filename '{item.DisplayName}' for {Path.GetFileName(lnkPath)}");

            if (string.IsNullOrEmpty(item.IconPath) && !string.IsNullOrEmpty(item.TargetPath))
            {
                item.IconPath = item.TargetPath;
                item.IconIndex = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in ResolveLnk: {ex.Message}");
            item.TargetPath = lnkPath;
            item.DisplayName = Path.GetFileNameWithoutExtension(lnkPath);
            item.IconPath = lnkPath;
        }
        finally
        {
            // Release COM object to prevent memory leaks
            if (shellLink != null)
            {
                try
                {
                    Marshal.ReleaseComObject(shellLink);
                }
                catch { }
            }
        }
    }

    private void ResolveUrl(string urlPath, ShortcutItem item)
    {
        try
        {
            var lines = File.ReadAllLines(urlPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    item.TargetPath = line[4..];
                else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                    item.IconPath = line[9..];
                else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                    if (int.TryParse(line[10..], out int idx)) item.IconIndex = idx;
            }
            item.DisplayName = Path.GetFileNameWithoutExtension(urlPath);
        }
        catch
        {
            item.TargetPath = urlPath;
            item.DisplayName = Path.GetFileNameWithoutExtension(urlPath);
        }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch,
            out WIN32_FIND_DATAW pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
            int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
}