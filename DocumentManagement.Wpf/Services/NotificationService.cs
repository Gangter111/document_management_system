namespace DocumentManagement.Wpf.Services;

public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    public event EventHandler<NotificationMessage>? NotificationRequested;

    public void ShowInfo(string message, string title = "Thông tin")
    {
        Show(title, message, NotificationType.Info);
    }

    public void ShowSuccess(string message, string title = "Thành công")
    {
        Show(title, message, NotificationType.Success);
    }

    public void ShowWarning(string message, string title = "Cảnh báo")
    {
        Show(title, message, NotificationType.Warning);
    }

    public void ShowError(string message, string title = "Lỗi")
    {
        Show(title, message, NotificationType.Error);
    }

    private void Show(string title, string message, NotificationType type)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var notification = new NotificationMessage(
            title,
            message,
            type,
            DefaultDuration);

        var app = global::System.Windows.Application.Current;

        if (app?.Dispatcher == null || app.Dispatcher.CheckAccess())
        {
            NotificationRequested?.Invoke(this, notification);
            return;
        }

        app.Dispatcher.Invoke(() =>
        {
            NotificationRequested?.Invoke(this, notification);
        });
    }
}