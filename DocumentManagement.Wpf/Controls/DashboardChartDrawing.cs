using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace DocumentManagement.Wpf.Controls;

internal static class DashboardChartDrawing
{
    public static readonly Typeface RegularTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public static readonly Typeface MediumTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    public static readonly Typeface SemiBoldTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    public static readonly Typeface BoldTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static FormattedText Text(string text, double size, Typeface typeface, Brush brush, double pixelsPerDip)
    {
        return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, Math.Max(1, size), brush, pixelsPerDip);
    }

    public static Brush TwoStopGradient(string c1, string c2, Point start, Point end)
    {
        return TwoStopGradient((Color)ColorConverter.ConvertFromString(c1), (Color)ColorConverter.ConvertFromString(c2), start, end);
    }

    public static Brush TwoStopGradient(Color c1, Color c2, Point start, Point end)
    {
        return new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end,
            GradientStops =
            {
                new GradientStop(c1, 0),
                new GradientStop(c2, 1)
            }
        };
    }

    public static Brush ThreeStopGradient(string c1, string c2, string c3, Point start, Point end)
    {
        return new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end,
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString(c2), 0.46),
                new GradientStop((Color)ColorConverter.ConvertFromString(c3), 1)
            }
        };
    }

    public static Brush RadialBrush(Color inner, Color outer)
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.5, 0.4),
            GradientOrigin = new Point(0.5, 0.32),
            RadiusX = 0.98,
            RadiusY = 0.98,
            GradientStops =
            {
                new GradientStop(inner, 0),
                new GradientStop(outer, 1)
            }
        };
    }

    public static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
