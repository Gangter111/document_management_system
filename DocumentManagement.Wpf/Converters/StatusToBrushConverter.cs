using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DocumentManagement.Wpf.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var statusId = value switch
        {
            long l => l,
            int i => i,
            null => 0,
            _ => 0
        };

        var color = statusId switch
        {
            1 => "#64748B", // Draft
            4 => "#059669", // Issued
            5 => "#7C3AED", // Archived
            _ => "#6B7280"
        };

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}