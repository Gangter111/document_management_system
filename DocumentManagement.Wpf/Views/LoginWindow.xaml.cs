using System.Windows;
using DocumentManagement.Application.Interfaces;

namespace DocumentManagement.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly IAuthService _authService;

    public LoginWindow(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        var username = UsernameTextBox.Text?.Trim() ?? "";
        var password = PasswordBox.Password ?? "";

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

        var result = await _authService.LoginAsync(username, password);

        if (result == null)
        {
            ErrorText.Text = "Sai tài khoản hoặc mật khẩu.";
            return;
        }

        DialogResult = true;
        Close();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}