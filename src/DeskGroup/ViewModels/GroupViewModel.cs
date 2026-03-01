using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class GroupViewModel : ObservableObject
{
    private readonly IconGroup _model;
    private readonly UsageTrackingService _usageTracker;
    private readonly IconExtractorService _iconExtractor;
    private readonly DesktopIconService _desktopIconService;
    private readonly PersistenceService _persistenceService;
    private readonly WindowPositionHelper _positionHelper;
    private bool _isInitializing = true; // Prevent syncing during initialization

    [ObservableProperty] private string _name = "New Group";

    partial void OnNameChanged(string value)
    {
        // Regenerate icon when name changes (if no custom icon)
        if (!_isInitializing && string.IsNullOrEmpty(_model.CustomIconPath))
        {
            LoadGroupIcon();
        }
    }
    [ObservableProperty] private GroupViewMode _viewMode = GroupViewMode.Minimized;
    [ObservableProperty] private GroupDockMode _dockMode = GroupDockMode.Sticky;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _snapToGrid = true;
    [ObservableProperty] private double _positionX = 100;
    [ObservableProperty] private double _positionY = 100;
    [ObservableProperty] private double _width = 300;
    [ObservableProperty] private double _height = 250;
    [ObservableProperty] private string _monitorDeviceName = string.Empty;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private SolidColorBrush _accentBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private string _accentColorHex = "#0078D4";
    [ObservableProperty] private ImageSource? _groupIcon;

    partial void OnViewModeChanged(GroupViewMode value)
    {
        if (_isInitializing) return; // Don't sync during initialization
        System.Diagnostics.Debug.WriteLine($"OnViewModeChanged: {value} for group '{Name}'");
        SyncToModel(); // Sync all properties to model
        OnPropertyChanged(nameof(ViewMode)); // Trigger save via MainViewModel
    }

    partial void OnPositionXChanged(double value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnPositionYChanged(double value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnWidthChanged(double value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnHeightChanged(double value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnAccentColorHexChanged(string value)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"OnAccentColorHexChanged: {value} for group '{Name}'");
            ApplyAccentColor(value);
            _model.AccentColor = value;

            // Regenerate icon if no custom icon set (color changed!)
            if (string.IsNullOrEmpty(_model.CustomIconPath))
            {
                LoadGroupIcon();
            }

            // Notify that a property changed to trigger auto-save
            OnPropertyChanged(nameof(AccentColorHex));
            System.Diagnostics.Debug.WriteLine($"  Color set in model and property changed notified");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in OnAccentColorHexChanged: {ex}");
        }
    }

    [ObservableProperty] private bool _isDragging;
    [ObservableProperty] private bool _isRenaming;

    public ObservableCollection<ShortcutItemViewModel> AllShortcuts { get; } = new();
    public ObservableCollection<ShortcutItemViewModel> TopUsedShortcuts { get; } = new();

    private GroupViewMode _preFullscreenViewMode = GroupViewMode.Normal;
    private double _preFullscreenX, _preFullscreenY, _preFullscreenW, _preFullscreenH;

    public Guid Id => _model.Id;
    public IconGroup Model => _model;

    public GroupViewModel(IconGroup model, UsageTrackingService usageTracker,
        IconExtractorService iconExtractor, DesktopIconService desktopIconService,
        PersistenceService persistenceService, WindowPositionHelper positionHelper)
    {
        _model = model; _usageTracker = usageTracker; _iconExtractor = iconExtractor;
        _desktopIconService = desktopIconService; _persistenceService = persistenceService;
        _positionHelper = positionHelper;

        System.Diagnostics.Debug.WriteLine($"GroupViewModel.Constructor: Creating '{model.Name}' with saved position ({model.PositionX},{model.PositionY}), ViewMode={model.ViewMode}, DockMode={model.DockMode}");

        Name = model.Name; 
        IsPinned = model.IsPinned; SnapToGrid = model.SnapToGrid;
        PositionX = model.PositionX; PositionY = model.PositionY;
        Width = model.Width; Height = model.Height;
        MonitorDeviceName = model.MonitorDeviceName;
        IsVisible = model.IsVisible; AccentColorHex = model.AccentColor;
        DockMode = model.DockMode;
        ViewMode = model.ViewMode; // Set ViewMode LAST so SyncToModel doesn't overwrite positions

        ApplyAccentColor(model.AccentColor);
        LoadGroupIcon();
        LoadShortcuts();
        RefreshTopUsed();

        _isInitializing = false; // Now allow syncing on property changes
    }

    private void ApplyAccentColor(string hex)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"ApplyAccentColor: {hex}");
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var newBrush = new SolidColorBrush(color);
            newBrush.Freeze();
            AccentBrush = newBrush;
            System.Diagnostics.Debug.WriteLine($"  Successfully created brush for color: {color}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"  ERROR: {ex.Message}");
            var fallbackBrush = new SolidColorBrush(Colors.DodgerBlue);
            fallbackBrush.Freeze();
            AccentBrush = fallbackBrush;
        }
    }

    private void LoadGroupIcon()
    {
        // Try to load custom icon first
        if (!string.IsNullOrEmpty(_model.CustomIconPath) && System.IO.File.Exists(_model.CustomIconPath))
        {
            GroupIcon = _iconExtractor.ExtractLargeIcon(_model.CustomIconPath);
            if (GroupIcon != null)
            {
                System.Diagnostics.Debug.WriteLine($"LoadGroupIcon: Loaded custom icon from '{_model.CustomIconPath}'");
                return;
            }
        }

        // Generate letter-based icon with INVERTED colors (accent color text on off-white bg)
        GroupIcon = CreateGroupLetterIcon(Name, AccentColorHex);
        System.Diagnostics.Debug.WriteLine($"LoadGroupIcon: Generated letter-based icon for '{Name}'");
    }

    private static ImageSource CreateGroupLetterIcon(string groupName, string accentColorHex)
    {
        // Extract initials (same logic as shortcuts)
        string initials = GetInitials(groupName);

        // Parse accent color
        Color accentColor;
        try
        {
            accentColor = (Color)ColorConverter.ConvertFromString(accentColorHex);
        }
        catch
        {
            accentColor = Colors.DodgerBlue;
        }

        // Create a 48x48 icon
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(48, 48, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            // Draw rounded rectangle background (off-white/light gray)
            var backgroundColor = Color.FromRgb(245, 245, 245); // Very light gray
            var rect = new System.Windows.Rect(2, 2, 44, 44);
            var roundedRect = new System.Windows.Media.RectangleGeometry(rect, 6, 6);
            context.DrawGeometry(new SolidColorBrush(backgroundColor), null, roundedRect);

            // Draw initials text in ACCENT COLOR
            var formattedText = new System.Windows.Media.FormattedText(
                initials,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"), 
                    FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                20,
                new SolidColorBrush(accentColor),
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

    private void LoadShortcuts()
    {
        AllShortcuts.Clear();
        foreach (var shortcut in _model.Shortcuts.OrderBy(s => s.SortOrder))
            AllShortcuts.Add(new ShortcutItemViewModel(shortcut, _usageTracker, _iconExtractor));
    }

    public void RefreshTopUsed()
    {
        var topItems = _usageTracker.GetTopUsedWithFallback(_model, 10);
        TopUsedShortcuts.Clear();
        foreach (var item in topItems)
        {
            var existing = AllShortcuts.FirstOrDefault(s => s.Id == item.Id);
            if (existing != null) TopUsedShortcuts.Add(existing);
        }
    }

    private void SyncToModel()
    {
        _model.Name = Name; _model.ViewMode = ViewMode; _model.DockMode = DockMode;
        _model.IsPinned = IsPinned; _model.SnapToGrid = SnapToGrid;
        _model.PositionX = PositionX; _model.PositionY = PositionY;
        _model.Width = Width; _model.Height = Height;
        _model.MonitorDeviceName = MonitorDeviceName;
        _model.IsVisible = IsVisible; _model.AccentColor = AccentColorHex;
    }

    public void RequestSave(List<IconGroup> allGroups)
    {
        SyncToModel();
        _persistenceService.SaveGroupsDebounced(allGroups);
    }

    [RelayCommand] private void SetMinimized()
    {
        if (ViewMode == GroupViewMode.Fullscreen) RestoreFromFullscreen();
        ViewMode = GroupViewMode.Minimized;
    }

    [RelayCommand] private void SetNormal()
    {
        if (ViewMode == GroupViewMode.Fullscreen) RestoreFromFullscreen();
        ViewMode = GroupViewMode.Normal;
    }

    [RelayCommand] private void SetGrid()
    {
        if (ViewMode == GroupViewMode.Fullscreen) RestoreFromFullscreen();
        ViewMode = GroupViewMode.Grid;
    }

    [RelayCommand] private void SetFullscreen()
    {
        if (ViewMode != GroupViewMode.Fullscreen) EnterFullscreen();
    }

    [RelayCommand] private void ToggleFullscreen()
    {
        if (ViewMode == GroupViewMode.Fullscreen) RestoreFromFullscreen();
        else EnterFullscreen();
    }

    private void EnterFullscreen()
    {
        _preFullscreenViewMode = ViewMode;
        _preFullscreenX = PositionX; _preFullscreenY = PositionY;
        _preFullscreenW = Width; _preFullscreenH = Height;

        var center = new Point(PositionX + Width / 2, PositionY + Height / 2);
        var fsRect = _positionHelper.GetFullscreenRect(center);
        PositionX = fsRect.X; PositionY = fsRect.Y;
        Width = fsRect.Width; Height = fsRect.Height;
        ViewMode = GroupViewMode.Fullscreen;
    }

    private void RestoreFromFullscreen()
    {
        PositionX = _preFullscreenX; PositionY = _preFullscreenY;
        Width = _preFullscreenW; Height = _preFullscreenH;
        ViewMode = _preFullscreenViewMode;
    }

    public void OnMouseEnter()
    {
        if (ViewMode == GroupViewMode.Minimized)
        {
            RefreshTopUsed();
            ViewMode = GroupViewMode.Hover;
        }
    }

    public void OnMouseLeave()
    {
        if (ViewMode == GroupViewMode.Hover)
            ViewMode = GroupViewMode.Minimized;
    }

    [RelayCommand]
    private void AddShortcut(string filePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"AddShortcut: {filePath} to {Name}");

            var resolver = new ShortcutResolverService(_iconExtractor);
            var shortcutItem = resolver.Resolve(filePath);
            shortcutItem.SortOrder = _model.Shortcuts.Count;
            _model.Shortcuts.Add(shortcutItem);

            System.Diagnostics.Debug.WriteLine($"  Creating ViewModel for shortcut: {shortcutItem.DisplayName}");

            var vm = new ShortcutItemViewModel(shortcutItem, _usageTracker, _iconExtractor);
            AllShortcuts.Add(vm);

            System.Diagnostics.Debug.WriteLine($"  Hiding desktop icon");

            var originalState = _desktopIconService.HideDesktopIcon(shortcutItem);
            if (originalState != null) _model.OriginalIconStates.Add(originalState);

            RefreshTopUsed();

            System.Diagnostics.Debug.WriteLine($"  AddShortcut completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in AddShortcut: {ex}");
            MessageBox.Show($"Failed to add shortcut:\n\n{ex.Message}\n\nFile: {filePath}", 
                "Add Shortcut Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RemoveShortcut(ShortcutItemViewModel shortcutVm)
    {
        var model = shortcutVm.Model;
        var originalState = _model.OriginalIconStates.FirstOrDefault(s => s.ShortcutItemId == model.Id);
        if (originalState != null)
        {
            _desktopIconService.RestoreDesktopIcon(originalState);
            _model.OriginalIconStates.Remove(originalState);
        }
        _model.Shortcuts.Remove(model);
        AllShortcuts.Remove(shortcutVm);
        RefreshTopUsed();
    }

    [RelayCommand]
    private void FinalizePosition()
    {
        if (IsPinned) return;
        var rect = new Rect(PositionX, PositionY, Width, Height);
        var gridSize = SnapToGrid ? 20 : 1;
        var final = _positionHelper.FinalizePosition(rect, SnapToGrid, gridSize);
        PositionX = final.X; PositionY = final.Y;
    }

    [RelayCommand] private void ToggleDockMode() =>
        DockMode = DockMode == GroupDockMode.Sticky ? GroupDockMode.Float : GroupDockMode.Sticky;
    [RelayCommand] private void TogglePin() => IsPinned = !IsPinned;
    [RelayCommand] private void ToggleSnapToGrid() => SnapToGrid = !SnapToGrid;

    [RelayCommand]
    private void SetGroupIcon(string iconPath)
    {
        _model.CustomIconPath = iconPath;
        LoadGroupIcon();
    }

    [RelayCommand]
    private void Rename(string newName)
    {
        if (!string.IsNullOrWhiteSpace(newName)) Name = newName.Trim();
        // Exit renaming mode after applying the name
        IsRenaming = false;
    }

    [RelayCommand]
    private void BeginRename()
    {
        IsRenaming = true;
    }

    [RelayCommand]
    private void CancelRename()
    {
        // Discard pending changes by resetting any two-way bound edit control via Name
        // and exit renaming mode
        IsRenaming = false;
    }
}

