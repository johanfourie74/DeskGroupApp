using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DeskGroup.Models;
using DeskGroup.ViewModels;

namespace DeskGroup.Views;

public partial class UngroupedIconsDialog : Window
{
    private readonly UngroupedIconsViewModel _viewModel;
    private bool _sortingApplied = false;

    /// <summary>
    /// Fired when the user clicks "Apply Sorting" with valid assignments.
    /// The dictionary maps file paths to their assigned groups (null = unassigned).
    /// </summary>
    public event EventHandler<Dictionary<string, IconGroup?>>? SortingApplied;

    /// <summary>
    /// Fired when the user dismisses all ungrouped icons.
    /// </summary>
    public event EventHandler? AllDismissed;

    public UngroupedIconsDialog(UngroupedIconsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Listen for completion events from the ViewModel
        _viewModel.Completed += OnViewModelCompleted;
    }

    /// <summary>
    /// Whether any sorting was applied during this dialog session.
    /// </summary>
    public bool WasSortingApplied => _sortingApplied;

    /// <summary>
    /// Get the final assignments (for callers who don't use events).
    /// </summary>
    public Dictionary<string, IconGroup?> GetAssignments()
    {
        return _viewModel.GetAssignments();
    }

    // â”€â”€ Event Handlers â”€â”€

    private void ApplySortButton_Click(object sender, RoutedEventArgs e)
    {
        var assignments = _viewModel.GetAssignments();

        // Check if any icons were actually assigned
        var assignedCount = assignments.Values.Count(g => g != null);

        if (assignedCount == 0)
        {
            var result = MessageBox.Show(
                "No icons have been assigned to groups.\n\n" +
                "Would you like to dismiss them instead?",
                "No Assignments",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    _viewModel.DismissAllCommand.Execute(null);
                    return;
                case MessageBoxResult.No:
                    return; // Go back to the dialog
                case MessageBoxResult.Cancel:
                    return;
            }
        }

        // Check for unassigned icons
        var unassignedCount = assignments.Values.Count(g => g == null);
        if (unassignedCount > 0)
        {
            var result = MessageBox.Show(
                $"{unassignedCount} icon(s) are not assigned to any group.\n\n" +
                "They will remain on your desktop. Continue?",
                "Unassigned Icons",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes)
                return;
        }

        _sortingApplied = true;
        SortingApplied?.Invoke(this, assignments);

        // Show confirmation
        ApplySortButton.Content = $"âœ“ {assignedCount} sorted";
        ApplySortButton.IsEnabled = false;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        timer.Tick += (s, args) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnViewModelCompleted(object? sender, EventArgs e)
    {
        // This is triggered by DismissAll â€” fire the event and close
        AllDismissed?.Invoke(this, EventArgs.Empty);

        Dispatcher.Invoke(() =>
        {
            Close();
        });
    }

    // â”€â”€ Closing â”€â”€

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        _viewModel.Completed -= OnViewModelCompleted;
    }
}