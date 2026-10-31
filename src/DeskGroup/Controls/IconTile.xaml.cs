using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskGroup.ViewModels;
using DeskGroup.Views;

namespace DeskGroup.Controls;

public partial class IconTile : UserControl
{
    public IconTile()
    {
        InitializeComponent();
    }

    // Grid mouse event handlers (triggered by XAML)
    private void Grid_MouseEnter(object sender, MouseEventArgs e)
    {
        try
        {
            var hoverInStoryboard = (Storyboard?)Resources["HoverIn"];
            hoverInStoryboard?.Begin(this);
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
            var hoverOutStoryboard = (Storyboard?)Resources["HoverOut"];
            hoverOutStoryboard?.Begin(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Grid_MouseLeave ERROR: {ex.Message}");
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
        _mouseDownPos = e.GetPosition(this);
        _hasMouseDown = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
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

    // Show Label (for hiding text in icon-only views like Compact)

    public static readonly DependencyProperty ShowLabelProperty =
        DependencyProperty.Register(nameof(ShowLabel), typeof(bool), typeof(IconTile),
            new PropertyMetadata(true)); // Default: show label

    public bool ShowLabel
    {
        get => (bool)GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
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
        TileWidth = IconSize + 24;
        TileHeight = IconSize + 40;

        ScaleTransform.CenterX = TileWidth / 2;
        ScaleTransform.CenterY = TileHeight / 2;
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

        var contextMenu = new ContextMenu
        {
            PlacementTarget = this
        };

        var launchItem = new MenuItem { Header = "Open" };
        launchItem.Click += (s, args) => LaunchCommand?.Execute(null);
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

    // Custom Routed Event: RemoveShortcut

    public static readonly RoutedEvent RemoveShortcutEvent =
        EventManager.RegisterRoutedEvent("RemoveShortcut", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(IconTile));

    public event RoutedEventHandler RemoveShortcut
    {
        add => AddHandler(RemoveShortcutEvent, value);
        remove => RemoveHandler(RemoveShortcutEvent, value);
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
        _hasMouseDown = false;

        if (!SingleClickToLaunch) return;

        var pos = e.GetPosition(this);
        bool isDrag = Math.Abs(pos.X - _mouseDownPos.X) > SystemParameters.MinimumHorizontalDragDistance ||
                      Math.Abs(pos.Y - _mouseDownPos.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!isDrag && LaunchCommand?.CanExecute(null) == true)
        {
            LaunchCommand.Execute(null);
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
