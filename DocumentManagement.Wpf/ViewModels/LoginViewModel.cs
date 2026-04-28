using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using DocumentManagement.Wpf.Commands;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly ApiAuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly IConfirmDialogService _confirmDialogService;

    private string _username = "admin";
    private string _password = "admin";
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(
        ApiAuthService authService,
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        IConfirmDialogService confirmDialogService)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _confirmDialogService = confirmDialogService;

        LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsBusy);
    }

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoginCommand { get; }

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Tên đăng nhập không được để trống.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Mật khẩu không được để trống.";
            return;
        }

        try
        {
            IsBusy = true;

            var result = await _authService.LoginAsync(Username, Password);

            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Token))
            {
                ErrorMessage = result?.Message ?? "Đăng nhập thất bại.";
                return;
            }

            AuthSession.Token = result.Token;
            AuthSession.UserId = result.UserId;
            AuthSession.Username = result.Username;
            AuthSession.FullName = result.FullName;
            AuthSession.Role = result.Role;

            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            var mainWindow = new MainWindow(
                mainViewModel,
                _serviceProvider,
                _notificationService,
                _confirmDialogService);

            mainWindow.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is LoginWindow)
                {
                    window.Close();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không kết nối được API: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}