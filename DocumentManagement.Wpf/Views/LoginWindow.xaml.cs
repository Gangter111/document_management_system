using System.Windows;
using System.Windows.Input;
using DocumentManagement.Wpf.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly ApiAuthService _authService;
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private bool _isPasswordVisible;
    private bool _isSyncingPassword;

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
        var password = GetCurrentPassword();

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
        if (!_isSyncingPassword)
        {
            _isSyncingPassword = true;
            VisiblePasswordTextBox.Text = PasswordBox.Password;
            _isSyncingPassword = false;
        }

        UpdatePasswordPlaceholder();
    }

    private void VisiblePasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_isSyncingPassword)
        {
            _isSyncingPassword = true;
            PasswordBox.Password = VisiblePasswordTextBox.Text;
            _isSyncingPassword = false;
        }

        UpdatePasswordPlaceholder();
    }

    private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        if (_isPasswordVisible)
        {
            VisiblePasswordTextBox.Text = PasswordBox.Password;
            VisiblePasswordTextBox.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            TogglePasswordIcon.Kind = PackIconKind.EyeOffOutline;
            VisiblePasswordTextBox.Focus();
            VisiblePasswordTextBox.CaretIndex = VisiblePasswordTextBox.Text.Length;
        }
        else
        {
            PasswordBox.Password = VisiblePasswordTextBox.Text;
            PasswordBox.Visibility = Visibility.Visible;
            VisiblePasswordTextBox.Visibility = Visibility.Collapsed;
            TogglePasswordIcon.Kind = PackIconKind.EyeOutline;
            PasswordBox.Focus();
        }

        UpdatePasswordPlaceholder();
    }

    private void TitleDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private string GetCurrentPassword()
    {
        return _isPasswordVisible
            ? VisiblePasswordTextBox.Text ?? string.Empty
            : PasswordBox.Password ?? string.Empty;
    }

    private void UpdatePasswordPlaceholder()
    {
        PasswordPlaceholder.Visibility = string.IsNullOrEmpty(GetCurrentPassword())
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
