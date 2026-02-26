using System;
using System.IO;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskGroup.Services;

public class IconExtractorService
{
    // Use managed APIs where possible and CsWin32-generated PInvoke for advanced scenarios.
    public ImageSource? ExtractLargeIcon(string filePath) => ExtractIconInternal(filePath, true);
    public ImageSource? ExtractSmallIcon(string filePath) => ExtractIconInternal(filePath, false);

    public ImageSource? ExtractIconByIndex(string filePath, int index)
    {
        // Attempt CsWin32-based extraction via Win32Interop
        try
        {
            var hIcon = DeskGroup.Helpers.Win32Interop.ExtractIcon(filePath, index);
            if (hIcon == IntPtr.Zero) return null;
            try
            {
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                imageSource.Freeze();
                return imageSource;
            }
            finally
            {
                try { DeskGroup.Helpers.Win32Interop.DestroyIcon(hIcon); } catch { }
            }
        }
        catch
        {
            return ExtractLargeIcon(filePath);
        }
    }

    public string GetShellDisplayName(string filePath)
    {
        try
        {
            // Prefer file description when available (for executables), otherwise filename without extension
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

    private ImageSource? ExtractIconInternal(string filePath, bool largeIcon)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
                return null;

            // Try managed extraction first
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon == null) return null;
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                imageSource.Freeze();
                return imageSource;
            }
            catch { }

            return null;
        }
        catch { return null; }
    }
}
