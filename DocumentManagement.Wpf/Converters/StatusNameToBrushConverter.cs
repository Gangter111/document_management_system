using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DocumentManagement.Wpf.Converters;

public class StatusNameToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;

        var color = status switch
        {
            "bản nháp" or "draft" => "#64748B",
            "chờ duyệt" or "đang duyệt" or "pending" or "pending approval" => "#F59E0B",
            "đã duyệt" or "approved" => "#2563EB",
            "đã ban hành" or "issued" => "#059669",
            "đã lưu trữ" or "archived" => "#7C3AED",
            "bị từ chối" or "rejected" => "#DC2626",
            _ => "#6B7280"
        };

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}