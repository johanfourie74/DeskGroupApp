using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeskGroup.Controls;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.ViewModels;
using Microsoft.Win32;

namespace DeskGroup.Views;

public partial class GroupWidget : Window
{
    private GroupViewModel? _viewModel;
    private IntPtr _windowHandle;
    private bool _isAttachedToDesktop;
    private System.Windows.Threading.DispatcherTimer? _hoverLeaveTimer;
    private bool _isApplyingChanges; // Guard against re-entry
    private bool _isInitializing = true; // Skip handlers during init
    private bool _isUpdatingViewMode = false; // Guard for view mode updates
    private bool _isSyncingSizeFromWindow = false; // Guard against resize feedback loop

    // WndProc hook for intercepting position changes
    private HwndSource? _hwndSource;
    private int _pendingX, _pendingY, _pendingW, _pendingH;
    private bool _hasPendingPosition = false;

    // Save position before fullscreen (view mode is saved in ViewModel)
    private double _preFullscreenLeft = double.NaN;
    private double _preFullscreenTop = double.NaN;
    private double _preFullscreenWidth;
    private double _preFullscreenHeight;

    // Track when temp-float was applied (to prevent immediate restoration on same click)
    private static DateTime _lastTempFloatTime = DateTime.MinValue;

    /// <summary>
    /// Called by MainViewModel when Show All applies temporary float.
    /// Sets timestamp to prevent immediate restoration on the same menu click.
    /// </summary>
    public static void SetLastTempFloatTime()
    {
        _lastTempFloatTime = DateTime.Now;
    }

    // DPI helpers — SetWindowPos takes physical pixels; WPF Left/Top are logical (DIPs)
    private double DpiScaleX => (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11) ?? 1.0;
    private double DpiScaleY => (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22) ?? 1.0;

    private bool SetWindowPosInDips(IntPtr insertAfter, int dipX, int dipY, int dipW, int dipH, uint flags)
        => Win32Interop.SetWindowPos(_windowHandle, insertAfter,
            (int)(dipX * DpiScaleX), (int)(dipY * DpiScaleY),
            (int)(dipW * DpiScaleX), (int)(dipH * DpiScaleY), flags);

    private void SetPendingPositionInDips(int dipX, int dipY, int dipW, int dipH)
    {
        _pendingX = (int)(dipX * DpiScaleX);
        _pendingY = (int)(dipY * DpiScaleY);
        _pendingW = (int)(dipW * DpiScaleX);
        _pendingH = (int)(dipH * DpiScaleY);
        _hasPendingPosition = true;
    }

    /// <summary>
    /// Fired when the user requests to delete this group.
    /// The MainViewModel handles the actual deletion.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Fired when a file is dropped onto this group widget.
    /// </summary>
    public event EventHandler<string[]>? FilesDropped;

    /// <summary>
    /// Fired when the user opens Settings from the group context menu.
    /// </summary>
    public event EventHandler? SettingsRequested;

    public GroupWidget()
    {
        InitializeComponent();

        // Reduce flickering during drag/resize
        System.Windows.Interop.HwndSource.DefaultAcquireHwndFocusInMenuMode = false;

        // Additional performance optimization - use layered window composition
        SourceInitialized += (s, e) =>
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.CompositionTarget.RenderMode = RenderMode.Default;

                // Hook WndProc to intercept position changes
                _hwndSource = source;
                _hwndSource.AddHook(WndProc);
            }
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        Activated += OnActivated; // Visual feedback when window gets focus
        Deactivated += OnDeactivated; // Remove feedback when window loses focus
        DataContextChanged += OnDataContextChanged;
        Drop += OnDrop;
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        KeyDown += OnKeyDown; // Keyboard shortcuts
        PreviewMouseLeftButtonDown += OnAnyMouseClick; // Catch ALL clicks to restore sticky mode
        PreviewMouseRightButtonDown += OnAnyMouseClick; // Activate Sticky widget on right-click so ContextMenu works reliably on Win11
        MouseLeftButtonUp += OnMouseUpSyncPosition; // Sync position after ANY mouse release
        SizeChanged += OnSizeChanged; // Track window resize

        // Subscribe to routed events from child controls
        AddHandler(IconTile.RemoveShortcutEvent, new RoutedEventHandler(OnRemoveShortcutRequested));
        AddHandler(DragThumb.TogglePinRequestedEvent, new RoutedEventHandler(OnTogglePinRequested));
        AddHandler(DragThumb.ToggleSnapRequestedEvent, new RoutedEventHandler(OnToggleSnapRequested));
        AddHandler(DragThumb.SetStickyRequestedEvent, new RoutedEventHandler(OnSetStickyRequested));
        AddHandler(DragThumb.SetFloatRequestedEvent, new RoutedEventHandler(OnSetFloatRequested));
        AddHandler(DragThumb.OpenSettingsRequestedEvent, new RoutedEventHandler(OnOpenSettingsRequested));
    }

    public GroupWidget(GroupViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    // WndProc Hook - Intercept position & Z-order changes BEFORE they happen

    private const int WM_WINDOWPOSCHANGING = 0x0046;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            var pos = System.Runtime.InteropServices.Marshal.PtrToStructure<WINDOWPOS>(lParam);
            bool modified = false;

            if (_hasPendingPosition)
            {
                const uint SWP_NOMOVE = 0x0002;
                if ((pos.flags & SWP_NOMOVE) == 0)
                {
                    if (pos.x != _pendingX || pos.y != _pendingY ||
                        pos.cx != _pendingW || pos.cy != _pendingH)
                    {
                        pos.x = _pendingX;
                        pos.y = _pendingY;
                        pos.cx = _pendingW;
                        pos.cy = _pendingH;
                        modified = true;
                    }
                }
            }

            // Keep Sticky widgets on the desktop layer (HWND_BOTTOM) even when activated for right-click menus or F11
            if (_viewModel != null &&
                _viewModel.DockMode == GroupDockMode.Sticky &&
                _viewModel.ViewMode != GroupViewMode.Fullscreen &&
                !_viewModel.SavedDockModeBeforeFloat.HasValue)
            {
                if ((pos.flags & Win32Interop.SWP_NOZORDER) == 0 && pos.hwndInsertAfter != Win32Interop.HWND_BOTTOM)
                {
                    pos.hwndInsertAfter = Win32Interop.HWND_BOTTOM;
                    modified = true;
                }
            }

            if (modified)
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(pos, lParam, true);
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Lazily binds ItemsSource ONLY to the currently visible view's ItemsControl.
    /// Prevents WPF from instantiating 4x duplicate IconTile controls for collapsed views at startup.
    /// </summary>
    private void BindActiveViewItemsSource(GroupViewMode mode)
    {
        if (_viewModel == null)
        {
            if (HoverItemsControl != null) HoverItemsControl.ItemsSource = null;
            if (CompactItemsControl != null) CompactItemsControl.ItemsSource = null;
            if (NormalItemsControl != null) NormalItemsControl.ItemsSource = null;
            if (GridItemsControl != null) GridItemsControl.ItemsSource = null;
            if (FullscreenItemsControl != null) FullscreenItemsControl.ItemsSource = null;
            return;
        }

        if (HoverItemsControl != null)
            HoverItemsControl.ItemsSource = mode == GroupViewMode.Hover ? _viewModel.TopUsedShortcuts : null;
        if (CompactItemsControl != null)
            CompactItemsControl.ItemsSource = mode == GroupViewMode.Compact ? _viewModel.TopUsedShortcuts : null;
        if (NormalItemsControl != null)
            NormalItemsControl.ItemsSource = mode == GroupViewMode.Normal ? _viewModel.AllShortcuts : null;
        if (GridItemsControl != null)
            GridItemsControl.ItemsSource = mode == GroupViewMode.Grid ? _viewModel.AllShortcuts : null;
        if (FullscreenItemsControl != null)
            FullscreenItemsControl.ItemsSource = mode == GroupViewMode.Fullscreen ? _viewModel.AllShortcuts : null;
    }

    // Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        Win32Interop.SetToolWindow(_windowHandle);

        if (_viewModel != null)
        {
            // Position and size already set by App.xaml.cs before Show()
            // Just show the correct view without calling UpdateViewMode (which would resize/reposition)
            _isInitializing = true; // Block property handlers during init

            BindActiveViewItemsSource(_viewModel.ViewMode);

            // Directly set visibility without going through UpdateViewMode
            MinimizedView.Visibility = _viewModel.ViewMode == GroupViewMode.Minimized ? Visibility.Visible : Visibility.Collapsed;
            HoverView.Visibility = _viewModel.ViewMode == GroupViewMode.Hover ? Visibility.Visible : Visibility.Collapsed;
            CompactView.Visibility = _viewModel.ViewMode == GroupViewMode.Compact ? Visibility.Visible : Visibility.Collapsed;
            NormalView.Visibility = _viewModel.ViewMode == GroupViewMode.Normal ? Visibility.Visible : Visibility.Collapsed;
            GridView.Visibility = _viewModel.ViewMode == GroupViewMode.Grid ? Visibility.Visible : Visibility.Collapsed;
            FullscreenView.Visibility = _viewModel.ViewMode == GroupViewMode.Fullscreen ? Visibility.Visible : Visibility.Collapsed;

            // Apply the view mode to set correct size/resizemode
            ApplyViewMode(_viewModel.ViewMode);

            _isInitializing = false;

            // Apply dock mode after a delay
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isInitializing = true;
                ApplyDockMode(_viewModel.DockMode);
                _isInitializing = false;

                // NOW enable property change handlers
                // This is NOT a duplicate - OnDataContextChanged doesn't subscribe when _windowHandle is Zero
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.AllShortcuts.CollectionChanged += OnShortcutsCollectionChanged;

                // Fade in now that everything is positioned correctly
                Opacity = 1.0;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.AllShortcuts.CollectionChanged -= OnShortcutsCollectionChanged;
        }

        // Detach from desktop if needed
        if (_isAttachedToDesktop && _windowHandle != IntPtr.Zero)
        {
            Win32Interop.DetachFromDesktop(_windowHandle);
            _isAttachedToDesktop = false;
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        // Add visual feedback - increase border thickness slightly
        if (_viewModel != null)
        {
            // Apply glow effect to the active view
            var activeView = GetActiveView();
            if (activeView is Border border)
            {
                border.BorderThickness = new Thickness(3); // Thicker border when focused
            }
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // Remove visual feedback
        if (_viewModel != null)
        {
            // Restore normal border thickness
            var activeView = GetActiveView();
            if (activeView is Border border)
            {
                // Restore based on view mode
                border.BorderThickness = _viewModel.ViewMode == GroupViewMode.Minimized 
                    ? new Thickness(1) 
                    : new Thickness(2);
            }

            // RESTORE WS_EX_NOACTIVATE for Sticky mode when window loses focus
            if (_viewModel.DockMode == GroupDockMode.Sticky && _windowHandle != IntPtr.Zero)
            {
                int exStyle = Win32Interop.GetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE);
                Win32Interop.SetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE, exStyle | Win32Interop.WS_EX_NOACTIVATE);
                Win32Interop.AttachToDesktop(_windowHandle);
            }
        }
    }

    /// <summary>
    /// Get the currently visible view Border element
    /// </summary>
    private Border? GetActiveView()
    {
        if (MinimizedView.Visibility == Visibility.Visible) return MinimizedView;
        if (HoverView.Visibility == Visibility.Visible) return HoverView;
        if (CompactView.Visibility == Visibility.Visible) return CompactView;
        if (NormalView.Visibility == Visibility.Visible) return NormalView;
        if (GridView.Visibility == Visibility.Visible) return GridView;
        if (FullscreenView.Visibility == Visibility.Visible) return FullscreenView;
        return null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GroupViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            oldVm.AllShortcuts.CollectionChanged -= OnShortcutsCollectionChanged;
        }

        _viewModel = e.NewValue as GroupViewModel;

        if (_viewModel != null && _windowHandle != IntPtr.Zero)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.AllShortcuts.CollectionChanged += OnShortcutsCollectionChanged;
            ApplyDockMode(_viewModel.DockMode);
            ApplyViewMode(_viewModel.ViewMode);
        }
    }

    // ViewModel Property Change Handler

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Skip during initialization
        if (_isInitializing)
        {
            return;
        }

        // Prevent re-entry
        if (_isApplyingChanges)
        {
            return;
        }

        _isApplyingChanges = true;

        try
        {
            switch (e.PropertyName)
            {
                case nameof(GroupViewModel.DockMode):
                    if (_viewModel != null) 
                    {
                        ApplyDockMode(_viewModel.DockMode);
                    }
                    break;

                case nameof(GroupViewModel.ViewMode):
                    if (_viewModel != null) 
                    {
                        // CRITICAL: Save position BEFORE switching to fullscreen!
                        if (_viewModel.ViewMode == GroupViewMode.Fullscreen)
                        {
                            // Save current window position/size (ViewModel saves view mode)
                            _preFullscreenLeft = Left;
                            _preFullscreenTop = Top;
                            _preFullscreenWidth = Width;
                            _preFullscreenHeight = Height;

                            System.Diagnostics.Debug.WriteLine($"💾 SAVING position before fullscreen: ({_preFullscreenLeft}, {_preFullscreenTop}), size: {_preFullscreenWidth}x{_preFullscreenHeight}");
                        }

                        UpdateViewMode(_viewModel.ViewMode);
                    }
                    break;

                case nameof(GroupViewModel.Width):
                case nameof(GroupViewModel.Height):
                    if (!_isSyncingSizeFromWindow && _viewModel != null && _viewModel.ViewMode == GroupViewMode.Normal)
                    {
                        SizeToContent = SizeToContent.Manual;
                        Width = _viewModel.Width;
                        Height = _viewModel.Height;
                    }
                    break;

                case nameof(GroupViewModel.GridViewWidth):
                case nameof(GroupViewModel.GridViewHeight):
                    if (!_isSyncingSizeFromWindow && _viewModel != null && _viewModel.ViewMode == GroupViewMode.Grid)
                    {
                        SizeToContent = SizeToContent.Manual;
                        Width = _viewModel.GridViewWidth;
                        Height = _viewModel.GridViewHeight;
                    }
                    break;

                case nameof(GroupViewModel.IsVisible):
                    // DISABLED: Causes infinite loop when hiding/showing
                    // Visibility is now controlled directly by App.xaml.cs
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnViewModelPropertyChanged: ERROR - {ex.Message}");
        }
        finally
        {
            _isApplyingChanges = false;
        }
    }

    // Dock Mode

    private void ApplyDockMode(GroupDockMode mode)
    {
        if (_windowHandle == IntPtr.Zero) return;

        switch (mode)
        {
            case GroupDockMode.Sticky:
                Topmost = false;
                Win32Interop.SetTopmost(_windowHandle, false);

                // Add WS_EX_NOACTIVATE to prevent activation on click
                int exStyle = Win32Interop.GetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE);
                Win32Interop.SetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE, exStyle | Win32Interop.WS_EX_NOACTIVATE);

                // Show window first
                Visibility = Visibility.Visible;
                Show();
                UpdateLayout();

                // Attach to desktop layer (HWND_BOTTOM)
                bool attached = Win32Interop.AttachToDesktop(_windowHandle);
                _isAttachedToDesktop = attached;

                if (_viewModel == null) break;
                var targetX = (int)Math.Round(_viewModel.PositionX);
                var targetY = (int)Math.Round(_viewModel.PositionY);
                SetWindowPosInDips(Win32Interop.HWND_BOTTOM,
                    targetX, targetY, 
                    (int)Width, (int)Height,
                    Win32Interop.SWP_SHOWWINDOW | Win32Interop.SWP_NOACTIVATE);

                // Sync WPF position to match Win32 position
                Left = targetX;
                Top = targetY;
                break;

            case GroupDockMode.Float:
                if (_isAttachedToDesktop)
                {
                    Win32Interop.DetachFromDesktop(_windowHandle);
                    _isAttachedToDesktop = false;
                }

                // Remove WS_EX_NOACTIVATE so window can be activated normally
                int exStyle2 = Win32Interop.GetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE);
                Win32Interop.SetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE, exStyle2 & ~Win32Interop.WS_EX_NOACTIVATE);

                Topmost = true;
                Win32Interop.SetTopmost(_windowHandle, true);
                Visibility = Visibility.Visible;
                Show();
                break;
        }
    }

    // View Mode

    private void ApplyViewMode(GroupViewMode mode)
    {
        BindActiveViewItemsSource(mode);

        switch (mode)
        {
            case GroupViewMode.Minimized:
                // Force manual sizing for minimized mode to avoid SizeToContent issues
                SizeToContent = SizeToContent.Manual;
                ResizeMode = ResizeMode.NoResize;
                Width = 220;   // Match App.xaml.cs startup size (fits icon + name + count)
                Height = 40;
                MinWidth = 200; // Match Border MinWidth
                MinHeight = 40;
                break;

            case GroupViewMode.Hover:
                SizeToContent = SizeToContent.WidthAndHeight;
                ResizeMode = ResizeMode.NoResize;
                MinWidth = 200;
                MinHeight = 100;
                break;

            case GroupViewMode.Compact:
                ResizeMode = ResizeMode.NoResize;
                MinWidth = 180;
                MinHeight = 60;
                if (CompactView != null)
                {
                    SizeToContent = SizeToContent.WidthAndHeight;
                    UpdateLayout();
                    Width = Math.Max(Math.Ceiling(ActualWidth), 200);
                    Height = Math.Max(Math.Ceiling(ActualHeight), 60);
                    SizeToContent = SizeToContent.Manual;
                }
                else
                {
                    SizeToContent = SizeToContent.Manual;
                    Width = 220;
                    Height = 140;
                }
                break;

            case GroupViewMode.Normal:
                SizeToContent = SizeToContent.Manual;
                ResizeMode = ResizeMode.CanResizeWithGrip;
                MinWidth = 200;
                MinHeight = 180;
                if (_viewModel != null)
                {
                    Width = _viewModel.Width;
                    Height = _viewModel.Height;
                }
                else
                {
                    Width = 300;
                    Height = 250;
                }
                break;

            case GroupViewMode.Grid:
                SizeToContent = SizeToContent.Manual;
                ResizeMode = ResizeMode.CanResizeWithGrip;
                MinWidth = 250;
                MinHeight = 230;
                if (_viewModel != null)
                {
                    Width = _viewModel.GridViewWidth;
                    Height = _viewModel.GridViewHeight;
                }
                else
                {
                    Width = 400;
                    Height = 300;
                }
                break;

            case GroupViewMode.Fullscreen:
                SizeToContent = SizeToContent.Manual;
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                ResizeMode = ResizeMode.NoResize;

                // If attached to desktop, temporarily detach for fullscreen
                if (_isAttachedToDesktop)
                {
                    Win32Interop.DetachFromDesktop(_windowHandle);
                    _isAttachedToDesktop = false;
                }

                // Ensure window is topmost and visible
                Topmost = true;
                Show();
                Activate();
                Win32Interop.SetTopmost(_windowHandle, true);
                Win32Interop.ShowWindow(_windowHandle, Win32Interop.SW_SHOW);
                break;
        }
    }

    // Mouse Enter/Leave for Minimized → Hover transition

    private void Minimized_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverLeaveTimer?.Stop();
        _viewModel?.OnMouseEnter();
    }

    private void Minimized_MouseLeave(object sender, MouseEventArgs e)
    {
        // Small delay before reverting to minimized (prevents flicker)
        StartHoverLeaveTimer();
    }

    private void Hover_MouseLeave(object sender, MouseEventArgs e)
    {
        StartHoverLeaveTimer();
    }

    private void StartHoverLeaveTimer()
    {
        if (_hoverLeaveTimer == null)
        {
            _hoverLeaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _hoverLeaveTimer.Tick += (s, args) =>
            {
                _hoverLeaveTimer!.Stop();
                if (!IsMouseOver && _viewModel?.ViewMode == GroupViewMode.Hover)
                    _viewModel.ViewMode = GroupViewMode.Minimized;
            };
        }
        else
        {
            _hoverLeaveTimer.Stop();
        }
        _hoverLeaveTimer.Start();
    }

    private void OnShortcutsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isInitializing || _isUpdatingViewMode) return;

        if (_viewModel?.ViewMode == GroupViewMode.Compact)
            Dispatcher.BeginInvoke(RefreshCompactSize, System.Windows.Threading.DispatcherPriority.ContextIdle);
        else if (_viewModel?.ViewMode == GroupViewMode.Hover)
            Dispatcher.BeginInvoke(RefreshHoverSize, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void RefreshCompactSize()
    {
        if (_viewModel?.ViewMode != GroupViewMode.Compact || _windowHandle == IntPtr.Zero) return;

        SizeToContent = SizeToContent.WidthAndHeight;
        UpdateLayout();
        int newW = (int)Math.Max(Math.Ceiling(ActualWidth), 200);
        int newH = (int)Math.Max(Math.Ceiling(ActualHeight), 60);
        SizeToContent = SizeToContent.Manual;
        Width = newW;
        Height = newH;
        SetWindowPosInDips(IntPtr.Zero, 0, 0, newW, newH,
            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_NOMOVE);
    }

    private void RefreshHoverSize()
    {
        if (_viewModel?.ViewMode != GroupViewMode.Hover || _windowHandle == IntPtr.Zero) return;

        SizeToContent = SizeToContent.WidthAndHeight;
        UpdateLayout();
        int newW = (int)Math.Max(Math.Ceiling(ActualWidth), 200);
        int newH = (int)Math.Max(Math.Ceiling(ActualHeight), 60);
        SizeToContent = SizeToContent.Manual;
        Width = newW;
        Height = newH;
        SetWindowPosInDips(IntPtr.Zero, 0, 0, newW, newH,
            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_NOMOVE);
    }

    // Drag and Drop

    private void OnDragEnter(object sender, DragEventArgs e)
    {
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
        if (e.Data.GetDataPresent("DeskGroupShortcut"))
            e.Effects = DragDropEffects.Move;
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        // Cross-group shortcut transfer — dropped on header, compact, or hover area
        if (e.Data.GetDataPresent("DeskGroupShortcut"))
        {
            var source = e.Data.GetData("DeskGroupShortcut") as ShortcutItemViewModel;
            if (source != null && _viewModel != null && !_viewModel.AllShortcuts.Contains(source))
            {
                var sourceGroup = GetMainViewModel()?.Groups.FirstOrDefault(g => g.AllShortcuts.Contains(source));
                if (sourceGroup != null)
                    sourceGroup.TransferShortcutToGroup(source, _viewModel);
                else
                {
                    var sourceDock = GetMainViewModel()?.ActiveEdgeDocks.FirstOrDefault(d => d.AllShortcuts.Contains(source));
                    sourceDock?.TransferShortcutToGroup(source, _viewModel);
                }
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                FilesDropped?.Invoke(this, files);

                // Also directly add to viewmodel
                foreach (var file in files)
                {
                    var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".lnk" || ext == ".url" || ext == ".exe" ||
                        System.IO.Directory.Exists(file))
                    {
                        _viewModel?.AddShortcutCommand.Execute(file);
                    }
                }
            }
        }
        e.Handled = true;
    }

    // Routed Event Handlers from Child Controls

    private void OnRemoveShortcutRequested(object sender, RoutedEventArgs e)
    {
        if (e is RemoveShortcutRoutedEventArgs removeArgs && _viewModel != null)
        {
            var tile = removeArgs.SourceTile;
            var shortcutVm = _viewModel.AllShortcuts
                .FirstOrDefault(s => s.DisplayName == tile.DisplayName);

            if (shortcutVm != null)
            {
                var result = MessageBox.Show(
                    $"Remove \"{shortcutVm.DisplayName}\" from this group?\n\n" +
                    "The original desktop shortcut will be restored.",
                    "Remove Shortcut",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.RemoveShortcutCommand.Execute(shortcutVm);
                }
            }
        }
        e.Handled = true;
    }

    private void OnTogglePinRequested(object sender, RoutedEventArgs e)
    {
        _viewModel?.TogglePinCommand.Execute(null);
        e.Handled = true;
    }

    private void OnToggleSnapRequested(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleSnapToGridCommand.Execute(null);
        e.Handled = true;
    }

    private void OnSetStickyRequested(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.DockMode != GroupDockMode.Sticky)
        {
            // CRITICAL: Sync window position to ViewModel BEFORE toggling mode!
            if (Left > -5000 && Top > -5000)
            {
                _viewModel.PositionX = Math.Round(Left);
                _viewModel.PositionY = Math.Round(Top);
            }

            _viewModel.ToggleDockModeCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void OnSetFloatRequested(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.DockMode != GroupDockMode.Float)
        {
            // CRITICAL: Sync window position to ViewModel BEFORE toggling mode!
            if (Left > -5000 && Top > -5000)
            {
                _viewModel.PositionX = Math.Round(Left);
                _viewModel.PositionY = Math.Round(Top);
            }

            _viewModel.ToggleDockModeCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void OnOpenSettingsRequested(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    // Context Menu Handlers

    private void WidgetContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu cm && _viewModel != null)
        {
            cm.DataContext = _viewModel;
        }
        if (_viewModel != null)
        {
            if (MenuPinPosition != null) MenuPinPosition.IsChecked = _viewModel.IsPinned;
            if (MenuSnapToGrid != null) MenuSnapToGrid.IsChecked = _viewModel.SnapToGrid;
        }
    }

    private void MenuPinPosition_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.TogglePinCommand.Execute(null);
        if (MenuPinPosition != null && _viewModel != null)
            MenuPinPosition.IsChecked = _viewModel.IsPinned;
    }

    private void MenuSnapToGrid_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ToggleSnapToGridCommand.Execute(null);
        if (MenuSnapToGrid != null && _viewModel != null)
            MenuSnapToGrid.IsChecked = _viewModel.SnapToGrid;
    }

    private void MenuToggleDockMode_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        if (Left > -5000 && Top > -5000)
        {
            _viewModel.PositionX = Math.Round(Left);
            _viewModel.PositionY = Math.Round(Top);
        }
        _viewModel.ToggleDockModeCommand.Execute(null);
    }

    private void MenuAddShortcut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a shortcut or application to add",
            Filter = "Shortcuts & Apps (*.lnk;*.url;*.exe)|*.lnk;*.url;*.exe|All files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        if (dialog.ShowDialog(this) == true)
        {
            foreach (var file in dialog.FileNames)
            {
                _viewModel?.AddShortcutCommand.Execute(file);
            }
        }
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e)
    {
        var inputDialog = new InputDialog("Rename Group", "Enter new group name:",
            _viewModel?.Name ?? "");
        try { if (IsLoaded && IsVisible) inputDialog.Owner = this; } catch { }
        inputDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.ResponseText))
        {
            _viewModel?.RenameCommand.Execute(inputDialog.ResponseText);
        }
    }

    private void MenuChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a group icon",
            Filter = "Icon files (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|Image files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel?.SetGroupIconCommand.Execute(dialog.FileName);
        }
    }

    private void MenuChangeColor_Click(object sender, RoutedEventArgs e)
    {
        var colorDialog = new ColorPickerDialog(_viewModel?.AccentColorHex ?? "#0078D4");
        try { if (IsLoaded && IsVisible) colorDialog.Owner = this; } catch { }
        colorDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        if (colorDialog.ShowDialog() == true)
        {
            var newColor = colorDialog.SelectedColor;
            if (_viewModel != null && !string.IsNullOrEmpty(newColor))
            {
                _viewModel.AccentColorHex = newColor;
            }
        }
    }

    private void MenuGroupAppearance_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        var dialog = new PerGroupAppearanceDialog(_viewModel);
        try { if (IsLoaded && IsVisible) dialog.Owner = this; } catch { }
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.Show();
    }

    private void MenuResetGroupAppearance_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ResetGroupAppearance();
    }

    private void MenuDeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Delete group \"{_viewModel?.Name}\"?\n\n" +
            "All shortcuts will be restored to the desktop.",
            "Delete Group",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MenuSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Footer click handler - restores sticky mode after Show All
    /// </summary>
    private void Footer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // DIRECTLY trigger restore logic
        if (_viewModel != null)
        {
            var app = Application.Current as App;
            if (app != null)
            {
                var proxy = app.Resources["AppDataProxy"] as DeskGroup.Helpers.BindingProxy;
                var mainVm = proxy?.Data as DeskGroup.ViewModels.MainViewModel;

                if (mainVm != null)
                {
                    // Check if ANY group has been temporarily floated
                    bool anyGroupTemporarilyFloated = mainVm.Groups.Any(g => g.SavedDockModeBeforeFloat.HasValue);

                    if (anyGroupTemporarilyFloated)
                    {
                        mainVm.RestoreAllTemporaryFloats();
                    }
                }
            }
        }
    }

    // ============================================================================================
    // VIEW MODE SWITCHING (Safe, no loops!)
    // ============================================================================================

    private void UpdateViewMode(GroupViewMode mode)
    {
        // CRITICAL: Prevent re-entry!
        if (_isUpdatingViewMode)
        {
            return;
        }

        _isUpdatingViewMode = true;

        try
        {
            BindActiveViewItemsSource(mode);

            // IMPORTANT: Set sizing mode FIRST before changing visibility
            switch (mode)
            {
                case GroupViewMode.Minimized:
                    bool wasFullscreen = (FullscreenView.Visibility == Visibility.Visible);
                    int targetX = wasFullscreen && !double.IsNaN(_preFullscreenLeft) ? (int)_preFullscreenLeft : (_viewModel != null ? (int)_viewModel.PositionX : 0);
                    int targetY = wasFullscreen && !double.IsNaN(_preFullscreenTop) ? (int)_preFullscreenTop : (_viewModel != null ? (int)_viewModel.PositionY : 0);

                    // CRITICAL: Set MinHeight BEFORE sizing — prevents WPF coercing Height up to the previous view's MinHeight
                    ResizeMode = ResizeMode.NoResize;
                    MinWidth = 180;
                    MinHeight = 30;

                    // Switch to MinimizedView content
                    MinimizedView.Visibility = Visibility.Visible;
                    HoverView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    // Let WPF auto-size to exact content (handles sub-pixel/DPI correctly, no gap)
                    SizeToContent = SizeToContent.WidthAndHeight;
                    UpdateLayout();
                    int targetWidth = (int)Math.Max(Math.Ceiling(ActualWidth), 200);
                    int targetHeight = (int)Math.Max(Math.Ceiling(ActualHeight), 30);
                    SizeToContent = SizeToContent.Manual;

                    // WIN32: Atomic position + size
                    if (_windowHandle != IntPtr.Zero)
                        SetWindowPosInDips(IntPtr.Zero, targetX, targetY, targetWidth, targetHeight,
                            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);

                    Left = targetX; Top = targetY;
                    Width = targetWidth; Height = targetHeight;
                    if (wasFullscreen) { WindowStyle = WindowStyle.None; Topmost = false; }
                    Visibility = Visibility.Visible;
                    if (wasFullscreen) _hoverLeaveTimer?.Stop();
                    break;

                case GroupViewMode.Hover:
                    bool wasFullscreenHover = (FullscreenView.Visibility == Visibility.Visible);
                    int hoverTargetX = wasFullscreenHover && !double.IsNaN(_preFullscreenLeft) ? (int)_preFullscreenLeft : (_viewModel != null ? (int)_viewModel.PositionX : 0);
                    int hoverTargetY = wasFullscreenHover && !double.IsNaN(_preFullscreenTop) ? (int)_preFullscreenTop : (_viewModel != null ? (int)_viewModel.PositionY : 0);

                    // Lock position via WndProc trap (prevents window jumping during visibility change)
                    SetPendingPositionInDips(hoverTargetX, hoverTargetY, 200, 60);
                    Left = hoverTargetX;
                    Top = hoverTargetY;
                    ResizeMode = ResizeMode.NoResize;
                    MinWidth = 200;
                    MinHeight = 60;

                    // Switch to HoverView content
                    MinimizedView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Visible;

                    // Clear WndProc trap; position is now locked
                    _hasPendingPosition = false;

                    // Let WPF auto-size to exact content (handles sub-pixel/DPI correctly, no gap)
                    SizeToContent = SizeToContent.WidthAndHeight;
                    UpdateLayout();
                    int hoverTargetW = (int)Math.Max(Math.Ceiling(ActualWidth), 200);
                    int hoverTargetH = (int)Math.Max(Math.Ceiling(ActualHeight), 60);
                    SizeToContent = SizeToContent.Manual;

                    // Apply size at correct position
                    Width = hoverTargetW;
                    Height = hoverTargetH;
                    if (_windowHandle != IntPtr.Zero)
                        SetWindowPosInDips(IntPtr.Zero, hoverTargetX, hoverTargetY, hoverTargetW, hoverTargetH,
                            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);

                    if (wasFullscreenHover) { WindowStyle = WindowStyle.None; Topmost = false; }
                    Visibility = Visibility.Visible;
                    break;

                case GroupViewMode.Compact:
                    bool wasFullscreenCompact = (FullscreenView.Visibility == Visibility.Visible);
                    int compactTargetX = wasFullscreenCompact && !double.IsNaN(_preFullscreenLeft) ? (int)_preFullscreenLeft : (_viewModel != null ? (int)_viewModel.PositionX : 0);
                    int compactTargetY = wasFullscreenCompact && !double.IsNaN(_preFullscreenTop) ? (int)_preFullscreenTop : (_viewModel != null ? (int)_viewModel.PositionY : 0);

                    // CRITICAL: Set MinHeight BEFORE sizing — prevents WPF coercing Height up to the previous view's MinHeight
                    ResizeMode = ResizeMode.NoResize;
                    MinWidth = 180;
                    MinHeight = 60;

                    // Switch to CompactView content
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Visible;

                    // Let WPF auto-size to exact content (handles sub-pixel/DPI correctly, no gap)
                    SizeToContent = SizeToContent.WidthAndHeight;
                    UpdateLayout();
                    int compactTargetW = (int)Math.Max(Math.Ceiling(ActualWidth), 200);
                    int compactTargetH = (int)Math.Max(Math.Ceiling(ActualHeight), 60);
                    SizeToContent = SizeToContent.Manual;

                    // WIN32: Atomic position + size
                    if (_windowHandle != IntPtr.Zero)
                        SetWindowPosInDips(IntPtr.Zero, compactTargetX, compactTargetY, compactTargetW, compactTargetH,
                            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);

                    Left = compactTargetX; Top = compactTargetY;
                    Width = compactTargetW; Height = compactTargetH;
                    if (wasFullscreenCompact) { WindowStyle = WindowStyle.None; Topmost = false; }
                    Visibility = Visibility.Visible;
                    break;

                case GroupViewMode.Normal:
                    bool wasFullscreenNormal = (FullscreenView.Visibility == Visibility.Visible);
                    int normalTargetX = wasFullscreenNormal && !double.IsNaN(_preFullscreenLeft) ? (int)_preFullscreenLeft : (_viewModel != null ? (int)_viewModel.PositionX : 0);
                    int normalTargetY = wasFullscreenNormal && !double.IsNaN(_preFullscreenTop) ? (int)_preFullscreenTop : (_viewModel != null ? (int)_viewModel.PositionY : 0);
                    int normalTargetW = wasFullscreenNormal && _preFullscreenWidth > 0 ? (int)_preFullscreenWidth : (_viewModel != null ? (int)_viewModel.Width : 300);
                    int normalTargetH = wasFullscreenNormal && _preFullscreenHeight > 0 ? (int)_preFullscreenHeight : (_viewModel != null ? (int)_viewModel.Height : 250);

                    // CRITICAL: Set MinHeight BEFORE sizing — prevents WPF coercing Height up to the previous view's MinHeight
                    SizeToContent = SizeToContent.Manual;
                    ResizeMode = ResizeMode.CanResizeWithGrip;
                    MinWidth = 200;
                    MinHeight = 180;

                    // Switch to NormalView content
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Visible;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    if (_windowHandle != IntPtr.Zero)
                        SetWindowPosInDips(IntPtr.Zero, normalTargetX, normalTargetY, normalTargetW, normalTargetH,
                            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);

                    Left = normalTargetX; Top = normalTargetY;
                    Width = normalTargetW; Height = normalTargetH;
                    if (wasFullscreenNormal) { WindowStyle = WindowStyle.None; Topmost = false; }
                    Visibility = Visibility.Visible;
                    break;

                case GroupViewMode.Grid:
                    bool wasFullscreenGrid = (FullscreenView.Visibility == Visibility.Visible);
                    int gridTargetX = wasFullscreenGrid && !double.IsNaN(_preFullscreenLeft) ? (int)_preFullscreenLeft : (_viewModel != null ? (int)_viewModel.PositionX : 0);
                    int gridTargetY = wasFullscreenGrid && !double.IsNaN(_preFullscreenTop) ? (int)_preFullscreenTop : (_viewModel != null ? (int)_viewModel.PositionY : 0);
                    int gridTargetW = wasFullscreenGrid && _preFullscreenWidth > 0 ? (int)_preFullscreenWidth : (_viewModel != null ? (int)_viewModel.GridViewWidth : 400);
                    int gridTargetH = wasFullscreenGrid && _preFullscreenHeight > 0 ? (int)_preFullscreenHeight : (_viewModel != null ? (int)_viewModel.GridViewHeight : 300);

                    // CRITICAL: Set MinHeight BEFORE sizing — prevents WPF coercing Height up to the previous view's MinHeight
                    SizeToContent = SizeToContent.Manual;
                    ResizeMode = ResizeMode.CanResizeWithGrip;
                    MinWidth = 250;
                    MinHeight = 230;

                    // Switch to GridView content
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Visible;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    if (_windowHandle != IntPtr.Zero)
                        SetWindowPosInDips(IntPtr.Zero, gridTargetX, gridTargetY, gridTargetW, gridTargetH,
                            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);

                    Left = gridTargetX; Top = gridTargetY;
                    Width = gridTargetW; Height = gridTargetH;
                    if (wasFullscreenGrid) { WindowStyle = WindowStyle.None; Topmost = false; }
                    Visibility = Visibility.Visible;
                    break;

                case GroupViewMode.Fullscreen:
                    // Calculate target dimensions FIRST
                    var fsMon = new Services.MonitorService().GetMonitorFromPoint((Left + Width / 2) * DpiScaleX, (Top + Height / 2) * DpiScaleY);
                    int fsX = fsMon != null ? (int)(fsMon.Bounds.Left / DpiScaleX) : 0;
                    int fsY = fsMon != null ? (int)(fsMon.Bounds.Top / DpiScaleY) : 0;
                    var screenWidth = fsMon != null ? (int)(fsMon.Bounds.Width / DpiScaleX) : (int)SystemParameters.PrimaryScreenWidth;
                    var screenHeight = fsMon != null ? (int)(fsMon.Bounds.Height / DpiScaleY) : (int)SystemParameters.PrimaryScreenHeight;

                    System.Diagnostics.Debug.WriteLine($"🖥️ ENTERING fullscreen: {screenWidth}x{screenHeight}");

                    // Collapse all non-fullscreen views first
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;

                    // WIN32 FIRST: Resize/reposition BEFORE content becomes visible (Fix #9 — no size flash)
                    if (_windowHandle != IntPtr.Zero)
                    {
                        bool success = SetWindowPosInDips(Win32Interop.HWND_TOPMOST,
                            fsX, fsY, screenWidth, screenHeight,
                            Win32Interop.SWP_SHOWWINDOW);

                        System.Diagnostics.Debug.WriteLine($"✅ SetWindowPos fullscreen: {success}");
                    }

                    // Show fullscreen content AFTER window is already at the correct size
                    FullscreenView.Visibility = Visibility.Visible;

                    // Sync WPF properties AFTER (bookkeeping only - no triggers!)
                    Left = fsX;
                    Top = fsY;
                    Width = screenWidth;
                    Height = screenHeight;
                    WindowStyle = WindowStyle.None;
                    Topmost = true;
                    SizeToContent = SizeToContent.Manual;
                    ResizeMode = ResizeMode.NoResize;
                    MinWidth = 800;
                    MinHeight = 600;
                    Visibility = Visibility.Visible;
                    break;

                default:
                    MinimizedView.Visibility = Visibility.Visible;
                    HoverView.Visibility = Visibility.Collapsed;
                    CompactView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;
                    SizeToContent = SizeToContent.Manual;
                    ResizeMode = ResizeMode.NoResize;
                    MinWidth = 200;
                    MinHeight = 40;
                    Width = 220;
                    Height = 40;
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateViewMode: ERROR - {ex.Message}");
        }
        finally
        {
            _isUpdatingViewMode = false;
        }
    }

    // Event Handlers for View Mode

    private void MinimizedView_MouseEnter(object sender, MouseEventArgs e)
    {
        // Don't expand if dragging
        if (_isDragging || _dragStarted)
        {
            return;
        }

        // Cancel any pending collapse timer
        _hoverLeaveTimer?.Stop();

        // Switch to hover view to show preview
        if (_viewModel != null && _viewModel.ViewMode == GroupViewMode.Minimized)
        {
            _viewModel.ViewMode = GroupViewMode.Hover;
        }
    }

    private void MinimizedView_MouseLeave(object sender, MouseEventArgs e)
    {
        // Don't collapse here - let hover view handle it
    }

    private void HoverView_MouseLeave(object sender, MouseEventArgs e)
    {
        // Don't collapse if dragging
        if (_isDragging || _dragStarted)
        {
            return;
        }

        // Start timer to collapse back to minimized after short delay
        StartHoverLeaveTimer();
    }

    private void SetMinimized_Click(object sender, RoutedEventArgs e)
    {
        if (!IsActive) { Activate(); Focus(); }
        _viewModel?.SetMinimizedCommand.Execute(null);
    }

    private void SetCompact_Click(object sender, RoutedEventArgs e)
    {
        if (!IsActive) { Activate(); Focus(); }
        _viewModel?.SetCompactCommand.Execute(null);
    }

    private void SetNormal_Click(object sender, RoutedEventArgs e)
    {
        if (!IsActive) { Activate(); Focus(); }
        _viewModel?.SetNormalCommand.Execute(null);
    }

    private void SetGrid_Click(object sender, RoutedEventArgs e)
    {
        if (!IsActive) { Activate(); Focus(); }
        _viewModel?.SetGridCommand.Execute(null);
    }

    private void SetFullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (!IsActive) { Activate(); Focus(); }
        _viewModel?.SetFullscreenCommand.Execute(null);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // F11 toggles fullscreen (ViewModel handles view mode restoration)
        if (e.Key == Key.F11)
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }

        // ESC exits fullscreen (ViewModel handles view mode restoration)
        if (e.Key == Key.Escape && _viewModel.ViewMode == GroupViewMode.Fullscreen)
        {
            _viewModel.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ============================================================================================
    // SHORTCUT CLICK HANDLER
    // ============================================================================================

    private void Shortcut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the ShortcutItemViewModel from the DataContext
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement?.DataContext is ShortcutItemViewModel shortcut)
            {
                // If in fullscreen, exit to previous view mode (ViewModel handles restoration)
                if (_viewModel?.ViewMode == GroupViewMode.Fullscreen)
                {
                    _viewModel.ToggleFullscreenCommand.Execute(null);
                }

                // RESTORE STICKY MODE: If user temporarily floated this window via Show/Hide All,
                // restore it back to Sticky when they click an icon
                if (_viewModel != null && _viewModel.SavedDockModeBeforeFloat.HasValue)
                {
                    _viewModel.DockMode = _viewModel.SavedDockModeBeforeFloat.Value;
                    _viewModel.SavedDockModeBeforeFloat = null;
                }

                shortcut.LaunchCommand.Execute(null);
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shortcut_Click: ERROR - {ex.Message}");
            MessageBox.Show(
                $"Failed to launch shortcut:\n\n{ex.Message}",
                "Launch Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Shortcut_RightClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement?.DataContext is ShortcutItemViewModel shortcut)
            {
                var menu = new ContextMenu();

                // Remove shortcut
                var removeItem = new MenuItem { Header = "Remove from group" };
                removeItem.Click += (s, args) =>
                {
                    _viewModel?.RemoveShortcutCommand.Execute(shortcut);
                };
                menu.Items.Add(removeItem);

                menu.PlacementTarget = frameworkElement;
                menu.IsOpen = true;
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shortcut_RightClick: ERROR - {ex.Message}");
        }
    }

    // ============================================================================================
    // GLOBAL MOUSE CLICK HANDLER (for sticky mode restoration)
    // ============================================================================================

    private void OnAnyMouseClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // ACTIVATE WINDOW: Give this window keyboard focus so F11 and ContextMenu work!
            // For Sticky mode, we temporarily remove WS_EX_NOACTIVATE (WndProc keeps it at HWND_BOTTOM).
            if (_viewModel?.DockMode == GroupDockMode.Sticky && _windowHandle != IntPtr.Zero)
            {
                int exStyle = Win32Interop.GetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE);
                Win32Interop.SetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE, exStyle & ~Win32Interop.WS_EX_NOACTIVATE);
            }

            if (!IsActive)
            {
                Activate();
                Focus();
            }

            // RESTORE ALL GROUPS: Check if ANY group has SavedDockModeBeforeFloat set
            if (_viewModel != null)
            {
                // CRITICAL: Prevent immediate restoration on the SAME click that triggered Show All!
                var timeSinceLastTempFloat = DateTime.Now - _lastTempFloatTime;
                if (timeSinceLastTempFloat.TotalMilliseconds < 500)
                {
                    return;
                }

                // Get the MainViewModel to check ALL groups
                var app = Application.Current as App;
                if (app != null)
                {
                    var proxy = app.Resources["AppDataProxy"] as DeskGroup.Helpers.BindingProxy;
                    var mainVm = proxy?.Data as DeskGroup.ViewModels.MainViewModel;

                    if (mainVm != null)
                    {
                        bool anyGroupTemporarilyFloated = mainVm.Groups.Any(g => g.SavedDockModeBeforeFloat.HasValue);

                        if (anyGroupTemporarilyFloated)
                        {
                            mainVm.RestoreAllTemporaryFloats();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAnyMouseClick: ERROR - {ex.Message}");
        }
    }

    /// <summary>
    /// Sync window position to ViewModel on EVERY mouse release.
    /// This ensures position is always up-to-date, even if window was moved by Windows (Win+Arrow, snap, etc.)
    /// </summary>
    private void OnMouseUpSyncPosition(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || _isInitializing) return;

        // Only sync if window is on-screen
        if (Left > -5000 && Top > -5000 && Left < 10000 && Top < 10000)
        {
            var currentX = Math.Round(Left);
            var currentY = Math.Round(Top);

            // Only update if position changed significantly (avoid spamming saves)
            if (Math.Abs(currentX - _viewModel.PositionX) > 1 || Math.Abs(currentY - _viewModel.PositionY) > 1)
            {
                _viewModel.PositionX = currentX;
                _viewModel.PositionY = currentY;
            }
        }
    }

    // ============================================================================================
    // WINDOW RESIZE HANDLER
    // ============================================================================================

    /// <summary>
    /// Sync window size to ViewModel when user resizes the window.
    /// Only applies to Normal and Grid modes (resizable modes).
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewModel == null || _isInitializing || _isUpdatingViewMode || _isSyncingSizeFromWindow) return;

        if (_viewModel.ViewMode == GroupViewMode.Normal)
        {
            if (Math.Abs(e.NewSize.Width - _viewModel.Width) > 1 || Math.Abs(e.NewSize.Height - _viewModel.Height) > 1)
            {
                _isSyncingSizeFromWindow = true; _viewModel.Width = Math.Round(e.NewSize.Width);
                _viewModel.Height = Math.Round(e.NewSize.Height); _isSyncingSizeFromWindow = false; return;
            }
        }
        else if (_viewModel.ViewMode == GroupViewMode.Grid)
        {
            if (Math.Abs(e.NewSize.Width - _viewModel.GridViewWidth) > 1 || Math.Abs(e.NewSize.Height - _viewModel.GridViewHeight) > 1)
            {
                _isSyncingSizeFromWindow = true; _viewModel.GridViewWidth = Math.Round(e.NewSize.Width);
                _viewModel.GridViewHeight = Math.Round(e.NewSize.Height); _isSyncingSizeFromWindow = false; return;
            }
        }
    }

    // ============================================================================================
    // DRAG HANDLERS (for moving group by dragging header)
    // ============================================================================================

    private bool _isDragging = false;
    private bool _dragStarted = false;
    private Point _dragStartMouseScreen; // Mouse position on screen
    private Point _dragStartWindowPosition; // Window position when drag started
    private double _preDragWidth;
    private double _preDragHeight;
    private const double DragThreshold = 5.0; // Pixels before drag starts

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.IsPinned == true) return;

        // Don't intercept clicks that originated from icon tiles — let them initiate DnD
        if (e.OriginalSource is DependencyObject origSrc &&
            (origSrc is IconTile || FindParentOfType<IconTile>(origSrc) != null))
            return;

        _isDragging = false;
        _dragStarted = true;
        _dragStartMouseScreen = PointToScreen(e.GetPosition(this));
        _dragStartWindowPosition = new Point(Left, Top);

        // Save current size
        _preDragWidth = ActualWidth;
        _preDragHeight = ActualHeight;

        var element = sender as UIElement;
        element?.CaptureMouse();

        e.Handled = true;
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragStarted || _viewModel?.IsPinned == true) return;

        var currentMouseScreen = PointToScreen(e.GetPosition(this));
        var deltaX = currentMouseScreen.X - _dragStartMouseScreen.X;
        var deltaY = currentMouseScreen.Y - _dragStartMouseScreen.Y;

        // Check if movement exceeds threshold
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        if (!_isDragging && distance < DragThreshold)
        {
            return; // Not enough movement yet
        }

        if (!_isDragging)
        {
            // Just crossed threshold - start actual drag
            _isDragging = true;

            // Stop hover timer to prevent collapse during drag
            _hoverLeaveTimer?.Stop();

            // Lock size to prevent jumping (especially for Hover view)
            SizeToContent = SizeToContent.Manual;
            Width = _preDragWidth;
            Height = _preDragHeight;
        }

        // Calculate position from original baseline + DPI-adjusted delta
        double targetLeft = _dragStartWindowPosition.X + deltaX / DpiScaleX;
        double targetTop = _dragStartWindowPosition.Y + deltaY / DpiScaleY;
        if (_viewModel?.SnapToGrid == true && _viewModel.Settings.SnapGridSize > 1)
        {
            int g = _viewModel.Settings.SnapGridSize;
            targetLeft = Math.Round(targetLeft / g) * g;
            targetTop = Math.Round(targetTop / g) * g;
        }
        if (_windowHandle != IntPtr.Zero)
        {
            Win32Interop.SetWindowPos(
                _windowHandle,
                IntPtr.Zero,
                (int)Math.Round(targetLeft * DpiScaleX),
                (int)Math.Round(targetTop * DpiScaleY),
                0, 0,
                Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE);
        }
        Left = targetLeft;
        Top = targetTop;
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragStarted) return;

        var element = sender as UIElement;
        element?.ReleaseMouseCapture();

        if (_isDragging)
        {
            if (_viewModel != null)
            {
                var newX = Math.Round(Left);
                var newY = Math.Round(Top);

                // Sanity check: positions should be within reasonable screen bounds
                if (newX > -5000 && newX < 10000 && newY > -5000 && newY < 10000)
                {
                    _viewModel.PositionX = newX;
                    _viewModel.PositionY = newY;
                }
            }
        }

        _isDragging = false;
        _dragStarted = false;
        e.Handled = true;
    }

    // ============================================================================================
    // ICON DRAG AND DROP (internal reorder + drag-out remove) — Fix #5
    // ============================================================================================

    private void OnIconDropInternal(object sender, DragEventArgs e)
    {
        if (_viewModel == null) return;
        if (!e.Data.GetDataPresent("DeskGroupShortcut")) { e.Handled = true; return; }

        var source = e.Data.GetData("DeskGroupShortcut") as ShortcutItemViewModel;
        if (source == null) { e.Handled = true; return; }

        if (!_viewModel.AllShortcuts.Contains(source))
        {
            // Cross-group: move the shortcut from its source group into this one
            var sourceGroup = GetMainViewModel()?.Groups.FirstOrDefault(g => g.AllShortcuts.Contains(source));
            if (sourceGroup != null)
                sourceGroup.TransferShortcutToGroup(source, _viewModel);
            else
            {
                var sourceDock = GetMainViewModel()?.ActiveEdgeDocks.FirstOrDefault(d => d.AllShortcuts.Contains(source));
                sourceDock?.TransferShortcutToGroup(source, _viewModel);
            }
        }
        else
        {
            // Same-group reorder
            var dropPoint = e.GetPosition(this);
            var target = FindShortcutViewModelAtPoint(dropPoint);
            if (target != null && !ReferenceEquals(source, target))
                _viewModel.MoveShortcut(source, target);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
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

    public void RemoveShortcutByViewModel(ShortcutItemViewModel vm)
    {
        _viewModel?.RemoveShortcutCommand.Execute(vm);
    }

    // ============================================================================================
    // SHOW / HIDE ALL HANDLERS — Fix #8
    // ============================================================================================

    private MainViewModel? GetMainViewModel()
    {
        var app = Application.Current as App;
        if (app == null) return null;
        var proxy = app.Resources["AppDataProxy"] as DeskGroup.Helpers.BindingProxy;
        return proxy?.Data as MainViewModel;
    }

    private void MenuShowAllGroups_Click(object sender, RoutedEventArgs e)
    {
        GetMainViewModel()?.ShowAllGroupsCommand.Execute(null);
    }

    private void MenuHideAllGroups_Click(object sender, RoutedEventArgs e)
    {
        GetMainViewModel()?.HideAllGroupsCommand.Execute(null);
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
}






/// <summary>
/// Simple input dialog for renaming groups.
/// </summary>
public class InputDialog : Window
{
    private readonly System.Windows.Controls.TextBox _textBox;

    public string ResponseText => _textBox.Text;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Title = title;
        Width = 350;
        Height = 160;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true; // Always on top

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.Margin = new Thickness(16);

        var label = new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 8)
        };
        System.Windows.Controls.Grid.SetRow(label, 0);
        grid.Children.Add(label);

        _textBox = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 0, 12)
        };
        _textBox.SelectAll();
        System.Windows.Controls.Grid.SetRow(_textBox, 1);
        grid.Children.Add(_textBox);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        System.Windows.Controls.Grid.SetRow(buttonPanel, 2);

        var okButton = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 80,
            Padding = new Thickness(0, 4, 0, 4),
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(0, 4, 0, 4),
            IsCancel = true
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        Content = grid;

        Loaded += (s, e) => _textBox.Focus();
    }
}
