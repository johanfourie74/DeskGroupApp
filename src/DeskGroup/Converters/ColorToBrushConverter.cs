using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeskGroup.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch { }
        }
        if (value is Color c)
        {
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }
        return Brushes.DodgerBlue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
            return brush.Color.ToString();
        return "#0078D4";
    }
}