using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly IConfirmDialogService _confirmDialogService;

    private Window? _activeToastWindow;

    public MainWindow(
        MainViewModel viewModel,
        IAuthService authService,
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _authService = authService;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;

        DataContext = _viewModel;

        _notificationService.NotificationRequested += NotificationService_NotificationRequested;
        Closed += MainWindow_Closed;
    }

    private void DashboardButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowDashboard();
    }

    private void DocumentsButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ShowDocuments();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = _confirmDialogService.Confirm(
            "Bạn muốn đăng xuất khỏi hệ thống?",
            "Xác nhận đăng xuất",
            "Đăng xuất",
            "Hủy",
            ConfirmDialogType.Warning);

        if (!confirmed)
        {
            return;
        }

        try
        {
            _authService.Logout();

            Hide();

            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            var loginResult = loginWindow.ShowDialog();

            if (loginResult == true)
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                global::System.Windows.Application.Current!.MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                global::System.Windows.Application.Current?.Shutdown();
            }

            Close();
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(
                $"Lỗi khi đăng xuất: {ex.Message}",
                "Lỗi đăng xuất");

            Show();
        }
    }

    private void NotificationService_NotificationRequested(object? sender, NotificationMessage notification)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ShowToast(notification));
            return;
        }

        ShowToast(notification);
    }

    private void ShowToast(NotificationMessage notification)
    {
        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        _activeToastWindow?.Close();
        _activeToastWindow = null;

        var accentBrush = GetAccentBrush(notification.Type);
        var backgroundBrush = GetBackgroundBrush(notification.Type);
        var titleBrush = GetTitleBrush(notification.Type);

        var rootBorder = new Border
        {
            Width = 380,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(14),
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 3,
                Opacity = 0.16
            }
        };

        var rootGrid = new Grid();
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var accentBar = new Border
        {
            Background = accentBrush,
            CornerRadius = new CornerRadius(14, 0, 0, 14)
        };

        Grid.SetColumn(accentBar, 0);

        var contentBorder = new Border
        {
            Background = backgroundBrush,
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(0, 14, 14, 0)
        };

        Grid.SetColumn(contentBorder, 1);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textPanel = new StackPanel();

        var titleText = new TextBlock
        {
            Text = notification.Title,
            Foreground = titleBrush,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        };

        var messageText = new TextBlock
        {
            Text = notification.Message,
            Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
            FontSize = 13,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        textPanel.Children.Add(titleText);
        textPanel.Children.Add(messageText);

        var closeButton = new Button
        {
            Content = "×",
            Width = 28,
            Height = 28,
            Margin = new Thickness(12, -4, -6, 0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Top
        };

        Grid.SetColumn(textPanel, 0);
        Grid.SetColumn(closeButton, 1);

        contentGrid.Children.Add(textPanel);
        contentGrid.Children.Add(closeButton);

        contentBorder.Child = contentGrid;

        rootGrid.Children.Add(accentBar);
        rootGrid.Children.Add(contentBorder);

        rootBorder.Child = rootGrid;

        var toastWindow = new Window
        {
            Owner = this,
            Content = rootBorder,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true
        };

        closeButton.Click += (_, _) => toastWindow.Close();

        toastWindow.Loaded += (_, _) =>
        {
            PositionToast(toastWindow);
        };

        toastWindow.Closed += (_, _) =>
        {
            if (_activeToastWindow == toastWindow)
            {
                _activeToastWindow = null;
            }
        };

        _activeToastWindow = toastWindow;
        toastWindow.Show();

        var timer = new DispatcherTimer
        {
            Interval = notification.Duration
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();

            if (toastWindow.IsVisible)
            {
                toastWindow.Close();
            }
        };

        timer.Start();
    }

    private void PositionToast(Window toastWindow)
    {
        var ownerLeft = Left;
        var ownerTop = Top;
        var ownerWidth = ActualWidth;

        if (double.IsNaN(ownerLeft) || double.IsInfinity(ownerLeft))
        {
            ownerLeft = 0;
        }

        if (double.IsNaN(ownerTop) || double.IsInfinity(ownerTop))
        {
            ownerTop = 0;
        }

        if (ownerWidth <= 0)
        {
            ownerWidth = Width;
        }

        toastWindow.Left = ownerLeft + ownerWidth - toastWindow.Width - 32;
        toastWindow.Top = ownerTop + 92;
    }

    private static SolidColorBrush GetAccentBrush(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            NotificationType.Warning => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
            NotificationType.Error => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            _ => new SolidColorBrush(Color.FromRgb(37, 99, 235))
        };
    }

    private static SolidColorBrush GetBackgroundBrush(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => new SolidColorBrush(Color.FromRgb(240, 253, 244)),
            NotificationType.Warning => new SolidColorBrush(Color.FromRgb(255, 251, 235)),
            NotificationType.Error => new SolidColorBrush(Color.FromRgb(254, 242, 242)),
            _ => new SolidColorBrush(Color.FromRgb(239, 246, 255))
        };
    }

    private static SolidColorBrush GetTitleBrush(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => new SolidColorBrush(Color.FromRgb(22, 101, 52)),
            NotificationType.Warning => new SolidColorBrush(Color.FromRgb(146, 64, 14)),
            NotificationType.Error => new SolidColorBrush(Color.FromRgb(153, 27, 27)),
            _ => new SolidColorBrush(Color.FromRgb(30, 64, 175))
        };
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _notificationService.NotificationRequested -= NotificationService_NotificationRequested;

        if (_activeToastWindow != null)
        {
            _activeToastWindow.Close();
            _activeToastWindow = null;
        }
    }
}