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
    }

    private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }

    private async void GetStarted_Click(object sender, RoutedEventArgs e)
    {
        bool installShortcut = ChkDesktop.IsChecked == true;
        bool pinStart = ChkStart.IsChecked == true;
        bool pinTaskbar = ChkTaskbar.IsChecked == true;

        if (installShortcut)
            ShortcutInstallHelper.InstallDesktopShortcut();

        _settings.DesktopShortcutInstalled = installShortcut;
        _settings.PinnedToStart = pinStart;
        _settings.PinnedToTaskbar = pinTaskbar;

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
