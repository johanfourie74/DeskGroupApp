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
        System.Diagnostics.Debug.WriteLine($"SetLastTempFloatTime: Set to {_lastTempFloatTime:HH:mm:ss.fff}");
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
        System.Diagnostics.Debug.WriteLine("GroupWidget.Constructor: START");

        InitializeComponent();

        System.Diagnostics.Debug.WriteLine("GroupWidget.Constructor: InitializeComponent done");

        // Reduce flickering during drag/resize
        System.Windows.Interop.HwndSource.DefaultAcquireHwndFocusInMenuMode = false;

        // Additional performance optimization - use layered window composition
        SourceInitialized += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine("GroupWidget.SourceInitialized: START");
            var source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null)
            {
                source.CompositionTarget.RenderMode = RenderMode.Default;
            }
            System.Diagnostics.Debug.WriteLine("GroupWidget.SourceInitialized: COMPLETE");
        };

        Loaded += OnLoaded;
        Closing += OnClosing;
        DataContextChanged += OnDataContextChanged;
        Drop += OnDrop;
        DragEnter += OnDragEnter;
        KeyDown += OnKeyDown; // Keyboard shortcuts
        PreviewMouseLeftButtonDown += OnAnyMouseClick; // Catch ALL clicks to restore sticky mode
        MouseLeftButtonUp += OnMouseUpSyncPosition; // Sync position after ANY mouse release

        System.Diagnostics.Debug.WriteLine("GroupWidget.Constructor: Wiring routed events");

        // Subscribe to routed events from child controls
        AddHandler(IconTile.RemoveShortcutEvent, new RoutedEventHandler(OnRemoveShortcutRequested));
        AddHandler(DragThumb.TogglePinRequestedEvent, new RoutedEventHandler(OnTogglePinRequested));
        AddHandler(DragThumb.ToggleSnapRequestedEvent, new RoutedEventHandler(OnToggleSnapRequested));
        AddHandler(DragThumb.SetStickyRequestedEvent, new RoutedEventHandler(OnSetStickyRequested));
        AddHandler(DragThumb.SetFloatRequestedEvent, new RoutedEventHandler(OnSetFloatRequested));

        System.Diagnostics.Debug.WriteLine("GroupWidget.Constructor: COMPLETE");
    }

    public GroupWidget(GroupViewModel viewModel) : this()
    {
        System.Diagnostics.Debug.WriteLine($"GroupWidget.Constructor(VM): Setting DataContext to '{viewModel?.Name}'");
        DataContext = viewModel;
        System.Diagnostics.Debug.WriteLine("GroupWidget.Constructor(VM): COMPLETE");
    }

    // Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("GroupWidget.OnLoaded: START");

        _windowHandle = new WindowInteropHelper(this).Handle;
        Win32Interop.SetToolWindow(_windowHandle);

        if (_viewModel != null)
        {
            System.Diagnostics.Debug.WriteLine($"GroupWidget.OnLoaded: Position/size already set by App.xaml.cs");

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

                System.Diagnostics.Debug.WriteLine($"GroupWidget.OnLoaded: Initialization complete, fading in");

                // Fade in now that everything is positioned correctly
                Opacity = 1.0;

                System.Diagnostics.Debug.WriteLine($"GroupWidget.OnLoaded: COMPLETE for '{_viewModel.Name}'");
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
            System.Diagnostics.Debug.WriteLine($"OnViewModelPropertyChanged: SKIPPED (initializing) - '{e.PropertyName}'");
            return;
        }

        // Prevent re-entry
        if (_isApplyingChanges)
        {
            System.Diagnostics.Debug.WriteLine($"OnViewModelPropertyChanged: BLOCKED (re-entry) - '{e.PropertyName}'");
            return;
        }

        _isApplyingChanges = true;

        try
        {
            System.Diagnostics.Debug.WriteLine($"OnViewModelPropertyChanged: '{e.PropertyName}' for '{_viewModel?.Name}'");

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
                        System.Diagnostics.Debug.WriteLine($"  ViewMode changing to: {_viewModel.ViewMode}");

                        // CRITICAL: Save position BEFORE switching to fullscreen!
                        if (_viewModel.ViewMode == GroupViewMode.Fullscreen)
                        {
                            // Save current state before fullscreen
                            _preFullscreenLeft = Left;
                            _preFullscreenTop = Top;
                            _preFullscreenWidth = Width;
                            _preFullscreenHeight = Height;
                            System.Diagnostics.Debug.WriteLine($"    -> Saved pre-fullscreen: pos=({_preFullscreenLeft},{_preFullscreenTop}), size=({_preFullscreenWidth}x{_preFullscreenHeight})");
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
                    System.Diagnostics.Debug.WriteLine($"  IsVisible handler: DISABLED to prevent loops");
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

        System.Diagnostics.Debug.WriteLine($"ApplyDockMode: {mode} for '{_viewModel?.Name}', Current window pos: ({Left},{Top}), ViewModel pos: ({_viewModel?.PositionX},{_viewModel?.PositionY})");

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
                    System.Diagnostics.Debug.WriteLine($"  SetWindowPos called with ViewModel pos: ({targetX},{targetY})");
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
                System.Diagnostics.Debug.WriteLine($"  Float mode set, window remains at: ({Left},{Top})");
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
        System.Diagnostics.Debug.WriteLine("StartHoverLeaveTimer: Starting 300ms timer");

        _hoverLeaveTimer?.Stop();
        _hoverLeaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _hoverLeaveTimer.Tick += (s, args) =>
        {
            _hoverLeaveTimer.Stop();

            System.Diagnostics.Debug.WriteLine($"  Timer Tick: IsMouseOver={IsMouseOver}, ViewMode={_viewModel?.ViewMode}");

            // Collapse if mouse is outside AND we're in hover mode
            if (!IsMouseOver && _viewModel != null)
            {
                if (_viewModel.ViewMode == GroupViewMode.Hover)
                {
                    System.Diagnostics.Debug.WriteLine("  -> Timer: Collapsing to Minimized");
                    _viewModel.ViewMode = GroupViewMode.Minimized;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  -> Timer: No collapse needed, mode is {_viewModel.ViewMode}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("  -> Timer: Mouse still over, not collapsing");
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
        System.Diagnostics.Debug.WriteLine($"OnSetStickyRequested: Current DockMode={_viewModel?.DockMode}");

        if (_viewModel != null && _viewModel.DockMode != GroupDockMode.Sticky)
        {
            // CRITICAL: Sync window position to ViewModel BEFORE toggling mode!
            // This ensures the JSON saves with the CURRENT position, not old position
            if (Left > -5000 && Top > -5000)
            {
                var currentX = Math.Round(Left);
                var currentY = Math.Round(Top);
                System.Diagnostics.Debug.WriteLine($"  → Syncing position BEFORE toggle: Window ({Left},{Top}) → ViewModel ({currentX},{currentY})");
                _viewModel.PositionX = currentX;
                _viewModel.PositionY = currentY;
            }

            System.Diagnostics.Debug.WriteLine($"  → Toggling from {_viewModel.DockMode} to Sticky");
            _viewModel.ToggleDockModeCommand.Execute(null);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"  → Already Sticky, no action needed");
        }
        e.Handled = true;
    }

    private void OnSetFloatRequested(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"OnSetFloatRequested: Current DockMode={_viewModel?.DockMode}");

        if (_viewModel != null && _viewModel.DockMode != GroupDockMode.Float)
        {
            // CRITICAL: Sync window position to ViewModel BEFORE toggling mode!
            // This ensures the JSON saves with the CURRENT position, not old position
            if (Left > -5000 && Top > -5000)
            {
                var currentX = Math.Round(Left);
                var currentY = Math.Round(Top);
                System.Diagnostics.Debug.WriteLine($"  → Syncing position BEFORE toggle: Window ({Left},{Top}) → ViewModel ({currentX},{currentY})");
                _viewModel.PositionX = currentX;
                _viewModel.PositionY = currentY;
            }

            System.Diagnostics.Debug.WriteLine($"  → Toggling from {_viewModel.DockMode} to Float");
            _viewModel.ToggleDockModeCommand.Execute(null);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"  → Already Float, no action needed");
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
                System.Diagnostics.Debug.WriteLine($"Color changed to: {newColor}");
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
    /// Footer click handler - logs which group was clicked for debugging Show All → Sticky restore
    /// </summary>
    private void Footer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"🖱️ FOOTER CLICKED in '{_viewModel?.Name}' (DockMode: {_viewModel?.DockMode}, ViewMode: {_viewModel?.ViewMode})");

        // DIRECTLY trigger restore logic instead of relying on event bubbling
        if (_viewModel != null)
        {
            // Get the MainViewModel to check ALL groups
            var app = Application.Current as App;
            System.Diagnostics.Debug.WriteLine($"  → Footer: app = {(app == null ? "NULL" : "OK")}");

            if (app != null)
            {
                object? proxyObj = null;
                try
                {
                    proxyObj = app.Resources["AppDataProxy"];
                    System.Diagnostics.Debug.WriteLine($"  → Footer: AppDataProxy = {(proxyObj == null ? "NULL" : proxyObj.GetType().Name)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"  → Footer: Error getting AppDataProxy: {ex.Message}");
                }

                var proxy = proxyObj as DeskGroup.Helpers.BindingProxy;
                System.Diagnostics.Debug.WriteLine($"  → Footer: proxy = {(proxy == null ? "NULL" : "OK")}");

                if (proxy != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  → Footer: proxy.Data = {(proxy.Data == null ? "NULL" : proxy.Data.GetType().Name)}");
                }

                var mainVm = proxy?.Data as DeskGroup.ViewModels.MainViewModel;
                System.Diagnostics.Debug.WriteLine($"  → Footer: mainVm = {(mainVm == null ? "NULL" : "OK")}");

                if (mainVm != null)
                {
                    // Check if ANY group (not just this one) has been temporarily floated
                    bool anyGroupTemporarilyFloated = mainVm.Groups.Any(g => g.SavedDockModeBeforeFloat.HasValue);

                    System.Diagnostics.Debug.WriteLine($"  → Footer: anyGroupTemporarilyFloated = {anyGroupTemporarilyFloated}");

                    if (anyGroupTemporarilyFloated)
                    {
                        System.Diagnostics.Debug.WriteLine($"  → Footer: RESTORING ALL from temp-float!");
                        mainVm.RestoreAllTemporaryFloats();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  → Footer: No temp-floated groups found");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  → Footer: Could not access MainViewModel!");
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
            System.Diagnostics.Debug.WriteLine($"UpdateViewMode: BLOCKED re-entry for mode {mode}");
            return;
        }

        _isUpdatingViewMode = true;

        try
        {
            System.Diagnostics.Debug.WriteLine($"UpdateViewMode: Switching to {mode} (current visible: Min={MinimizedView.Visibility}, Hover={HoverView.Visibility}, Normal={NormalView.Visibility})");

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
                        System.Diagnostics.Debug.WriteLine($"  Restoring position from fullscreen: ({Left}, {Top})");
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

                    System.Diagnostics.Debug.WriteLine($"  -> Minimized view active at ({Left}, {Top}), size (220x40) - layout forced");
                    break;

                case GroupViewMode.Hover:
                    // 1. Check if coming from fullscreen
                    bool wasFullscreenHover = (FullscreenView.Visibility == Visibility.Visible);

                    // 2. Restore position if coming from fullscreen
                    if (wasFullscreenHover && _preFullscreenLeft > 0)
                    {
                        Left = _preFullscreenLeft;
                        Top = _preFullscreenTop;
                        System.Diagnostics.Debug.WriteLine($"  Restoring position from fullscreen to hover: ({Left}, {Top})");
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

                    System.Diagnostics.Debug.WriteLine($"  HoverView measured: {desiredWidth}x{desiredHeight}");

                    // 7. Set window to exact size immediately
                    Width = desiredWidth;
                    Height = desiredHeight;
                    MinWidth = 200;
                    MinHeight = 60;

                    // 8. Force layout
                    InvalidateMeasure();
                    InvalidateArrange();
                    UpdateLayout();

                    System.Diagnostics.Debug.WriteLine($"  -> Hover view active (fixed size: {Width}x{Height}) at ({Left},{Top}) - layout forced");
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
                        System.Diagnostics.Debug.WriteLine($"  Restoring position/size from fullscreen: ({Left}, {Top}, {Width}x{Height})");
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

                    System.Diagnostics.Debug.WriteLine($"  -> Normal view active at ({Left}, {Top}), size ({Width}x{Height}) - layout forced");
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
                        System.Diagnostics.Debug.WriteLine($"  Restoring position/size from fullscreen: ({Left}, {Top}, {Width}x{Height})");
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

                    System.Diagnostics.Debug.WriteLine($"  -> Grid view active at ({Left}, {Top}), size ({Width}x{Height}) - layout forced");
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
                    System.Diagnostics.Debug.WriteLine($"  -> Fullscreen view active ({Width}x{Height}) at (0,0) - layout forced");
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
                    System.Diagnostics.Debug.WriteLine($"  -> Default to minimized (size: 220x40, MinSize: {MinWidth}x{MinHeight}) - layout forced");
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
        System.Diagnostics.Debug.WriteLine($"MinimizedView_MouseEnter - Current mode: {_viewModel?.ViewMode}");

        // Don't expand if dragging
        if (_isDragging || _dragStarted)
        {
            System.Diagnostics.Debug.WriteLine("  -> Ignoring hover during drag");
            return;
        }

        // Cancel any pending collapse timer
        _hoverLeaveTimer?.Stop();

        // Switch to hover view to show preview
        if (_viewModel != null && _viewModel.ViewMode == GroupViewMode.Minimized)
        {
            System.Diagnostics.Debug.WriteLine("  -> Switching to Hover");
            _viewModel.ViewMode = GroupViewMode.Hover;
        }
    }

    private void MinimizedView_MouseLeave(object sender, MouseEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MinimizedView_MouseLeave");
        // Don't collapse here - let hover view handle it
    }

    private void HoverView_MouseLeave(object sender, MouseEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"HoverView_MouseLeave - Current mode: {_viewModel?.ViewMode}");

        // Don't collapse if dragging
        if (_isDragging || _dragStarted)
        {
            System.Diagnostics.Debug.WriteLine("  -> Ignoring leave during drag");
            return;
        }

        // Start timer to collapse back to minimized after short delay
        StartHoverLeaveTimer();
    }

    private void SetMinimized_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SetMinimized_Click");
        _viewModel?.SetMinimizedCommand.Execute(null);
    }

    private void SetNormal_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SetNormal_Click");
        _viewModel?.SetNormalCommand.Execute(null);
    }

    private void SetGrid_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SetGrid_Click");
        _viewModel?.SetGridCommand.Execute(null);
    }

    private void SetFullscreen_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SetFullscreen_Click");
        _viewModel?.SetFullscreenCommand.Execute(null);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        // F11 toggles fullscreen
        if (e.Key == Key.F11)
        {
            System.Diagnostics.Debug.WriteLine("F11 pressed - toggling fullscreen");
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
            System.Diagnostics.Debug.WriteLine("ESC pressed - exiting fullscreen");
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
                System.Diagnostics.Debug.WriteLine($"Shortcut_Click: Launching '{shortcut.DisplayName}'");

                // If in fullscreen, exit to minimized so launched app is visible
                if (_viewModel?.ViewMode == GroupViewMode.Fullscreen)
                {
                    System.Diagnostics.Debug.WriteLine("  Exiting fullscreen before launch");
                    _viewModel.SetMinimizedCommand.Execute(null);
                }

                // RESTORE STICKY MODE: If user temporarily floated this window via Show/Hide All,
                // restore it back to Sticky when they click an icon
                if (_viewModel != null && _viewModel.SavedDockModeBeforeFloat.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"  Restoring dock mode from {_viewModel.DockMode} to {_viewModel.SavedDockModeBeforeFloat}");
                    _viewModel.DockMode = _viewModel.SavedDockModeBeforeFloat.Value;
                    _viewModel.SavedDockModeBeforeFloat = null; // Clear the saved state
                }

                shortcut.LaunchCommand.Execute(null);
                e.Handled = true; // Mark as handled to prevent bubbling
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
                System.Diagnostics.Debug.WriteLine($"Shortcut_RightClick: Showing menu for '{shortcut.DisplayName}'");

                var menu = new ContextMenu();

                // Remove shortcut
                var removeItem = new MenuItem { Header = "Remove from group" };
                removeItem.Click += (s, args) =>
                {
                    System.Diagnostics.Debug.WriteLine($"  Removing '{shortcut.DisplayName}' from group");
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
            System.Diagnostics.Debug.WriteLine($"OnAnyMouseClick: FIRED! (clicked in '{_viewModel?.Name}')");

            // RESTORE ALL GROUPS: Check if ANY group has SavedDockModeBeforeFloat set (not just this one!)
            // This happens when user clicks Show All (all sticky groups become Float temporarily)
            // Then when they click ANY shortcut in ANY group, ALL groups restore to their saved mode
            if (_viewModel != null)
            {
                // CRITICAL: Prevent immediate restoration on the SAME click that triggered Show All!
                // Give at least 500ms grace period for user to see the floated windows
                var timeSinceLastTempFloat = DateTime.Now - _lastTempFloatTime;
                if (timeSinceLastTempFloat.TotalMilliseconds < 500)
                {
                    System.Diagnostics.Debug.WriteLine($"  → Ignoring click (too soon after Show All: {timeSinceLastTempFloat.TotalMilliseconds}ms)");
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
                        // Check if ANY group (not just this one) has been temporarily floated
                        bool anyGroupTemporarilyFloated = mainVm.Groups.Any(g => g.SavedDockModeBeforeFloat.HasValue);

                        if (anyGroupTemporarilyFloated)
                        {
                            System.Diagnostics.Debug.WriteLine($"  → Found temporarily floated group(s), restoring ALL to saved modes");
                            mainVm.RestoreAllTemporaryFloats();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"  → No temporarily floated groups found");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  → WARNING: Could not access MainViewModel!");
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
                System.Diagnostics.Debug.WriteLine($"OnMouseUpSyncPosition: Syncing position Window ({Left},{Top}) → ViewModel ({currentX},{currentY})");
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
            System.Diagnostics.Debug.WriteLine($"Drag started (threshold crossed) at screen pos {currentMouseScreen}");

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

            System.Diagnostics.Debug.WriteLine($"Size locked to {Width}x{Height}, position: ({Left}, {Top})");
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
                // Allow negative (multi-monitor) but reject extreme values
                if (newX > -5000 && newX < 10000 && newY > -5000 && newY < 10000)
                {
                    _viewModel.PositionX = newX;
                    _viewModel.PositionY = newY;
                    System.Diagnostics.Debug.WriteLine($"Drag ended at ({newX}, {newY}) - position saved ✓");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Drag ended at INVALID position ({newX}, {newY}) - position NOT saved! Using ViewModel values: ({_viewModel.PositionX}, {_viewModel.PositionY})");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Drag ended at ({Left}, {Top})");
            }
        }
        else
        {
            // No drag occurred (click or tiny movement) - allow hover-to-expand to trigger
            System.Diagnostics.Debug.WriteLine("Click detected (no drag)");
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
