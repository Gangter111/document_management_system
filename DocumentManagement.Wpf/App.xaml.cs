using System.IO;
using System.Windows;
using System.Windows.Threading;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Application.Security;
using DocumentManagement.Application.Services;
using DocumentManagement.Infrastructure.Data;
using DocumentManagement.Infrastructure.Repositories;
using DocumentManagement.Infrastructure.Services;
using DocumentManagement.Wpf.ViewModels;
using DocumentManagement.Wpf.Views;
using DocumentManagement.Wpf.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DocumentManagement.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private IConfiguration? _configuration;

    public ServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("Service provider chưa được khởi tạo.");

    protected override void OnStartup(StartupEventArgs e)
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        Log.Information("Ứng dụng khởi động");

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

            var dbInitializer = _serviceProvider.GetRequiredService<DatabaseInitializer>();
            dbInitializer.Initialize();

            // Quan trọng:
            // Không cho app tự tắt khi LoginWindow đóng.
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

            // Sau khi đã có MainWindow, quay về hành vi chuẩn:
            // app tắt khi MainWindow đóng.
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Lỗi khởi động");
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
        var appConfig = _configuration!.GetSection("AppConfig")
                                       .Get<AppConfig>() ?? new AppConfig();

        services.Configure<AppConfig>(_configuration.GetSection("AppConfig"));

        var dbPath = Path.Combine(AppContext.BaseDirectory, "database", appConfig.DatabaseName);
        var attachmentRoot = Path.Combine(AppContext.BaseDirectory, appConfig.AttachmentFolderName);

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        Directory.CreateDirectory(attachmentRoot);

        var connectionString = $"Data Source={dbPath}";

        // Core Infrastructure
        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddSingleton<DatabaseInitializer>(sp =>
            new DatabaseInitializer(sp.GetRequiredService<SqliteConnectionFactory>()));

        // Auth / Permission Core
        services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<IAuthService, AuthService>();

        services.AddSingleton<IFileStorageService>(new LocalFileStorageService(attachmentRoot));
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IReportService, ExcelReportService>();
        services.AddSingleton<IOcrService, PdfExtractionService>();
        // WPF Services
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IConfirmDialogService, ConfirmDialogService>();

        // Repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IHistoryRepository, HistoryRepository>();

        // Application Services
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DocumentListViewModel>();
        services.AddTransient<DocumentFormViewModel>();
        services.AddTransient<DocumentDetailViewModel>();
        services.AddTransient<DashboardViewModel>();

        // Views
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainWindow>();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Lỗi UI không xác định");
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
            Log.Fatal(ex, "Lỗi vĩnh viễn hệ thống");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Ứng dụng tắt");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}