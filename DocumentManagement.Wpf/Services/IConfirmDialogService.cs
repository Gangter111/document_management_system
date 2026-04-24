namespace DocumentManagement.Wpf.Services;

public enum ConfirmDialogType
{
    Info,
    Warning,
    Danger
}

public interface IConfirmDialogService
{
    bool Confirm(
        string message,
        string title = "Xác nhận",
        string confirmText = "Đồng ý",
        string cancelText = "Hủy",
        ConfirmDialogType type = ConfirmDialogType.Warning);
}