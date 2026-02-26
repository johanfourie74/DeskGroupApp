using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskGroup.Controls;

public partial class ColorPickerButton : UserControl
{
    public ColorPickerButton()
    {
        InitializeComponent();
        UpdateBrush();
    }

    // â”€â”€ Selected Color (hex string) â”€â”€

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(string), typeof(ColorPickerButton),
            new FrameworkPropertyMetadata("#0078D4",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerButton picker)
        {
            picker.UpdateBrush();
            picker.RaiseColorChangedEvent();
        }
    }

    // â”€â”€ Selected Brush (read-only, derived from SelectedColor) â”€â”€

    public static readonly DependencyProperty SelectedBrushProperty =
        DependencyProperty.Register(nameof(SelectedBrush), typeof(SolidColorBrush), typeof(ColorPickerButton),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))));

    public SolidColorBrush SelectedBrush
    {
        get => (SolidColorBrush)GetValue(SelectedBrushProperty);
        private set => SetValue(SelectedBrushProperty, value);
    }

    // â”€â”€ ColorChanged Command (for MVVM binding) â”€â”€

    public static readonly DependencyProperty ColorChangedCommandProperty =
        DependencyProperty.Register(nameof(ColorChangedCommand), typeof(ICommand), typeof(ColorPickerButton),
            new PropertyMetadata(null));

    public ICommand? ColorChangedCommand
    {
        get => (ICommand?)GetValue(ColorChangedCommandProperty);
        set => SetValue(ColorChangedCommandProperty, value);
    }

    // â”€â”€ ColorChanged Routed Event â”€â”€

    public static readonly RoutedEvent ColorChangedEvent =
        EventManager.RegisterRoutedEvent("ColorChanged", RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<string>), typeof(ColorPickerButton));

    public event RoutedPropertyChangedEventHandler<string> ColorChanged
    {
        add => AddHandler(ColorChangedEvent, value);
        remove => RemoveHandler(ColorChangedEvent, value);
    }

    private void RaiseColorChangedEvent()
    {
        var args = new RoutedPropertyChangedEventArgs<string>(
            string.Empty, SelectedColor, ColorChangedEvent);
        RaiseEvent(args);

        // Also execute the command if bound
        if (ColorChangedCommand?.CanExecute(SelectedColor) == true)
        {
            ColorChangedCommand.Execute(SelectedColor);
        }
    }

    // â”€â”€ Brush Update â”€â”€

    private void UpdateBrush()
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(SelectedColor);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            SelectedBrush = brush;
        }
        catch
        {
            var brush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
            brush.Freeze();
            SelectedBrush = brush;
        }
    }

    // â”€â”€ Event Handlers â”€â”€

    private void MainButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPopup.IsOpen = !ColorPopup.IsOpen;

        if (ColorPopup.IsOpen)
        {
            // Pre-fill the custom input with current color (without #)
            var hex = SelectedColor.TrimStart('#');
            CustomColorInput.Text = hex;
            CustomColorInput.Focus();
            CustomColorInput.SelectAll();
        }
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string hexColor)
        {
            SelectedColor = hexColor;
            ColorPopup.IsOpen = false;
        }
    }

    private void CustomColorApply_Click(object sender, RoutedEventArgs e)
    {
        ApplyCustomColor();
    }

    private void ApplyCustomColor()
    {
        var input = CustomColorInput.Text.Trim().TrimStart('#');

        if (string.IsNullOrEmpty(input))
            return;

        // Support 3-char shorthand (e.g., "F00" â†’ "FF0000")
        if (input.Length == 3)
        {
            input = $"{input[0]}{input[0]}{input[1]}{input[1]}{input[2]}{input[2]}";
        }

        if (input.Length != 6)
            return;

        var hex = $"#{input}";

        try
        {
            // Validate it parses
            ColorConverter.ConvertFromString(hex);
            SelectedColor = hex;
            ColorPopup.IsOpen = false;
        }
        catch
        {
            // Invalid color â€” flash the input border red briefly
            CustomColorInput.BorderBrush = Brushes.Red;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            timer.Tick += (s, args) =>
            {
                CustomColorInput.ClearValue(Control.BorderBrushProperty);
                timer.Stop();
            };
            timer.Start();
        }
    }
}