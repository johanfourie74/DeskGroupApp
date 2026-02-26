using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DeskGroup.Models;

namespace DeskGroup.Converters;

public class ViewModeToVisibilityConverter : IValueConverter
{
    public GroupViewMode TargetMode { get; set; } = GroupViewMode.Minimized;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GroupViewMode mode)
        {
            if (parameter is string paramStr && Enum.TryParse<GroupViewMode>(paramStr, out var paramMode))
                return mode == paramMode ? Visibility.Visible : Visibility.Collapsed;

            return mode == TargetMode ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}