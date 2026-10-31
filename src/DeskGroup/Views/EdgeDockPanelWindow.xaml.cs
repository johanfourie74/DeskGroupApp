using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeskGroup.Controls;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.ViewModels;
using Microsoft.Win32;

namespace DeskGroup.Views;

public partial class EdgeDockPanelWindow : Window
{
    private readonly EdgeDockViewModel _viewModel;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private bool _isInitializing = true;
    private bool _isDragging;
    private bool _dragStarted;
    private bool _isUserResizing;
    private Point _dragStartScreen;
    private const double DragThreshold = 5.0;

    private const int WM_ENTERSIZEMOVE = 0x0231;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const int WM_SIZING = 0x0214;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? PanelMouseEntered;
    public event EventHandler? PanelMouseLeft;

    public EdgeDockPanelWindow(EdgeDockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Drop += OnDrop;
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        SizeChanged += OnSizeChanged;

        AddHandler(IconTile.RemoveShortcutEvent, new RoutedEventHandler(OnRemoveShortcutRequested));
    }

    private double DpiScaleX => (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11) ?? 1.0;
    private double DpiScaleY => (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22) ?? 1.0;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_ENTERSIZEMOVE)
        {
            _isUserResizing = true;
            _viewModel.SetSuppressAutoSave(true);
        }
        else if (msg == WM_SIZING && _viewModel.IsVerticalEdge)
        {
            // Snap width live in physical pixels during Win32 resize drag so width snaps to exact 1, 2, 3... icon columns!
            var rect = Marshal.PtrToStructure<RECT>(lParam);
            double scaleX = DpiScaleX;
            double currentDipWidth = (rect.Right - rect.Left) / scaleX;
            int snappedCols = _viewModel.SnapWidthToColumns(currentDipWidth);
            double snappedDipWidth = _viewModel.CalculateSnappedWidthForColumns(snappedCols);
            int snappedPhysWidth = (int)Math.Round(snappedDipWidth * scaleX);

            // wParam indicates which edge is being dragged (1=Left, 2=Right, 4=TopLeft, 5=TopRight, 7=BottomLeft, 8=BottomRight)
            int edge = wParam.ToInt32();
            if (edge == 1 || edge == 4 || edge == 7)
                rect.Left = rect.Right - snappedPhysWidth;
            else if (edge == 2 || edge == 5 || edge == 8)
                rect.Right = rect.Left + snappedPhysWidth;

            Marshal.StructureToPtr(rect, lParam, true);
        }
        else if (msg == WM_EXITSIZEMOVE)
        {
            _isUserResizing = false;
            _viewModel.SetSuppressAutoSave(false);

            if (_viewModel.IsVerticalEdge)
            {
                int cols = _viewModel.SnapWidthToColumns(ActualWidth);
                _viewModel.IconColumns = cols;
                _viewModel.PanelLength = Math.Round(ActualHeight);
            }
            else
            {
                _viewModel.PanelLength = Math.Round(ActualWidth);
            }
            _viewModel.SyncToConfig();
            UpdatePanelGeometry();
        }
        return IntPtr.Zero;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        Win32Interop.SetToolWindow(_windowHandle);
        Win32Interop.SetTopmost(_windowHandle, true);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.LayoutChanged += OnLayoutChanged;

        UpdatePanelGeometry();
        _isInitializing = false;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.LayoutChanged -= OnLayoutChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isDragging || _isUserResizing) return;

        if (e.PropertyName == nameof(EdgeDockViewModel.IsPanelOpen) ||
            e.PropertyName == nameof(EdgeDockViewModel.IsEnabled) ||
            e.PropertyName == nameof(EdgeDockViewModel.Edge) ||
            e.PropertyName == nameof(EdgeDockViewModel.EdgePositionRatio) ||
            e.PropertyName == nameof(EdgeDockViewModel.IconColumns) ||
            e.PropertyName == nameof(EdgeDockViewModel.LockState))
        {
            UpdatePanelGeometry();
        }
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        if (_isDragging || _isUserResizing) return;
        UpdatePanelGeometry();
    }

    public void UpdatePanelGeometry()
    {
        if (!_viewModel.IsEnabled || !_viewModel.IsPanelOpen)
        {
            Visibility = Visibility.Hidden;
            return;
        }

        var mon = _viewModel.TargetMonitor;
        double scaleX = DpiScaleX > 0 ? DpiScaleX : mon.ScaleFactor;
        double scaleY = DpiScaleY > 0 ? DpiScaleY : mon.ScaleFactor;

        double monLeft = mon.WorkingArea.Left / scaleX;
        double monTop = mon.WorkingArea.Top / scaleY;
        double monWidth = mon.WorkingArea.Width / scaleX;
        double monHeight = mon.WorkingArea.Height / scaleY;

        double snappedThickness = _viewModel.IsVerticalEdge
            ? _viewModel.CalculateSnappedWidthForColumns(_viewModel.IconColumns)
            : _viewModel.CalculateSnappedHeightForRows(_viewModel.IconColumns);

        double minSingleColW = _viewModel.CalculateSnappedWidthForColumns(1);
        MinWidth = _viewModel.IsVerticalEdge ? minSingleColW : 140;
        MinHeight = _viewModel.IsVerticalEdge ? 140 : _viewModel.CalculateSnappedHeightForRows(1);

        double length = Math.Clamp(_viewModel.PanelLength, 140, 1400);

        double w = _viewModel.IsVerticalEdge ? snappedThickness : Math.Min(length, monWidth);
        double h = _viewModel.IsVerticalEdge ? Math.Min(length, monHeight) : snappedThickness;

        double ratio = Math.Clamp(_viewModel.EdgePositionRatio, 0.05, 0.95);
        double x = monLeft;
        double y = monTop;

        int bubbleOffset = (_viewModel.ShowBubbleButton && _viewModel.EffectiveBubbleEdge == _viewModel.Edge)
            ? Math.Clamp(_viewModel.BubbleSize, 12, 48) + 2
            : 0;

        switch (_viewModel.Edge)
        {
            case DockEdge.Left:
                x = monLeft + bubbleOffset;
                y = monTop + (monHeight - h) * ratio;
                break;
            case DockEdge.Right:
                x = monLeft + monWidth - w - bubbleOffset;
                y = monTop + (monHeight - h) * ratio;
                break;
            case DockEdge.Top:
                x = monLeft + (monWidth - w) * ratio;
                y = monTop + bubbleOffset;
                break;
            case DockEdge.Bottom:
                x = monLeft + (monWidth - w) * ratio;
                y = monTop + monHeight - h - bubbleOffset;
                break;
        }

        _isInitializing = true;
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
        _isInitializing = false;

        Visibility = Visibility.Visible;
        Topmost = true;
    }

    public void EnsureTopmost()
    {
        if (_windowHandle != IntPtr.Zero && IsVisible)
        {
            Win32Interop.SetTopmost(_windowHandle, true);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitializing || !_viewModel.IsPanelOpen || _isUserResizing) return;

        if (_viewModel.IsVerticalEdge)
        {
            int cols = _viewModel.SnapWidthToColumns(e.NewSize.Width);
            if (cols != _viewModel.IconColumns)
                _viewModel.IconColumns = cols;
            if (Math.Abs(e.NewSize.Height - _viewModel.PanelLength) > 2)
                _viewModel.PanelLength = Math.Round(e.NewSize.Height);
        }
        else
        {
            if (Math.Abs(e.NewSize.Width - _viewModel.PanelLength) > 2)
                _viewModel.PanelLength = Math.Round(e.NewSize.Width);
        }
    }

    private void PanelBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        PanelMouseEntered?.Invoke(this, EventArgs.Empty);
    }

    private void PanelBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging && !_dragStarted && !_isUserResizing && !IconTile.IsDraggingShortcut)
        {
            PanelMouseLeft?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't start drag if clicking a header button
        if (e.OriginalSource is DependencyObject src && FindParentOfType<System.Windows.Controls.Button>(src) != null)
            return;

        if (e.ClickCount == 2)
        {
            _viewModel.TogglePin();
            e.Handled = true;
            return;
        }

        if (_viewModel.IsPinned) return;

        _dragStarted = true;
        _isDragging = false;
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        (sender as UIElement)?.CaptureMouse();
        e.Handled = true;
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragStarted || _viewModel.IsPinned) return;

        var currentScreen = PointToScreen(e.GetPosition(this));
        double dx = currentScreen.X - _dragStartScreen.X;
        double dy = currentScreen.Y - _dragStartScreen.Y;
        if (!_isDragging && Math.Sqrt(dx * dx + dy * dy) >= DragThreshold)
        {
            _isDragging = true;
            _viewModel.SetSuppressAutoSave(true);
        }

        if (_isDragging)
        {
            var mon = _viewModel.TargetMonitor;
            double relX = Math.Clamp(currentScreen.X - mon.Bounds.Left, 0, mon.Bounds.Width);
            double relY = Math.Clamp(currentScreen.Y - mon.Bounds.Top, 0, mon.Bounds.Height);

            double distLeft = relX;
            double distRight = mon.Bounds.Width - relX;
            double distTop = relY;
            double distBottom = mon.Bounds.Height - relY;

            double minDist = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            if (minDist == distLeft)
            {
                _viewModel.Edge = DockEdge.Left;
                _viewModel.EdgePositionRatio = Math.Clamp(relY / Math.Max(1, mon.Bounds.Height), 0.05, 0.95);
            }
            else if (minDist == distRight)
            {
                _viewModel.Edge = DockEdge.Right;
                _viewModel.EdgePositionRatio = Math.Clamp(relY / Math.Max(1, mon.Bounds.Height), 0.05, 0.95);
            }
            else if (minDist == distTop)
            {
                _viewModel.Edge = DockEdge.Top;
                _viewModel.EdgePositionRatio = Math.Clamp(relX / Math.Max(1, mon.Bounds.Width), 0.05, 0.95);
            }
            else
            {
                _viewModel.Edge = DockEdge.Bottom;
                _viewModel.EdgePositionRatio = Math.Clamp(relX / Math.Max(1, mon.Bounds.Width), 0.05, 0.95);
            }

            UpdatePanelGeometry();
        }
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragStarted) return;
        (sender as UIElement)?.ReleaseMouseCapture();
        bool wasDrag = _isDragging;
        _dragStarted = false;
        _isDragging = false;
        _viewModel.SetSuppressAutoSave(false);

        if (wasDrag)
        {
            _viewModel.SyncToConfig();
            UpdatePanelGeometry();
        }
        e.Handled = true;
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPanelOpen = false;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        PanelMouseEntered?.Invoke(this, EventArgs.Empty);
        if (e.Data.GetDataPresent("DeskGroupShortcut"))
            e.Effects = DragDropEffects.Move;
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        PanelMouseEntered?.Invoke(this, EventArgs.Empty);
        if (e.Data.GetDataPresent("DeskGroupShortcut"))
        {
            e.Effects = DragDropEffects.Move;
            var source = e.Data.GetData("DeskGroupShortcut") as ShortcutItemViewModel;
            int targetIdx = FindTargetShortcutIndexAtPoint(e.GetPosition(this));
            UpdateDropHighlight(targetIdx, source);
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            int targetIdx = FindTargetShortcutIndexAtPoint(e.GetPosition(this));
            UpdateDropHighlight(targetIdx, null);
        }
        else
        {
            e.Effects = DragDropEffects.None;
            ClearDropHighlights();
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        ClearDropHighlights();
    }

    private void UpdateDropHighlight(int targetIdx, ShortcutItemViewModel? source)
    {
        for (int i = 0; i < _viewModel.AllShortcuts.Count; i++)
        {
            var item = _viewModel.AllShortcuts[i];
            item.IsSelected = (i == targetIdx && !ReferenceEquals(item, source));
        }
    }

    private void ClearDropHighlights()
    {
        foreach (var item in _viewModel.AllShortcuts)
            item.IsSelected = false;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        HandleShortcutOrFileDrop(e);
    }

    private void OnIconDropInternal(object sender, DragEventArgs e)
    {
        HandleShortcutOrFileDrop(e);
    }

    private void HandleShortcutOrFileDrop(DragEventArgs e)
    {
        ClearDropHighlights();

        if (e.Data.GetDataPresent("DeskGroupShortcut"))
        {
            var source = e.Data.GetData("DeskGroupShortcut") as ShortcutItemViewModel;
            if (source != null)
            {
                int targetIndex = FindTargetShortcutIndexAtPoint(e.GetPosition(this));

                if (!_viewModel.AllShortcuts.Contains(source))
                {
                    var mainVm = GetMainViewModel();
                    var sourceGroup = mainVm?.Groups.FirstOrDefault(g => g.AllShortcuts.Contains(source));
                    if (sourceGroup != null)
                    {
                        _viewModel.TransferShortcutFromGroup(source, sourceGroup);
                    }
                    else
                    {
                        var sourceDock = mainVm?.ActiveEdgeDocks.FirstOrDefault(d => d.AllShortcuts.Contains(source));
                        if (sourceDock != null)
                            _viewModel.TransferShortcutFromDock(source, sourceDock);
                    }

                    if (targetIndex >= 0 && _viewModel.AllShortcuts.Contains(source))
                        _viewModel.MoveShortcutToIndex(source, targetIndex);
                }
                else if (targetIndex >= 0)
                {
                    _viewModel.MoveShortcutToIndex(source, targetIndex);
                }
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null)
            {
                int targetIndex = FindTargetShortcutIndexAtPoint(e.GetPosition(this));
                foreach (var file in files)
                {
                    var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".lnk" || ext == ".url" || ext == ".exe" || System.IO.Directory.Exists(file))
                    {
                        _viewModel.AddShortcutCommand.Execute(file);
                        if (targetIndex >= 0 && _viewModel.AllShortcuts.Count > 1)
                        {
                            var added = _viewModel.AllShortcuts.LastOrDefault();
                            if (added != null)
                                _viewModel.MoveShortcutToIndex(added, targetIndex++);
                        }
                    }
                }
            }
            e.Effects = DragDropEffects.Copy;
        }
        e.Handled = true;
    }

    private int FindTargetShortcutIndexAtPoint(Point pointInWindow)
    {
        if (_viewModel.AllShortcuts.Count == 0) return -1;

        // 1. Direct visual hit-test on an IconTile
        var directVm = FindShortcutViewModelAtPoint(pointInWindow);
        if (directVm != null)
        {
            int directIdx = _viewModel.AllShortcuts.IndexOf(directVm);
            if (directIdx >= 0) return directIdx;
        }

        // 2. Fallback: find nearest item container in ShortcutsItemsControl (handles drops in 4px margins, header, footer, or empty space)
        int bestIndex = -1;
        double bestDistSq = double.MaxValue;
        double firstTop = double.MaxValue;
        double lastBottom = double.MinValue;
        double firstLeft = double.MaxValue;
        double lastRight = double.MinValue;

        for (int i = 0; i < _viewModel.AllShortcuts.Count; i++)
        {
            if (ShortcutsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container &&
                container.ActualWidth > 0 && container.ActualHeight > 0)
            {
                var topLeft = container.TranslatePoint(new Point(0, 0), this);
                var center = new Point(topLeft.X + container.ActualWidth / 2.0, topLeft.Y + container.ActualHeight / 2.0);

                if (i == 0)
                {
                    firstTop = topLeft.Y;
                    firstLeft = topLeft.X;
                }
                if (i == _viewModel.AllShortcuts.Count - 1)
                {
                    lastBottom = topLeft.Y + container.ActualHeight;
                    lastRight = topLeft.X + container.ActualWidth;
                }

                double dx = pointInWindow.X - center.X;
                double dy = pointInWindow.Y - center.Y;
                double distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }
        }

        if (_viewModel.IsVerticalEdge)
        {
            if (pointInWindow.Y < firstTop) return 0;
            if (pointInWindow.Y > lastBottom) return _viewModel.AllShortcuts.Count - 1;
        }
        else
        {
            if (pointInWindow.X < firstLeft) return 0;
            if (pointInWindow.X > lastRight) return _viewModel.AllShortcuts.Count - 1;
        }

        return bestIndex >= 0 ? bestIndex : _viewModel.AllShortcuts.Count - 1;
    }

    private ShortcutItemViewModel? FindShortcutViewModelAtPoint(Point point)
    {
        var result = VisualTreeHelper.HitTest(this, point);
        if (result == null) return null;

        var element = result.VisualHit as DependencyObject;
        while (element != null)
        {
            if (element is FrameworkElement fe && fe.DataContext is ShortcutItemViewModel vm)
                return vm;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static T? FindParentOfType<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T t) return t;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void OnRemoveShortcutRequested(object sender, RoutedEventArgs e)
    {
        if (e is RemoveShortcutRoutedEventArgs removeArgs)
        {
            var tile = removeArgs.SourceTile;
            var shortcutVm = _viewModel.AllShortcuts.FirstOrDefault(s => s.DisplayName == tile.DisplayName);
            if (shortcutVm != null)
            {
                var result = MessageBox.Show(
                    $"Remove \"{shortcutVm.DisplayName}\" from this Edge Dock?\n\n" +
                    "The original desktop shortcut will be restored.",
                    "Remove Shortcut",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    _viewModel.RemoveShortcutCommand.Execute(shortcutVm);
            }
        }
        e.Handled = true;
    }

    public void RemoveShortcutByViewModel(ShortcutItemViewModel vm)
    {
        _viewModel.RemoveShortcutCommand.Execute(vm);
    }

    private MainViewModel? GetMainViewModel()
    {
        var app = Application.Current as App;
        var proxy = app?.Resources["AppDataProxy"] as DeskGroup.Helpers.BindingProxy;
        return proxy?.Data as MainViewModel;
    }

    private void MenuAddShortcut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a shortcut or application to add to Edge Dock",
            Filter = "Shortcuts & Apps (*.lnk;*.url;*.exe)|*.lnk;*.url;*.exe|All files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        if (dialog.ShowDialog(this) == true)
        {
            foreach (var file in dialog.FileNames)
                _viewModel.AddShortcutCommand.Execute(file);
        }
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e)
    {
        var inputDialog = new InputDialog("Rename Edge Dock", "Enter dock name:", _viewModel.Name)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.ResponseText))
        {
            _viewModel.Name = inputDialog.ResponseText.Trim();
        }
    }

    private void MenuChangeColor_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new ColorPickerDialog(_viewModel.AccentColorHex)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        if (colorDialog.ShowDialog() == true && !string.IsNullOrEmpty(colorDialog.SelectedColor))
        {
            _viewModel.AccentColorHex = colorDialog.SelectedColor;
        }
    }

    private void MenuChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a dock icon",
            Filter = "Icon files (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|Image files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.SetDockIconCommand.Execute(dialog.FileName);
        }
    }

    private void MenuDockAppearance_Click(object sender, RoutedEventArgs e)
    {
        ShowAppearanceDialog();
    }

    public void ShowAppearanceDialog()
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

    private void MenuResetDockAppearance_Click(object sender, RoutedEventArgs e) => _viewModel.ResetDockAppearance();
    private void MenuResetBubblePos_Click(object sender, RoutedEventArgs e) => _viewModel.ResetBubblePosition();
    private void MenuCols1_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconColumns(1);
    private void MenuCols2_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconColumns(2);
    private void MenuCols3_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconColumns(3);
    private void MenuCols4_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconColumns(4);
    private void MenuCols5_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconColumns(5);
    private void MenuCols6_Click(object sender, RoutedEventArgs e) => _viewModel.SetIconColumns(6);
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
    private void MenuSettings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
}
