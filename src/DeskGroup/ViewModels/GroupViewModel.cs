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

    // Track the saved dock mode before temporary float (for Show/Hide All)
    private GroupDockMode? _savedDockModeBeforeFloat = null;

    public AppSettings Settings { get; }

    // Expose GroupBackgroundOpacity as observable property for live binding updates
    public double GroupBackgroundOpacity => Settings.GroupBackgroundOpacity;

    // Per-group appearance overrides (0/negative = use global settings)
    [ObservableProperty] private int _perGroupIconSize = 0;
    [ObservableProperty] private double _perGroupOpacity = -1.0;

    // Effective values — per-group takes priority over global settings
    public int EffectiveIconSize =>
        _perGroupIconSize > 0 ? _perGroupIconSize :
        (Settings.IconSizeMode == IconSizeMode.Custom ? Settings.CustomIconSize : 48);

    public int EffectiveListIconSize => Math.Clamp(EffectiveIconSize / 2, 16, 32);
    public double EffectiveListTileHeight => EffectiveListIconSize + 8;

    public double EffectiveOpacity =>
        _perGroupOpacity >= 0.0 ? _perGroupOpacity : Settings.GroupBackgroundOpacity;

    // Proxy read/write properties for TwoWay slider binding in context menu
    public int DisplayIconSize
    {
        get => EffectiveIconSize;
        set { PerGroupIconSize = value; OnPropertyChanged(nameof(DisplayIconSize)); }
    }

    public double DisplayOpacity
    {
        get => EffectiveOpacity;
        set { PerGroupOpacity = value; OnPropertyChanged(nameof(DisplayOpacity)); }
    }

    partial void OnPerGroupIconSizeChanged(int value)
    {
        if (_isInitializing) return;
        OnPropertyChanged(nameof(EffectiveIconSize));
        OnPropertyChanged(nameof(EffectiveListIconSize));
        OnPropertyChanged(nameof(EffectiveListTileHeight));
        OnPropertyChanged(nameof(DisplayIconSize));
        SyncToModel();
    }

    partial void OnPerGroupOpacityChanged(double value)
    {
        if (_isInitializing) return;
        OnPropertyChanged(nameof(EffectiveOpacity));
        OnPropertyChanged(nameof(DisplayOpacity));
        UpdateTransparentAccentBrush();
        SyncToModel();
    }

    // Method to refresh opacity when settings change
    public void RefreshOpacity()
    {
        OnPropertyChanged(nameof(GroupBackgroundOpacity));
        OnPropertyChanged(nameof(EffectiveOpacity));
        OnPropertyChanged(nameof(DisplayOpacity));
        UpdateTransparentAccentBrush();
    }

    // Method to refresh icon sizes when global settings change
    public void RefreshIconSize()
    {
        OnPropertyChanged(nameof(EffectiveIconSize));
        OnPropertyChanged(nameof(EffectiveListIconSize));
        OnPropertyChanged(nameof(EffectiveListTileHeight));
        OnPropertyChanged(nameof(DisplayIconSize));
    }

    // Reset per-group appearance overrides back to global defaults
    public void ResetGroupAppearance()
    {
        PerGroupIconSize = 0;
        PerGroupOpacity = -1.0;
    }

    private void UpdateTransparentAccentBrush()
    {
        // Create a new brush with the accent color but with effective opacity applied
        var color = AccentBrush.Color;
        var alpha = (byte)(EffectiveOpacity * 255);
        var transparentColor = Color.FromArgb(alpha, color.R, color.G, color.B);
        TransparentAccentBrush = new SolidColorBrush(transparentColor);

        // Border fades at slower rate: borderAlpha = (opacity * 0.8) + 0.2
        // So at 100% background → 100% border, at 0% background → 20% border
        var borderOpacity = (EffectiveOpacity * 0.8) + 0.2;
        var borderAlpha = (byte)(borderOpacity * 255);
        var borderColor = Color.FromArgb(borderAlpha, color.R, color.G, color.B);
        TransparentBorderBrush = new SolidColorBrush(borderColor);
    }

    [ObservableProperty] private string _name = "New Group";

    partial void OnNameChanged(string value)
    {
        // Regenerate icon when name changes (if no custom icon)
        if (!_isInitializing && string.IsNullOrEmpty(_model.CustomIconPath))
        {
            LoadGroupIcon();
        }
    }
    [ObservableProperty] private GroupViewMode _viewMode = GroupViewMode.Normal;
    [ObservableProperty] private GroupDockMode _dockMode = GroupDockMode.Sticky;

    partial void OnDockModeChanged(GroupDockMode value)
    {
        if (_isInitializing) return;

        // CRITICAL: Don't save temporary Float mode from Show All!
        // PropertyChanged is already raised by the source generator after this partial runs.
        if (SavedDockModeBeforeFloat.HasValue)
            return; // Skip JSON save

        SyncToModel();
    }

    // Public property to access saved dock mode for restoration
    public GroupDockMode? SavedDockModeBeforeFloat
    {
        get => _savedDockModeBeforeFloat;
        set => _savedDockModeBeforeFloat = value;
    }

    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _snapToGrid = false;
    [ObservableProperty] private double _positionX = 100;
    [ObservableProperty] private double _positionY = 100;
    [ObservableProperty] private double _width = 300;
    [ObservableProperty] private double _height = 250;
    [ObservableProperty] private double _gridViewWidth = 400;
    [ObservableProperty] private double _gridViewHeight = 300;
    [ObservableProperty] private string _monitorDeviceName = string.Empty;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private SolidColorBrush _accentBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private SolidColorBrush _transparentAccentBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private SolidColorBrush _transparentBorderBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private string _accentColorHex = "#0078D4";
    [ObservableProperty] private ImageSource? _groupIcon;

    partial void OnViewModeChanged(GroupViewMode value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnPositionXChanged(double value)
    {
        if (_isInitializing) return;

        // DON'T save off-screen positions (from hide/show transitions)
        if (value < -5000 || value > 10000)
        {
            System.Diagnostics.Debug.WriteLine($"OnPositionXChanged: Skipping save for off-screen X={value}");
            return;
        }

        SyncToModel();
    }

    partial void OnPositionYChanged(double value)
    {
        if (_isInitializing) return;

        // DON'T save off-screen positions (from hide/show transitions)
        if (value < -5000 || value > 10000)
        {
            System.Diagnostics.Debug.WriteLine($"OnPositionYChanged: Skipping save for off-screen Y={value}");
            return;
        }

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

    partial void OnGridViewWidthChanged(double value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnGridViewHeightChanged(double value)
    {
        if (_isInitializing) return;
        SyncToModel();
    }

    partial void OnAccentColorHexChanged(string value)
    {
        try
        {
            ApplyAccentColor(value);
            _model.AccentColor = value;
            if (string.IsNullOrEmpty(_model.CustomIconPath))
                LoadGroupIcon();
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
        PersistenceService persistenceService, WindowPositionHelper positionHelper,
        AppSettings settings)
    {
        _model = model; _usageTracker = usageTracker; _iconExtractor = iconExtractor;
        _desktopIconService = desktopIconService; _persistenceService = persistenceService;
        _positionHelper = positionHelper;
        Settings = settings;

        System.Diagnostics.Debug.WriteLine($"GroupViewModel.Constructor: Creating '{model.Name}' with saved position ({model.PositionX},{model.PositionY}), ViewMode={model.ViewMode}, DockMode={model.DockMode}");

        // VALIDATE AND SANITIZE loaded positions to prevent billion-digit corruption
        if (model.PositionX < -5000 || model.PositionX > 10000)
        {
            System.Diagnostics.Debug.WriteLine($"  WARNING: Corrupted PositionX={model.PositionX} detected! Resetting to 100");
            model.PositionX = 100;
        }

        if (model.PositionY < -5000 || model.PositionY > 10000)
        {
            System.Diagnostics.Debug.WriteLine($"  WARNING: Corrupted PositionY={model.PositionY} detected! Resetting to 100");
            model.PositionY = 100;
        }

        Name = model.Name; 
        IsPinned = model.IsPinned; SnapToGrid = model.SnapToGrid;
        PositionX = model.PositionX; PositionY = model.PositionY;
        Width = model.Width; Height = model.Height;
        GridViewWidth = model.GridViewWidth > 0 ? model.GridViewWidth : 400;
        GridViewHeight = model.GridViewHeight > 0 ? model.GridViewHeight : 300;
        MonitorDeviceName = model.MonitorDeviceName;
        IsVisible = model.IsVisible; AccentColorHex = model.AccentColor;
        DockMode = model.DockMode;
        ViewMode = model.ViewMode; // Set ViewMode LAST so SyncToModel doesn't overwrite positions

        // Load per-group overrides BEFORE ApplyAccentColor so EffectiveOpacity is correct
        PerGroupIconSize = model.PerGroupIconSize;
        PerGroupOpacity = model.PerGroupOpacity;

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
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var newBrush = new SolidColorBrush(color);
            newBrush.Freeze();
            AccentBrush = newBrush;

            // Also create transparent version
            UpdateTransparentAccentBrush();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"  ERROR: {ex.Message}");
            var fallbackBrush = new SolidColorBrush(Colors.DodgerBlue);
            fallbackBrush.Freeze();
            AccentBrush = fallbackBrush;
            UpdateTransparentAccentBrush();
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
                return;
            }
        }

        // Use DeskGroup app icon as default group icon
        GroupIcon = LoadDefaultDeskGroupIcon() ?? CreateGroupLetterIcon(Name, AccentColorHex);
        System.Diagnostics.Debug.WriteLine($"LoadGroupIcon: Using default DeskGroup icon for '{Name}'");
    }

    private static ImageSource? LoadDefaultDeskGroupIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/dg_icon.png", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo == null) return null;
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = streamInfo.Stream;
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 48;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
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
        var topItems = _usageTracker.GetTopUsedWithFallback(_model, 12); // Changed from 10 to 12 for Compact view (2 rows x 6 icons)
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
        _model.GridViewWidth = GridViewWidth; _model.GridViewHeight = GridViewHeight;
        _model.MonitorDeviceName = MonitorDeviceName;
        _model.IsVisible = IsVisible; _model.AccentColor = AccentColorHex;
        _model.PerGroupIconSize = PerGroupIconSize;
        _model.PerGroupOpacity = PerGroupOpacity;
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

    [RelayCommand] private void SetCompact()
    {
        if (ViewMode == GroupViewMode.Fullscreen) RestoreFromFullscreen();
        ViewMode = GroupViewMode.Compact;
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
        _preFullscreenW = ViewMode == GroupViewMode.Grid ? GridViewWidth : Width;
        _preFullscreenH = ViewMode == GroupViewMode.Grid ? GridViewHeight : Height;

        var center = new Point(PositionX + _preFullscreenW / 2, PositionY + _preFullscreenH / 2);
        var fsRect = _positionHelper.GetFullscreenRect(center);
        ViewMode = GroupViewMode.Fullscreen;
        PositionX = fsRect.X; PositionY = fsRect.Y;
        if (_preFullscreenViewMode == GroupViewMode.Grid)
        {
            GridViewWidth = fsRect.Width; GridViewHeight = fsRect.Height;
        }
        else
        {
            Width = fsRect.Width; Height = fsRect.Height;
        }
    }

    private void RestoreFromFullscreen()
    {
        PositionX = _preFullscreenX; PositionY = _preFullscreenY;
        if (_preFullscreenViewMode == GroupViewMode.Grid)
        {
            GridViewWidth = _preFullscreenW; GridViewHeight = _preFullscreenH;
        }
        else
        {
            Width = _preFullscreenW; Height = _preFullscreenH;
        }
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

    public void MoveShortcut(ShortcutItemViewModel source, ShortcutItemViewModel target)
    {
        int si = AllShortcuts.IndexOf(source);
        int ti = AllShortcuts.IndexOf(target);
        if (si < 0 || ti < 0 || si == ti) return;
        AllShortcuts.Move(si, ti);
        var srcModel = source.Model;
        _model.Shortcuts.Remove(srcModel);
        _model.Shortcuts.Insert(ti, srcModel);
        for (int i = 0; i < _model.Shortcuts.Count; i++)
            _model.Shortcuts[i].SortOrder = i;
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

    [RelayCommand]
    private void ToggleDockMode()
    {
        var oldMode = DockMode;
        var newMode = DockMode == GroupDockMode.Sticky ? GroupDockMode.Float : GroupDockMode.Sticky;
        System.Diagnostics.Debug.WriteLine($"ToggleDockMode: {oldMode} → {newMode} for '{Name}'");
        DockMode = newMode;
    }

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

    /// <summary>
    /// Moves a shortcut from this group into <paramref name="destination"/> without touching
    /// the desktop. The OriginalIconState ownership transfers with it so desktop restore
    /// continues to work correctly on the new group.
    /// </summary>
    public void TransferShortcutToGroup(ShortcutItemViewModel vm, GroupViewModel destination)
    {
        // Transfer the OriginalIconState so desktop-restore still knows where the .lnk lives
        var originalState = _model.OriginalIconStates.FirstOrDefault(s => s.ShortcutItemId == vm.Model.Id);
        if (originalState != null)
        {
            _model.OriginalIconStates.Remove(originalState);
            destination.Model.OriginalIconStates.Add(originalState);
        }

        // Remove from source (no desktop restore — file stays in HiddenIcons storage)
        _model.Shortcuts.Remove(vm.Model);
        AllShortcuts.Remove(vm);
        for (int i = 0; i < _model.Shortcuts.Count; i++)
            _model.Shortcuts[i].SortOrder = i;
        RefreshTopUsed();

        // Add to destination (no desktop hide — already hidden or not on desktop)
        vm.Model.SortOrder = destination.Model.Shortcuts.Count;
        destination.Model.Shortcuts.Add(vm.Model);
        destination.AllShortcuts.Add(vm);
        destination.RefreshTopUsed();
    }
}

