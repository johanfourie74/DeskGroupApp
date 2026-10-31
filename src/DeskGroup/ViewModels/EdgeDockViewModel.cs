using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class EdgeDockViewModel : ObservableObject
{
    private readonly EdgeDockConfig _config;
    private readonly UsageTrackingService _usageTracker;
    private readonly IconExtractorService _iconExtractor;
    private readonly DesktopIconService _desktopIconService;
    private readonly PersistenceService _persistenceService;
    private bool _isInitializing = true;
    private bool _suppressAutoSave;
    private DateTime _lastHoverOpenTime = DateTime.MinValue;

    public AppSettings Settings { get; }
    public MonitorInfo TargetMonitor { get; private set; }
    public EdgeDockConfig Config => _config;
    public Guid Id => _config.Id;

    [ObservableProperty] private string _name = "Edge Dock";
    [ObservableProperty] private DockEdge _edge = DockEdge.Left;
    [ObservableProperty] private SideDockLockState _lockState = SideDockLockState.AutoHide;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _showOnEdgeHover = true;
    [ObservableProperty] private bool _showBubbleButton = true;
    [ObservableProperty] private int _bubbleSize = 16;
    [ObservableProperty] private double _edgePositionRatio = 0.5;
    [ObservableProperty] private double _bubblePositionRatio = -1.0;
    [ObservableProperty] private DockEdge? _bubbleEdgeOverride;
    [ObservableProperty] private int _iconColumns = 1;
    [ObservableProperty] private double _panelThickness = 100;
    [ObservableProperty] private double _panelLength = 360;
    [ObservableProperty] private int _perDockIconSize = 0;
    [ObservableProperty] private double _perDockOpacity = -1.0;
    [ObservableProperty] private bool _isPanelOpen;
    [ObservableProperty] private bool _isEnabled = true;

    [ObservableProperty] private SolidColorBrush _accentBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private SolidColorBrush _transparentAccentBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private SolidColorBrush _transparentBorderBrush = new(Colors.DodgerBlue);
    [ObservableProperty] private string _accentColorHex = "#0078D4";
    [ObservableProperty] private ImageSource? _dockIcon;

    public ObservableCollection<ShortcutItemViewModel> AllShortcuts { get; } = new();

    public event EventHandler? SaveRequested;
    public event EventHandler? LayoutChanged;

    public int EffectiveIconSize =>
        PerDockIconSize > 0 ? PerDockIconSize :
        (Settings.DefaultEdgeDockIconSize > 0 ? Settings.DefaultEdgeDockIconSize :
        (Settings.IconSizeMode == IconSizeMode.Custom ? Settings.CustomIconSize : 48));

    public int EffectiveListIconSize => Math.Clamp(EffectiveIconSize / 2, 16, 32);
    public double EffectiveListTileHeight => EffectiveListIconSize + 8;

    public double EffectiveOpacity =>
        PerDockOpacity >= 0.0 ? PerDockOpacity : Settings.GroupBackgroundOpacity;

    // Proxy read/write properties for PerGroupAppearanceDialog TwoWay slider binding
    public int DisplayIconSize
    {
        get => EffectiveIconSize;
        set { PerDockIconSize = value; OnPropertyChanged(nameof(DisplayIconSize)); }
    }

    public double DisplayOpacity
    {
        get => EffectiveOpacity;
        set { PerDockOpacity = value; OnPropertyChanged(nameof(DisplayOpacity)); }
    }

    public bool IsVerticalEdge => Edge == DockEdge.Left || Edge == DockEdge.Right;
    public bool IsHorizontalEdge => Edge == DockEdge.Top || Edge == DockEdge.Bottom;

    // Effective bubble edge & position (allows bubble to be moved independently to any least-obstructive spot)
    public DockEdge EffectiveBubbleEdge => BubbleEdgeOverride ?? Edge;
    public double EffectiveBubbleRatio => BubblePositionRatio >= 0.0
        ? Math.Clamp(BubblePositionRatio, 0.02, 0.98)
        : Math.Clamp(EdgePositionRatio, 0.05, 0.95);

    // Column width & snapped thickness calculations (1 icon wide default, no horizontal scrolling)
    public double SingleIconColumnWidth => Math.Max(EffectiveIconSize + 24, 68) + 8; // TileWidth + 8px margin
    public double SingleIconRowHeight => (EffectiveIconSize + 40) + 8; // TileHeight + 8px margin

    public bool IsSingleColumn => IsVerticalEdge && IconColumns <= 1;
    public bool IsMultiColumn => !IsSingleColumn;

    public double CalculateSnappedWidthForColumns(int columns)
    {
        int cols = Math.Clamp(columns, 1, 12);
        // BorderThickness=2 (4px) + Padding=6 (12px) = 16px chrome
        return (cols * SingleIconColumnWidth) + 16;
    }

    public double CalculateSnappedHeightForRows(int rows)
    {
        int r = Math.Clamp(rows, 1, 12);
        // Header (~36px) + Footer (~28px) + Chrome (16px) = 80px
        return (r * SingleIconRowHeight) + 80;
    }

    public int SnapWidthToColumns(double rawWidth)
    {
        double colW = SingleIconColumnWidth;
        int cols = (int)Math.Round((rawWidth - 16) / Math.Max(1, colW));
        return Math.Clamp(cols, 1, 12);
    }

    public string LockStateText => LockState switch
    {
        SideDockLockState.AutoHide => "Auto-Hide",
        SideDockLockState.LockedOpen => "Locked Open",
        SideDockLockState.LockedClosed => "Locked Closed",
        _ => "Auto-Hide"
    };

    public string PinIconText => IsPinned ? "📌" : "📍";
    public string LockIconText => LockState switch
    {
        SideDockLockState.LockedOpen => "🔒",
        SideDockLockState.LockedClosed => "⛔",
        _ => "🔓"
    };

    public EdgeDockViewModel(
        EdgeDockConfig config,
        MonitorInfo targetMonitor,
        UsageTrackingService usageTracker,
        IconExtractorService iconExtractor,
        DesktopIconService desktopIconService,
        PersistenceService persistenceService,
        AppSettings settings)
    {
        _config = config;
        TargetMonitor = targetMonitor;
        _usageTracker = usageTracker;
        _iconExtractor = iconExtractor;
        _desktopIconService = desktopIconService;
        _persistenceService = persistenceService;
        Settings = settings;

        Name = config.Name;
        Edge = config.Edge;
        LockState = config.LockState;
        IsPinned = config.IsPinned;
        ShowOnEdgeHover = config.ShowOnEdgeHover;
        ShowBubbleButton = config.ShowBubbleButton;
        BubbleSize = Math.Clamp(config.BubbleSize, 12, 48);
        EdgePositionRatio = Math.Clamp(config.EdgePositionRatio, 0.05, 0.95);
        BubblePositionRatio = config.BubblePositionRatio;
        BubbleEdgeOverride = config.BubbleEdgeOverride;
        PerDockIconSize = config.PerDockIconSize;
        PerDockOpacity = config.PerDockOpacity;
        IconColumns = Math.Clamp(config.IconColumns <= 0 ? 1 : config.IconColumns, 1, 12);
        PanelThickness = IsVerticalEdge
            ? CalculateSnappedWidthForColumns(IconColumns)
            : CalculateSnappedHeightForRows(IconColumns);
        PanelLength = Math.Clamp(config.PanelLength, 140, 1400);
        IsEnabled = config.IsEnabled;
        AccentColorHex = string.IsNullOrWhiteSpace(config.AccentColor) ? "#0078D4" : config.AccentColor;

        IsPanelOpen = LockState == SideDockLockState.LockedOpen;

        ApplyAccentColor(AccentColorHex);
        LoadDockIcon();
        LoadShortcuts();

        _isInitializing = false;
    }

    public void UpdateTargetMonitor(MonitorInfo monitor)
    {
        TargetMonitor = monitor;
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value)
    {
        if (_isInitializing) return;
        if (string.IsNullOrEmpty(_config.CustomIconPath)) LoadDockIcon();
        SyncToConfig();
    }

    partial void OnEdgeChanged(DockEdge value)
    {
        if (_isInitializing) return;
        OnPropertyChanged(nameof(IsVerticalEdge));
        OnPropertyChanged(nameof(IsHorizontalEdge));
        OnPropertyChanged(nameof(EffectiveBubbleEdge));
        OnPropertyChanged(nameof(IsSingleColumn));
        OnPropertyChanged(nameof(IsMultiColumn));
        RecalculateSnappedThickness();
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnLockStateChanged(SideDockLockState value)
    {
        OnPropertyChanged(nameof(LockStateText));
        OnPropertyChanged(nameof(LockIconText));
        if (value == SideDockLockState.LockedOpen)
            IsPanelOpen = true;
        else if (value == SideDockLockState.LockedClosed)
            IsPanelOpen = false;

        if (_isInitializing) return;
        SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(PinIconText));
        if (_isInitializing) return;
        SyncToConfig();
    }

    partial void OnShowOnEdgeHoverChanged(bool value)
    {
        if (_isInitializing) return;
        SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnShowBubbleButtonChanged(bool value)
    {
        if (_isInitializing) return;
        SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnBubbleSizeChanged(int value)
    {
        if (_isInitializing) return;
        SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEdgePositionRatioChanged(double value)
    {
        if (_isInitializing) return;
        OnPropertyChanged(nameof(EffectiveBubbleRatio));
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnBubblePositionRatioChanged(double value)
    {
        if (_isInitializing) return;
        OnPropertyChanged(nameof(EffectiveBubbleRatio));
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnBubbleEdgeOverrideChanged(DockEdge? value)
    {
        if (_isInitializing) return;
        OnPropertyChanged(nameof(EffectiveBubbleEdge));
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIconColumnsChanged(int value)
    {
        OnPropertyChanged(nameof(IsSingleColumn));
        OnPropertyChanged(nameof(IsMultiColumn));
        if (_isInitializing) return;
        RecalculateSnappedThickness();
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPanelThicknessChanged(double value)
    {
        if (_isInitializing) return;
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPanelLengthChanged(double value)
    {
        if (_isInitializing) return;
        if (!_suppressAutoSave) SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPerDockIconSizeChanged(int value)
    {
        if (_isInitializing) return;
        RefreshIconSize();
        SyncToConfig();
    }

    partial void OnPerDockOpacityChanged(double value)
    {
        if (_isInitializing) return;
        RefreshOpacity();
        SyncToConfig();
    }

    partial void OnAccentColorHexChanged(string value)
    {
        ApplyAccentColor(value);
        if (_isInitializing) return;
        if (string.IsNullOrEmpty(_config.CustomIconPath)) LoadDockIcon();
        SyncToConfig();
    }

    public void SetSuppressAutoSave(bool suppress)
    {
        _suppressAutoSave = suppress;
    }

    public void RecalculateSnappedThickness()
    {
        bool prev = _suppressAutoSave;
        _suppressAutoSave = true;
        PanelThickness = IsVerticalEdge
            ? CalculateSnappedWidthForColumns(IconColumns)
            : CalculateSnappedHeightForRows(IconColumns);
        _suppressAutoSave = prev;
    }

    public void RefreshOpacity()
    {
        OnPropertyChanged(nameof(EffectiveOpacity));
        OnPropertyChanged(nameof(DisplayOpacity));
        UpdateTransparentAccentBrush();
    }

    public void RefreshIconSize()
    {
        OnPropertyChanged(nameof(EffectiveIconSize));
        OnPropertyChanged(nameof(EffectiveListIconSize));
        OnPropertyChanged(nameof(EffectiveListTileHeight));
        OnPropertyChanged(nameof(DisplayIconSize));
        OnPropertyChanged(nameof(SingleIconColumnWidth));
        OnPropertyChanged(nameof(SingleIconRowHeight));
        RecalculateSnappedThickness();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetDockAppearance()
    {
        PerDockIconSize = 0;
        PerDockOpacity = -1.0;
    }

    [RelayCommand]
    public void SetIconSize(int sizePx)
    {
        PerDockIconSize = sizePx <= 0 ? 0 : Math.Clamp(sizePx, 16, 96);
    }

    public void SyncIconSizeFromConfig()
    {
        bool prev = _isInitializing;
        _isInitializing = true;
        PerDockIconSize = _config.PerDockIconSize;
        _isInitializing = prev;
        RefreshIconSize();
    }

    [RelayCommand]
    public void SetDockIcon(string iconPath)
    {
        _config.CustomIconPath = iconPath;
        LoadDockIcon();
        SyncToConfig();
    }

    private void ApplyAccentColor(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var newBrush = new SolidColorBrush(color);
            newBrush.Freeze();
            AccentBrush = newBrush;
            UpdateTransparentAccentBrush();
        }
        catch
        {
            var fallback = new SolidColorBrush(Colors.DodgerBlue);
            fallback.Freeze();
            AccentBrush = fallback;
            UpdateTransparentAccentBrush();
        }
    }

    private void UpdateTransparentAccentBrush()
    {
        var color = AccentBrush.Color;
        // Clamp minimum alpha to 1 (out of 255) so 0% transparency is visually transparent
        // but NEVER allows accidental mouse click-through to windows beneath the panel!
        var alpha = Math.Max((byte)1, (byte)(Math.Clamp(EffectiveOpacity, 0.0, 1.0) * 255));
        var tb = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        tb.Freeze();
        TransparentAccentBrush = tb;

        var borderOpacity = (Math.Clamp(EffectiveOpacity, 0.0, 1.0) * 0.8) + 0.2;
        var borderAlpha = Math.Max((byte)1, (byte)(borderOpacity * 255));
        var bb = new SolidColorBrush(Color.FromArgb(borderAlpha, color.R, color.G, color.B));
        bb.Freeze();
        TransparentBorderBrush = bb;
    }

    private void LoadDockIcon()
    {
        if (!string.IsNullOrEmpty(_config.CustomIconPath) && System.IO.File.Exists(_config.CustomIconPath))
        {
            DockIcon = _iconExtractor.ExtractLargeIcon(_config.CustomIconPath);
            if (DockIcon != null) return;
        }

        try
        {
            var uri = new Uri("pack://application:,,,/Assets/dg_icon.png", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = streamInfo.Stream;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 48;
                bitmap.EndInit();
                bitmap.Freeze();
                DockIcon = bitmap;
            }
        }
        catch { }
    }

    public void LoadShortcuts()
    {
        AllShortcuts.Clear();
        foreach (var shortcut in _config.Shortcuts.OrderBy(s => s.SortOrder))
            AllShortcuts.Add(new ShortcutItemViewModel(shortcut, _usageTracker, _iconExtractor));
    }

    public void SyncToConfig()
    {
        _config.Name = Name;
        _config.Edge = Edge;
        _config.LockState = LockState;
        _config.IsPinned = IsPinned;
        _config.ShowOnEdgeHover = ShowOnEdgeHover;
        _config.ShowBubbleButton = ShowBubbleButton;
        _config.BubbleSize = BubbleSize;
        _config.EdgePositionRatio = EdgePositionRatio;
        _config.BubblePositionRatio = BubblePositionRatio;
        _config.BubbleEdgeOverride = BubbleEdgeOverride;
        _config.IconColumns = IconColumns;
        _config.PanelThickness = PanelThickness;
        _config.PanelLength = PanelLength;
        _config.PerDockIconSize = PerDockIconSize;
        _config.PerDockOpacity = PerDockOpacity;
        _config.AccentColor = AccentColorHex;
        _config.IsEnabled = IsEnabled;
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void TogglePanel()
    {
        if (IsPanelOpen && (DateTime.UtcNow - _lastHoverOpenTime).TotalMilliseconds < 650)
        {
            _lastHoverOpenTime = DateTime.MinValue;
            return;
        }
        _lastHoverOpenTime = DateTime.MinValue;
        IsPanelOpen = !IsPanelOpen;
    }

    public void RequestOpenFromHover()
    {
        if (!ShowOnEdgeHover) return;
        if (LockState == SideDockLockState.LockedClosed) return;
        if (!IsPanelOpen)
        {
            _lastHoverOpenTime = DateTime.UtcNow;
            IsPanelOpen = true;
        }
    }

    public void SyncFromSharedConfig()
    {
        _isInitializing = true;
        Name = _config.Name;
        Edge = _config.Edge;
        LockState = _config.LockState;
        IsPinned = _config.IsPinned;
        ShowOnEdgeHover = _config.ShowOnEdgeHover;
        ShowBubbleButton = _config.ShowBubbleButton;
        BubbleSize = Math.Clamp(_config.BubbleSize, 12, 48);
        EdgePositionRatio = Math.Clamp(_config.EdgePositionRatio, 0.05, 0.95);
        BubblePositionRatio = _config.BubblePositionRatio;
        BubbleEdgeOverride = _config.BubbleEdgeOverride;
        PerDockIconSize = _config.PerDockIconSize;
        PerDockOpacity = _config.PerDockOpacity;
        IconColumns = Math.Clamp(_config.IconColumns <= 0 ? 1 : _config.IconColumns, 1, 12);
        PanelThickness = IsVerticalEdge
            ? CalculateSnappedWidthForColumns(IconColumns)
            : CalculateSnappedHeightForRows(IconColumns);
        PanelLength = Math.Clamp(_config.PanelLength, 140, 1400);
        AccentColorHex = string.IsNullOrWhiteSpace(_config.AccentColor) ? "#0078D4" : _config.AccentColor;
        ApplyAccentColor(AccentColorHex);
        LoadShortcuts();
        _isInitializing = false;
        OnPropertyChanged(nameof(IsVerticalEdge));
        OnPropertyChanged(nameof(IsHorizontalEdge));
        OnPropertyChanged(nameof(EffectiveBubbleEdge));
        OnPropertyChanged(nameof(EffectiveBubbleRatio));
        OnPropertyChanged(nameof(IsSingleColumn));
        OnPropertyChanged(nameof(IsMultiColumn));
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestCloseFromHoverLeave()
    {
        if (LockState == SideDockLockState.LockedOpen) return;
        if (LockState == SideDockLockState.AutoHide)
            IsPanelOpen = false;
    }

    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    public void CycleLockState()
    {
        LockState = LockState switch
        {
            SideDockLockState.AutoHide => SideDockLockState.LockedOpen,
            SideDockLockState.LockedOpen => SideDockLockState.LockedClosed,
            _ => SideDockLockState.AutoHide
        };
    }

    [RelayCommand]
    public void SetEdge(DockEdge edge)
    {
        if (IsPinned) return;
        Edge = edge;
    }

    [RelayCommand]
    public void SetIconColumns(int columns)
    {
        IconColumns = Math.Clamp(columns, 1, 12);
    }

    [RelayCommand]
    public void ResetBubblePosition()
    {
        BubblePositionRatio = -1.0;
        BubbleEdgeOverride = null;
        SyncToConfig();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void AddShortcut(string filePath)
    {
        try
        {
            var resolver = new ShortcutResolverService(_iconExtractor);
            var shortcutItem = resolver.Resolve(filePath);
            shortcutItem.SortOrder = _config.Shortcuts.Count;
            _config.Shortcuts.Add(shortcutItem);

            var vm = new ShortcutItemViewModel(shortcutItem, _usageTracker, _iconExtractor);
            AllShortcuts.Add(vm);

            if (Settings.HideGroupedIcons)
            {
                var originalState = _desktopIconService.HideDesktopIcon(shortcutItem);
                if (originalState != null) _config.OriginalIconStates.Add(originalState);
            }

            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to add shortcut to Edge Dock:\n\n{ex.Message}",
                "Add Shortcut Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void RemoveShortcut(ShortcutItemViewModel shortcutVm)
    {
        var model = shortcutVm.Model;
        var originalState = _config.OriginalIconStates.FirstOrDefault(s => s.ShortcutItemId == model.Id);
        if (originalState != null)
        {
            _desktopIconService.RestoreDesktopIcon(originalState);
            _config.OriginalIconStates.Remove(originalState);
        }
        _config.Shortcuts.Remove(model);
        AllShortcuts.Remove(shortcutVm);
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void MoveShortcut(ShortcutItemViewModel source, ShortcutItemViewModel target)
    {
        int ti = AllShortcuts.IndexOf(target);
        if (ti < 0) return;
        MoveShortcutToIndex(source, ti);
    }

    public void MoveShortcutToIndex(ShortcutItemViewModel source, int targetIndex)
    {
        int si = AllShortcuts.IndexOf(source);
        if (si < 0 || AllShortcuts.Count <= 1) return;
        int ti = Math.Clamp(targetIndex, 0, AllShortcuts.Count - 1);
        if (si == ti) return;
        AllShortcuts.Move(si, ti);
        var srcModel = source.Model;
        _config.Shortcuts.Remove(srcModel);
        _config.Shortcuts.Insert(ti, srcModel);
        for (int i = 0; i < _config.Shortcuts.Count; i++)
            _config.Shortcuts[i].SortOrder = i;
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void TransferShortcutFromGroup(ShortcutItemViewModel vm, GroupViewModel sourceGroup)
    {
        var originalState = sourceGroup.Model.OriginalIconStates.FirstOrDefault(s => s.ShortcutItemId == vm.Model.Id);
        if (originalState != null)
        {
            sourceGroup.Model.OriginalIconStates.Remove(originalState);
            _config.OriginalIconStates.Add(originalState);
        }

        sourceGroup.Model.Shortcuts.Remove(vm.Model);
        sourceGroup.AllShortcuts.Remove(vm);
        for (int i = 0; i < sourceGroup.Model.Shortcuts.Count; i++)
            sourceGroup.Model.Shortcuts[i].SortOrder = i;
        sourceGroup.RefreshTopUsed();

        vm.Model.SortOrder = _config.Shortcuts.Count;
        _config.Shortcuts.Add(vm.Model);
        AllShortcuts.Add(vm);
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void TransferShortcutToGroup(ShortcutItemViewModel vm, GroupViewModel destGroup)
    {
        var originalState = _config.OriginalIconStates.FirstOrDefault(s => s.ShortcutItemId == vm.Model.Id);
        if (originalState != null)
        {
            _config.OriginalIconStates.Remove(originalState);
            destGroup.Model.OriginalIconStates.Add(originalState);
        }

        _config.Shortcuts.Remove(vm.Model);
        AllShortcuts.Remove(vm);
        for (int i = 0; i < _config.Shortcuts.Count; i++)
            _config.Shortcuts[i].SortOrder = i;

        vm.Model.SortOrder = destGroup.Model.Shortcuts.Count;
        destGroup.Model.Shortcuts.Add(vm.Model);
        destGroup.AllShortcuts.Add(vm);
        destGroup.RefreshTopUsed();
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void TransferShortcutFromDock(ShortcutItemViewModel vm, EdgeDockViewModel sourceDock)
    {
        var originalState = sourceDock.Config.OriginalIconStates.FirstOrDefault(s => s.ShortcutItemId == vm.Model.Id);
        if (originalState != null)
        {
            sourceDock.Config.OriginalIconStates.Remove(originalState);
            _config.OriginalIconStates.Add(originalState);
        }

        sourceDock.Config.Shortcuts.Remove(vm.Model);
        sourceDock.AllShortcuts.Remove(vm);
        for (int i = 0; i < sourceDock.Config.Shortcuts.Count; i++)
            sourceDock.Config.Shortcuts[i].SortOrder = i;

        vm.Model.SortOrder = _config.Shortcuts.Count;
        _config.Shortcuts.Add(vm.Model);
        AllShortcuts.Add(vm);
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }
}
