using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskGroup.Services;

public class IconExtractorService
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string szFileName, int nIconIndex,
        IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_DISPLAYNAME = 0x000000200;

    public ImageSource? ExtractLargeIcon(string filePath) => ExtractIconInternal(filePath, true);
    public ImageSource? ExtractSmallIcon(string filePath) => ExtractIconInternal(filePath, false);

    public ImageSource? ExtractIconByIndex(string filePath, int index)
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            var largeIcons = new IntPtr[1];
            var smallIcons = new IntPtr[1];
            int count = ExtractIconEx(filePath, index, largeIcons, smallIcons, 1);
            if (count > 0 && largeIcons[0] != IntPtr.Zero)
            {
                hIcon = largeIcons[0];
                var imageSource = IconToImageSource(hIcon);
                if (smallIcons[0] != IntPtr.Zero) DestroyIcon(smallIcons[0]);
                return imageSource;
            }
        }
        catch { }
        finally { if (hIcon != IntPtr.Zero) DestroyIcon(hIcon); }
        return ExtractLargeIcon(filePath);
    }

    public string GetShellDisplayName(string filePath)
    {
        try
        {
            var shfi = new SHFILEINFO();
            SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_DISPLAYNAME);
            return string.IsNullOrEmpty(shfi.szDisplayName)
                ? Path.GetFileNameWithoutExtension(filePath)
                : shfi.szDisplayName;
        }
        catch { return Path.GetFileNameWithoutExtension(filePath); }
    }

    private ImageSource? ExtractIconInternal(string filePath, bool largeIcon)
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) && !Directory.Exists(filePath))
                return null;
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            SHGetFileInfo(filePath, 0, ref shfi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);
            if (shfi.hIcon == IntPtr.Zero) return null;
            hIcon = shfi.hIcon;
            return IconToImageSource(hIcon);
        }
        catch { return null; }
        finally { if (hIcon != IntPtr.Zero) DestroyIcon(hIcon); }
    }

    private static ImageSource? IconToImageSource(IntPtr hIcon)
    {
        try
        {
            var imageSource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            imageSource.Freeze();
            return imageSource;
        }
        catch { return null; }
    }
}