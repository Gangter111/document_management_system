using System.Windows;
using System.Windows.Media;
using DocumentManagement.Wpf.Services;

namespace DocumentManagement.Wpf.Views;

public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow(
        string title,
        string message,
        string confirmText,
        string cancelText,
        ConfirmDialogType type)
    {
        InitializeComponent();

        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        ApplyDialogType(type);
    }

    private void ApplyDialogType(ConfirmDialogType type)
    {
        switch (type)
        {
            case ConfirmDialogType.Danger:
                IconBackground.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(153, 27, 27));
                IconText.Text = "!";
                ConfirmButton.Style = (Style)FindResource("DangerButtonStyle");
                break;

            case ConfirmDialogType.Info:
                IconBackground.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175));
                IconText.Text = "i";
                ConfirmButton.Style = (Style)FindResource("PrimaryButtonStyle");
                break;

            default:
                IconBackground.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14));
                IconText.Text = "!";
                ConfirmButton.Style = (Style)FindResource("PrimaryButtonStyle");
                break;
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}