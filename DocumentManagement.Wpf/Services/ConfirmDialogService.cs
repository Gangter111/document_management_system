using DocumentManagement.Wpf.Views;

namespace DocumentManagement.Wpf.Services;

public sealed class ConfirmDialogService : IConfirmDialogService
{
    public bool Confirm(
        string message,
        string title = "Xác nhận",
        string confirmText = "Đồng ý",
        string cancelText = "Hủy",
        ConfirmDialogType type = ConfirmDialogType.Warning)
    {
        var app = global::System.Windows.Application.Current;

        if (app?.Dispatcher == null || app.Dispatcher.CheckAccess())
        {
            return ShowDialog(message, title, confirmText, cancelText, type);
        }

        return app.Dispatcher.Invoke(() =>
            ShowDialog(message, title, confirmText, cancelText, type));
    }

    private static bool ShowDialog(
        string message,
        string title,
        string confirmText,
        string cancelText,
        ConfirmDialogType type)
    {
        var owner = global::System.Windows.Application.Current?.MainWindow;

        var dialog = new ConfirmDialogWindow(
            title,
            message,
            confirmText,
            cancelText,
            type);

        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }
}