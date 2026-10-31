using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.ViewModels;

namespace DeskGroup.Views;

public partial class EdgeDockBubbleWindow : Window
{
    private readonly EdgeDockViewModel _viewModel;
    private IntPtr _windowHandle;
    private bool _isDragging;
    private bool _dragStarted;
    private Point _dragStartScreen;
    private const double DragThreshold = 4.0;

    public event EventHandler? SettingsRequested;
    public event EventHandler? HoverEntered;
    public event EventHandler? HoverLeft;

    public EdgeDockBubbleWindow(EdgeDockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private double DpiScaleX => (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11) ?? 1.0;
    private double DpiScaleY => (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22) ?? 1.0;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        Win32Interop.SetToolWindow(_windowHandle);
        Win32Interop.SetTopmost(_windowHandle, true);

        // Add WS_EX_NOACTIVATE so clicking the bubble doesn't steal foreground focus unnecessarily
        int exStyle = Win32Interop.GetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE);
        Win32Interop.SetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE, exStyle | Win32Interop.WS_EX_NOACTIVATE);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.LayoutChanged += OnLayoutChanged;

        UpdateBubbleGeometry();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.LayoutChanged -= OnLayoutChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isDragging) return;

        if (e.PropertyName == nameof(EdgeDockViewModel.BubbleSize) ||
            e.PropertyName == nameof(EdgeDockViewModel.ShowBubbleButton) ||
            e.PropertyName == nameof(EdgeDockViewModel.ShowOnEdgeHover) ||
            e.PropertyName == nameof(EdgeDockViewModel.IsPanelOpen) ||
            e.PropertyName == nameof(EdgeDockViewModel.Edge) ||
            e.PropertyName == nameof(EdgeDockViewModel.EdgePositionRatio) ||
            e.PropertyName == nameof(EdgeDockViewModel.BubblePositionRatio) ||
            e.PropertyName == nameof(EdgeDockViewModel.BubbleEdgeOverride))
        {
            UpdateBubbleGeometry();
        }
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        if (_isDragging) return;
        UpdateBubbleGeometry();
    }

    public void UpdateBubbleGeometry()
    {
        if (!_viewModel.IsEnabled)
        {
            Visibility = Visibility.Hidden;
            return;
        }

        var mon = _viewModel.TargetMonitor;
        double scaleX = DpiScaleX > 0 ? DpiScaleX : mon.ScaleFactor;
        double scaleY = DpiScaleY > 0 ? DpiScaleY : mon.ScaleFactor;

        double monLeft = mon.Bounds.Left / scaleX;
        double monTop = mon.Bounds.Top / scaleY;
        double monWidth = mon.Bounds.Width / scaleX;
        double monHeight = mon.Bounds.Height / scaleY;

        int bubbleSize = Math.Clamp(_viewModel.BubbleSize, 12, 48);
        BubbleBorder.Width = bubbleSize;
        BubbleBorder.Height = bubbleSize;
        BubbleBorder.CornerRadius = new CornerRadius(bubbleSize / 2.0);
        BubbleBorder.Visibility = _viewModel.ShowBubbleButton ? Visibility.Visible : Visibility.Collapsed;

        if (!_viewModel.ShowBubbleButton && !_viewModel.ShowOnEdgeHover)
        {
            Visibility = Visibility.Hidden;
            return;
        }

        Visibility = Visibility.Visible;

        DockEdge bubbleEdge = _viewModel.EffectiveBubbleEdge;
        double ratio = _viewModel.EffectiveBubbleRatio;
        double w = _viewModel.ShowBubbleButton ? bubbleSize : 4;
        double h = _viewModel.ShowBubbleButton ? bubbleSize : 4;

        if (!_viewModel.ShowBubbleButton && _viewModel.ShowOnEdgeHover)
        {
            if (bubbleEdge == DockEdge.Left || bubbleEdge == DockEdge.Right)
            {
                w = 3;
                h = Math.Min(_viewModel.PanelLength, monHeight * 0.8);
            }
            else
            {
                w = Math.Min(_viewModel.PanelLength, monWidth * 0.8);
                h = 3;
            }
        }

        double x = monLeft;
        double y = monTop;

        switch (bubbleEdge)
        {
            case DockEdge.Left:
                x = monLeft;
                y = monTop + (monHeight - h) * ratio;
                break;
            case DockEdge.Right:
                x = monLeft + monWidth - w;
                y = monTop + (monHeight - h) * ratio;
                break;
            case DockEdge.Top:
                x = monLeft + (monWidth - w) * ratio;
                y = monTop;
                break;
            case DockEdge.Bottom:
                x = monLeft + (monWidth - w) * ratio;
                y = monTop + monHeight - h;
                break;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            Win32Interop.SetWindowPos(_windowHandle, Win32Interop.HWND_TOPMOST,
                (int)Math.Round(x * scaleX), (int)Math.Round(y * scaleY),
                (int)Math.Round(w * scaleX), (int)Math.Round(h * scaleY),
                Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);
        }

        Width = w;
        Height = h;
        Left = Math.Round(x);
        Top = Math.Round(y);
    }

    public void EnsureTopmost()
    {
        if (_windowHandle != IntPtr.Zero && IsVisible)
        {
            Win32Interop.SetTopmost(_windowHandle, true);
        }
    }

    private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
    {
        BubbleBorder.Opacity = 1.0;
        if (!_isDragging && !_dragStarted)
        {
            HoverEntered?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
    {
        BubbleBorder.Opacity = 0.65;
        if (!_isDragging && !_dragStarted)
        {
            HoverLeft?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStarted = true;
        _isDragging = false;
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        RootGrid.CaptureMouse();
        e.Handled = true;
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragStarted) return;

        var currentScreen = PointToScreen(e.GetPosition(this));
        double dx = currentScreen.X - _dragStartScreen.X;
        double dy = currentScreen.Y - _dragStartScreen.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (!_isDragging && dist >= DragThreshold)
        {
            _isDragging = true;
            _viewModel.SetSuppressAutoSave(true);
        }

        if (_isDragging)
        {
            // Bubble is ALWAYS movable so the user can position it wherever it is least obstructive!
            var mon = _viewModel.TargetMonitor;
            double relX = Math.Clamp(currentScreen.X - mon.Bounds.Left, 0, mon.Bounds.Width);
            double relY = Math.Clamp(currentScreen.Y - mon.Bounds.Top, 0, mon.Bounds.Height);

            double distLeft = relX;
            double distRight = mon.Bounds.Width - relX;
            double distTop = relY;
            double distBottom = mon.Bounds.Height - relY;

            double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            DockEdge targetEdge;
            double targetRatio;

            if (minDist == distLeft)
            {
                targetEdge = DockEdge.Left;
                targetRatio = Math.Clamp(relY / Math.Max(1, mon.Bounds.Height), 0.02, 0.98);
            }
            else if (minDist == distRight)
            {
                targetEdge = DockEdge.Right;
                targetRatio = Math.Clamp(relY / Math.Max(1, mon.Bounds.Height), 0.02, 0.98);
            }
            else if (minDist == distTop)
            {
                targetEdge = DockEdge.Top;
                targetRatio = Math.Clamp(relX / Math.Max(1, mon.Bounds.Width), 0.02, 0.98);
            }
            else
            {
                targetEdge = DockEdge.Bottom;
                targetRatio = Math.Clamp(relX / Math.Max(1, mon.Bounds.Width), 0.02, 0.98);
            }

            _viewModel.BubbleEdgeOverride = targetEdge;
            _viewModel.BubblePositionRatio = targetRatio;

            // If the dock panel itself is NOT pinned and user isn't holding Shift, also move the dock edge if desired,
            // or keep bubble independent so moving the bubble never obstructs anything.
            if (!_viewModel.IsPinned && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                _viewModel.Edge = targetEdge;
                _viewModel.EdgePositionRatio = Math.Clamp(targetRatio, 0.05, 0.95);
            }

            UpdateBubbleGeometry();
        }
    }

    private void RootGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragStarted) return;
        RootGrid.ReleaseMouseCapture();

        bool wasDrag = _isDragging;
        _dragStarted = false;
        _isDragging = false;
        _viewModel.SetSuppressAutoSave(false);

        if (wasDrag)
        {
            _viewModel.SyncToConfig();
            UpdateBubbleGeometry();
        }
        else
        {
            // Clicked the bubble -> toggle the Edge Dock panel open/closed!
            _viewModel.TogglePanel();
        }
        e.Handled = true;
    }

    private void MenuToggle_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePanel();
    private void MenuPin_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePin();
    private void MenuResetBubblePos_Click(object sender, RoutedEventArgs e) => _viewModel.ResetBubblePosition();
    private void MenuEdgeLeft_Click(object sender, RoutedEventArgs e) => _viewModel.SetEdge(DockEdge.Left);
    private void MenuEdgeTop_Click(object sender, RoutedEventArgs e) => _viewModel.SetEdge(DockEdge.Top);
    private void MenuEdgeRight_Click(object sender, RoutedEventArgs e) => _viewModel.SetEdge(DockEdge.Right);
    private void MenuEdgeBottom_Click(object sender, RoutedEventArgs e) => _viewModel.SetEdge(DockEdge.Bottom);
    private void MenuLockAuto_Click(object sender, RoutedEventArgs e) => _viewModel.LockState = SideDockLockState.AutoHide;
    private void MenuLockOpen_Click(object sender, RoutedEventArgs e) => _viewModel.LockState = SideDockLockState.LockedOpen;
    private void MenuLockClosed_Click(object sender, RoutedEventArgs e) => _viewModel.LockState = SideDockLockState.LockedClosed;
    private void MenuSize12_Click(object sender, RoutedEventArgs e) => _viewModel.BubbleSize = 12;
    private void MenuSize16_Click(object sender, RoutedEventArgs e) => _viewModel.BubbleSize = 16;
    private void MenuSize20_Click(object sender, RoutedEventArgs e) => _viewModel.BubbleSize = 20;
    private void MenuSize24_Click(object sender, RoutedEventArgs e) => _viewModel.BubbleSize = 24;
    private void MenuSize32_Click(object sender, RoutedEventArgs e) => _viewModel.BubbleSize = 32;
    private void MenuDockAppearance_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PerGroupAppearanceDialog(_viewModel)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Show();
    }
    private void MenuIconSize16_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(16);
    private void MenuIconSize24_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(24);
    private void MenuIconSize32_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(32);
    private void MenuIconSize40_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(40);
    private void MenuIconSize48_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(48);
    private void MenuIconSize64_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(64);
    private void MenuIconSize72_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(72);
    private void MenuIconSize96_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(96);
    private void MenuIconSizeGlobal_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconSize(0);
    private void MenuSettings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
}
