using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DocumentManagement.Wpf;
using DocumentManagement.Wpf.Controls;
using DocumentManagement.Wpf.Services;
using DocumentManagement.Wpf.ViewModels;
using DocumentManagement.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashboardScreenshotHarness;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var root = GetRepositoryRoot();
        var outputDirectory = Path.Combine(root, "artifacts", "screenshots");
        Directory.CreateDirectory(outputDirectory);

        var app = new App();
        app.InitializeComponent();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(root, "DocumentManagement.Wpf"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var apiUrl = args
            .FirstOrDefault(arg => arg.StartsWith("--api-url=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("--api-url=".Length)
            ?? "http://localhost:5033/";

        var settings = new ClientSettingsService(configuration);
        var api = new ApiService(settings);
        api.SetBaseUrl(apiUrl);

        if (args.Any(arg => string.Equals(arg, "login", StringComparison.OrdinalIgnoreCase)))
        {
            RenderLogin(app, api, outputDirectory);
            Environment.Exit(0);
            return;
        }

        RenderDashboard(app, api, outputDirectory, args);
        Environment.Exit(0);
    }

    private static void RenderDashboard(App app, ApiService api, string outputDirectory, IReadOnlyCollection<string> args)
    {
        var renderRadialTotalHover = args.Any(arg => string.Equals(arg, "radial-total-hover", StringComparison.OrdinalIgnoreCase));

        var viewModel = new DashboardViewModel(api);
        viewModel.LoadAsync().GetAwaiter().GetResult();

        var view = new DashboardView
        {
            DataContext = viewModel
        };

        if (renderRadialTotalHover)
        {
            view.HoveredChartMetric = RadialDocumentChart.TotalMetric;
        }

        var window = new Window
        {
            Width = 1366,
            Height = 850,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Content = view
        };

        window.Loaded += (_, _) =>
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1800)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.UpdateLayout();
                view.UpdateLayout();

                var bitmap = new RenderTargetBitmap(
                    (int)window.ActualWidth,
                    (int)window.ActualHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(window);

                if (renderRadialTotalHover)
                {
                    SavePng(bitmap, Path.Combine(outputDirectory, "dashboard-radial-total-hover-full.png"));
                    SavePng(new CroppedBitmap(bitmap, new Int32Rect(176, 74, 602, 372)), Path.Combine(outputDirectory, "dashboard-radial-total-hover.png"));
                }
                else
                {
                    SavePng(bitmap, Path.Combine(outputDirectory, "dashboard-final-full.png"));
                    SavePng(new CroppedBitmap(bitmap, new Int32Rect(786, 74, 558, 372)), Path.Combine(outputDirectory, "dashboard-department-panel.png"));
                }

                window.Close();
                app.Shutdown();
            };

            timer.Start();
        };

        app.Run(window);
    }

    private static void RenderLogin(App app, ApiService api, string outputDirectory)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var auth = new ApiAuthService(api);
        var window = new LoginWindow(auth, api, services)
        {
            ShowInTaskbar = false
        };

        window.Loaded += (_, _) =>
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.UpdateLayout();

                var bitmap = new RenderTargetBitmap(
                    (int)window.ActualWidth,
                    (int)window.ActualHeight,
                    96,
                    96,
                    PixelFormats.Pbgra32);
                bitmap.Render(window);

                SavePng(bitmap, Path.Combine(outputDirectory, "login-final.png"));

                window.Close();
                app.Shutdown();
            };

            timer.Start();
        };

        app.Run(window);
    }

    private static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "DocumentManagement.Wpf", "DocumentManagement.Wpf.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
