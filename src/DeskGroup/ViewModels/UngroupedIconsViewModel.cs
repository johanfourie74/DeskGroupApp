using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskGroup.Models;
using DeskGroup.Services;

namespace DeskGroup.ViewModels;

public partial class UngroupedIconsViewModel : ObservableObject
{
    private readonly IconExtractorService _iconExtractor;
    private readonly AppSettings _settings;
    private readonly PersistenceService _persistenceService;
    private readonly IconGroup _createNewGroupPlaceholder;

    public ObservableCollection<UngroupedIconItem> UngroupedIcons { get; } = new();
    public ObservableCollection<IconGroup> AvailableGroups { get; } = new();
    public event EventHandler? Completed;
    public event EventHandler<string>? CreateNewGroupRequested; // New event

    public UngroupedIconsViewModel(List<string> ungroupedPaths, List<IconGroup> existingGroups,
        IconExtractorService iconExtractor, AppSettings settings, PersistenceService persistenceService)
    {
        _iconExtractor = iconExtractor; _settings = settings; _persistenceService = persistenceService;

        // Add placeholder for "Create new group..."
        _createNewGroupPlaceholder = new IconGroup { Id = Guid.Empty, Name = "➕ Create new group..." };
        AvailableGroups.Add(_createNewGroupPlaceholder);

        foreach (var group in existingGroups) AvailableGroups.Add(group);
        foreach (var path in ungroupedPaths)
        {
            var icon = _iconExtractor.ExtractLargeIcon(path);
            var displayName = _iconExtractor.GetShellDisplayName(path);
            UngroupedIcons.Add(new UngroupedIconItem { FilePath = path, DisplayName = displayName, Icon = icon });
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DismissAll()
    {
        foreach (var item in UngroupedIcons)
        {
            var fileName = Path.GetFileName(item.FilePath);
            if (!_settings.DismissedUngroupedIcons.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                _settings.DismissedUngroupedIcons.Add(fileName);
        }
        await _persistenceService.SaveSettingsImmediateAsync(_settings);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ApplySorting() => Completed?.Invoke(this, EventArgs.Empty);

    public Dictionary<string, IconGroup?> GetAssignments() =>
        UngroupedIcons.ToDictionary(i => i.FilePath, 
            i => i.SelectedGroup?.Id == Guid.Empty ? null : i.SelectedGroup); // Treat placeholder as unassigned

    public bool IsCreateNewGroupPlaceholder(IconGroup? group) => group?.Id == Guid.Empty;

    public List<UngroupedIconItem> GetItemsWithCreateNewGroup() =>
        UngroupedIcons.Where(i => i.SelectedGroup?.Id == Guid.Empty).ToList();
}

public partial class UngroupedIconItem : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    [ObservableProperty] private System.Windows.Media.ImageSource? _icon;
    [ObservableProperty] private IconGroup? _selectedGroup;
}