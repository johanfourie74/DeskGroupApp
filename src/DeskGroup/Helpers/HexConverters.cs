using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DeskGroup.Helpers
{
    // Converts a hex color string ("#RRGGBB" or "#AARRGGBB") to a System.Windows.Media.Color
    public class HexToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return Colors.Transparent;
            if (value is Color c) return c;
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return Colors.Transparent;
            try
            {
                // Ensure leading '#'
                if (!s.StartsWith("#")) s = "#" + s;
                var color = (Color)ColorConverter.ConvertFromString(s);
                return color;
            }
            catch
            {
                return Colors.Transparent;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Color c)
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            return null;
        }
    }

    // Converts a hex color string to a frozen SolidColorBrush
    public class HexToBrushConverter : IValueConverter
    {
        private static readonly HexToColorConverter _colorConv = new HexToColorConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var colorObj = _colorConv.Convert(value, typeof(Color), parameter, culture);
            if (colorObj is Color color)
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush b)
            {
                var c = b.Color;
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            return null;
        }
    }
}
