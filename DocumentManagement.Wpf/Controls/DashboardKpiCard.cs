using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DocumentManagement.Wpf.Controls;

public class DashboardKpiCard : Border
{
    private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(7, 19, 52)));
    private static readonly Brush CardBrush = Freeze(new SolidColorBrush(Colors.White));
    private static readonly Brush CardSelectedBrush = Freeze(new SolidColorBrush(Color.FromRgb(248, 251, 255)));
    private static readonly Brush WhiteBrush = Freeze(new SolidColorBrush(Colors.White));

    public static readonly DependencyProperty MetricKeyProperty =
        DependencyProperty.Register(nameof(MetricKey), typeof(string), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(RadialDocumentChart.TotalMetric));

    public static readonly DependencyProperty SelectedMetricProperty =
        DependencyProperty.Register(nameof(SelectedMetric), typeof(string), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(RadialDocumentChart.TotalMetric, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));

    public static readonly DependencyProperty HoveredMetricProperty =
        DependencyProperty.Register(nameof(HoveredMetric), typeof(string), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(string.Empty, OnVisualChanged));

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(nameof(IconKind), typeof(string), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata("Pie", OnVisualChanged));

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(Brushes.DodgerBlue, OnVisualChanged));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(DashboardKpiCard),
            new FrameworkPropertyMetadata(Brushes.DodgerBlue, OnVisualChanged));

    public string MetricKey
    {
        get => (string)GetValue(MetricKeyProperty);
        set => SetValue(MetricKeyProperty, value);
    }

    public string SelectedMetric
    {
        get => (string)GetValue(SelectedMetricProperty);
        set => SetValue(SelectedMetricProperty, value);
    }

    public string HoveredMetric
    {
        get => (string)GetValue(HoveredMetricProperty);
        set => SetValue(HoveredMetricProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public string IconKind
    {
        get => (string)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public Brush ValueBrush
    {
        get => (Brush)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public DashboardKpiCard()
    {
        Height = 46;
        CornerRadius = new CornerRadius(10);
        Background = CardBrush;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        Loaded += (_, _) => Build();
        MouseEnter += (_, _) =>
        {
            HoveredMetric = MetricKey;
            Build();
        };
        MouseLeave += (_, _) =>
        {
            if (string.Equals(HoveredMetric, MetricKey, StringComparison.Ordinal))
            {
                HoveredMetric = string.Empty;
            }

            Build();
        };
        MouseLeftButtonDown += (_, e) =>
        {
            SelectedMetric = MetricKey;
            e.Handled = true;
        };
    }

    private bool IsActive
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(HoveredMetric))
            {
                return string.Equals(HoveredMetric, MetricKey, StringComparison.Ordinal);
            }

            return string.Equals(SelectedMetric, MetricKey, StringComparison.Ordinal);
        }
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DashboardKpiCard card && card.IsLoaded)
        {
            card.Build();
        }
    }

    private void Build()
    {
        var active = IsActive;
        var muted = (!string.IsNullOrWhiteSpace(HoveredMetric) || !string.IsNullOrWhiteSpace(SelectedMetric)) && !active;
        Background = active ? CardSelectedBrush : CardBrush;
        Opacity = muted ? 0.64 : 1;
        Effect = new DropShadowEffect
        {
            BlurRadius = active ? 14 : 9,
            ShadowDepth = active ? 4 : 3,
            Opacity = active ? 0.18 : 0.10,
            Color = Color.FromRgb(37, 99, 235)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(active ? 6 : 4) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });

        var accent = new Border
        {
            Background = AccentBrush,
            CornerRadius = new CornerRadius(10, 0, 0, 10)
        };
        Grid.SetColumn(accent, 0);
        grid.Children.Add(accent);

        var iconCircle = new Border
        {
            Width = active ? 31 : 29,
            Height = active ? 31 : 29,
            CornerRadius = new CornerRadius(18),
            Background = AccentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new DropShadowEffect
            {
                BlurRadius = active ? 10 : 7,
                ShadowDepth = 3,
                Opacity = active ? 0.22 : 0.14,
                Color = Colors.Black
            },
            Child = CreateIcon()
        };
        Grid.SetColumn(iconCircle, 1);
        grid.Children.Add(iconCircle);

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        textStack.Children.Add(new TextBlock
        {
            Text = Title,
            FontSize = active ? 11.5 : 11,
            FontWeight = active ? FontWeights.Black : FontWeights.Bold,
            Foreground = TextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textStack.Children.Add(new TextBlock
        {
            Text = "••••••••",
            FontSize = 10,
            Foreground = AccentBrush,
            Opacity = active ? 0.95 : 0.68,
            Margin = new Thickness(0, -1, 0, 0)
        });
        Grid.SetColumn(textStack, 2);
        grid.Children.Add(textStack);

        var value = new TextBlock
        {
            Text = ValueText,
            FontSize = active ? 15.5 : 15,
            FontWeight = FontWeights.Black,
            Foreground = ValueBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };
        Grid.SetColumn(value, 3);
        grid.Children.Add(value);

        Child = grid;
    }

    private UIElement CreateIcon()
    {
        var viewbox = new Viewbox
        {
            Width = 18,
            Height = 18,
            Margin = new Thickness(7)
        };
        var canvas = new Canvas
        {
            Width = 32,
            Height = 32
        };

        if (IconKind == "Send")
        {
            canvas.Children.Add(new Path { Data = Geometry.Parse("M27 5 L5 15.2 L14 18.3 L17.2 27 Z"), Fill = WhiteBrush });
        }
        else if (IconKind == "Shield")
        {
            canvas.Children.Add(new Path { Data = Geometry.Parse("M16 4 L26 8 V15.5 C26 22 21.8 27 16 29 C10.2 27 6 22 6 15.5 V8 Z"), Fill = WhiteBrush });
            canvas.Children.Add(new Path
            {
                Data = Geometry.Parse("M11.5 16.2 L14.6 19.3 L21.2 12.3"),
                Stroke = AccentBrush,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            });
        }
        else if (IconKind == "Expired")
        {
            var rect = new Rectangle { Width = 16, Height = 18, RadiusX = 3, RadiusY = 3, Fill = WhiteBrush };
            Canvas.SetLeft(rect, 8);
            Canvas.SetTop(rect, 7);
            canvas.Children.Add(rect);
            canvas.Children.Add(new Path
            {
                Data = Geometry.Parse("M12.5 13.5 L19.5 20.5 M19.5 13.5 L12.5 20.5"),
                Stroke = AccentBrush,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }
        else
        {
            canvas.Children.Add(new Path { Data = Geometry.Parse("M16 4 V16 H28 C27.5 9.5 22.5 4.5 16 4 Z"), Fill = WhiteBrush });
            canvas.Children.Add(new Path { Data = Geometry.Parse("M14 6 A12 12 0 1 0 26 18 H14 Z"), Fill = WhiteBrush, Opacity = 0.58 });
        }

        viewbox.Child = canvas;
        return viewbox;
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
