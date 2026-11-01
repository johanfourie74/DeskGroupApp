using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DeskGroup.Models;
using DeskGroup.ViewModels;
using DeskGroup.Views;

namespace DeskGroup.Controls;

public partial class IconTile : UserControl
{
    private ToolTip? _hoverToolTip;
    private DispatcherTimer? _toolTipDelayTimer;
    public const int ToolTipShowDelayMs = 450;

    public IconTile()
    {
        InitializeComponent();
        Unloaded += (_, _) => HideHoverToolTip();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) HideHoverToolTip();
        };
    }

    // Grid mouse event handlers (triggered by XAML)
    private void Grid_MouseEnter(object sender, MouseEventArgs e)
    {
        try
        {
            var hoverInStoryboard = (Storyboard?)Resources["HoverIn"];
            hoverInStoryboard?.Begin(this);

            if (!ShowLabel && !string.IsNullOrWhiteSpace(DisplayName) && !IsDraggingShortcut)
            {
                StartHoverToolTipTimer();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Grid_MouseEnter ERROR: {ex.Message}");
        }
    }

    private void Grid_MouseLeave(object sender, MouseEventArgs e)
    {
        try
        {
            HideHoverToolTip();
            var hoverOutStoryboard = (Storyboard?)Resources["HoverOut"];
            hoverOutStoryboard?.Begin(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Grid_MouseLeave ERROR: {ex.Message}");
        }
    }

    private void StartHoverToolTipTimer()
    {
        if (_toolTipDelayTimer == null)
        {
            _toolTipDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ToolTipShowDelayMs)
            };
            _toolTipDelayTimer.Tick += OnToolTipDelayTimerTick;
        }
        _toolTipDelayTimer.Stop();
        _toolTipDelayTimer.Start();
    }

    private void OnToolTipDelayTimerTick(object? sender, EventArgs e)
    {
        _toolTipDelayTimer?.Stop();

        if (!IsMouseOver || ShowLabel || IsDraggingShortcut || !IsVisible || string.IsNullOrWhiteSpace(DisplayName))
            return;

        _hoverToolTip ??= new ToolTip
        {
            IsHitTestVisible = false,
            HasDropShadow = true
        };

        _hoverToolTip.Content = DisplayName;
        _hoverToolTip.PlacementTarget = this;

        var parentWin = Window.GetWindow(this);
        if (parentWin is EdgeDockPanelWindow dockWin && dockWin.DataContext is EdgeDockViewModel dockVm)
        {
            switch (dockVm.Edge)
            {
                case DockEdge.Left:
                    _hoverToolTip.Placement = PlacementMode.Right;
                    _hoverToolTip.HorizontalOffset = 6;
                    _hoverToolTip.VerticalOffset = 0;
                    break;
                case DockEdge.Right:
                    _hoverToolTip.Placement = PlacementMode.Left;
                    _hoverToolTip.HorizontalOffset = -6;
                    _hoverToolTip.VerticalOffset = 0;
                    break;
                case DockEdge.Top:
                    _hoverToolTip.Placement = PlacementMode.Bottom;
                    _hoverToolTip.HorizontalOffset = 0;
                    _hoverToolTip.VerticalOffset = 6;
                    break;
                case DockEdge.Bottom:
                    _hoverToolTip.Placement = PlacementMode.Top;
                    _hoverToolTip.HorizontalOffset = 0;
                    _hoverToolTip.VerticalOffset = -6;
                    break;
            }
        }
        else
        {
            _hoverToolTip.Placement = PlacementMode.Top;
            _hoverToolTip.HorizontalOffset = 0;
            _hoverToolTip.VerticalOffset = -4;
        }

        _hoverToolTip.IsOpen = true;
    }

    public void HideHoverToolTip()
    {
        _toolTipDelayTimer?.Stop();
        if (_hoverToolTip != null && _hoverToolTip.IsOpen)
        {
            _hoverToolTip.IsOpen = false;
        }
    }

    // Drag and drop

    private Point _mouseDownPos;
    private bool _hasMouseDown;

    /// <summary>
    /// True while a shortcut tile is actively being dragged via OLE DragDrop.
    /// Prevents EdgeDock auto-hide timers from closing the dock mid-drag.
    /// </summary>
    public static bool IsDraggingShortcut { get; private set; }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        HideHoverToolTip();

        if (e.ClickCount == 2)
        {
            _hasMouseDown = false;
            ExecuteLaunch();
            e.Handled = true;
            return;
        }

        _mouseDownPos = e.GetPosition(this);
        _hasMouseDown = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HideHoverToolTip();
        if (e.LeftButton != MouseButtonState.Pressed)
            _hasMouseDown = false;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!_hasMouseDown || e.LeftButton != MouseButtonState.Pressed)
        {
            _hasMouseDown = false;
            return;
        }
        if (Mouse.Captured != null) return; // parent window is being dragged

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _mouseDownPos.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(pos.Y - _mouseDownPos.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _hasMouseDown = false;
            HideHoverToolTip();
            if (DataContext is ShortcutItemViewModel vm)
            {
                DragDropEffects result = DragDropEffects.None;
                try
                {
                    IsDraggingShortcut = true;
                    result = DragDrop.DoDragDrop(this, new DataObject("DeskGroupShortcut", vm),
                        DragDropEffects.Move | DragDropEffects.None);
                }
                finally
                {
                    IsDraggingShortcut = false;
                }

                if (result == DragDropEffects.None)
                {
                    // Dropped outside any group — remove from GroupWidget (EdgeDock uses right-click Remove to prevent accidental slip-deletion on narrow edge docks)
                    if (Window.GetWindow(this) is GroupWidget widget)
                        widget.RemoveShortcutByViewModel(vm);
                }
            }
        }
    }

    // Icon Source

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(IconTile),
            new PropertyMetadata(null));

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // Display Name

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(IconTile),
            new PropertyMetadata("Shortcut"));

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    // Icon Size

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(IconTile),
            new PropertyMetadata(48.0, OnIconSizeChanged));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconTile tile)
        {
            tile.UpdateTileSize();
        }
    }

    // Tile Width (auto-calculated from icon size, or manually set)

    public static readonly DependencyProperty TileWidthProperty =
        DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(IconTile),
            new PropertyMetadata(double.NaN)); // Changed from 72.0 to NaN (means "not set")

    public double TileWidth
    {
        get => (double)GetValue(TileWidthProperty);
        set => SetValue(TileWidthProperty, value);
    }

    // Tile Height (auto-calculated from icon size, or manually set)

    public static readonly DependencyProperty TileHeightProperty =
        DependencyProperty.Register(nameof(TileHeight), typeof(double), typeof(IconTile),
            new PropertyMetadata(88.0));

    public double TileHeight
    {
        get => (double)GetValue(TileHeightProperty);
        set => SetValue(TileHeightProperty, value);
    }

    // Accent Color (for hover border)

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Brush), typeof(IconTile),
            new PropertyMetadata(null));

    public Brush? AccentColor
    {
        get => (Brush?)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    // Layout Orientation (for horizontal vs vertical layout)

    public static readonly DependencyProperty LayoutOrientationProperty =
        DependencyProperty.Register(nameof(LayoutOrientation), typeof(Orientation), typeof(IconTile),
            new PropertyMetadata(Orientation.Vertical));

    public Orientation LayoutOrientation
    {
        get => (Orientation)GetValue(LayoutOrientationProperty);
        set => SetValue(LayoutOrientationProperty, value);
    }

    // Show Label (for hiding text in icon-only views like Compact & Edge Dock)

    public static readonly DependencyProperty ShowLabelProperty =
        DependencyProperty.Register(nameof(ShowLabel), typeof(bool), typeof(IconTile),
            new PropertyMetadata(true, OnShowLabelChanged)); // Default: show label

    public bool ShowLabel
    {
        get => (bool)GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    private static void OnShowLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconTile tile)
        {
            tile.UpdateTileSize();
            if ((bool)e.NewValue)
                tile.HideHoverToolTip();
        }
    }

    // Launch Command (double-click)

    public static readonly DependencyProperty LaunchCommandProperty =
        DependencyProperty.Register(nameof(LaunchCommand), typeof(ICommand), typeof(IconTile),
            new PropertyMetadata(null));

    public ICommand? LaunchCommand
    {
        get => (ICommand?)GetValue(LaunchCommandProperty);
        set => SetValue(LaunchCommandProperty, value);
    }

    // Select Command (single click)

    public static readonly DependencyProperty SelectCommandProperty =
        DependencyProperty.Register(nameof(SelectCommand), typeof(ICommand), typeof(IconTile),
            new PropertyMetadata(null));

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    // IsSelected

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(IconTile),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconTile tile)
        {
            tile.UpdateSelectionVisual();
        }
    }

    // Size Calculation

    private void UpdateTileSize()
    {
        if (ShowLabel)
        {
            TileWidth = IconSize + 24;
            TileHeight = IconSize + 40;
        }
        else
        {
            TileWidth = IconSize + 8;
            TileHeight = IconSize + 8;
        }
    }

    private void UpdateSelectionVisual()
    {
        if (IsSelected)
        {
            HoverBorder.Opacity = 1;
            HoverBorder.Background = (Brush)FindResource("AccentPrimary");
            HoverBorder.Opacity = 0.2;
        }
        else
        {
            HoverBorder.Opacity = 0;
            HoverBorder.Background = (Brush)FindResource("BackgroundTertiary");
        }
    }

    // Context Menu (right-click)

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        HideHoverToolTip();

        var contextMenu = new ContextMenu
        {
            PlacementTarget = this
        };

        var launchItem = new MenuItem { Header = "Open" };
        launchItem.Click += (s, args) => ExecuteLaunch();
        contextMenu.Items.Add(launchItem);

        if (DataContext is ShortcutItemViewModel shortcutVm)
        {
            var targetPath = !string.IsNullOrEmpty(shortcutVm.Model.TargetPath)
                ? shortcutVm.Model.TargetPath
                : shortcutVm.Model.OriginalShortcutPath;
            if (!string.IsNullOrEmpty(targetPath) && (File.Exists(targetPath) || Directory.Exists(targetPath)))
            {
                var openLocItem = new MenuItem { Header = "Open file location" };
                openLocItem.Click += (s, args) =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{targetPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                };
                contextMenu.Items.Add(openLocItem);
            }

            var refreshIconItem = new MenuItem { Header = "Refresh icon" };
            refreshIconItem.Click += (s, args) => shortcutVm.RefreshIconCommand.Execute(null);
            contextMenu.Items.Add(refreshIconItem);
        }

        contextMenu.Items.Add(new Separator());

        var removeItem = new MenuItem { Header = "Remove from group" };
        removeItem.Click += (s, args) =>
        {
            RaiseEvent(new RemoveShortcutRoutedEventArgs(RemoveShortcutEvent, this));
        };
        contextMenu.Items.Add(removeItem);

        var parentWin = Window.GetWindow(this);
        if (parentWin is EdgeDockPanelWindow dockWin && dockWin.DataContext is EdgeDockViewModel dockVm)
        {
            contextMenu.Items.Add(new Separator());
            var iconSizeMenu = new MenuItem { Header = "📏 Icon Size" };

            var sliderItem = new MenuItem { Header = "🎛️ Icon Size & Opacity Slider..." };
            sliderItem.Click += (s, args) => dockWin.ShowAppearanceDialog();
            iconSizeMenu.Items.Add(sliderItem);
            iconSizeMenu.Items.Add(new Separator());

            int[] sizes = { 16, 24, 32, 40, 48, 64, 72, 96 };
            foreach (var sz in sizes)
            {
                int captured = sz;
                var szItem = new MenuItem
                {
                    Header = $"{captured} px{(captured == 48 ? " (Default)" : "")}",
                    IsCheckable = true,
                    IsChecked = dockVm.EffectiveIconSize == captured
                };
                szItem.Click += (s, args) => dockVm.SetIconSize(captured);
                iconSizeMenu.Items.Add(szItem);
            }

            iconSizeMenu.Items.Add(new Separator());
            var globalItem = new MenuItem
            {
                Header = "↩️ Follow Global Icon Size",
                IsCheckable = true,
                IsChecked = dockVm.PerDockIconSize == 0
            };
            globalItem.Click += (s, args) => dockVm.SetIconSize(0);
            iconSizeMenu.Items.Add(globalItem);

            contextMenu.Items.Add(iconSizeMenu);
        }
        else if (parentWin is GroupWidget groupWin && groupWin.DataContext is GroupViewModel groupVm)
        {
            contextMenu.Items.Add(new Separator());
            var iconSizeMenu = new MenuItem { Header = "📏 Icon Size" };

            var sliderItem = new MenuItem { Header = "🎛️ Icon Size & Opacity Slider..." };
            sliderItem.Click += (s, args) =>
            {
                var dialog = new PerGroupAppearanceDialog(groupVm);
                try { if (groupWin.IsLoaded && groupWin.IsVisible) dialog.Owner = groupWin; } catch { }
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Show();
            };
            iconSizeMenu.Items.Add(sliderItem);
            iconSizeMenu.Items.Add(new Separator());

            int[] sizes = { 16, 24, 32, 40, 48, 64, 72, 96 };
            foreach (var sz in sizes)
            {
                int captured = sz;
                var szItem = new MenuItem
                {
                    Header = $"{captured} px{(captured == 48 ? " (Default)" : "")}",
                    IsCheckable = true,
                    IsChecked = groupVm.EffectiveIconSize == captured
                };
                szItem.Click += (s, args) => groupVm.DisplayIconSize = captured;
                iconSizeMenu.Items.Add(szItem);
            }

            iconSizeMenu.Items.Add(new Separator());
            var globalItem = new MenuItem
            {
                Header = "↩️ Follow Global Icon Size",
                IsCheckable = true,
                IsChecked = groupVm.PerGroupIconSize == 0
            };
            globalItem.Click += (s, args) => groupVm.PerGroupIconSize = 0;
            iconSizeMenu.Items.Add(globalItem);

            contextMenu.Items.Add(iconSizeMenu);
        }

        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    // Custom Routed Events: RemoveShortcut & ShortcutLaunched

    public static readonly RoutedEvent RemoveShortcutEvent =
        EventManager.RegisterRoutedEvent("RemoveShortcut", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(IconTile));

    public event RoutedEventHandler RemoveShortcut
    {
        add => AddHandler(RemoveShortcutEvent, value);
        remove => RemoveHandler(RemoveShortcutEvent, value);
    }

    public static readonly RoutedEvent ShortcutLaunchedEvent =
        EventManager.RegisterRoutedEvent("ShortcutLaunched", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(IconTile));

    public event RoutedEventHandler ShortcutLaunched
    {
        add => AddHandler(ShortcutLaunchedEvent, value);
        remove => RemoveHandler(ShortcutLaunchedEvent, value);
    }

    private DateTime _lastLaunchTime = DateTime.MinValue;

    private void ExecuteLaunch()
    {
        if ((DateTime.UtcNow - _lastLaunchTime).TotalMilliseconds < 400)
            return;

        HideHoverToolTip();
        if (LaunchCommand?.CanExecute(null) == true)
        {
            _lastLaunchTime = DateTime.UtcNow;
            LaunchCommand.Execute(null);
            RaiseEvent(new RoutedEventArgs(ShortcutLaunchedEvent, this));
        }
    }

    // Single-Click to Launch

    public static readonly DependencyProperty SingleClickToLaunchProperty =
        DependencyProperty.Register(nameof(SingleClickToLaunch), typeof(bool), typeof(IconTile),
            new PropertyMetadata(false));

    public bool SingleClickToLaunch
    {
        get => (bool)GetValue(SingleClickToLaunchProperty);
        set => SetValue(SingleClickToLaunchProperty, value);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        bool hadMouseDown = _hasMouseDown;
        _hasMouseDown = false;

        if (!hadMouseDown) return;

        var pos = e.GetPosition(this);
        bool isDrag = Math.Abs(pos.X - _mouseDownPos.X) > SystemParameters.MinimumHorizontalDragDistance ||
                      Math.Abs(pos.Y - _mouseDownPos.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (isDrag) return;

        bool shouldSingleClickLaunch = SingleClickToLaunch;
        if (!shouldSingleClickLaunch)
        {
            var parentWin = Window.GetWindow(this);
            if (parentWin is EdgeDockPanelWindow)
            {
                // In Edge Dock panels, clicking an icon launches it and closes the dock
                shouldSingleClickLaunch = true;
            }
            else if (parentWin is GroupWidget gw && gw.DataContext is GroupViewModel gvm)
            {
                // When groups are temporarily floated (Show All) or in Hover view, clicking an icon launches & closes
                if (gvm.SavedDockModeBeforeFloat.HasValue || gvm.ViewMode == GroupViewMode.Hover)
                    shouldSingleClickLaunch = true;
            }
        }

        if (shouldSingleClickLaunch)
        {
            ExecuteLaunch();
            e.Handled = true;
        }
    }
}

public class RemoveShortcutRoutedEventArgs : RoutedEventArgs
{
    public IconTile SourceTile { get; }

    public RemoveShortcutRoutedEventArgs(RoutedEvent routedEvent, IconTile sourceTile)
        : base(routedEvent)
    {
        SourceTile = sourceTile;
    }
}
