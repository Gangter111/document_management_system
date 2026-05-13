using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DocumentManagement.Wpf.Converters
{
    public class HexToBrushConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var hex = value?.ToString();

            if (string.IsNullOrWhiteSpace(hex))
            {
                return Brushes.Transparent;
            }

            try
            {
                return BrushCache.GetOrAdd(hex.Trim(), static key =>
                {
                    var color = (Color)ColorConverter.ConvertFromString(key);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                });
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }

            return "#00000000";
        }
    }
}
