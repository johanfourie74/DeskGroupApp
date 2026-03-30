using System.Windows;
using DeskGroup.Helpers;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.Views;

public partial class FirstRunSetupDialog : Window
{
    private readonly AppSettings _settings;
    private readonly PersistenceService _persistence;

    public FirstRunSetupDialog(AppSettings settings, PersistenceService persistence)
    {
        InitializeComponent();
        _settings = settings;
        _persistence = persistence;

        // ── Appearance ────────────────────────────────────────────────────────
        CbTheme.SelectedIndex = (int)settings.Theme;          // Light=0 Dark=1 System=2
        SliderTransparency.Value = 0.25;                       // always default 25% on first run
        LblTransparency.Text = "25%";

        // ── Desktop Integration ───────────────────────────────────────────────
        ChkHideGroupedIcons.IsChecked  = settings.HideGroupedIcons;
        ChkShellContextMenu.IsChecked  = settings.EnableShellContextMenu;

        // ── Hotkeys ───────────────────────────────────────────────────────────
        HkToggleAll.HotkeyValue     = settings.ToggleAllHotkey;
        HkToggleMouse.HotkeyValue   = settings.ToggleAllMouseHotkey;
        HkFullscreenKey.HotkeyValue = settings.FullscreenToggleHotkey;
        HkFullscreenMouse.HotkeyValue = settings.FullscreenMouseHotkey;

        // ── Startup & Behaviour ───────────────────────────────────────────────
        ChkStartWithWindows.IsChecked = settings.StartWithWindows;
        ChkStartMinimized.IsChecked   = settings.StartMinimizedToTray;
        ChkMultiMonitor.IsChecked     = settings.AllowMultiMonitor;
        ChkDesktop.IsChecked          = settings.DesktopShortcutInstalled;
        ChkStart.IsChecked            = settings.PinnedToStart;
        ChkTaskbar.IsChecked          = settings.PinnedToTaskbar;

        // ── Updates ───────────────────────────────────────────────────────────
        ChkAutoUpdate.IsChecked          = settings.AutoUpdate;
        ChkAutoInstall.IsChecked         = settings.AutoInstall;
        ChkOptInBugReporting.IsChecked   = settings.OptInBugReporting;
    }

    private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }

    private void SliderTransparency_ValueChanged(object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblTransparency != null)
            LblTransparency.Text = $"{(int)(e.NewValue * 100)}%";
    }

    private async void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        // ── Appearance ────────────────────────────────────────────────────────
        _settings.Theme                 = (Models.ThemeMode)CbTheme.SelectedIndex;
        _settings.GroupBackgroundOpacity = SliderTransparency.Value;

        // ── Desktop Integration ───────────────────────────────────────────────
        _settings.HideGroupedIcons    = ChkHideGroupedIcons.IsChecked == true;
        _settings.EnableShellContextMenu = ChkShellContextMenu.IsChecked == true;

        // ── Hotkeys ───────────────────────────────────────────────────────────
        _settings.ToggleAllHotkey       = HkToggleAll.HotkeyValue ?? string.Empty;
        _settings.ToggleAllMouseHotkey  = HkToggleMouse.HotkeyValue ?? string.Empty;
        _settings.FullscreenToggleHotkey = HkFullscreenKey.HotkeyValue ?? string.Empty;
        _settings.FullscreenMouseHotkey = HkFullscreenMouse.HotkeyValue ?? string.Empty;

        // ── Startup & Behaviour ───────────────────────────────────────────────
        _settings.StartWithWindows      = ChkStartWithWindows.IsChecked == true;
        _settings.StartMinimizedToTray  = ChkStartMinimized.IsChecked == true;
        _settings.AllowMultiMonitor     = ChkMultiMonitor.IsChecked == true;

        bool installShortcut = ChkDesktop.IsChecked == true;
        bool pinStart        = ChkStart.IsChecked == true;
        bool pinTaskbar      = ChkTaskbar.IsChecked == true;

        _settings.DesktopShortcutInstalled = installShortcut;
        _settings.PinnedToStart            = pinStart;
        _settings.PinnedToTaskbar          = pinTaskbar;

        // ── Updates ───────────────────────────────────────────────────────────
        _settings.AutoUpdate         = ChkAutoUpdate.IsChecked == true;
        _settings.AutoInstall        = ChkAutoInstall.IsChecked == true;
        _settings.OptInBugReporting  = ChkOptInBugReporting.IsChecked == true;

        // ── Apply side-effects ────────────────────────────────────────────────
        new StartupService().SyncWithSetting(_settings.StartWithWindows);

        if (_settings.EnableShellContextMenu)
            ShellContextMenuHelper.RegisterContextMenu();
        else
            ShellContextMenuHelper.UnregisterContextMenu();

        if (installShortcut)
            ShortcutInstallHelper.InstallDesktopShortcut();

        await _persistence.SaveSettingsImmediateAsync(_settings);

        if (pinStart)
            ShowPinInstructions(ShortcutInstallHelper.PinToStartMenu(), "Start menu");
        if (pinTaskbar)
            ShowPinInstructions(ShortcutInstallHelper.PinToTaskbar(), "taskbar");

        DialogResult = true;
    }

    private void ShowPinInstructions(ShortcutInstallHelper.PinResult result, string target)
    {
        if (result == ShortcutInstallHelper.PinResult.Success) return;
        MessageBox.Show(
            $"Automatic {target} pinning isn't supported on Windows 11.\n\n" +
            $"To pin DeskGroup to the {target} manually:\n" +
            $"1. Find the DeskGroup shortcut on your desktop\n" +
            $"2. Right-click it and choose 'Pin to {target}'\n\n" +
            $"Your preference has been saved — this won't appear again.",
            $"Pin to {target} — Manual Step Needed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

