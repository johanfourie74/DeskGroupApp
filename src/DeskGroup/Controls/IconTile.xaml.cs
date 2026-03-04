using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
            // Start hover-in animation
            var hoverInStoryboard = (Storyboard?)Resources["HoverIn"];
            if (hoverInStoryboard != null)
            {
                hoverInStoryboard.Begin(this);
                System.Diagnostics.Debug.WriteLine($"🖱️ HOVER IN: {DisplayName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Grid_MouseEnter ERROR: {ex.Message}");
        }
    }

    private void Grid_MouseLeave(object sender, MouseEventArgs e)
    {
        try
        {
            // Start hover-out animation
            var hoverOutStoryboard = (Storyboard?)Resources["HoverOut"];
            if (hoverOutStoryboard != null)
            {
                hoverOutStoryboard.Begin(this);
                System.Diagnostics.Debug.WriteLine($"🖱️ HOVER OUT: {DisplayName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Grid_MouseLeave ERROR: {ex.Message}");
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
        DependencyProperty.Register(nameof(AccentColor), typeof(System.Windows.Media.Brush), typeof(IconTile),
            new PropertyMetadata(null));

    public System.Windows.Media.Brush? AccentColor
    {
        get => (System.Windows.Media.Brush?)GetValue(AccentColorProperty);
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

    // Context Menu (right-click)

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
            // Bubble up via routed event - handled by GroupWidget
            RaiseEvent(new RemoveShortcutRoutedEventArgs(RemoveShortcutEvent, this));
        };
        contextMenu.Items.Add(removeItem);

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