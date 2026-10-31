using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskGroup.Models;

namespace DeskGroup.Services;

public class IconExtractorService
{
    private static readonly ConcurrentDictionary<string, ImageSource> s_memoryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _diskCacheDir;

    public IconExtractorService(string? dataDirectory = null)
    {
        var baseDir = !string.IsNullOrWhiteSpace(dataDirectory)
            ? dataDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskGroup");
        _diskCacheDir = Path.Combine(baseDir, "IconCache");
        try
        {
            Directory.CreateDirectory(_diskCacheDir);
        }
        catch { }
    }

    public ImageSource? ExtractLargeIcon(string filePath) => ExtractIconInternal(filePath, true);
    public ImageSource? ExtractSmallIcon(string filePath) => ExtractIconInternal(filePath, false);

    public bool TryGetCachedShortcutIcon(ShortcutItem model, out ImageSource? icon)
    {
        icon = null;
        string key = BuildShortcutCacheKey(model);
        if (string.IsNullOrEmpty(key)) return false;

        if (s_memoryCache.TryGetValue(key, out var memIcon))
        {
            icon = memIcon;
            return true;
        }

        var diskIcon = TryLoadFromDiskCache(key);
        if (diskIcon != null)
        {
            s_memoryCache[key] = diskIcon;
            icon = diskIcon;
            return true;
        }

        return false;
    }

    public ImageSource? ExtractShortcutIcon(ShortcutItem model, bool bypassCache = false)
    {
        string key = BuildShortcutCacheKey(model);
        if (!bypassCache && !string.IsNullOrEmpty(key))
        {
            if (TryGetCachedShortcutIcon(model, out var cached) && cached != null)
                return cached;
        }

        ImageSource? extracted = null;

        if (!string.IsNullOrEmpty(model.IconPath))
            extracted = ExtractIconByIndex(model.IconPath, model.IconIndex, bypassCache);
        if (extracted == null && !string.IsNullOrEmpty(model.TargetPath))
            extracted = ExtractIconInternal(model.TargetPath, true, bypassCache);
        if (extracted == null && !string.IsNullOrEmpty(model.StoredFilePath))
            extracted = ExtractIconInternal(model.StoredFilePath, true, bypassCache);
        if (extracted == null && !string.IsNullOrEmpty(model.OriginalShortcutPath))
            extracted = ExtractIconInternal(model.OriginalShortcutPath, true, bypassCache);
        if (extracted == null && !string.IsNullOrEmpty(model.TargetPath))
            extracted = ExtractSystemFileTypeIcon(model.TargetPath);

        if (extracted != null && !string.IsNullOrEmpty(key))
        {
            s_memoryCache[key] = extracted;
            SaveToDiskCache(key, extracted);
        }

        return extracted;
    }

    public ImageSource? ExtractIconByIndex(string filePath, int index, bool bypassCache = false)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        string cacheKey = $"idx|{filePath.Trim()}|{index}";
        if (!bypassCache)
        {
            if (s_memoryCache.TryGetValue(cacheKey, out var cached))
                return cached;
            var diskCached = TryLoadFromDiskCache(cacheKey);
            if (diskCached != null)
            {
                s_memoryCache[cacheKey] = diskCached;
                return diskCached;
            }
        }

        try
        {
            var hIcon = DeskGroup.Helpers.Win32Interop.ExtractIcon(filePath, index);
            if (hIcon == IntPtr.Zero) return ExtractIconInternal(filePath, true, bypassCache);
            try
            {
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                imageSource.Freeze();
                s_memoryCache[cacheKey] = imageSource;
                SaveToDiskCache(cacheKey, imageSource);
                return imageSource;
            }
            finally
            {
                try { DeskGroup.Helpers.Win32Interop.DestroyIcon(hIcon); } catch { }
            }
        }
        catch
        {
            return ExtractIconInternal(filePath, true, bypassCache);
        }
    }

    public string GetShellDisplayName(string filePath)
    {
        try
        {
            try
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(filePath);
                if (!string.IsNullOrEmpty(info.FileDescription)) return info.FileDescription;
            }
            catch { }
            return Path.GetFileNameWithoutExtension(filePath);
        }
        catch { return Path.GetFileNameWithoutExtension(filePath); }
    }

    private ImageSource? ExtractIconInternal(string filePath, bool largeIcon, bool bypassCache = false)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
                return null;

            string cacheKey = $"file|{filePath.Trim()}|{(largeIcon ? "L" : "S")}";
            if (!bypassCache)
            {
                if (s_memoryCache.TryGetValue(cacheKey, out var cached))
                    return cached;
                var diskCached = TryLoadFromDiskCache(cacheKey);
                if (diskCached != null)
                {
                    s_memoryCache[cacheKey] = diskCached;
                    return diskCached;
                }
            }

            // 1. Try managed extraction first for files
            if (File.Exists(filePath))
            {
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(filePath);
                    if (icon != null)
                    {
                        var imageSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        imageSource.Freeze();
                        s_memoryCache[cacheKey] = imageSource;
                        SaveToDiskCache(cacheKey, imageSource);
                        return imageSource;
                    }
                }
                catch { }
            }

            // 2. Fallback to SHGetFileInfo (works for folders, .url, special shell items, and Windows 11 modern icons)
            var shellIcon = ExtractShellIcon(filePath, largeIcon, useFileAttributes: false);
            if (shellIcon != null)
            {
                s_memoryCache[cacheKey] = shellIcon;
                SaveToDiskCache(cacheKey, shellIcon);
                return shellIcon;
            }

            return null;
        }
        catch { return null; }
    }

    public ImageSource? ExtractSystemFileTypeIcon(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        string ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return null;

        string cacheKey = $"ext|{ext.ToLowerInvariant()}";
        if (s_memoryCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var icon = ExtractShellIcon(filePath, largeIcon: true, useFileAttributes: true);
        if (icon != null)
        {
            s_memoryCache[cacheKey] = icon;
        }
        return icon;
    }

    private static ImageSource? ExtractShellIcon(string filePath, bool largeIcon, bool useFileAttributes)
    {
        try
        {
            var shinfo = new SHFILEINFO();
            const uint SHGFI_ICON = 0x100;
            const uint SHGFI_LARGEICON = 0x0;
            const uint SHGFI_SMALLICON = 0x1;
            const uint SHGFI_USEFILEATTRIBUTES = 0x10;

            uint flags = SHGFI_ICON | (largeIcon ? SHGFI_LARGEICON : SHGFI_SMALLICON);
            if (useFileAttributes)
                flags |= SHGFI_USEFILEATTRIBUTES;

            IntPtr res = SHGetFileInfo(
                filePath,
                0,
                ref shinfo,
                (uint)Marshal.SizeOf(shinfo),
                flags);

            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    imageSource.Freeze();
                    return imageSource;
                }
                finally
                {
                    try { DeskGroup.Helpers.Win32Interop.DestroyIcon(shinfo.hIcon); } catch { }
                }
            }
        }
        catch { }

        return null;
    }

    private static string BuildShortcutCacheKey(ShortcutItem model)
    {
        var primary = !string.IsNullOrEmpty(model.IconPath)
            ? $"ico:{model.IconPath}|{model.IconIndex}"
            : !string.IsNullOrEmpty(model.TargetPath)
                ? $"tgt:{model.TargetPath}"
                : !string.IsNullOrEmpty(model.StoredFilePath)
                    ? $"sto:{model.StoredFilePath}"
                    : !string.IsNullOrEmpty(model.OriginalShortcutPath)
                        ? $"orig:{model.OriginalShortcutPath}"
                        : string.Empty;
        return primary;
    }

    private string GetDiskCacheFilePath(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
        var hash = Convert.ToHexString(bytes)[..24];
        return Path.Combine(_diskCacheDir, $"{hash}.png");
    }

    private ImageSource? TryLoadFromDiskCache(string key)
    {
        try
        {
            var path = GetDiskCacheFilePath(key);
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0) return null;

            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void SaveToDiskCache(string key, ImageSource source)
    {
        if (source is not BitmapSource bitmapSource) return;

        try
        {
            var path = GetDiskCacheFilePath(key);
            if (File.Exists(path)) return;

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            File.WriteAllBytes(path, ms.ToArray());
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);
}
