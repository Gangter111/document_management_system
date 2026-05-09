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
                IconBackground.Background = (Brush)FindResource("DenseDangerSoftBrush");
                IconText.Foreground = (Brush)FindResource("DenseDangerTextBrush");
                IconText.Text = "!";
                ConfirmButton.Style = (Style)FindResource("DenseFormDangerButtonStyle");
                break;

            case ConfirmDialogType.Info:
                IconBackground.Background = (Brush)FindResource("DenseInfoSoftBrush");
                IconText.Foreground = (Brush)FindResource("DenseInfoTextBrush");
                IconText.Text = "i";
                ConfirmButton.Style = (Style)FindResource("DenseDialogConfirmButtonStyle");
                break;

            default:
                IconBackground.Background = (Brush)FindResource("DenseWarningSoftBrush");
                IconText.Foreground = (Brush)FindResource("DenseWarningTextBrush");
                IconText.Text = "!";
                ConfirmButton.Style = (Style)FindResource("DenseDialogConfirmButtonStyle");
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
