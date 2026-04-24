using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DocumentManagement.Domain.Enums;

namespace DocumentManagement.Wpf.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long statusId = value switch
        {
            long l => l,
            int i => i,
            DocumentStatus status => (long)status,
            _ => 0
        };

        var color = statusId switch
        {
            (long)DocumentStatus.Draft => "#64748B",
            (long)DocumentStatus.PendingApproval => "#F59E0B",
            (long)DocumentStatus.Approved => "#2563EB",
            (long)DocumentStatus.Issued => "#059669",
            (long)DocumentStatus.Archived => "#7C3AED",
            (long)DocumentStatus.Rejected => "#DC2626",
            _ => "#6B7280"
        };

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}