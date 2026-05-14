using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DocumentManagement.Wpf.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.Views;

public partial class LoginWindow : Window
{
    private static readonly Brush ErrorBrush = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38)));
    private static readonly Brush SuccessBrush = Freeze(new SolidColorBrush(Color.FromRgb(22, 101, 52)));

    private readonly ApiAuthService _authService;
    private readonly ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private bool _isPasswordVisible;
    private bool _isSyncingPassword;
    private bool _isLoginInProgress;
    private bool _isRegisterInProgress;

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
        if (_isLoginInProgress)
        {
            return;
        }

        var loginButton = sender as Button;

        _isLoginInProgress = true;
        if (loginButton != null)
        {
            loginButton.IsEnabled = false;
        }

        ErrorText.Foreground = ErrorBrush;
        ErrorText.Text = string.Empty;

        try
        {
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
        finally
        {
            _isLoginInProgress = false;
            if (loginButton != null)
            {
                loginButton.IsEnabled = true;
            }
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

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRegisterInProgress)
        {
            return;
        }

        ErrorText.Foreground = ErrorBrush;
        ErrorText.Text = string.Empty;

        var dialog = CreateRegisterDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var fields = (RegisterDialogFields)dialog.Tag;

        try
        {
            var registerButton = sender as Button;

            _isRegisterInProgress = true;
            if (registerButton != null)
            {
                registerButton.IsEnabled = false;
            }

            var result = await _apiService.RegisterAsync(
                fields.Username.Text.Trim(),
                fields.Password.Password,
                fields.FullName.Text.Trim(),
                fields.Department.Text.Trim());

            if (!result.Success)
            {
                ErrorText.Text = result.Message;
                return;
            }

            UsernameTextBox.Text = result.Username;
            PasswordBox.Password = fields.Password.Password;
            VisiblePasswordTextBox.Text = fields.Password.Password;
            ErrorText.Foreground = SuccessBrush;
            ErrorText.Text = "Đăng ký thành công. Có thể đăng nhập bằng tài khoản vừa tạo.";
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Không thể đăng ký tài khoản qua API: " + ex.Message;
        }
        finally
        {
            _isRegisterInProgress = false;
            if (sender is Button registerButton)
            {
                registerButton.IsEnabled = true;
            }
        }
    }

    private void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Hệ thống hiện chưa cấu hình email đặt lại mật khẩu.\n\n" +
            "Cách xử lý an toàn: liên hệ Admin để đặt lại mật khẩu trong môi trường nội bộ, hoặc dùng tài khoản quản trị mặc định khi kiểm thử local: admin / admin123.",
            "Khôi phục mật khẩu",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private Window CreateRegisterDialog()
    {
        var username = new TextBox { Height = 34, Margin = new Thickness(0, 4, 0, 12) };
        var fullName = new TextBox { Height = 34, Margin = new Thickness(0, 4, 0, 12) };
        var department = new TextBox { Height = 34, Margin = new Thickness(0, 4, 0, 12), Text = "Phòng nghiệp vụ" };
        var password = new PasswordBox { Height = 34, Margin = new Thickness(0, 4, 0, 12) };
        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var form = new StackPanel { Margin = new Thickness(22, 20, 22, 18) };
        form.Children.Add(new TextBlock { Text = "Đăng ký tài khoản local", FontSize = 18, FontWeight = FontWeights.SemiBold });
        form.Children.Add(new TextBlock { Text = "Tài khoản mới được tạo trên API hiện tại với vai trò Staff.", Margin = new Thickness(0, 6, 0, 14), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        form.Children.Add(new TextBlock { Text = "Tên đăng nhập" });
        form.Children.Add(username);
        form.Children.Add(new TextBlock { Text = "Họ tên" });
        form.Children.Add(fullName);
        form.Children.Add(new TextBlock { Text = "Phòng ban" });
        form.Children.Add(department);
        form.Children.Add(new TextBlock { Text = "Mật khẩu" });
        form.Children.Add(password);
        form.Children.Add(error);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancel = new Button { Content = "Hủy", Width = 92, Height = 32, Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
        var submit = new Button { Content = "Đăng ký", Width = 110, Height = 32, IsDefault = true };
        actions.Children.Add(cancel);
        actions.Children.Add(submit);
        form.Children.Add(actions);

        var dialog = new Window
        {
            Owner = this,
            Title = "Đăng ký tài khoản",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = form,
            Tag = new RegisterDialogFields(username, fullName, department, password)
        };

        submit.Click += (_, _) =>
        {
            error.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(username.Text))
            {
                error.Text = "Vui lòng nhập tên đăng nhập.";
                return;
            }

            if (string.IsNullOrWhiteSpace(password.Password) || password.Password.Length < 6)
            {
                error.Text = "Mật khẩu phải có ít nhất 6 ký tự.";
                return;
            }

            dialog.DialogResult = true;
        };

        return dialog;
    }

    private void RefreshServerUrlText()
    {
        ServerUrlText.Text = string.IsNullOrWhiteSpace(_apiService.BaseUrl)
            ? "Chưa cấu hình máy chủ"
            : _apiService.BaseUrl;
    }

    private sealed record RegisterDialogFields(
        TextBox Username,
        TextBox FullName,
        TextBox Department,
        PasswordBox Password);

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        if (freezable.CanFreeze)
        {
            freezable.Freeze();
        }

        return freezable;
    }
}
