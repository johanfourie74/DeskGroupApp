using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

    // Save position before fullscreen
    private double _preFullscreenLeft;
    private double _preFullscreenTop;
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

    /// <summary>
    /// Fired when the user requests to delete this group.
    /// The MainViewModel handles the actual deletion.
    /// </summary>
    public event EventHandler? DeleteRequested;

    /// <summary>
    /// Fired when a file is dropped onto this group widget.
    /// </summary>
    public event EventHandler<string[]>? FilesDropped;

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
            }
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        DataContextChanged += OnDataContextChanged;
        Drop += OnDrop;
        DragEnter += OnDragEnter;
        KeyDown += OnKeyDown; // Keyboard shortcuts
        PreviewMouseLeftButtonDown += OnAnyMouseClick; // Catch ALL clicks to restore sticky mode
        MouseLeftButtonUp += OnMouseUpSyncPosition; // Sync position after ANY mouse release

        // Subscribe to routed events from child controls
        AddHandler(IconTile.RemoveShortcutEvent, new RoutedEventHandler(OnRemoveShortcutRequested));
        AddHandler(DragThumb.TogglePinRequestedEvent, new RoutedEventHandler(OnTogglePinRequested));
        AddHandler(DragThumb.ToggleSnapRequestedEvent, new RoutedEventHandler(OnToggleSnapRequested));
        AddHandler(DragThumb.SetStickyRequestedEvent, new RoutedEventHandler(OnSetStickyRequested));
        AddHandler(DragThumb.SetFloatRequestedEvent, new RoutedEventHandler(OnSetFloatRequested));
    }

    public GroupWidget(GroupViewModel viewModel) : this()
    {
        DataContext = viewModel;
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

            // Directly set visibility without going through UpdateViewMode
            MinimizedView.Visibility = _viewModel.ViewMode == GroupViewMode.Minimized ? Visibility.Visible : Visibility.Collapsed;
            HoverView.Visibility = _viewModel.ViewMode == GroupViewMode.Hover ? Visibility.Visible : Visibility.Collapsed;
            NormalView.Visibility = _viewModel.ViewMode == GroupViewMode.Normal ? Visibility.Visible : Visibility.Collapsed;
            GridView.Visibility = _viewModel.ViewMode == GroupViewMode.Grid ? Visibility.Visible : Visibility.Collapsed;
            FullscreenView.Visibility = _viewModel.ViewMode == GroupViewMode.Fullscreen ? Visibility.Visible : Visibility.Collapsed;

            _isInitializing = false;

            // Apply dock mode after a delay
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isInitializing = true;
                ApplyDockMode(_viewModel.DockMode);
                _isInitializing = false;

                // NOW enable property change handlers
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;

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
        }

        // Detach from desktop if needed
        if (_isAttachedToDesktop && _windowHandle != IntPtr.Zero)
        {
            Win32Interop.DetachFromDesktop(_windowHandle);
            _isAttachedToDesktop = false;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GroupViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as GroupViewModel;

        if (_viewModel != null && _windowHandle != IntPtr.Zero)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
                            // Save current state before fullscreen
                            _preFullscreenLeft = Left;
                            _preFullscreenTop = Top;
                            _preFullscreenWidth = Width;
                            _preFullscreenHeight = Height;
                        }

                        UpdateViewMode(_viewModel.ViewMode);
                    }
                    break;

                case nameof(GroupViewModel.Width):
                case nameof(GroupViewModel.Height):
                    if (_viewModel != null && (_viewModel.ViewMode == GroupViewMode.Normal || _viewModel.ViewMode == GroupViewMode.Grid))
                    {
                        SizeToContent = SizeToContent.Manual;
                        Width = _viewModel.Width;
                        Height = _viewModel.Height;
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
                if (!_isAttachedToDesktop)
                {
                    Topmost = false;
                    Win32Interop.SetTopmost(_windowHandle, false);

                    // Add WS_EX_NOACTIVATE to prevent activation on click
                    int exStyle = Win32Interop.GetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE);
                    Win32Interop.SetWindowLong(_windowHandle, Win32Interop.GWL_EXSTYLE, exStyle | Win32Interop.WS_EX_NOACTIVATE);

                    // Show window first
                    Visibility = Visibility.Visible;
                    Show();
                    UpdateLayout();

                    // Try to attach to desktop
                    bool attached = Win32Interop.AttachToDesktop(_windowHandle);
                    _isAttachedToDesktop = attached;

                    // CRITICAL: Use ViewModel position (already synced before toggle), NOT Window.Left/Top!
                    // Window.Left/Top might be stale if WPF hasn't updated yet from ViewModel changes
                    var targetX = (int)Math.Round(_viewModel.PositionX);
                    var targetY = (int)Math.Round(_viewModel.PositionY);
                    Win32Interop.SetWindowPos(_windowHandle, Win32Interop.HWND_BOTTOM,
                        targetX, targetY, 
                        (int)Width, (int)Height,
                        Win32Interop.SWP_SHOWWINDOW | Win32Interop.SWP_NOACTIVATE);

                    // Sync WPF position to match Win32 position
                    Left = targetX;
                    Top = targetY;
                }
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

            case GroupViewMode.Normal:
                SizeToContent = SizeToContent.Manual;
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
                MinWidth = 200;
                MinHeight = 150;
                ResizeMode = ResizeMode.NoResize;
                break;

            case GroupViewMode.Fullscreen:
                SizeToContent = SizeToContent.Manual;
                if (_viewModel != null)
                {
                    Left = _viewModel.PositionX;
                    Top = _viewModel.PositionY;
                    Width = _viewModel.Width;
                    Height = _viewModel.Height;
                }
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

    // Mouse Enter/Leave for Minimized â†’ Hover transition

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
        _hoverLeaveTimer?.Stop();
        _hoverLeaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _hoverLeaveTimer.Tick += (s, args) =>
        {
            _hoverLeaveTimer.Stop();

            // Collapse if mouse is outside AND we're in hover mode
            if (!IsMouseOver && _viewModel != null)
            {
                if (_viewModel.ViewMode == GroupViewMode.Hover)
                {
                    _viewModel.ViewMode = GroupViewMode.Minimized;
                }
            }
        };
        _hoverLeaveTimer.Start();
    }

    // Drag and Drop

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
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

    // Context Menu Handlers

    private void MenuAddShortcut_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a shortcut or application to add",
            Filter = "Shortcuts & Apps (*.lnk;*.url;*.exe)|*.lnk;*.url;*.exe|All files (*.*)|*.*",
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                _viewModel?.AddShortcutCommand.Execute(file);
            }
        }
    }

    private void MenuRename_Click(object sender, RoutedEventArgs e)
    {
        // Simple input dialog
        var inputDialog = new InputDialog("Rename Group", "Enter new group name:",
            _viewModel?.Name ?? "");

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

        if (dialog.ShowDialog() == true)
        {
            _viewModel?.SetGroupIconCommand.Execute(dialog.FileName);
        }
    }

    private void MenuChangeColor_Click(object sender, RoutedEventArgs e)
    {
        // Simple color picker dialog
        var colorDialog = new ColorPickerDialog(_viewModel?.AccentColorHex ?? "#0078D4");

        if (colorDialog.ShowDialog() == true)
        {
            var newColor = colorDialog.SelectedColor;
            if (_viewModel != null && !string.IsNullOrEmpty(newColor))
            {
                _viewModel.AccentColorHex = newColor;
            }
        }
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
            // IMPORTANT: Set sizing mode FIRST before changing visibility
            switch (mode)
            {
                case GroupViewMode.Minimized:
                    // 1. Check if coming from fullscreen
                    bool wasFullscreen = (FullscreenView.Visibility == Visibility.Visible);

                    // 2. Restore position FIRST (before any size/visibility changes!)
                    if (wasFullscreen && _preFullscreenLeft > 0)
                    {
                        Left = _preFullscreenLeft;
                        Top = _preFullscreenTop;
                    }
                    else if (_viewModel != null)
                    {
                        Left = _viewModel.PositionX;
                        Top = _viewModel.PositionY;
                    }

                    // 3. Restore window style if coming from fullscreen
                    if (wasFullscreen)
                    {
                        WindowStyle = WindowStyle.None; // Keep borderless
                        Topmost = false; // Not topmost in normal modes
                    }

                    // 4. Set sizing mode
                    SizeToContent = SizeToContent.Manual;
                    // 5. Set minimum sizes (match App.xaml.cs and ApplyViewMode!)
                    MinWidth = 200;  // Match Border MinWidth
                    MinHeight = 40;
                    // 6. Set size (match App.xaml.cs and ApplyViewMode!)
                    Width = 220;     // Fits icon + name + count
                    Height = 40;
                    // 7. Force layout with new size
                    UpdateLayout();

                    // 8. Change visibility
                    MinimizedView.Visibility = Visibility.Visible;
                    HoverView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    // 9. Final layout update
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();

                    // 10. If coming from fullscreen, stop any pending hover timer
                    if (wasFullscreen)
                    {
                        _hoverLeaveTimer?.Stop();
                    }
                    break;

                case GroupViewMode.Hover:
                    // 1. Check if coming from fullscreen
                    bool wasFullscreenHover = (FullscreenView.Visibility == Visibility.Visible);

                    // 2. Restore position if coming from fullscreen
                    if (wasFullscreenHover && _preFullscreenLeft > 0)
                    {
                        Left = _preFullscreenLeft;
                        Top = _preFullscreenTop;
                    }

                    // 3. Switch to MANUAL sizing to prevent progressive resize
                    SizeToContent = SizeToContent.Manual;

                    // 4. Hide all views
                    MinimizedView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    // 5. Show HoverView but measure it off-screen
                    HoverView.Visibility = Visibility.Visible;
                    HoverView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                    // 6. Get the desired size
                    var desiredWidth = Math.Max(HoverView.DesiredSize.Width + 20, 200); // +20 for borders/padding
                    var desiredHeight = Math.Max(HoverView.DesiredSize.Height + 20, 60);

                    // 7. Set window to exact size immediately
                    Width = desiredWidth;
                    Height = desiredHeight;
                    MinWidth = 200;
                    MinHeight = 60;

                    // 8. Force layout
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();
                    break;

                case GroupViewMode.Normal:
                    // 1. Check if coming from fullscreen
                    bool wasFullscreenNormal = (FullscreenView.Visibility == Visibility.Visible);

                    // 2. Restore topmost if coming from fullscreen
                    if (wasFullscreenNormal)
                    {
                        Topmost = false;
                    }

                    // 3. Set sizing mode FIRST
                    SizeToContent = SizeToContent.Manual;
                    // 4. RESET minimum sizes
                    MinWidth = 200;
                    MinHeight = 150;

                    // 5. Restore position/size (AFTER setting sizing mode!)
                    if (wasFullscreenNormal && _preFullscreenLeft > 0)
                    {
                        Left = _preFullscreenLeft;
                        Top = _preFullscreenTop;
                        Width = _preFullscreenWidth;
                        Height = _preFullscreenHeight;
                    }
                    else if (_viewModel != null)
                    {
                        Left = _viewModel.PositionX;
                        Top = _viewModel.PositionY;
                        Width = _viewModel.Width;
                        Height = _viewModel.Height;
                    }

                    // 6. Force layout update
                    UpdateLayout();

                    // 7. Change visibility
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Visible;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    // 8. Final layout update
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();
                    break;

                case GroupViewMode.Grid:
                    // 1. Check if coming from fullscreen
                    bool wasFullscreenGrid = (FullscreenView.Visibility == Visibility.Visible);

                    // 2. Restore topmost if coming from fullscreen
                    if (wasFullscreenGrid)
                    {
                        Topmost = false;
                    }

                    // 3. Set sizing mode FIRST
                    SizeToContent = SizeToContent.Manual;
                    // 4. RESET minimum sizes
                    MinWidth = 250;
                    MinHeight = 200;

                    // 5. Restore position/size (AFTER setting sizing mode!)
                    if (wasFullscreenGrid && _preFullscreenLeft > 0)
                    {
                        Left = _preFullscreenLeft;
                        Top = _preFullscreenTop;
                        Width = _preFullscreenWidth;
                        Height = _preFullscreenHeight;
                    }
                    else if (_viewModel != null)
                    {
                        Left = _viewModel.PositionX;
                        Top = _viewModel.PositionY;
                        Width = _viewModel.Width;
                        Height = _viewModel.Height;
                    }

                    // 6. Force layout update
                    UpdateLayout();

                    // 7. Change visibility
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Visible;
                    FullscreenView.Visibility = Visibility.Collapsed;

                    // 8. Final layout update
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();
                    break;

                case GroupViewMode.Fullscreen:
                    // Position is already saved in OnViewModelPropertyChanged before we get here!
                    // 1. Set to fullscreen size
                    SizeToContent = SizeToContent.Manual;
                    MinWidth = 800;
                    MinHeight = 600;
                    // 2. Get screen dimensions (use WPF SystemParameters)
                    Width = SystemParameters.PrimaryScreenWidth;
                    Height = SystemParameters.PrimaryScreenHeight;
                    Left = 0;
                    Top = 0;
                    // 3. Change visibility
                    MinimizedView.Visibility = Visibility.Collapsed;
                    HoverView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Visible;
                    // 4. Set window properties for fullscreen
                    WindowStyle = WindowStyle.None;
                    Topmost = true;
                    // 5. Final layout update
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();
                    break;

                default:
                    // Default to minimized (match the new sizes!)
                    SizeToContent = SizeToContent.Manual;
                    MinWidth = 200;  // Match Border MinWidth
                    MinHeight = 40;
                    UpdateLayout();
                    Width = 220;     // Fits icon + name + count
                    Height = 40;
                    MinimizedView.Visibility = Visibility.Visible;
                    HoverView.Visibility = Visibility.Collapsed;
                    NormalView.Visibility = Visibility.Collapsed;
                    GridView.Visibility = Visibility.Collapsed;
                    FullscreenView.Visibility = Visibility.Collapsed;
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();
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
        _viewModel?.SetMinimizedCommand.Execute(null);
    }

    private void SetNormal_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetNormalCommand.Execute(null);
    }

    private void SetGrid_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetGridCommand.Execute(null);
    }

    private void SetFullscreen_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.SetFullscreenCommand.Execute(null);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // F11 toggles fullscreen
        if (e.Key == Key.F11)
        {
            if (_viewModel.ViewMode == GroupViewMode.Fullscreen)
            {
                _viewModel.SetMinimizedCommand.Execute(null);
            }
            else
            {
                _viewModel.SetFullscreenCommand.Execute(null);
            }
            e.Handled = true;
        }

        // ESC exits fullscreen
        if (e.Key == Key.Escape && _viewModel.ViewMode == GroupViewMode.Fullscreen)
        {
            _viewModel.SetMinimizedCommand.Execute(null);
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
                // If in fullscreen, exit to minimized so launched app is visible
                if (_viewModel?.ViewMode == GroupViewMode.Fullscreen)
                {
                    _viewModel.SetMinimizedCommand.Execute(null);
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
            else
            {
                System.Diagnostics.Debug.WriteLine($"Shortcut_Click: Could not get shortcut from DataContext. Sender type: {sender?.GetType().Name}, DataContext type: {frameworkElement?.DataContext?.GetType().Name}");
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

            // Save current position BEFORE locking size
            var posBeforeLock = new Point(Left, Top);

            // Lock size to prevent jumping (especially for Hover view)
            SizeToContent = SizeToContent.Manual;
            Width = _preDragWidth;
            Height = _preDragHeight;

            // Force layout
            UpdateLayout();

            // Restore position (size lock might have shifted it)
            Left = posBeforeLock.X;
            Top = posBeforeLock.Y;
        }

        // Calculate position from original baseline + current mouse delta
        Left = _dragStartWindowPosition.X + deltaX;
        Top = _dragStartWindowPosition.Y + deltaY;
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragStarted) return;

        var element = sender as UIElement;
        element?.ReleaseMouseCapture();

        if (_isDragging)
        {
            // Actual drag occurred - update ViewModel position
            // Round to nearest integer to avoid fractional pixel issues
            // VALIDATE: Ensure positions are reasonable (not corrupted billion-digit values!)
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
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Drag ended at INVALID position ({newX}, {newY}) - position NOT saved!");
                }
            }
        }
        else
        {
            // No drag occurred (click or tiny movement)
        }

        _isDragging = false;
        _dragStarted = false;
        e.Handled = true;
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
