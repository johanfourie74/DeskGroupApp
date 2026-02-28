using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
    private SingleInstanceGuard? _singleInstanceGuard;
    private MainViewModel? _mainViewModel;
    private TaskbarIcon? _trayIcon;
    private readonly Dictionary<Guid, GroupWidget> _groupWindows = new();

    // Hidden helper window used for hotkey registration
    // (needs an HWND but doesn't need to be visible)
    private Window? _helperWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single Instance Check
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.TryAcquire())
        {
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

        // Set application-level binding proxy to main view model (for resource bindings)
        try
        {
            if (Resources["AppDataProxy"] is DeskGroup.Helpers.BindingProxy proxy)
            {
                proxy.Data = _mainViewModel;
            }
        }
        catch { }

        // Apply Theme
        ApplyTheme(_mainViewModel.Settings.Theme);

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

        // Show first-run hint if applicable
        if (_mainViewModel.Settings.IsFirstRun)
        {
            _trayIcon?.ShowBalloonTip(
                "DeskGroup is running",
                "Right-click the tray icon to create groups and manage settings.",
                BalloonIcon.Info);
        }
        else if (_mainViewModel.Groups.Count > 0)
        {
            // Not first run and groups exist - show helpful message
            _trayIcon?.ShowBalloonTip(
                $"DeskGroup loaded {_mainViewModel.Groups.Count} group(s)",
                "If you don't see your groups, try 'Show All Groups' from the tray menu.",
                BalloonIcon.Info);
        }

        // Note: Removed diagnostic message box to avoid blocking startup
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

        // Dispose tray icon
        _trayIcon?.Dispose();
        _trayIcon = null;

        // Close helper window
        try { _helperWindow?.Close(); } catch { }

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

            // Use the embedded icon resource
            try
            {
                System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Loading icon from resources");

                var iconUri = new Uri("pack://application:,,,/Assets/tray-icon.ico", UriKind.Absolute);
                var streamInfo = GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Icon stream loaded, creating Icon object");
                    _trayIcon.Icon = new Icon(streamInfo.Stream);
                    System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Icon set successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Icon stream is null, using fallback");
                    _trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch (Exception iconEx)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTrayIcon: Icon loading failed: {iconEx.Message}");
                // Fallback: use default application icon
                _trayIcon.Icon = SystemIcons.Application;
            }

            System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Building context menu");

            // Build context menu
            _trayIcon.ContextMenu = BuildTrayContextMenu();

            System.Diagnostics.Debug.WriteLine("CreateTrayIcon: Wiring double-click event");

            // Double-click tray icon -> toggle visibility
            _trayIcon.TrayMouseDoubleClick += (s, e) =>
            {
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

        // DEBUG: Force all groups to visible float mode
        var forceVisible = new System.Windows.Controls.MenuItem { Header = "🔧 DEBUG: Force All Visible (Float+Normal)" };
        forceVisible.Click += (s, e) => ForceAllGroupsVisible();
        menu.Items.Add(forceVisible);

        // DEBUG: Show test window
        var testWindow = new System.Windows.Controls.MenuItem { Header = "🧪 DEBUG: Show Test Window" };
        testWindow.Click += (s, e) => ShowTestWindow();
        menu.Items.Add(testWindow);

        menu.Items.Add(new System.Windows.Controls.Separator());

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

        foreach (var groupVm in _mainViewModel.Groups)
        {
            groupVm.IsVisible = true;
            groupVm.ViewMode = GroupViewMode.Normal;
            groupVm.DockMode = GroupDockMode.Float;

            // Force window update
            if (_groupWindows.TryGetValue(groupVm.Id, out var window))
            {
                window.Topmost = true;
                window.Activate();
            }
        }

        MessageBox.Show(
            $"Forced {_mainViewModel.Groups.Count} group(s) to:\n" +
            $"• ViewMode: Normal (large window)\n" +
            $"• DockMode: Float (on top of windows)\n" +
            $"• IsVisible: True\n\n" +
            $"Check your screen at (100, 100) - top-left area!",
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
        window.Left = groupVm.PositionX;
        window.Top = groupVm.PositionY;

        // Pre-set size and constraints based on view mode to prevent visible resize
        switch (groupVm.ViewMode)
        {
            case GroupViewMode.Minimized:
                window.MinWidth = 60;
                window.MinHeight = 40;
                window.Width = 160;
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
        Dispatcher.Invoke(() => CreateGroupWindow(groupVm));
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

        _settingsWindow.SettingsApplied += (s, e) =>
        {
            if (_helperWindow != null)
            {
                _mainViewModel.ApplySettingsChanges(_helperWindow);
            }
        };

        _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        _settingsWindow.Show();
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

                // Add shortcuts to it
                var assignments = filePaths.ToDictionary(path => path, path => newGroup.Model);
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

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
            "DeskGroup will try to continue running.",
            "DeskGroup Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true; // Don't crash the app
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fatal domain exception: {ex}");

            try
            {
                // Try to save data before crashing
                _mainViewModel?.SaveAllAsync().GetAwaiter().GetResult();
            }
            catch { }
        }
    }
}