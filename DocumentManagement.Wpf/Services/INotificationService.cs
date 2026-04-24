namespace DocumentManagement.Wpf.Services;

public interface INotificationService
{
    event EventHandler<NotificationMessage>? NotificationRequested;

    void ShowInfo(string message, string title = "Thông tin");

    void ShowSuccess(string message, string title = "Thành công");

    void ShowWarning(string message, string title = "Cảnh báo");

    void ShowError(string message, string title = "Lỗi");
}