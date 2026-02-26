using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
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
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        DataContextChanged += OnDataContextChanged;
        Drop += OnDrop;
        DragEnter += OnDragEnter;

        // Subscribe to routed events from child controls
        AddHandler(IconTile.RemoveShortcutEvent, new RoutedEventHandler(OnRemoveShortcutRequested));
        AddHandler(DragThumb.TogglePinRequestedEvent, new RoutedEventHandler(OnTogglePinRequested));
        AddHandler(DragThumb.ToggleSnapRequestedEvent, new RoutedEventHandler(OnToggleSnapRequested));
        AddHandler(DragThumb.SetStickyRequestedEvent, new RoutedEventHandler(OnSetStickyRequested));
        AddHandler(DragThumb.SetFloatRequestedEvent, new RoutedEventHandler(OnSetFloatRequested));
    }

    public GroupWidget(GroupViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    // Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;

        // Hide from Alt+Tab and taskbar
        Win32Interop.SetToolWindow(_windowHandle);

        if (_viewModel != null)
        {
            ApplyDockMode(_viewModel.DockMode);
            ApplyViewMode(_viewModel.ViewMode);

            // Listen for property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
        switch (e.PropertyName)
        {
            case nameof(GroupViewModel.DockMode):
                if (_viewModel != null) ApplyDockMode(_viewModel.DockMode);
                break;

            case nameof(GroupViewModel.ViewMode):
                if (_viewModel != null) ApplyViewMode(_viewModel.ViewMode);
                break;

            case nameof(GroupViewModel.Width):
            case nameof(GroupViewModel.Height):
                if (_viewModel != null && _viewModel.ViewMode == GroupViewMode.Normal)
                {
                    SizeToContent = SizeToContent.Manual;
                    Width = _viewModel.Width;
                    Height = _viewModel.Height;
                }
                break;

            case nameof(GroupViewModel.IsVisible):
                if (_viewModel != null)
                {
                    Visibility = _viewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                }
                break;
        }
    }

    // Dock Mode

    private void ApplyDockMode(GroupDockMode mode)
    {
        if (_windowHandle == IntPtr.Zero) return;

        switch (mode)
        {
            case GroupDockMode.Sticky:
                if (!_isAttachedToDesktop)
                {
                    // Detach topmost first
                    Win32Interop.SetTopmost(_windowHandle, false);

                    bool attached = Win32Interop.AttachToDesktop(_windowHandle);
                    _isAttachedToDesktop = attached;

                    if (!attached)
                    {
                        // Fallback: just send to bottom
                        Win32Interop.SetWindowPos(_windowHandle, Win32Interop.HWND_BOTTOM,
                            0, 0, 0, 0,
                            Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
                    }
                }
                break;

            case GroupDockMode.Float:
                if (_isAttachedToDesktop)
                {
                    Win32Interop.DetachFromDesktop(_windowHandle);
                    _isAttachedToDesktop = false;
                }
                Win32Interop.SetTopmost(_windowHandle, true);
                break;
        }
    }

    // View Mode

    private void ApplyViewMode(GroupViewMode mode)
    {
        switch (mode)
        {
            case GroupViewMode.Minimized:
                SizeToContent = SizeToContent.WidthAndHeight;
                ResizeMode = ResizeMode.NoResize;
                break;

            case GroupViewMode.Hover:
                SizeToContent = SizeToContent.WidthAndHeight;
                ResizeMode = ResizeMode.NoResize;
                break;

            case GroupViewMode.Normal:
                SizeToContent = SizeToContent.Manual;
                if (_viewModel != null)
                {
                    Width = _viewModel.Width;
                    Height = _viewModel.Height;
                }
                ResizeMode = ResizeMode.NoResize; // We handle resize via our own thumb
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
                    Win32Interop.SetTopmost(_windowHandle, true);
                }
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
        _hoverLeaveTimer?.Stop();
        _hoverLeaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _hoverLeaveTimer.Tick += (s, args) =>
        {
            _hoverLeaveTimer.Stop();

            // Only revert if mouse is truly outside
            if (!IsMouseOver)
            {
                _viewModel?.OnMouseLeave();
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
        if (_viewModel != null && _viewModel.DockMode != GroupDockMode.Sticky)
        {
            _viewModel.ToggleDockModeCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void OnSetFloatRequested(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _viewModel.DockMode != GroupDockMode.Float)
        {
            _viewModel.ToggleDockModeCommand.Execute(null);
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