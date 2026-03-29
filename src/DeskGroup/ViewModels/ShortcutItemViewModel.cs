using System;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class ShortcutItemViewModel : ObservableObject
{
    private readonly ShortcutItem _model;
    private readonly UsageTrackingService _usageTracker;
    private readonly IconExtractorService _iconExtractor;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private ImageSource? _icon;

    // Alias for backward compatibility with XAML bindings
    public ImageSource? IconImage => Icon;

    partial void OnIconChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(IconImage));
    }

    [ObservableProperty]
    private int _launchCount;

    [ObservableProperty]
    private bool _isSelected;

    public Guid Id => _model.Id;
    public ShortcutItem Model => _model;

    public ShortcutItemViewModel(ShortcutItem model, UsageTrackingService usageTracker,
        IconExtractorService iconExtractor)
    {
        _model = model;
        _usageTracker = usageTracker;
        _iconExtractor = iconExtractor;
        DisplayName = model.DisplayName;
        LaunchCount = model.LaunchCount;
        LoadIcon();
    }

    private void LoadIcon()
    {
        try
        {
            // Try to load icon from multiple sources
            if (!string.IsNullOrEmpty(_model.IconPath))
                Icon = _iconExtractor.ExtractIconByIndex(_model.IconPath, _model.IconIndex);
            if (Icon == null && !string.IsNullOrEmpty(_model.TargetPath))
                Icon = _iconExtractor.ExtractLargeIcon(_model.TargetPath);
            if (Icon == null && !string.IsNullOrEmpty(_model.OriginalShortcutPath))
                Icon = _iconExtractor.ExtractLargeIcon(_model.OriginalShortcutPath);

            // If all extraction attempts failed, try Windows system icon for file type
            if (Icon == null && !string.IsNullOrEmpty(_model.TargetPath))
            {
                Icon = GetWindowsSystemIcon(_model.TargetPath);
            }

            // Last resort: Create a letter-based icon (Google/Microsoft style)
            if (Icon == null)
            {
                System.Diagnostics.Debug.WriteLine($"LoadIcon: All icon extraction failed for '{_model.DisplayName}', creating letter-based icon");
                Icon = CreateLetterBasedIcon(_model.DisplayName);
            }
        }
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine($"LoadIcon: Exception loading icon for '{_model.DisplayName}': {ex.Message}");
            Icon = CreateLetterBasedIcon(_model.DisplayName); 
        }
    }

    private static ImageSource? GetWindowsSystemIcon(string filePath)
    {
        try
        {
            // Get the file extension
            string extension = System.IO.Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
                return null;

            // Use SHGetFileInfo to get the system icon for this file type
            var shinfo = new SHFILEINFO();
            const uint SHGFI_ICON = 0x100;
            const uint SHGFI_LARGEICON = 0x0;
            const uint SHGFI_USEFILEATTRIBUTES = 0x10;

            IntPtr hImgLarge = SHGetFileInfo(
                filePath,
                0,
                ref shinfo,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(shinfo),
                SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);

            if (hImgLarge != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
                    var imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    imageSource.Freeze();

                    // Clean up
                    DestroyIcon(shinfo.hIcon);

                    System.Diagnostics.Debug.WriteLine($"GetWindowsSystemIcon: SUCCESS for extension '{extension}'");
                    return imageSource;
                }
                catch
                {
                    // Clean up on error
                    if (shinfo.hIcon != IntPtr.Zero)
                        DestroyIcon(shinfo.hIcon);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetWindowsSystemIcon: Failed - {ex.Message}");
        }

        return null;
    }

    private static ImageSource CreateLetterBasedIcon(string displayName)
    {
        // Extract initials (first letter of first 2 words)
        string initials = GetInitials(displayName);

        // Get a consistent color based on the name
        var color = GetColorForName(displayName);

        // Create a 48x48 icon (larger for better quality)
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(48, 48, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            // Draw rounded rectangle background
            var rect = new System.Windows.Rect(2, 2, 44, 44);
            var roundedRect = new System.Windows.Media.RectangleGeometry(rect, 6, 6);
            context.DrawGeometry(new System.Windows.Media.SolidColorBrush(color), null, roundedRect);

            // Draw initials text
            var formattedText = new System.Windows.Media.FormattedText(
                initials,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface("Segoe UI"),
                20,
                System.Windows.Media.Brushes.White,
                96);

            // Center the text
            var textPoint = new System.Windows.Point(
                24 - formattedText.Width / 2,
                24 - formattedText.Height / 2);

            context.DrawText(formattedText, textPoint);
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "??";

        // Split into words and take first letter of first 2 words
        var words = displayName.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return "??";

        if (words.Length == 1)
        {
            // Single word - take first 2 letters or first letter twice
            string word = words[0].ToUpperInvariant();
            return word.Length >= 2 ? word.Substring(0, 2) : word.Substring(0, 1) + word.Substring(0, 1);
        }

        // Two or more words - first letter of first 2 words
        return words[0][0].ToString().ToUpperInvariant() + words[1][0].ToString().ToUpperInvariant();
    }

    private static System.Windows.Media.Color GetColorForName(string name)
    {
        // Generate consistent color based on name hash
        int hash = name.GetHashCode();

        // Predefined nice colors (Material Design inspired)
        var colors = new[]
        {
            System.Windows.Media.Color.FromRgb(244, 67, 54),   // Red
            System.Windows.Media.Color.FromRgb(233, 30, 99),   // Pink
            System.Windows.Media.Color.FromRgb(156, 39, 176),  // Purple
            System.Windows.Media.Color.FromRgb(103, 58, 183),  // Deep Purple
            System.Windows.Media.Color.FromRgb(63, 81, 181),   // Indigo
            System.Windows.Media.Color.FromRgb(33, 150, 243),  // Blue
            System.Windows.Media.Color.FromRgb(3, 169, 244),   // Light Blue
            System.Windows.Media.Color.FromRgb(0, 188, 212),   // Cyan
            System.Windows.Media.Color.FromRgb(0, 150, 136),   // Teal
            System.Windows.Media.Color.FromRgb(76, 175, 80),   // Green
            System.Windows.Media.Color.FromRgb(139, 195, 74),  // Light Green
            System.Windows.Media.Color.FromRgb(255, 152, 0),   // Orange
            System.Windows.Media.Color.FromRgb(255, 87, 34),   // Deep Orange
            System.Windows.Media.Color.FromRgb(121, 85, 72),   // Brown
            System.Windows.Media.Color.FromRgb(96, 125, 139)   // Blue Grey
        };

        // Use hash to pick a consistent color
        int index = Math.Abs(hash) % colors.Length;
        return colors[index];
    }

    // P/Invoke for Windows system icons
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
        public uint dwAttributes;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [RelayCommand]
    private void Launch()
    {
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.AppStarting;
        var resetTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        resetTimer.Tick += (_, _) => { System.Windows.Input.Mouse.OverrideCursor = null; resetTimer.Stop(); };
        resetTimer.Start();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = !string.IsNullOrEmpty(_model.TargetPath)
                    ? _model.TargetPath : _model.OriginalShortcutPath
            };
            if (!string.IsNullOrEmpty(_model.Arguments)) startInfo.Arguments = _model.Arguments;
            if (!string.IsNullOrEmpty(_model.WorkingDirectory)) startInfo.WorkingDirectory = _model.WorkingDirectory;

            Process.Start(startInfo);
            _usageTracker.RecordLaunch(_model);
            LaunchCount = _model.LaunchCount;
        }
        catch (Exception ex)
        {
            resetTimer.Stop();
            System.Windows.Input.Mouse.OverrideCursor = null;
            Debug.WriteLine($"Failed to launch shortcut: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RefreshIcon() => LoadIcon();

    [RelayCommand]
    private void ResetUsage()
    {
        _usageTracker.ResetUsage(_model);
        LaunchCount = 0;
    }
}