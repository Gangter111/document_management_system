using DocumentManagement.Contracts.Auth;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Dashboard;
using DocumentManagement.Contracts.Documents;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DocumentManagement.Wpf.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(IConfiguration configuration)
    {
        var baseUrl = configuration["Api:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Thiếu cấu hình Api:BaseUrl trong appsettings.json.");
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public void SetToken(string? token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;

        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public void SetCurrentRole(string? role)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-User-Role");

        if (!string.IsNullOrWhiteSpace(role))
        {
            _httpClient.DefaultRequestHeaders.Add("X-User-Role", role);
        }
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var request = new LoginRequest
        {
            Username = username,
            Password = password
        };

        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Tên đăng nhập hoặc mật khẩu không đúng."
            };
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var badRequest = await response.Content.ReadFromJsonAsync<LoginResponse>();

            return badRequest ?? new LoginResponse
            {
                Success = false,
                Message = "Dữ liệu đăng nhập không hợp lệ."
            };
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

        return result ?? new LoginResponse
        {
            Success = false,
            Message = "API không trả về dữ liệu đăng nhập."
        };
    }

    public async Task<bool> ChangePasswordAsync(long userId, string newPassword)
    {
        var request = new ChangePasswordRequest
        {
            UserId = userId,
            NewPassword = newPassword
        };

        var response = await _httpClient.PostAsJsonAsync("api/auth/change-password", request);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard");

        return result ?? new DashboardDto();
    }

    public async Task<List<LookupItemDto>> GetCategoriesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<LookupItemDto>>("api/lookups/categories");

        return result ?? new List<LookupItemDto>();
    }

    public async Task<List<LookupItemDto>> GetStatusesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<LookupItemDto>>("api/lookups/statuses");

        return result ?? new List<LookupItemDto>();
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<DocumentDto>>("api/documents");

        return result ?? new List<DocumentDto>();
    }

    public async Task<PagedResultDto<DocumentDto>> SearchDocumentsAsync(
        string? keyword,
        long? categoryId,
        long? statusId,
        string? urgency,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize)
    {
        var query = new List<string>
        {
            $"pageNumber={pageNumber}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query.Add($"categoryId={categoryId.Value}");
        }

        if (statusId.HasValue && statusId.Value > 0)
        {
            query.Add($"statusId={statusId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(urgency))
        {
            query.Add($"urgency={Uri.EscapeDataString(urgency)}");
        }

        if (fromDate.HasValue)
        {
            query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        }

        if (toDate.HasValue)
        {
            query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        }

        var url = "api/documents/search?" + string.Join("&", query);

        var result = await _httpClient.GetFromJsonAsync<PagedResultDto<DocumentDto>>(url);

        return result ?? new PagedResultDto<DocumentDto>();
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(long id)
    {
        return await _httpClient.GetFromJsonAsync<DocumentDto>($"api/documents/{id}");
    }

    public async Task<long> CreateDocumentAsync(CreateDocumentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/documents", request);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<long>();
    }

    public async Task UpdateDocumentAsync(long id, UpdateDocumentRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/documents/{id}", request);

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDocumentAsync(long id)
    {
        var response = await _httpClient.DeleteAsync($"api/documents/{id}");

        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadBackupAsync()
    {
        var response = await _httpClient.GetAsync("api/backup/download");

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền sao lưu dữ liệu.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task RestoreBackupAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Đường dẫn file khôi phục không hợp lệ.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Không tìm thấy file khôi phục.", filePath);
        }

        await using var stream = File.OpenRead(filePath);

        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync("api/backup/restore", content);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("Chỉ Admin được khôi phục dữ liệu.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(message);
        }

        response.EnsureSuccessStatusCode();
    }
}