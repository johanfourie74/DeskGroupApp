using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
    private readonly List<EdgeDockWindowInstance> _edgeDockInstances = new();
    private DispatcherTimer? _edgeDockTopmostTimer;
    private DispatcherTimer? _trayClickTimer;
    private DispatcherTimer? _updateCheckTimer;
    private NamedPipeServer? _pipeServer;

    // Hidden helper window used for hotkey registration
    // (needs an HWND but doesn't need to be visible)
    private Window? _helperWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle special launch modes before any other initialization
        string? addToGroupPath = null;
        bool showAllGroups  = e.Args.Contains("--show-all");
        bool newGroup        = e.Args.Contains("--new-group");
        bool showSettings    = e.Args.Contains("--show-settings");
        bool exitApp         = e.Args.Contains("--exit");
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
            else if (newGroup)
                NamedPipeServer.TrySend("--new-group");
            else if (showSettings)
                NamedPipeServer.TrySend("--show-settings");
            else if (exitApp)
                NamedPipeServer.TrySend("--exit");
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
        AppDomain.CurrentDomain.FirstChanceException += (s, args) =>
        {
            if (args.Exception is InvalidOperationException invOp)
            {
                Debug.WriteLine($"[FirstChance] InvalidOperationException (handled by WPF): {invOp.Message}");
            }
        };

        // Initialize MainViewModel
        _mainViewModel = new MainViewModel();

        // Set application-level binding proxy to main view model (BEFORE InitializeAsync!)
        try
        {
            if (Resources["AppDataProxy"] is BindingProxy proxy)
            {
                proxy.Data = _mainViewModel;
            }
        }
        catch (Exception proxyEx)
        {
            Debug.WriteLine($"STARTUP: BindingProxy set FAILED: {proxyEx.Message}");
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
            else if (msg == "--new-group")
                _mainViewModel?.CreateGroupCommand.Execute(null);
            else if (msg == "--show-settings")
                ShowSettingsWindow();
            else if (msg == "--exit")
                Shutdown();
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
        _mainViewModel.EdgeDocksRebuilt += (s, e) => Dispatcher.Invoke(() =>
        {
            SyncEdgeDockWindows();
            if (_trayIcon != null)
                _trayIcon.ContextMenu = BuildTrayContextMenu();
        });
        _mainViewModel.UngroupedIconsDetected += OnUngroupedIconsDetected;
        _mainViewModel.OpenSettingsRequested += (s, e) => ShowSettingsWindow();

        try
        {
            // Create System Tray Icon
            CreateTrayIcon();
        }
        catch (Exception trayEx)
        {
            Debug.WriteLine($"STARTUP: Tray icon creation FAILED: {trayEx.Message}");
            MessageBox.Show(
                $"Failed to create system tray icon:\n\n{trayEx.Message}\n\n" +
                $"The application will continue but you'll need to create groups manually.\n\n" +
                $"Use Alt+F4 to close the app.",
                "Tray Icon Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Jump List for taskbar / desktop shortcut right-click (Note A)
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (!string.IsNullOrEmpty(exePath))
            {
                var jumpList = new System.Windows.Shell.JumpList();
                jumpList.JumpItems.Add(new System.Windows.Shell.JumpTask
                {
                    Title = "New Group",
                    ApplicationPath = exePath,
                    Arguments = "--new-group",
                    Description = "Create a new icon group",
                    IconResourcePath = exePath,
                    IconResourceIndex = 0
                });
                jumpList.JumpItems.Add(new System.Windows.Shell.JumpTask
                {
                    Title = "Show / Hide All Groups",
                    ApplicationPath = exePath,
                    Arguments = "--show-all",
                    Description = "Toggle all groups visible or hidden",
                    IconResourcePath = exePath,
                    IconResourceIndex = 0
                });
                jumpList.JumpItems.Add(new System.Windows.Shell.JumpTask
                {
                    Title = "Settings",
                    ApplicationPath = exePath,
                    Arguments = "--show-settings",
                    Description = "Open DeskGroup settings",
                    IconResourcePath = exePath,
                    IconResourceIndex = 0
                });
                jumpList.JumpItems.Add(new System.Windows.Shell.JumpTask
                {
                    Title = "Exit DeskGroup",
                    ApplicationPath = exePath,
                    Arguments = "--exit",
                    Description = "Exit DeskGroup",
                    IconResourcePath = exePath,
                    IconResourceIndex = 0
                });
                jumpList.Apply();
                System.Windows.Shell.JumpList.SetJumpList(this, jumpList);
            }
        }
        catch (Exception jlEx)
        {
            Debug.WriteLine($"STARTUP: Jump List setup failed: {jlEx.Message}");
        }

        // Create Widget Windows for Existing Groups
        foreach (var groupVm in _mainViewModel.Groups)
        {
            try
            {
                CreateGroupWindow(groupVm);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"STARTUP ERROR: Group window failed: {ex.Message}");
                MessageBox.Show(
                    $"Failed to create window for group '{groupVm.Name}':\n\n{ex.Message}",
                    "Window Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Create Edge Dock Windows if enabled
        SyncEdgeDockWindows();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // Show first-run setup dialog
        if (_mainViewModel.WasFirstRun)
        {
            var setupDialog = new FirstRunSetupDialog(
                _mainViewModel.Settings,
                new Services.PersistenceService());
            setupDialog.Owner = _helperWindow;
            setupDialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            setupDialog.ShowDialog();
            ApplyTheme(_mainViewModel.Settings.Theme);
            foreach (var g in _mainViewModel.Groups) g.RefreshOpacity();
            if (_helperWindow != null) _mainViewModel.ApplySettingsChanges(_helperWindow);
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

        // ── Auto-update background check ──────────────────────────────────────
        if (_mainViewModel.Settings.AutoUpdate)
        {
            _ = Task.Delay(5000).ContinueWith(
                _ => Dispatcher.InvokeAsync(() => RunBackgroundUpdateCheckAsync()),
                System.Threading.CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        // Weekly background check timer
        _updateCheckTimer = new DispatcherTimer
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
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

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

        foreach (var window in _groupWindows.Values.ToList())
        {
            try { window.Close(); } catch { }
        }
        _groupWindows.Clear();
        DestroyAllEdgeDockWindows();

        if (_trayIcon != null)
        {
            try
            {
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

                    Shell_NotifyIcon(NIM_DELETE, ref nid);
                }

                _trayIcon.Icon = null;
                _trayIcon.Visibility = Visibility.Hidden;
                _trayIcon.ToolTipText = string.Empty;
                _trayIcon.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing tray icon: {ex.Message}");
            }
            finally
            {
                _trayIcon = null;
            }
        }

        try { _helperWindow?.Close(); } catch { }
        _pipeServer?.Dispose();
        _singleInstanceGuard?.Dispose();

        base.OnExit(e);
    }

    // ============================================================================================
    // SYSTEM TRAY
    // ============================================================================================

    private static Icon? s_cachedTrayIcon;

    private void CreateTrayIcon()
    {
        try
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "DeskGroup - Desktop Icon Organizer",
                MenuActivation = PopupActivationMode.RightClick
            };

            Icon? loadedIcon = s_cachedTrayIcon;

            if (loadedIcon == null)
            {
                try
                {
                    var pngUri = new Uri("pack://application:,,,/Assets/dg_icon.png", UriKind.Absolute);
                    var streamInfo = GetResourceStream(pngUri);

                    if (streamInfo?.Stream != null)
                    {
                        try
                        {
                            using var bitmap = new Bitmap(streamInfo.Stream);
                            int minX = bitmap.Width, minY = bitmap.Height, maxX = 0, maxY = 0;
                            bool foundPixel = false;

                            // Fast LockBits scan instead of 260,000 slow GetPixel P/Invoke calls
                            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                            try
                            {
                                int stride = Math.Abs(bmpData.Stride);
                                byte[] pixels = new byte[stride * bitmap.Height];
                                Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

                                for (int y = 0; y < bitmap.Height; y++)
                                {
                                    int rowOffset = y * stride;
                                    for (int x = 0; x < bitmap.Width; x++)
                                    {
                                        byte alpha = pixels[rowOffset + x * 4 + 3];
                                        if (alpha > 10)
                                        {
                                            foundPixel = true;
                                            if (x < minX) minX = x;
                                            if (y < minY) minY = y;
                                            if (x > maxX) maxX = x;
                                            if (y > maxY) maxY = y;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                bitmap.UnlockBits(bmpData);
                            }

                            var srcRect = foundPixel && maxX > minX && maxY > minY
                                ? new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1)
                                : new Rectangle(0, 0, bitmap.Width, bitmap.Height);

                            const int targetSize = 64;
                            using var resized = new Bitmap(targetSize, targetSize, PixelFormat.Format32bppArgb);
                            using (var graphics = Graphics.FromImage(resized))
                            {
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                graphics.DrawImage(bitmap, new Rectangle(0, 0, targetSize, targetSize), srcRect, GraphicsUnit.Pixel);
                            }

                            IntPtr hIcon = resized.GetHicon();
                            loadedIcon = Icon.FromHandle(hIcon);
                            s_cachedTrayIcon = loadedIcon;
                        }
                        catch (Exception convEx)
                        {
                            Debug.WriteLine($"CreateTrayIcon: PNG conversion failed: {convEx.Message}");
                        }
                        finally
                        {
                            streamInfo.Stream?.Dispose();
                        }
                    }
                }
                catch (Exception iconEx)
                {
                    Debug.WriteLine($"CreateTrayIcon: Icon loading failed: {iconEx.Message}");
                }
            }

            _trayIcon.Icon = loadedIcon ?? SystemIcons.Application;
            _trayIcon.ContextMenu = BuildTrayContextMenu();

            // Left single-click -> toggle visibility (debounced to prevent double-toggle on double-click)
            _trayIcon.TrayLeftMouseUp += (s, e) =>
            {
                if (_trayClickTimer == null)
                {
                    _trayClickTimer = new DispatcherTimer
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateTrayIcon: FAILED with exception: {ex.Message}");
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
            var forceVisible = new System.Windows.Controls.MenuItem { Header = "🔧 DEBUG: Force All Visible (Float+Normal)" };
            forceVisible.Click += (s, e) => ForceAllGroupsVisible();
            menu.Items.Add(forceVisible);

            var testWindow = new System.Windows.Controls.MenuItem { Header = "🧪 DEBUG: Show Test Window" };
            testWindow.Click += (s, e) => ShowTestWindow();
            menu.Items.Add(testWindow);

            menu.Items.Add(new System.Windows.Controls.Separator());
        }

        // Create New Group (fixed duplicate Click handler)
        var newGroup = new System.Windows.Controls.MenuItem { Header = "New Group" };
        newGroup.Click += (s, e) => _mainViewModel?.CreateGroupCommand.Execute(null);
        menu.Items.Add(newGroup);

        var edgeDockMenu = new System.Windows.Controls.MenuItem
        {
            Header = _mainViewModel?.Settings.EnableEdgeDock == true
                ? "✓ Edge Dock (Side Panel)"
                : "Edge Dock (Side Panel)"
        };
        var toggleEdgeDock = new System.Windows.Controls.MenuItem
        {
            Header = _mainViewModel?.Settings.EnableEdgeDock == true
                ? "Disable Side Panel"
                : "Enable Side Panel"
        };
        toggleEdgeDock.Click += async (s, e) =>
        {
            if (_mainViewModel != null)
                await _mainViewModel.ToggleEdgeDockFeatureAsync();
        };
        edgeDockMenu.Items.Add(toggleEdgeDock);

        if (_mainViewModel?.Settings.EnableEdgeDock == true && _mainViewModel.ActiveEdgeDocks.Count > 0)
        {
            edgeDockMenu.Items.Add(new System.Windows.Controls.Separator());
            foreach (var dockVm in _mainViewModel.ActiveEdgeDocks)
            {
                var capturedDock = dockVm;
                var showHideDock = new System.Windows.Controls.MenuItem
                {
                    Header = $"Toggle {capturedDock.Name} ({capturedDock.Edge})"
                };
                showHideDock.Click += (s, e) => capturedDock.TogglePanel();
                edgeDockMenu.Items.Add(showHideDock);
            }
        }
        menu.Items.Add(edgeDockMenu);

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
                    FileName = Environment.ProcessPath ?? AppContext.BaseDirectory + "DeskGroup.exe",
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

        int offsetY = 100;
        const int spacing = 60;

        foreach (var groupVm in _mainViewModel.Groups)
        {
            groupVm.IsVisible = true;
            groupVm.ViewMode = GroupViewMode.Normal;
            groupVm.DockMode = GroupDockMode.Float;

            groupVm.PositionX = 100;
            groupVm.PositionY = offsetY;
            offsetY += spacing;

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
        if (_groupWindows.ContainsKey(groupVm.Id))
            return;

        var window = new GroupWidget(groupVm);

        window.DeleteRequested += (s, e) =>
        {
            _mainViewModel?.DeleteGroupCommand.Execute(groupVm);
        };

        window.FilesDropped += (s, files) => { };
        window.SettingsRequested += (s, e) => ShowSettingsWindow();

        // Handle visibility changes directly to avoid infinite loops
        groupVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(GroupViewModel.IsVisible))
            {
                if (_groupWindows.TryGetValue(groupVm.Id, out var win))
                {
                    win.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (groupVm.IsVisible)
                            {
                                win.Left = groupVm.PositionX;
                                win.Top = groupVm.PositionY;
                            }
                            else
                            {
                                win.Left = -10000;
                                win.Top = -10000;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"  App: ERROR changing position: {ex.Message}");
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
        };

        // Set all sizing properties BEFORE showing to prevent visible resize
        window.SizeToContent = SizeToContent.Manual;
        window.Opacity = 0;

        window.Left = groupVm.IsVisible ? Math.Round(groupVm.PositionX) : -10000;
        window.Top = groupVm.IsVisible ? Math.Round(groupVm.PositionY) : -10000;

        switch (groupVm.ViewMode)
        {
            case GroupViewMode.Minimized:
                window.MinWidth = 200;
                window.MinHeight = 40;
                window.Width = 220;
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
        _groupWindows[groupVm.Id] = window;
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
    // EDGE DOCK (OPTIONAL SIDE PANEL) WINDOW MANAGEMENT
    // ============================================================================================

    private sealed class EdgeDockWindowInstance
    {
        public EdgeDockViewModel ViewModel { get; init; } = null!;
        public EdgeDockBubbleWindow BubbleWindow { get; init; } = null!;
        public EdgeDockPanelWindow PanelWindow { get; init; } = null!;
        public DispatcherTimer AutoHideTimer { get; init; } = null!;
    }

    private void SyncEdgeDockWindows()
    {
        DestroyAllEdgeDockWindows();
        if (_mainViewModel == null || !_mainViewModel.Settings.EnableEdgeDock) return;

        foreach (var dockVm in _mainViewModel.ActiveEdgeDocks)
        {
            try
            {
                var bubbleWin = new EdgeDockBubbleWindow(dockVm);
                var panelWin = new EdgeDockPanelWindow(dockVm);

                var autoHideTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(380)
                };

                autoHideTimer.Tick += (s, e) =>
                {
                    autoHideTimer.Stop();
                    if (bubbleWin.IsMouseOver || panelWin.IsMouseOver || Controls.IconTile.IsDraggingShortcut) return;
                    if (bubbleWin.ContextMenu?.IsOpen == true || panelWin.ContextMenu?.IsOpen == true) return;
                    if (panelWin.OwnedWindows.Count > 0) return;
                    dockVm.RequestCloseFromHoverLeave();
                };

                bubbleWin.HoverEntered += (s, e) =>
                {
                    autoHideTimer.Stop();
                    dockVm.RequestOpenFromHover();
                };

                bubbleWin.HoverLeft += (s, e) =>
                {
                    if (dockVm.LockState == SideDockLockState.AutoHide && dockVm.IsPanelOpen)
                    {
                        autoHideTimer.Stop();
                        autoHideTimer.Start();
                    }
                };

                panelWin.PanelMouseEntered += (s, e) =>
                {
                    autoHideTimer.Stop();
                };

                panelWin.PanelMouseLeft += (s, e) =>
                {
                    if (dockVm.LockState == SideDockLockState.AutoHide && dockVm.IsPanelOpen)
                    {
                        autoHideTimer.Stop();
                        autoHideTimer.Start();
                    }
                };

                bubbleWin.SettingsRequested += (s, e) => ShowSettingsWindow();
                panelWin.SettingsRequested += (s, e) => ShowSettingsWindow();

                bubbleWin.Show();
                panelWin.Show();

                _edgeDockInstances.Add(new EdgeDockWindowInstance
                {
                    ViewModel = dockVm,
                    BubbleWindow = bubbleWin,
                    PanelWindow = panelWin,
                    AutoHideTimer = autoHideTimer
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SyncEdgeDockWindows ERROR: {ex.Message}");
            }
        }

        if (_edgeDockInstances.Count > 0)
        {
            if (_edgeDockTopmostTimer == null)
            {
                _edgeDockTopmostTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _edgeDockTopmostTimer.Tick += (s, e) =>
                {
                    if (_mainViewModel != null &&
                        _mainViewModel.Settings.EnableEdgeDock &&
                        _mainViewModel.Settings.EdgeDockMonitorMode == SideDockMonitorMode.MainMonitorOnly &&
                        _edgeDockInstances.Count > 0)
                    {
                        var currentPrimary = new Services.MonitorService().GetPrimaryMonitor();
                        if (currentPrimary != null &&
                            !string.Equals(_edgeDockInstances[0].ViewModel.TargetMonitor.DeviceName, currentPrimary.DeviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            _mainViewModel.RebuildEdgeDocks();
                            return;
                        }
                    }

                    foreach (var inst in _edgeDockInstances)
                    {
                        inst.BubbleWindow.EnsureTopmost();
                        inst.PanelWindow.EnsureTopmost();
                    }
                };
            }
            _edgeDockTopmostTimer.Start();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _mainViewModel?.RebuildEdgeDocks();
        }), DispatcherPriority.Background);
    }

    private void DestroyAllEdgeDockWindows()
    {
        _edgeDockTopmostTimer?.Stop();
        foreach (var inst in _edgeDockInstances)
        {
            try { inst.AutoHideTimer.Stop(); } catch { }
            try { inst.BubbleWindow.Close(); } catch { }
            try { inst.PanelWindow.Close(); } catch { }
        }
        _edgeDockInstances.Clear();
    }

    // ============================================================================================
    // THEME MANAGEMENT
    // ============================================================================================

    private void ApplyTheme(DeskGroup.Models.ThemeMode mode)
    {
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

    private static void BringWindowToFront(Window win)
    {
        try
        {
            if (win.WindowState == WindowState.Minimized)
                win.WindowState = WindowState.Normal;
            win.Show();
            bool origTopmost = win.Topmost;
            win.Topmost = true;
            win.Activate();
            win.Focus();
            win.Topmost = origTopmost;
        }
        catch { }
    }

    private void ShowSettingsWindow()
    {
        if (_mainViewModel == null) return;

        // Reuse existing window if open
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            BringWindowToFront(_settingsWindow);
            return;
        }

        var settingsVm = _mainViewModel.CreateSettingsViewModel();
        _settingsWindow = new SettingsWindow(settingsVm);

        // Subscribe to opacity changes for live updates
        settingsVm.OpacityChanged += (s, e) =>
        {
            foreach (var groupVm in _mainViewModel.Groups)
                groupVm.RefreshOpacity();
            foreach (var dockVm in _mainViewModel.ActiveEdgeDocks)
                dockVm.RefreshOpacity();
        };

        // Subscribe to icon size changes for live updates
        settingsVm.IconSizeChanged += (s, e) =>
        {
            foreach (var groupVm in _mainViewModel.Groups)
                groupVm.RefreshIconSize();
            foreach (var dockVm in _mainViewModel.ActiveEdgeDocks)
                dockVm.SyncIconSizeFromConfig();
        };

        // Live hotkey re-registration when user changes any keyboard/mouse shortcut in Settings
        settingsVm.HotkeysChanged += (s, e) =>
        {
            if (_helperWindow != null)
            {
                _mainViewModel.RegisterHotkeys(_helperWindow);
            }
        };

        // Live desktop icon hide/restore when user toggles "Hide grouped icons" in Settings
        settingsVm.HideGroupedIconsChanged += (s, e) =>
        {
            _mainViewModel.SyncGroupedDesktopIconsVisibility();
        };

        settingsVm.LiveSettingsChanged += (s, e) =>
        {
            ApplyTheme(_mainViewModel.Settings.Theme);
            _mainViewModel.RebuildEdgeDocks();
            if (_trayIcon != null)
                _trayIcon.ContextMenu = BuildTrayContextMenu();

            if (_mainViewModel.Settings.AutoUpdate)
                _updateCheckTimer?.Start();
            else
                _updateCheckTimer?.Stop();
        };

        _settingsWindow.SettingsApplied += (s, e) =>
        {
            if (_helperWindow != null)
            {
                _mainViewModel.ApplySettingsChanges(_helperWindow);
            }

            ApplyTheme(_mainViewModel.Settings.Theme);

            if (_trayIcon != null)
                _trayIcon.ContextMenu = BuildTrayContextMenu();

            if (_mainViewModel.Settings.AutoUpdate)
                _updateCheckTimer?.Start();
            else
                _updateCheckTimer?.Stop();
        };

        _settingsWindow.Closed += (s, e) => _settingsWindow = null;

        BringWindowToFront(_settingsWindow);
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
            BringWindowToFront(_ungroupedDialog);
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
            await _mainViewModel.CreateGroupCommand.ExecuteAsync(null);

            var newGroup = _mainViewModel.Groups.LastOrDefault();
            if (newGroup != null)
            {
                newGroup.Name = groupName;
                var assignments = filePaths.ToDictionary(path => path, path => (IconGroup?)newGroup.Model);
                _mainViewModel.ProcessUngroupedAssignments(assignments);
            }
        };

        _ungroupedDialog.Closed += (s, e) => _ungroupedDialog = null;
        BringWindowToFront(_ungroupedDialog);
    }

    // ============================================================================================
    // GLOBAL EXCEPTION HANDLING
    // ============================================================================================

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"Unhandled UI exception: {e.Exception}");

        CrashReporter.WriteCrashLog(e.Exception, "UI dispatcher");
        if (_mainViewModel?.Settings.OptInBugReporting == true)
            CrashReporter.ShowCrashDialog(e.Exception);

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Debug.WriteLine($"Fatal domain exception: {ex}");

            CrashReporter.WriteCrashLog(ex, "AppDomain unhandled");

            try
            {
                _mainViewModel?.SaveAllAsync().GetAwaiter().GetResult();
            }
            catch { }
        }
    }
}
