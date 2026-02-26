using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskGroup.Controls;

public partial class IconTile : UserControl
{
    public IconTile()
    {
        InitializeComponent();
    }

    // â”€â”€ Icon Source â”€â”€

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(IconTile),
            new PropertyMetadata(null));

    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // â”€â”€ Display Name â”€â”€

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(nameof(DisplayName), typeof(string), typeof(IconTile),
            new PropertyMetadata("Shortcut"));

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    // â”€â”€ Icon Size â”€â”€

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

    // â”€â”€ Tile Width (auto-calculated from icon size, or manually set) â”€â”€

    public static readonly DependencyProperty TileWidthProperty =
        DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(IconTile),
            new PropertyMetadata(72.0));

    public double TileWidth
    {
        get => (double)GetValue(TileWidthProperty);
        set => SetValue(TileWidthProperty, value);
    }

    // â”€â”€ Tile Height (auto-calculated from icon size, or manually set) â”€â”€

    public static readonly DependencyProperty TileHeightProperty =
        DependencyProperty.Register(nameof(TileHeight), typeof(double), typeof(IconTile),
            new PropertyMetadata(88.0));

    public double TileHeight
    {
        get => (double)GetValue(TileHeightProperty);
        set => SetValue(TileHeightProperty, value);
    }

    // â”€â”€ Launch Command (double-click) â”€â”€

    public static readonly DependencyProperty LaunchCommandProperty =
        DependencyProperty.Register(nameof(LaunchCommand), typeof(ICommand), typeof(IconTile),
            new PropertyMetadata(null));

    public ICommand? LaunchCommand
    {
        get => (ICommand?)GetValue(LaunchCommandProperty);
        set => SetValue(LaunchCommandProperty, value);
    }

    // â”€â”€ Select Command (single click) â”€â”€

    public static readonly DependencyProperty SelectCommandProperty =
        DependencyProperty.Register(nameof(SelectCommand), typeof(ICommand), typeof(IconTile),
            new PropertyMetadata(null));

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    // â”€â”€ IsSelected â”€â”€

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

    // â”€â”€ Size Calculation â”€â”€

    private void UpdateTileSize()
    {
        // Tile is icon size + padding + label height
        TileWidth = IconSize + 24;
        TileHeight = IconSize + 40;

        // Update the scale transform center
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

    // â”€â”€ Context Menu (right-click) â”€â”€

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);

        var contextMenu = new ContextMenu();

        var launchItem = new MenuItem { Header = "Open" };
        launchItem.Click += (s, args) => LaunchCommand?.Execute(null);
        contextMenu.Items.Add(launchItem);

        contextMenu.Items.Add(new Separator());

        var removeItem = new MenuItem { Header = "Remove from group" };
        removeItem.Click += (s, args) =>
        {
            // Bubble up via routed event â€” handled by GroupWidget
            RaiseEvent(new RemoveShortcutRoutedEventArgs(RemoveShortcutEvent, this));
        };
        contextMenu.Items.Add(removeItem);

        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    // â”€â”€ Custom Routed Event: RemoveShortcut â”€â”€

    public static readonly RoutedEvent RemoveShortcutEvent =
        EventManager.RegisterRoutedEvent("RemoveShortcut", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(IconTile));

    public event RoutedEventHandler RemoveShortcut
    {
        add => AddHandler(RemoveShortcutEvent, value);
        remove => RemoveHandler(RemoveShortcutEvent, value);
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