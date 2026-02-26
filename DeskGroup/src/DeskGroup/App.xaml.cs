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

        // â”€â”€ Single Instance Check â”€â”€
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

        // â”€â”€ Global Exception Handling â”€â”€
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // â”€â”€ Initialize MainViewModel â”€â”€
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

        // â”€â”€ Apply Theme â”€â”€
        ApplyTheme(_mainViewModel.Settings.Theme);

        // â”€â”€ Create Helper Window for Hotkeys â”€â”€
        _helperWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };
        _helperWindow.Show();
        _helperWindow.Hide();

        // â”€â”€ Register Hotkeys â”€â”€
        _mainViewModel.RegisterHotkeys(_helperWindow);

        // â”€â”€ Subscribe to MainViewModel Events â”€â”€
        _mainViewModel.ThemeChanged += OnThemeChanged;
        _mainViewModel.GroupAdded += OnGroupAdded;
        _mainViewModel.GroupRemoved += OnGroupRemoved;
        _mainViewModel.UngroupedIconsDetected += OnUngroupedIconsDetected;

        // â”€â”€ Create System Tray Icon â”€â”€
        CreateTrayIcon();

        // â”€â”€ Create Widget Windows for Existing Groups â”€â”€
        foreach (var groupVm in _mainViewModel.Groups)
        {
            CreateGroupWindow(groupVm);
        }

        // â”€â”€ Show first-run hint if applicable â”€â”€
        if (_mainViewModel.Settings.IsFirstRun)
        {
            _trayIcon?.ShowBalloonTip(
                "DeskGroup is running",
                "Right-click the tray icon to create groups and manage settings.",
                BalloonIcon.Info);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // â”€â”€ Save all data â”€â”€
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

        // â”€â”€ Close all group windows â”€â”€
        foreach (var window in _groupWindows.Values.ToList())
        {
            try { window.Close(); } catch { }
        }
        _groupWindows.Clear();

        // â”€â”€ Dispose tray icon â”€â”€
        _trayIcon?.Dispose();
        _trayIcon = null;

        // â”€â”€ Close helper window â”€â”€
        try { _helperWindow?.Close(); } catch { }

        // â”€â”€ Release single instance â”€â”€
        _singleInstanceGuard?.Dispose();

        base.OnExit(e);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SYSTEM TRAY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "DeskGroup â€” Desktop Icon Organizer",
            MenuActivation = PopupActivationMode.RightClick
        };

        // Use the embedded icon resource
        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/tray-icon.ico", UriKind.Absolute);
            var streamInfo = GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                _trayIcon.Icon = new Icon(streamInfo.Stream);
            }
        }
        catch
        {
            // Fallback: use default application icon
            _trayIcon.Icon = SystemIcons.Application;
        }

        // Build context menu
        _trayIcon.ContextMenu = BuildTrayContextMenu();

        // Double-click tray icon â†’ toggle visibility
        _trayIcon.TrayMouseDoubleClick += (s, e) =>
        {
            _mainViewModel?.ToggleAllGroupsVisibility();
        };
    }

    private System.Windows.Controls.ContextMenu BuildTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // â”€â”€ Show / Hide All â”€â”€
        var showAll = new System.Windows.Controls.MenuItem { Header = "Show All Groups" };
        showAll.Click += (s, e) => _mainViewModel?.ShowAllGroupsCommand.Execute(null);
        menu.Items.Add(showAll);

        var hideAll = new System.Windows.Controls.MenuItem { Header = "Hide All Groups" };
        hideAll.Click += (s, e) => _mainViewModel?.HideAllGroupsCommand.Execute(null);
        menu.Items.Add(hideAll);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // â”€â”€ Create New Group â”€â”€
        var newGroup = new System.Windows.Controls.MenuItem { Header = "New Group" };
        newGroup.Click += (s, e) => _mainViewModel?.CreateGroupCommand.Execute(null);
        menu.Items.Add(newGroup);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // â”€â”€ Sort Ungrouped Icons â”€â”€
        var sortIcons = new System.Windows.Controls.MenuItem { Header = "Sort Ungrouped Icons..." };
        sortIcons.Click += (s, e) => ShowUngroupedIconsDialog();
        menu.Items.Add(sortIcons);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // â”€â”€ Settings â”€â”€
        var settings = new System.Windows.Controls.MenuItem { Header = "Settings..." };
        settings.Click += (s, e) => ShowSettingsWindow();
        menu.Items.Add(settings);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // â”€â”€ Exit â”€â”€
        var exit = new System.Windows.Controls.MenuItem { Header = "Exit DeskGroup" };
        exit.Click += (s, e) => Shutdown();
        menu.Items.Add(exit);

        return menu;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // GROUP WINDOW MANAGEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void CreateGroupWindow(GroupViewModel groupVm)
    {
        if (_groupWindows.ContainsKey(groupVm.Id)) return;

        var window = new GroupWidget(groupVm);

        window.DeleteRequested += (s, e) =>
        {
            _mainViewModel?.DeleteGroupCommand.Execute(groupVm);
        };

        window.FilesDropped += (s, files) =>
        {
            // Already handled inside GroupWidget, but hook available for future use
        };

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
        Dispatcher.Invoke(() => CreateGroupWindow(groupVm));
    }

    private void OnGroupRemoved(object? sender, GroupViewModel groupVm)
    {
        Dispatcher.Invoke(() => DestroyGroupWindow(groupVm));
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // THEME MANAGEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void ApplyTheme(ThemeMode mode)
    {
        var themeUri = mode switch
        {
            ThemeMode.Light => new Uri("Themes/LightTheme.xaml", UriKind.Relative),
            ThemeMode.Dark => new Uri("Themes/DarkTheme.xaml", UriKind.Relative),
            ThemeMode.System => IsSystemDarkMode()
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

    private void OnThemeChanged(object? sender, ThemeMode mode)
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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SETTINGS WINDOW
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // UNGROUPED ICONS DIALOG
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
                "All desktop icons are already grouped! ðŸŽ‰",
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

        _ungroupedDialog.Closed += (s, e) => _ungroupedDialog = null;
        _ungroupedDialog.Show();
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // GLOBAL EXCEPTION HANDLING
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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