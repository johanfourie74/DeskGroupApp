using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DeskGroup.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        if (Invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool result = value is Visibility v && v == Visibility.Visible;
        if (Invert) result = !result;
        return result;
    }
}