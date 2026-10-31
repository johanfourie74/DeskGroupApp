using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class ShortcutItemViewModel : ObservableObject
{
    private static readonly ConcurrentDictionary<string, ImageSource> s_letterIconCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim s_extractionLimiter = new(4, 4);

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
        LoadIcon(forceRefresh: false);
    }

    private void LoadIcon(bool forceRefresh = false)
    {
        try
        {
            // Fast path: check memory + disk cache synchronously (< 0.2ms)
            if (!forceRefresh && _iconExtractor.TryGetCachedShortcutIcon(_model, out var cached) && cached != null)
            {
                Icon = cached;
                return;
            }

            // Set instant letter-based fallback immediately so startup UI never blocks on slow shell/exe icon extraction
            Icon = CreateLetterBasedIcon(_model.DisplayName);

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !forceRefresh)
            {
                _ = Task.Run(async () =>
                {
                    await s_extractionLimiter.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var extracted = _iconExtractor.ExtractShortcutIcon(_model, bypassCache: false);
                        if (extracted != null)
                        {
                            _ = dispatcher.BeginInvoke(() =>
                            {
                                Icon = extracted;
                            }, DispatcherPriority.Background);
                        }
                    }
                    catch { }
                    finally
                    {
                        s_extractionLimiter.Release();
                    }
                });
            }
            else
            {
                var extracted = _iconExtractor.ExtractShortcutIcon(_model, bypassCache: forceRefresh);
                if (extracted != null)
                {
                    Icon = extracted;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadIcon: Exception loading icon for '{_model.DisplayName}': {ex.Message}");
            Icon = CreateLetterBasedIcon(_model.DisplayName);
        }
    }

    private static ImageSource CreateLetterBasedIcon(string displayName)
    {
        string initials = GetInitials(displayName);
        var color = GetColorForName(displayName);
        string cacheKey = $"{initials}|{color}";

        if (s_letterIconCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            var rect = new System.Windows.Rect(2, 2, 44, 44);
            var roundedRect = new RectangleGeometry(rect, 6, 6);
            var bgBrush = new SolidColorBrush(color);
            bgBrush.Freeze();
            context.DrawGeometry(bgBrush, null, roundedRect);

            var formattedText = new FormattedText(
                initials,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                20,
                Brushes.White,
                96);

            var textPoint = new System.Windows.Point(
                24 - formattedText.Width / 2,
                24 - formattedText.Height / 2);

            context.DrawText(formattedText, textPoint);
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        s_letterIconCache[cacheKey] = bitmap;
        return bitmap;
    }

    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "??";

        var words = displayName.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return "??";

        if (words.Length == 1)
        {
            string word = words[0].ToUpperInvariant();
            return word.Length >= 2 ? word.Substring(0, 2) : word.Substring(0, 1) + word.Substring(0, 1);
        }

        return words[0][0].ToString().ToUpperInvariant() + words[1][0].ToString().ToUpperInvariant();
    }

    private static Color GetColorForName(string name)
    {
        int hash = (name ?? string.Empty).GetHashCode();

        var colors = new[]
        {
            Color.FromRgb(244, 67, 54),   // Red
            Color.FromRgb(233, 30, 99),   // Pink
            Color.FromRgb(156, 39, 176),  // Purple
            Color.FromRgb(103, 58, 183),  // Deep Purple
            Color.FromRgb(63, 81, 181),   // Indigo
            Color.FromRgb(33, 150, 243),  // Blue
            Color.FromRgb(3, 169, 244),   // Light Blue
            Color.FromRgb(0, 188, 212),   // Cyan
            Color.FromRgb(0, 150, 136),   // Teal
            Color.FromRgb(76, 175, 80),   // Green
            Color.FromRgb(139, 195, 74),  // Light Green
            Color.FromRgb(255, 152, 0),   // Orange
            Color.FromRgb(255, 87, 34),   // Deep Orange
            Color.FromRgb(121, 85, 72),   // Brown
            Color.FromRgb(96, 125, 139)   // Blue Grey
        };

        int index = Math.Abs(hash == int.MinValue ? 0 : hash) % colors.Length;
        return colors[index];
    }

    [RelayCommand]
    private void Launch()
    {
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.AppStarting;
        var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        resetTimer.Tick += (_, _) => { System.Windows.Input.Mouse.OverrideCursor = null; resetTimer.Stop(); };
        resetTimer.Start();

        try
        {
            Process.Start(BuildLaunchInfo());
            _usageTracker.RecordLaunch(_model);
            LaunchCount = _model.LaunchCount;
        }
        catch (Exception ex)
        {
            resetTimer.Stop();
            System.Windows.Input.Mouse.OverrideCursor = null;
            Debug.WriteLine($"Failed to launch shortcut: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"Could not open \"{_model.DisplayName}\".\n\n{ex.Message}\n\nTry removing and re-adding the shortcut.",
                "Launch Failed",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private ProcessStartInfo BuildLaunchInfo()
    {
        if (!string.IsNullOrEmpty(_model.StoredFilePath) && System.IO.File.Exists(_model.StoredFilePath))
            return new ProcessStartInfo { UseShellExecute = true, FileName = _model.StoredFilePath };

        if (!string.IsNullOrEmpty(_model.OriginalShortcutPath) && System.IO.File.Exists(_model.OriginalShortcutPath))
            return new ProcessStartInfo { UseShellExecute = true, FileName = _model.OriginalShortcutPath };

        if (!string.IsNullOrEmpty(_model.TargetPath))
        {
            var info = new ProcessStartInfo { UseShellExecute = true, FileName = _model.TargetPath };
            if (!string.IsNullOrEmpty(_model.Arguments)) info.Arguments = _model.Arguments;
            if (!string.IsNullOrEmpty(_model.WorkingDirectory)) info.WorkingDirectory = _model.WorkingDirectory;
            return info;
        }

        throw new InvalidOperationException("No valid launch path found. The shortcut may have been moved or uninstalled.");
    }

    [RelayCommand]
    private void RefreshIcon() => LoadIcon(forceRefresh: true);

    [RelayCommand]
    private void ResetUsage()
    {
        _usageTracker.ResetUsage(_model);
        LaunchCount = 0;
    }
}
