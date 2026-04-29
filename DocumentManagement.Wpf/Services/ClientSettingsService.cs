using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DocumentManagement.Wpf.Services;

public class ClientSettingsService
{
    private const string DefaultBaseUrl = "http://localhost:5033/";

    private readonly IConfiguration _configuration;
    private readonly string _settingsPath;

    public ClientSettingsService(IConfiguration configuration)
    {
        _configuration = configuration;

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var settingsDir = Path.Combine(programData, "PhuGiaGroup", "DocumentManagement");

        Directory.CreateDirectory(settingsDir);

        _settingsPath = Path.Combine(settingsDir, "clientsettings.json");
    }

    public string SettingsPath => _settingsPath;

    public string GetApiBaseUrl()
    {
        var saved = ReadSavedSettings();

        if (!string.IsNullOrWhiteSpace(saved?.Api?.BaseUrl))
        {
            return NormalizeBaseUrl(saved.Api.BaseUrl);
        }

        var configured = _configuration["Api:BaseUrl"];

        return NormalizeBaseUrl(string.IsNullOrWhiteSpace(configured)
            ? DefaultBaseUrl
            : configured);
    }

    public void SaveApiBaseUrl(string baseUrl)
    {
        var normalized = NormalizeBaseUrl(baseUrl);

        var settings = new ClientSettings
        {
            Api = new ApiClientSettings
            {
                BaseUrl = normalized
            }
        };

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsPath, json);
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Vui lòng nhập địa chỉ máy chủ API.");
        }

        var normalized = baseUrl.Trim();

        if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "http://" + normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Địa chỉ máy chủ API không hợp lệ.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Địa chỉ máy chủ API phải dùng http hoặc https.");
        }

        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        return normalized;
    }

    private ClientSettings? ReadSavedSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);

            return JsonSerializer.Deserialize<ClientSettings>(json);
        }
        catch
        {
            return null;
        }
    }
}

public class ClientSettings
{
    public ApiClientSettings Api { get; set; } = new();
}

public class ApiClientSettings
{
    public string BaseUrl { get; set; } = string.Empty;
}
