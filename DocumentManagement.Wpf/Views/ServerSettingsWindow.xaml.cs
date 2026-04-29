using System.Windows;
using DocumentManagement.Wpf.Services;

namespace DocumentManagement.Wpf.Views;

public partial class ServerSettingsWindow : Window
{
    private readonly ClientSettingsService _settingsService;
    private readonly ApiService _apiService;

    public ServerSettingsWindow(
        ClientSettingsService settingsService,
        ApiService apiService)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _apiService = apiService;

        ServerUrlTextBox.Text = _settingsService.GetApiBaseUrl();
        StatusText.Text = $"Cấu hình lưu tại: {_settingsService.SettingsPath}";
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var oldBaseUrl = _apiService.BaseUrl;

        try
        {
            var baseUrl = ClientSettingsService.NormalizeBaseUrl(ServerUrlTextBox.Text);
            _apiService.SetBaseUrl(baseUrl);

            StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            StatusText.Text = "Đang kiểm tra kết nối...";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var ok = await _apiService.CheckHealthAsync(cts.Token);

            StatusText.Foreground = ok
                ? System.Windows.Media.Brushes.ForestGreen
                : System.Windows.Media.Brushes.DarkOrange;

            StatusText.Text = ok
                ? "Kết nối API thành công."
                : "API phản hồi không hợp lệ. Kiểm tra lại địa chỉ server.";
        }
        catch (Exception ex)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
            StatusText.Text = "Không kết nối được API: " + ex.Message;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(oldBaseUrl))
            {
                _apiService.SetBaseUrl(oldBaseUrl);
            }
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseUrl = ClientSettingsService.NormalizeBaseUrl(ServerUrlTextBox.Text);

            _settingsService.SaveApiBaseUrl(baseUrl);
            _apiService.SetBaseUrl(baseUrl);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.Firebrick;
            StatusText.Text = ex.Message;
        }
    }
}
