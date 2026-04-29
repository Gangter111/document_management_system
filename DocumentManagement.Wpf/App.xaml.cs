using System.IO;
using System.Windows;
using System.Windows.Threading;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.ViewModels;
using DocumentManagement.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentManagement.Wpf;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;

    public ServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Service provider chưa được khởi tạo.");

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            base.OnStartup(e);

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();

            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            var loginResult = loginWindow.ShowDialog();

            if (loginResult != true)
            {
                Shutdown();
                return;
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Lỗi khởi động hệ thống: {ex.Message}",
                "Lỗi hệ thống",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        if (_configuration == null)
        {
            throw new InvalidOperationException("Configuration chưa được khởi tạo.");
        }

        /*
         * CLIENT-SERVER RULE:
         * WPF không được tự tạo SQLite DB.
         * WPF không được đăng ký Repository.
         * WPF không được chạy bước khởi tạo database local.
         * WPF không được chứa business logic backend.
         * WPF chỉ gọi API qua ApiService.
         */

        services.AddSingleton<IConfiguration>(_configuration);

        services.AddSingleton<ClientSettingsService>();
        services.AddSingleton<ApiService>();
        services.AddSingleton<ApiAuthService>();
        services.AddSingleton<ClientPermissionService>();

        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IConfirmDialogService, ConfirmDialogService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<DocumentListViewModel>();
        services.AddTransient<DocumentFormViewModel>();
        services.AddTransient<DocumentDetailViewModel>();
        services.AddTransient<DashboardViewModel>();

        services.AddTransient<LoginWindow>();
        services.AddTransient<ServerSettingsWindow>();
        services.AddTransient<MainWindow>();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Lỗi UI: {e.Exception.Message}",
            "Lỗi không mong muốn",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "fatal.log");
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex}\n");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
