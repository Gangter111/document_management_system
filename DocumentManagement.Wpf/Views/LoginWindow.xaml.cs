using System.Windows;
using DocumentManagement.Wpf.Services;

namespace DocumentManagement.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly ApiAuthService _authService;

    public LoginWindow(ApiAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        var username = UsernameTextBox.Text?.Trim() ?? string.Empty;
        var password = PasswordBox.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorText.Text = "Vui lòng nhập tên đăng nhập.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "Vui lòng nhập mật khẩu.";
            return;
        }

        try
        {
            var result = await _authService.LoginAsync(username, password);

            if (result == null)
            {
                ErrorText.Text = "Sai tài khoản hoặc mật khẩu.";
                return;
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Không thể đăng nhập qua API: " + ex.Message;
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}