using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace DeskGroup.Controls;

public partial class DragThumb : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _windowStartPosition;

    public DragThumb()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    // â”€â”€ Is Pinned (disables dragging) â”€â”€

    public static readonly DependencyProperty IsPinnedProperty =
        DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(DragThumb),
            new PropertyMetadata(false, OnIsPinnedChanged));

    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    private static void OnIsPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DragThumb thumb)
        {
            thumb.Cursor = thumb.IsPinned ? Cursors.Arrow : Cursors.SizeAll;
            thumb.IsPinnedVisible = thumb.IsPinned ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // â”€â”€ IsPinnedVisible (for XAML binding) â”€â”€

    public static readonly DependencyProperty IsPinnedVisibleProperty =
        DependencyProperty.Register(nameof(IsPinnedVisible), typeof(Visibility), typeof(DragThumb),
            new PropertyMetadata(Visibility.Collapsed));

    public Visibility IsPinnedVisible
    {
        get => (Visibility)GetValue(IsPinnedVisibleProperty);
        set => SetValue(IsPinnedVisibleProperty, value);
    }

    // â”€â”€ Events for parent window to handle position updates â”€â”€

    public event EventHandler<DragDeltaEventArgs>? DragMoved;
    public event EventHandler? DragStarted;
    public event EventHandler? DragCompleted;

    // â”€â”€ Mouse Handlers â”€â”€

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsPinned) return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(null);

        // Get the parent window position
        var parentWindow = Window.GetWindow(this);
        if (parentWindow != null)
        {
            _windowStartPosition = new Point(parentWindow.Left, parentWindow.Top);
        }

        CaptureMouse();
        DragStarted?.Invoke(this, EventArgs.Empty);

        // Show drag indicator
        var storyboard = (Storyboard)FindResource("DragActiveIn");
        storyboard.Begin(this);

        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || IsPinned) return;

        var currentPoint = e.GetPosition(null);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        // Move the parent window
        var parentWindow = Window.GetWindow(this);
        if (parentWindow != null)
        {
            parentWindow.Left = _windowStartPosition.X + deltaX;
            parentWindow.Top = _windowStartPosition.Y + deltaY;
        }

        DragMoved?.Invoke(this, new DragDeltaEventArgs(deltaX, deltaY));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        ReleaseMouseCapture();

        // Hide drag indicator
        var storyboard = (Storyboard)FindResource("DragActiveOut");
        storyboard.Begin(this);

        // Update ViewModel position now that drag is complete
        var parentWindow = Window.GetWindow(this);
        if (parentWindow != null)
        {
            // Fire DragCompleted with final position so ViewModel can update
            var finalDeltaX = parentWindow.Left - _windowStartPosition.X;
            var finalDeltaY = parentWindow.Top - _windowStartPosition.Y;
            DragCompleted?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            DragCompleted?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }

    // â”€â”€ Double-click to toggle pin â”€â”€

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (e.ChangedButton == MouseButton.Left)
        {
            // Bubble up a pin toggle request
            RaiseEvent(new RoutedEventArgs(TogglePinRequestedEvent));
            e.Handled = true;
        }
    }

    // â”€â”€ Right-click context menu â”€â”€

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);

        var menu = new ContextMenu();

        var pinItem = new MenuItem
        {
            Header = IsPinned ? "Unpin position" : "Pin position",
            IsChecked = IsPinned
        };
        pinItem.Click += (s, args) => RaiseEvent(new RoutedEventArgs(TogglePinRequestedEvent));
        menu.Items.Add(pinItem);

        var snapItem = new MenuItem { Header = "Snap to grid" };
        snapItem.Click += (s, args) => RaiseEvent(new RoutedEventArgs(ToggleSnapRequestedEvent));
        menu.Items.Add(snapItem);

        menu.Items.Add(new Separator());

        var stickyItem = new MenuItem { Header = "Sticky (desktop layer)" };
        stickyItem.Click += (s, args) => RaiseEvent(new RoutedEventArgs(SetStickyRequestedEvent));
        menu.Items.Add(stickyItem);

        var floatItem = new MenuItem { Header = "Float (above windows)" };
        floatItem.Click += (s, args) => RaiseEvent(new RoutedEventArgs(SetFloatRequestedEvent));
        menu.Items.Add(floatItem);

        menu.IsOpen = true;
        e.Handled = true;
    }

    // â”€â”€ Custom Routed Events â”€â”€

    public static readonly RoutedEvent TogglePinRequestedEvent =
        EventManager.RegisterRoutedEvent("TogglePinRequested", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DragThumb));

    public static readonly RoutedEvent ToggleSnapRequestedEvent =
        EventManager.RegisterRoutedEvent("ToggleSnapRequested", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DragThumb));

    public static readonly RoutedEvent SetStickyRequestedEvent =
        EventManager.RegisterRoutedEvent("SetStickyRequested", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DragThumb));

    public static readonly RoutedEvent SetFloatRequestedEvent =
        EventManager.RegisterRoutedEvent("SetFloatRequested", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(DragThumb));

    public event RoutedEventHandler TogglePinRequested
    {
        add => AddHandler(TogglePinRequestedEvent, value);
        remove => RemoveHandler(TogglePinRequestedEvent, value);
    }

    public event RoutedEventHandler ToggleSnapRequested
    {
        add => AddHandler(ToggleSnapRequestedEvent, value);
        remove => RemoveHandler(ToggleSnapRequestedEvent, value);
    }

    public event RoutedEventHandler SetStickyRequested
    {
        add => AddHandler(SetStickyRequestedEvent, value);
        remove => RemoveHandler(SetStickyRequestedEvent, value);
    }

    public event RoutedEventHandler SetFloatRequested
    {
        add => AddHandler(SetFloatRequestedEvent, value);
        remove => RemoveHandler(SetFloatRequestedEvent, value);
    }
}

/// <summary>
/// Event args for drag delta, carrying the X/Y offset.
/// </summary>
public class DragDeltaEventArgs : EventArgs
{
    public double DeltaX { get; }
    public double DeltaY { get; }

    public DragDeltaEventArgs(double deltaX, double deltaY)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }
}