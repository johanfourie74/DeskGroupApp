using System;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class ShortcutItemViewModel : ObservableObject
{
    private readonly ShortcutItem _model;
    private readonly UsageTrackingService _usageTracker;
    private readonly IconExtractorService _iconExtractor;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private ImageSource? _icon;

    [ObservableProperty]
    private int _launchCount;

    [ObservableProperty]
    private bool _isSelected;

    public Guid Id => _model.Id;
    public ShortcutItem Model => _model;

    public ShortcutItemViewModel(ShortcutItem model, UsageTrackingService usageTracker,
        IconExtractorService iconExtractor)
    {
        _model = model;
        _usageTracker = usageTracker;
        _iconExtractor = iconExtractor;
        DisplayName = model.DisplayName;
        LaunchCount = model.LaunchCount;
        LoadIcon();
    }

    private void LoadIcon()
    {
        try
        {
            if (!string.IsNullOrEmpty(_model.IconPath))
                Icon = _iconExtractor.ExtractIconByIndex(_model.IconPath, _model.IconIndex);
            if (Icon == null && !string.IsNullOrEmpty(_model.TargetPath))
                Icon = _iconExtractor.ExtractLargeIcon(_model.TargetPath);
            if (Icon == null && !string.IsNullOrEmpty(_model.OriginalShortcutPath))
                Icon = _iconExtractor.ExtractLargeIcon(_model.OriginalShortcutPath);
        }
        catch { Icon = null; }
    }

    [RelayCommand]
    private void Launch()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = !string.IsNullOrEmpty(_model.TargetPath)
                    ? _model.TargetPath : _model.OriginalShortcutPath
            };
            if (!string.IsNullOrEmpty(_model.Arguments)) startInfo.Arguments = _model.Arguments;
            if (!string.IsNullOrEmpty(_model.WorkingDirectory)) startInfo.WorkingDirectory = _model.WorkingDirectory;

            Process.Start(startInfo);
            _usageTracker.RecordLaunch(_model);
            LaunchCount = _model.LaunchCount;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to launch shortcut: {ex.Message}");
        }
    }

    [RelayCommand]
    private void RefreshIcon() => LoadIcon();

    [RelayCommand]
    private void ResetUsage()
    {
        _usageTracker.ResetUsage(_model);
        LaunchCount = 0;
    }
}