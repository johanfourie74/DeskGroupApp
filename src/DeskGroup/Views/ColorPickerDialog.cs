using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskGroup.Views;

/// <summary>
/// Simple color picker dialog for selecting accent colors.
/// </summary>
public class ColorPickerDialog : Window
{
    private readonly TextBox _hexInput;
    private readonly Border _previewBorder;
    
    public string SelectedColor { get; private set; }

    public ColorPickerDialog(string currentColor)
    {
        Title = "Choose Color";
        Width = 380;
        Height = 310;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;

        SelectedColor = currentColor;

        var grid = new Grid();
        grid.Margin = new Thickness(16);

        // Define ALL rows upfront
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 0: Label
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 1: Hex input + preview
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 2: Presets label
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 3: Preset colors
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 4: Spacer
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Row 5: Buttons

        // Label
        var label = new TextBlock
        {
            Text = "Enter color (hex format):",
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        // Hex input with preview
        var inputPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };

        _hexInput = new TextBox
        {
            Text = currentColor,
            Width = 150,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _hexInput.TextChanged += OnHexInputChanged;
        inputPanel.Children.Add(_hexInput);

        _previewBorder = new Border
        {
            Width = 120,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1)
        };
        UpdatePreview(currentColor);
        inputPanel.Children.Add(_previewBorder);

        Grid.SetRow(inputPanel, 1);
        grid.Children.Add(inputPanel);

        // Preset colors label
        var presetsLabel = new TextBlock
        {
            Text = "Quick colors:",
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(presetsLabel, 2);
        grid.Children.Add(presetsLabel);

        // Preset colors — all 22 Windows 11 accent colors, 11 per row × 2 rows
        var presetsPanel = new WrapPanel { Width = 242 };
        var presetColors = new[]
        {
            "#0078D4", "#0063B1", "#0099BC", "#2D7D9A", "#00B7C3", "#038387",
            "#00B294", "#018574", "#00CC6A", "#10893E", "#107C10",
            "#498205", "#847545", "#FF8C00", "#E81123", "#D13438",
            "#C30052", "#BF0077", "#9B0062", "#881798", "#744DA9", "#8764B8"
        };

        foreach (var color in presetColors)
        {
            var hex = color; // capture for lambda
            var colorButton = new Button
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                ToolTip = color,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            colorButton.Click += (s, e) =>
            {
                _hexInput.Text = hex;
                SelectedColor = hex;
            };
            presetsPanel.Children.Add(colorButton);
        }

        Grid.SetRow(presetsPanel, 3);
        presetsPanel.Margin = new Thickness(0, 0, 0, 16);
        grid.Children.Add(presetsPanel);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Padding = new Thickness(0, 6, 0, 6),
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        okButton.Click += (s, e) => { DialogResult = true; Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(0, 6, 0, 6),
            IsCancel = true
        };
        cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelButton);

        Grid.SetRow(buttonPanel, 5); // Last row
        grid.Children.Add(buttonPanel);

        Content = grid;
    }

    private void OnHexInputChanged(object sender, TextChangedEventArgs e)
    {
        var hex = _hexInput.Text;
        UpdatePreview(hex);
        SelectedColor = hex;
    }

    private void UpdatePreview(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _previewBorder.Background = new SolidColorBrush(color);
        }
        catch
        {
            _previewBorder.Background = Brushes.White;
        }
    }
}
