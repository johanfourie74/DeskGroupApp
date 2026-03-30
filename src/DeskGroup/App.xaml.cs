using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.ViewModels;
using DeskGroup.Views;
using Hardcodet.Wpf.TaskbarNotification;

namespace DeskGroup;

public partial class App : Application
{
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    private const int NIM_DELETE = 0x00000002;

    private SingleInstanceGuard? _singleInstanceGuard;
    private MainViewModel? _mainViewModel;
    private TaskbarIcon? _trayIcon;
    private readonly Dictionary<Guid, GroupWidget> _groupWindows = new();
    private System.Windows.Threading.DispatcherTimer? _trayClickTimer;
    private System.Windows.Threading.DispatcherTimer? _updateCheckTimer;
    private NamedPipeServer? _pipeServer;

    // Hidden helper window used for hotkey registration
    // (needs an HWND but doesn't need to be visible)
    private Window? _helperWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle special launch modes before any other initialization
        string? addToGroupPath = null;
        bool showAllGroups = e.Args.Contains("--show-all");
        for (int i = 0; i < e.Args.Length - 1; i++)
            if (e.Args[i] == "--add-to-group") { addToGroupPath = e.Args[i + 1]; break; }

        if (e.Args.Contains("--uninstall-cleanup"))
        {
            await UninstallHelper.RunAsync();
            Shutdown(0);
            return;
        }

        // Single Instance Check
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.TryAcquire())
        {
            if (addToGroupPath != null)
                NamedPipeServer.TrySend(addToGroupPath);
            else if (showAllGroups)
                NamedPipeServer.TrySend("--show-all");
            else
                MessageBox.Show(
                    "DeskGroup is already running.\n\nCheck your system tray.",
                    "DeskGroup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Global Exception Handling
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // Handle first-chance exceptions (for debugging)
        // These are exceptions that are caught internally by WPF but break the debugger
        AppDomain.CurrentDomain.FirstChanceException += (s, args) =>
        {
            // Log WPF internal exceptions that are normally handled
            if (args.Exception is InvalidOperationException invOp)
            {
                System.Diagnostics.Debug.WriteLine($"[FirstChance] InvalidOperationException (handled by WPF): {invOp.Message}");
            }
        };

        // Initialize MainViewModel
        _mainViewModel = new MainViewModel();

        // Set application-level binding proxy to main view model (BEFORE InitializeAsync!)
        // This must happen before resources are frozen by first window creation
        try
        {
            if (Resources["AppDataProxy"] is DeskGroup.Helpers.BindingProxy proxy)
            {
                proxy.Data = _mainViewModel;
                System.Diagnostics.Debug.WriteLine("STARTUP: BindingProxy.Data set successfully");
            }
        }
        catch (Exception proxyEx)
        {
            System.Diagnostics.Debug.WriteLine($"STARTUP: BindingProxy set FAILED: {proxyEx.Message}");
        }

        try
        {
            await _mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize DeskGroup:\n\n{ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Apply Theme
        ApplyTheme(_mainViewModel.Settings.Theme);

        // Sync shell context menu registration with current setting
        if (_mainViewModel.Settings.EnableShellContextMenu)
            ShellContextMenuHelper.RegisterContextMenu();
        else
            ShellContextMenuHelper.UnregisterContextMenu();

        // Start IPC pipe server for --add-to-group and --show-all messages
        _pipeServer = new NamedPipeServer();
        _pipeServer.MessageReceived += (s, msg) => Dispatcher.Invoke(() =>
        {
            if (msg == "--show-all")
                _mainViewModel?.ShowAllGroupsCommand.Execute(null);
            else
                ShowUngroupedIconsDialog(new List<string> { msg });
        });
        _pipeServer.Start();

        // Create Helper Window for Hotkeys
        _helperWindow = new Window
        {
            Width = 1,
            Height = 1,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Opacity = 0,
            Left = -10000,
            Top = -10000,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };
        _helperWindow.Show();

        // Register Hotkeys
        _mainViewModel.RegisterHotkeys(_helperWindow);

        // Subscribe to MainViewModel Events
        _mainViewModel.ThemeChanged += OnThemeChanged;
        _mainViewModel.GroupAdded += OnGroupAdded;
        _mainViewModel.GroupRemoved += OnGroupRemoved;
        _mainViewModel.UngroupedIconsDetected += OnUngroupedIconsDetected;
        _mainViewModel.OpenSettingsRequested += (s, e) => ShowSettingsWindow();

        System.Diagnostics.Debug.WriteLine("STARTUP: Creating tray icon");

        try
        {
            // Create System Tray Icon
            CreateTrayIcon();
            System.Diagnostics.Debug.WriteLine("STARTUP: Tray icon created successfully");
        }
        catch (Exception trayEx)
        {
            System.Diagnostics.Debug.WriteLine($"STARTUP: Tray icon creation FAILED: {trayEx.Message}");
            System.Diagnostics.Debug.WriteLine($"STARTUP: Stack: {trayEx.StackTrace}");

            // Show error but continue without tray icon
            MessageBox.Show(
                $"Failed to create system tray icon:\n\n{trayEx.Message}\n\n" +
                $"The application will continue but you'll need to create groups manually.\n\n" +
                $"Use Alt+F4 to close the app.",
                "Tray Icon Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        System.Diagnostics.Debug.WriteLine($"STARTUP: Creating windows for {_mainViewModel.Groups.Count} groups");

        // Create Widget Windows for Existing Groups
        int windowNum = 0;
        foreach (var groupVm in _mainViewModel.Groups)
        {
            windowNum++;
            System.Diagnostics.Debug.WriteLine($"STARTUP: Creating window {windowNum} for group '{groupVm.Name}'");

            try
            {
                CreateGroupWindow(groupVm);
                System.Diagnostics.Debug.WriteLine($"STARTUP: Window {windowNum} created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"STARTUP ERROR: Window {windowNum} failed: {ex.Message}");
                MessageBox.Show(
                    $"Failed to create window for group '{groupVm.Name}':\n\n{ex.Message}\n\nStack:\n{ex.StackTrace}",
                    "Window Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        System.Diagnostics.Debug.WriteLine("STARTUP: All windows created, showing balloon tip");

        // Show first-run setup dialog
        if (_mainViewModel.WasFirstRun)
        {
            var setupDialog = new FirstRunSetupDialog(
                _mainViewModel.Settings,
                new Services.PersistenceService());
            setupDialog.Owner = _helperWindow;
            setupDialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            setupDialog.ShowDialog();
        }

        // Show first-run hint if applicable
        if (_mainViewModel.WasFirstRun)
        {
            _trayIcon?.ShowBalloonTip(
                "Welcome to DeskGroup! 🎉",
                "Right-click the tray icon to create your first group and organize your desktop icons!",
                BalloonIcon.Info);
        }
        else if (_mainViewModel.Groups.Count > 0)
        {
            _trayIcon?.ShowBalloonTip(
                $"DeskGroup loaded {_mainViewModel.Groups.Count} group(s) ✓",
                "Tip: Double-click the tray icon to toggle all groups on/off!",
                BalloonIcon.Info);
        }
        else
        {
            _trayIcon?.ShowBalloonTip(
                "DeskGroup is running",
                "Right-click the tray icon to create groups and organize your desktop!",
                BalloonIcon.Info);
        }

        // Note: Removed diagnostic message box to avoid blocking startup

        // ── Auto-update background check ──────────────────────────────────────
        // Startup check: run 5 seconds after launch so it doesn't compete with init
        if (_mainViewModel.Settings.AutoUpdate)
        {
            _ = Task.Delay(5000).ContinueWith(
                _ => Dispatcher.InvokeAsync(() => RunBackgroundUpdateCheckAsync()),
                System.Threading.CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        // Weekly background check timer
        _updateCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromDays(7)
        };
        _updateCheckTimer.Tick += async (s, e) => await RunBackgroundUpdateCheckAsync();
        if (_mainViewModel.Settings.AutoUpdate)
            _updateCheckTimer.Start();

        // If launched via shell context menu --add-to-group, open the dialog after startup
        if (addToGroupPath != null)
        {
            _ = Task.Delay(800).ContinueWith(
                _ => Dispatcher.Invoke(() => ShowUngroupedIconsDialog(new List<string> { addToGroupPath })),
                System.Threading.CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Save all data
        if (_mainViewModel != null)
        {
            try
            {
                await _mainViewModel.SaveAllAsync();
            }
            catch { }

            _mainViewModel.Shutdown();
            _mainViewModel.ThemeChanged -= OnThemeChanged;
            _mainViewModel.GroupAdded -= OnGroupAdded;
            _mainViewModel.GroupRemoved -= OnGroupRemoved;
            _mainViewModel.UngroupedIconsDetected -= OnUngroupedIconsDetected;
        }

        // Close all group windows
        foreach (var window in _groupWindows.Values.ToList())
        {
            try { window.Close(); } catch { }
        }
        _groupWindows.Clear();

        // Dispose tray icon - FORCE Windows to remove it immediately with Shell_NotifyIcon API
        if (_trayIcon != null)
        {
            try
            {
                // CRITICAL: Use Shell_NotifyIcon API to FORCE immediate removal
                // This bypasses WPF's delayed cleanup that causes the icon to hang
                var iconHandle = _trayIcon.Icon?.Handle ?? IntPtr.Zero;

                if (iconHandle != IntPtr.Zero)
                {
                    var nid = new NOTIFYICONDATA
                    {
                        cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                        hWnd = new System.Windows.Interop.WindowInteropHelper(this.MainWindow ?? _helperWindow!).Handle,
                        uID = 1,
                        hIcon = iconHandle
                    };

                    // Force Windows to DELETE the icon immediately
                    Shell_NotifyIcon(NIM_DELETE, ref nid);
                    System.Diagnostics.Debug.WriteLine("OnExit: Shell_NotifyIcon(NIM_DELETE) called - icon forcibly removed!");
                }

                // Now cleanup the WPF wrapper
                _trayIcon.Icon = null;
                _trayIcon.Visibility = Visibility.Hidden;
                _trayIcon.ToolTipText = string.Empty;
                _trayIcon.Dispose();

                System.Diagnostics.Debug.WriteLine("OnExit: Tray icon disposed successfully!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing tray icon: {ex.Message}");
            }
            finally
            {
                _trayIcon = null;
            }
        }

        // Close helper window
        try { _helperWindow?.Close(); } catch { }

        // Dispose IPC pipe server
        _pipeServer?.Dispose();

        // Release single instance
        _singleInstanceGuard?.Dispose();

        base.OnExit(e);
    }

    // ============================================================================================
    // SYSTEM TRAY
    // ============================================================================================

    private void CreateTrayIcon()
    {
        System.Diagnostics.Debug.WriteLine("CreateTrayIcon: START");

        try
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "DeskGroup - Desktop Icon Organizer",
                MenuActivation = PopupActivationMode.RightClick
            };

            System.Diagnostics.Debug.WriteLine("CreateTrayIcon: TaskbarIcon created");

            // Load PNG and convert to Icon (avoids ICO file issues)
            Icon? loadedIcon = null;

            try
            {
                System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Loading dg_icon.png and converting to Icon");

                var pngUri = new Uri("pack://application:,,,/Assets/dg_icon.png", UriKind.Absolute);
                var streamInfo = GetResourceStream(pngUri);

                if (streamInfo != null && streamInfo.Stream != null)
                {
                    try
                    {
                        // Load PNG as WPF BitmapImage
                        var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = streamInfo.Stream;
                        bitmapImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        // Convert to System.Drawing.Bitmap
                        using (var memoryStream = new MemoryStream())
                        {
                            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapImage));
                            encoder.Save(memoryStream);
                            memoryStream.Seek(0, SeekOrigin.Begin);

                            using (var bitmap = new System.Drawing.Bitmap(memoryStream))
                            {
                                System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Original bitmap: {bitmap.Width}x{bitmap.Height}");

                                // STEP 1: Find the bounds of non-transparent pixels (crop the padding!)
                                int minX = bitmap.Width, minY = bitmap.Height, maxX = 0, maxY = 0;
                                bool foundPixel = false;

                                for (int y = 0; y < bitmap.Height; y++)
                                {
                                    for (int x = 0; x < bitmap.Width; x++)
                                    {
                                        var pixel = bitmap.GetPixel(x, y);
                                        // Check if pixel is not fully transparent
                                        if (pixel.A > 10) // More than 10 alpha = visible
                                        {
                                            foundPixel = true;
                                            if (x < minX) minX = x;
                                            if (y < minY) minY = y;
                                            if (x > maxX) maxX = x;
                                            if (y > maxY) maxY = y;
                                        }
                                    }
                                }

                                System.Drawing.Bitmap croppedBitmap;

                                if (foundPixel && (maxX > minX) && (maxY > minY))
                                {
                                    // Calculate crop rectangle
                                    int cropWidth = maxX - minX + 1;
                                    int cropHeight = maxY - minY + 1;

                                    System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Found content bounds: {minX},{minY} to {maxX},{maxY} (size: {cropWidth}x{cropHeight})");

                                    // Crop to content
                                    var cropRect = new System.Drawing.Rectangle(minX, minY, cropWidth, cropHeight);
                                    croppedBitmap = bitmap.Clone(cropRect, bitmap.PixelFormat);

                                    System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Cropped to: {croppedBitmap.Width}x{croppedBitmap.Height}");
                                }
                                else
                                {
                                    // No cropping needed or failed to find bounds
                                    croppedBitmap = (System.Drawing.Bitmap)bitmap.Clone();
                                    System.Diagnostics.Debug.WriteLine("CreateTrayIcon: No cropping needed");
                                }

                                // STEP 2: Resize to 64x64 with high quality (now without the padding!)
                                var targetSize = 64;
                                var resized = new System.Drawing.Bitmap(targetSize, targetSize);

                                using (var graphics = System.Drawing.Graphics.FromImage(resized))
                                {
                                    // Use highest quality settings
                                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                                    // Draw the CROPPED image at target size
                                    graphics.DrawImage(croppedBitmap, 0, 0, targetSize, targetSize);
                                }

                                croppedBitmap.Dispose();

                                // Convert to Icon - Windows will scale for DPI
                                IntPtr hIcon = resized.GetHicon();
                                loadedIcon = Icon.FromHandle(hIcon);
                                resized.Dispose();

                                System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Final icon created at {targetSize}x{targetSize} (cropped & resized)");
                            }
                        }
                    }
                    catch (Exception convEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: PNG conversion failed: {convEx.Message}");
                    }
                    finally
                    {
                        streamInfo.Stream?.Dispose();
                    }
                }
            }
            catch (Exception iconEx)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Icon loading failed: {iconEx.Message}");
            }

            // Use loaded icon or fallback to system default
            if (loadedIcon != null)
            {
                _trayIcon.Icon = loadedIcon;
                System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Using PNG-converted icon ✓");
            }
            else
            {
                _trayIcon.Icon = SystemIcons.Application;
                System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Using fallback system icon");
            }

            System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Building context menu");

            // Build context menu
            _trayIcon.ContextMenu = BuildTrayContextMenu();

            System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Wiring click events");

            // Left single-click -> toggle visibility (debounced to prevent double-toggle on double-click)
            _trayIcon.TrayLeftMouseUp += (s, e) =>
            {
                if (_trayClickTimer == null)
                {
                    _trayClickTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(300)
                    };
                    _trayClickTimer.Tick += (ts, te) =>
                    {
                        _trayClickTimer.Stop();
                        _mainViewModel?.ToggleAllGroupsVisibility();
                    };
                }
                _trayClickTimer.Stop();
                _trayClickTimer.Start();
            };

            // Double-click also toggles (cancel single-click timer to avoid double-toggle)
            _trayIcon.TrayMouseDoubleClick += (s, e) =>
            {
                _trayClickTimer?.Stop();
                _mainViewModel?.ToggleAllGroupsVisibility();
            };

            System.Diagnostics.Debug.WriteLine("CreateTrayIcon: COMPLETE");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: FAILED with exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Stack trace: {ex.StackTrace}");

            MessageBox.Show(
                $"Failed to create system tray icon:\n\n{ex.Message}\n\nThe application will continue but won't show in the system tray.",
                "Tray Icon Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private System.Windows.Controls.ContextMenu BuildTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // Show / Hide All
        var showAll = new System.Windows.Controls.MenuItem { Header = "Show All Groups" };
        showAll.Click += (s, e) => _mainViewModel?.ShowAllGroupsCommand.Execute(null);
        menu.Items.Add(showAll);

        var hideAll = new System.Windows.Controls.MenuItem { Header = "Hide All Groups" };
        hideAll.Click += (s, e) => _mainViewModel?.HideAllGroupsCommand.Execute(null);
        menu.Items.Add(hideAll);

        menu.Items.Add(new System.Windows.Controls.Separator());

        if (_mainViewModel?.Settings.ShowDebugMenu == true)
        {
            // DEBUG: Force all groups to visible float mode
            var forceVisible = new System.Windows.Controls.MenuItem { Header = "🔧 DEBUG: Force All Visible (Float+Normal)" };
            forceVisible.Click += (s, e) => ForceAllGroupsVisible();
            menu.Items.Add(forceVisible);

            // DEBUG: Show test window
            var testWindow = new System.Windows.Controls.MenuItem { Header = "🧪 DEBUG: Show Test Window" };
            testWindow.Click += (s, e) => ShowTestWindow();
            menu.Items.Add(testWindow);

            menu.Items.Add(new System.Windows.Controls.Separator());
        }

        // Create New Group
        var newGroup = new System.Windows.Controls.MenuItem { Header = "New Group" };
        newGroup.Click += (s, e) => _mainViewModel?.CreateGroupCommand.Execute(null);
        menu.Items.Add(newGroup);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // Sort Ungrouped Icons
        var sortIcons = new System.Windows.Controls.MenuItem { Header = "Sort Ungrouped Icons..." };
        sortIcons.Click += (s, e) => ShowUngroupedIconsDialog();
        menu.Items.Add(sortIcons);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // Settings
        var settings = new System.Windows.Controls.MenuItem { Header = "Settings..." };
        settings.Click += (s, e) => ShowSettingsWindow();
        menu.Items.Add(settings);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // Restart
        var restart = new System.Windows.Controls.MenuItem { Header = "Restart DeskGroup" };
        restart.Click += async (s, e) =>
        {
            var result = MessageBox.Show(
                "Restart DeskGroup?\n\nAll groups will be saved.",
                "Restart",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                if (_mainViewModel != null)
                    await _mainViewModel.SaveAllAsync();
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? System.AppContext.BaseDirectory + "DeskGroup.exe",
                    UseShellExecute = true
                });
                Shutdown();
            }
        };
        menu.Items.Add(restart);

        // Exit
        var exit = new System.Windows.Controls.MenuItem { Header = "Exit DeskGroup" };
        exit.Click += (s, e) => Shutdown();
        menu.Items.Add(exit);

        return menu;
    }

    // ============================================================================================
    // GROUP WINDOW MANAGEMENT
    // ============================================================================================

    private void ShowTestWindow()
    {
        var testWin = new Window
        {
            Title = "DeskGroup Test Window",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true,
            Background = System.Windows.Media.Brushes.White
        };

        var stack = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(20)
        };

        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "✓ DeskGroup Window Rendering Works!",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 20)
        });

        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"Groups loaded: {_mainViewModel?.Groups.Count ?? 0}",
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        });

        if (_mainViewModel != null)
        {
            foreach (var g in _mainViewModel.Groups)
            {
                stack.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = $"• {g.Name}: {g.ViewMode}, {g.DockMode}, Visible={g.IsVisible}",
                    FontSize = 12,
                    Margin = new Thickness(10, 5, 0, 0)
                });
            }
        }

        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "\nIf you see THIS window, rendering works.\n" +
                   "The issue is with GroupWidget windows specifically.",
            FontSize = 12,
            Margin = new Thickness(0, 20, 0, 0),
            Foreground = System.Windows.Media.Brushes.Red
        });

        var btn = new System.Windows.Controls.Button
        {
            Content = "Close",
            Width = 100,
            Height = 30,
            Margin = new Thickness(0, 20, 0, 0)
        };
        btn.Click += (s, e) => testWin.Close();
        stack.Children.Add(btn);

        testWin.Content = stack;
        testWin.Show();
    }

    private void ForceAllGroupsVisible()
    {
        if (_mainViewModel == null) return;

        int offsetY = 100; // Start at 100px from top
        const int spacing = 60; // Space between groups

        foreach (var groupVm in _mainViewModel.Groups)
        {
            groupVm.IsVisible = true;
            groupVm.ViewMode = GroupViewMode.Normal;
            groupVm.DockMode = GroupDockMode.Float;

            // Move to main screen, stacked vertically
            groupVm.PositionX = 100;
            groupVm.PositionY = offsetY;
            offsetY += spacing;

            // Force window update
            if (_groupWindows.TryGetValue(groupVm.Id, out var window))
            {
                window.Left = groupVm.PositionX;
                window.Top = groupVm.PositionY;
                window.Topmost = true;
                window.Activate();
            }
        }

        MessageBox.Show(
            $"Forced {_mainViewModel.Groups.Count} group(s) to:\n" +
            $"• ViewMode: Normal (large window)\n" +
            $"• DockMode: Float (on top of windows)\n" +
            $"• IsVisible: True\n" +
            $"• Position: Top-left of main screen, stacked vertically\n\n" +
            $"All groups should now be visible at x=100!",
            "Debug: Force Visible",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void CreateGroupWindow(GroupViewModel groupVm)
    {
        System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: START for '{groupVm.Name}'");

        if (_groupWindows.ContainsKey(groupVm.Id))
        {
            System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: Window already exists for '{groupVm.Name}', skipping");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: Creating new GroupWidget for '{groupVm.Name}'");

        var window = new GroupWidget(groupVm);

        System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: GroupWidget created, wiring events");

        window.DeleteRequested += (s, e) =>
        {
            _mainViewModel?.DeleteGroupCommand.Execute(groupVm);
        };

        window.FilesDropped += (s, files) =>
        {
            // Already handled inside GroupWidget, but hook available for future use
        };

        window.SettingsRequested += (s, e) => ShowSettingsWindow();

        // Handle visibility changes directly to avoid infinite loops
        groupVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GroupViewModel.IsVisible))
            {
                System.Diagnostics.Debug.WriteLine($"App: GroupVM.IsVisible changed to {groupVm.IsVisible} for '{groupVm.Name}'");

                // Control window visibility using POSITION instead of Visibility property
                // This avoids WPF layout recalculation loops!
                if (_groupWindows.TryGetValue(groupVm.Id, out var win))
                {
                    win.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (groupVm.IsVisible)
                            {
                                System.Diagnostics.Debug.WriteLine($"  App: Moving window on-screen for '{groupVm.Name}'");
                                // Move to saved position
                                win.Left = groupVm.PositionX;
                                win.Top = groupVm.PositionY;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"  App: Moving window off-screen for '{groupVm.Name}'");
                                // Move far off-screen (invisible but still "shown")
                                win.Left = -10000;
                                win.Top = -10000;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"  App: ERROR changing position: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        };

        System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: Calling Show() for '{groupVm.Name}'");

        // CRITICAL: Set all sizing properties BEFORE showing to prevent visible resize
        window.SizeToContent = SizeToContent.Manual;
        window.Opacity = 0; // Hide during initialization

        // Pre-set position and size based on saved values to prevent visible resize
        // IMPORTANT: Force integer conversion to prevent decimal/comma parsing corruption!
        window.Left = Math.Round(groupVm.PositionX);
        window.Top = Math.Round(groupVm.PositionY);

        // Pre-set size and constraints based on view mode to prevent visible resize
        switch (groupVm.ViewMode)
        {
            case GroupViewMode.Minimized:
                window.MinWidth = 200;  // Increased to match Border MinWidth
                window.MinHeight = 40;
                window.Width = 220;     // Slightly wider to fit icon + name + count
                window.Height = 40;
                break;
            case GroupViewMode.Hover:
                window.MinWidth = 200;
                window.MinHeight = 60;
                window.Width = 250;
                window.Height = 150;
                break;
            case GroupViewMode.Normal:
            case GroupViewMode.Grid:
                window.MinWidth = 200;
                window.MinHeight = 150;
                window.Width = groupVm.Width;
                window.Height = groupVm.Height;
                break;
            case GroupViewMode.Fullscreen:
                window.MinWidth = 800;
                window.MinHeight = 600;
                window.Width = SystemParameters.PrimaryScreenWidth;
                window.Height = SystemParameters.PrimaryScreenHeight;
                window.Left = 0;
                window.Top = 0;
                break;
        }

        window.Show();

        System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: Show() returned at ({window.Left},{window.Top}) size ({window.Width}x{window.Height})");

        _groupWindows[groupVm.Id] = window;

        System.Diagnostics.Debug.WriteLine($"CreateGroupWindow: COMPLETE for '{groupVm.Name}'");
    }

    private void DestroyGroupWindow(GroupViewModel groupVm)
    {
        if (_groupWindows.TryGetValue(groupVm.Id, out var window))
        {
            try { window.Close(); } catch { }
            _groupWindows.Remove(groupVm.Id);
        }
    }

    private void OnGroupAdded(object? sender, GroupViewModel groupVm)
    {
        Dispatcher.Invoke(() =>
        {
            CreateGroupWindow(groupVm);

            // Show helpful tip when user creates a new group
            _trayIcon?.ShowBalloonTip(
                $"Group '{groupVm.Name}' created! 🎨",
                "Tip: Right-click the group window for options, or drag desktop shortcuts into it!",
                BalloonIcon.Info);
        });
    }

    private void OnGroupRemoved(object? sender, GroupViewModel groupVm)
    {
        Dispatcher.Invoke(() => DestroyGroupWindow(groupVm));
    }

    // ============================================================================================
    // THEME MANAGEMENT
    // ============================================================================================

    private void ApplyTheme(DeskGroup.Models.ThemeMode mode)
    {
        // Apply theme to UI windows (Settings, Dialogs)
        var themeUri = mode switch
        {
            DeskGroup.Models.ThemeMode.Light => new Uri("Themes/LightTheme.xaml", UriKind.Relative),
            DeskGroup.Models.ThemeMode.Dark => new Uri("Themes/DarkTheme.xaml", UriKind.Relative),
            DeskGroup.Models.ThemeMode.System => IsSystemDarkMode()
                ? new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
                : new Uri("Themes/LightTheme.xaml", UriKind.Relative),
            _ => new Uri("Themes/LightTheme.xaml", UriKind.Relative)
        };

        var newTheme = new ResourceDictionary { Source = themeUri };

        // Find and replace the existing theme dictionary (index 1, after Generic.xaml)
        var mergedDicts = Resources.MergedDictionaries;
        if (mergedDicts.Count >= 2)
        {
            mergedDicts[1] = newTheme;
        }
        else
        {
            mergedDicts.Add(newTheme);
        }
    }

    private void OnThemeChanged(object? sender, DeskGroup.Models.ThemeMode mode)
    {
        Dispatcher.Invoke(() => ApplyTheme(mode));
    }

    /// <summary>
    /// Check if Windows is currently in dark mode via registry.
    /// </summary>
    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intVal && intVal == 0;
        }
        catch
        {
            return false;
        }
    }

    // ============================================================================================
    // SETTINGS WINDOW
    // ============================================================================================

    private SettingsWindow? _settingsWindow;

    private void ShowSettingsWindow()
    {
        if (_mainViewModel == null) return;

        // Reuse existing window if open
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsVm = _mainViewModel.CreateSettingsViewModel();
        _settingsWindow = new SettingsWindow(settingsVm);

        // Subscribe to opacity changes for live updates
        settingsVm.OpacityChanged += (s, e) =>
        {
            foreach (var groupVm in _mainViewModel.Groups)
                groupVm.RefreshOpacity();
        };

        // Subscribe to icon size changes for live updates
        settingsVm.IconSizeChanged += (s, e) =>
        {
            foreach (var groupVm in _mainViewModel.Groups)
                groupVm.RefreshIconSize();
        };

        _settingsWindow.SettingsApplied += (s, e) =>
        {
            if (_helperWindow != null)
            {
                _mainViewModel.ApplySettingsChanges(_helperWindow);
            }

            // Re-apply UI theme
            ApplyTheme(_mainViewModel.Settings.Theme);

            // Rebuild tray menu so debug item visibility reflects new setting
            if (_trayIcon != null)
                _trayIcon.ContextMenu = BuildTrayContextMenu();
        };

        _settingsWindow.Closed += (s, e) => _settingsWindow = null;

        // Restart weekly timer in case AutoUpdate was toggled
        if (_mainViewModel.Settings.AutoUpdate)
        {
            _updateCheckTimer?.Start();
        }
        else
        {
            _updateCheckTimer?.Stop();
        }

        _settingsWindow.Show();
    }

    // ============================================================================================
    // AUTO-UPDATE BACKGROUND CHECK
    // ============================================================================================

    private async Task RunBackgroundUpdateCheckAsync()
    {
        if (_mainViewModel == null || !_mainViewModel.Settings.AutoUpdate) return;
        try
        {
            var info = await Services.UpdateService.CheckAsync(
                _mainViewModel.Settings.SkippedUpdateVersion).ConfigureAwait(true);

            _mainViewModel.Settings.LastUpdateCheck = DateTime.UtcNow;
            await _mainViewModel.SaveSettingsAsync();

            if (info == null) return;

            if (_mainViewModel.Settings.AutoUpdate && _mainViewModel.Settings.AutoInstall)
            {
                if (!string.IsNullOrEmpty(info.DownloadUrl))
                {
                    var path = await Services.UpdateService.DownloadAsync(
                        info.DownloadUrl, null).ConfigureAwait(true);
                    Services.UpdateService.LaunchInstallerAndShutdown(path);
                }
            }
            else
            {
                _trayIcon?.ShowBalloonTip(
                    $"DeskGroup v{info.Version} available 🆕",
                    "Open Settings → Updates to install.",
                    BalloonIcon.Info);
            }
        }
        catch { /* silent — background check */ }
    }

    // ============================================================================================
    // UNGROUPED ICONS DIALOG
    // ============================================================================================

    private UngroupedIconsDialog? _ungroupedDialog;

    private void OnUngroupedIconsDetected(object? sender, List<string> ungroupedPaths)
    {
        Dispatcher.Invoke(() => ShowUngroupedIconsDialog(ungroupedPaths));
    }

    private void ShowUngroupedIconsDialog(List<string>? specificPaths = null)
    {
        if (_mainViewModel == null) return;

        // Reuse existing dialog if open
        if (_ungroupedDialog != null && _ungroupedDialog.IsVisible)
        {
            _ungroupedDialog.Activate();
            return;
        }

        var paths = specificPaths ?? _mainViewModel.GetUngroupedShortcuts();

        if (paths.Count == 0)
        {
            MessageBox.Show(
                "All desktop icons are already grouped!",
                "No Ungrouped Icons",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var vm = new UngroupedIconsViewModel(
            paths,
            _mainViewModel.Groups.Select(g => g.Model).ToList(),
            new Services.IconExtractorService(),
            _mainViewModel.Settings,
            new Services.PersistenceService());

        _ungroupedDialog = new UngroupedIconsDialog(vm);

        _ungroupedDialog.SortingApplied += (s, assignments) =>
        {
            _mainViewModel.ProcessUngroupedAssignments(assignments);
        };

        _ungroupedDialog.CreateNewGroupRequested += async (s, data) =>
        {
            var (groupName, filePaths) = data;
            System.Diagnostics.Debug.WriteLine($"CreateNewGroupRequested: '{groupName}' with {filePaths.Count} file(s)");

            // Create new group
            await _mainViewModel.CreateGroupCommand.ExecuteAsync(null);

            // Get the newly created group (last one)
            var newGroup = _mainViewModel.Groups.LastOrDefault();
            if (newGroup != null)
            {
                // Rename it
                newGroup.Name = groupName;

                // Add shortcuts to it - cast to IconGroup? to match method signature
                var assignments = filePaths.ToDictionary(path => path, path => (IconGroup?)newGroup.Model);
                _mainViewModel.ProcessUngroupedAssignments(assignments);

                System.Diagnostics.Debug.WriteLine($"Created group '{groupName}' with {filePaths.Count} shortcuts");
            }
        };

        _ungroupedDialog.Closed += (s, e) => _ungroupedDialog = null;
        _ungroupedDialog.Show();
    }

    // ============================================================================================
    // GLOBAL EXCEPTION HANDLING
    // ============================================================================================

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled UI exception: {e.Exception}");

        Helpers.CrashReporter.WriteCrashLog(e.Exception, "UI dispatcher");
        if (_mainViewModel?.Settings.OptInBugReporting == true)
            Helpers.CrashReporter.ShowCrashDialog(e.Exception);

        e.Handled = true; // Don't crash the app
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fatal domain exception: {ex}");

            Helpers.CrashReporter.WriteCrashLog(ex, "AppDomain unhandled");

            try
            {
                // Try to save data before crashing
                _mainViewModel?.SaveAllAsync().GetAwaiter().GetResult();
            }
            catch { }
        }
    }
}