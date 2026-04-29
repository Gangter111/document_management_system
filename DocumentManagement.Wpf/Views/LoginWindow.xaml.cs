using System.Windows;
using DocumentManagement.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly ApiAuthService _authService;
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;

    public LoginWindow(
        ApiAuthService authService,
        ApiService apiService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _authService = authService;
        _apiService = apiService;
        _serviceProvider = serviceProvider;

        RefreshServerUrlText();
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

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordBox.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ServerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var serverSettingsWindow = _serviceProvider.GetRequiredService<ServerSettingsWindow>();
        serverSettingsWindow.Owner = this;
        serverSettingsWindow.ShowDialog();

        RefreshServerUrlText();
    }

    private void RefreshServerUrlText()
    {
        ServerUrlText.Text = string.IsNullOrWhiteSpace(_apiService.BaseUrl)
            ? "Chưa cấu hình máy chủ"
            : _apiService.BaseUrl;
    }
}
