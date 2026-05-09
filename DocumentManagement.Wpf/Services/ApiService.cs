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
    private readonly ClientSettingsService _settingsService;

    public ApiService(ClientSettingsService settingsService)
    {
        _settingsService = settingsService;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        SetBaseUrl(_settingsService.GetApiBaseUrl());
    }

    public string BaseUrl => _httpClient.BaseAddress?.ToString() ?? string.Empty;

    public void SetBaseUrl(string baseUrl)
    {
        var normalized = ClientSettingsService.NormalizeBaseUrl(baseUrl);
        _httpClient.BaseAddress = new Uri(normalized);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("health", cancellationToken);

        return response.IsSuccessStatusCode;
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

    private static async Task EnsureSuccessWithMessageAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await response.Content.ReadAsStringAsync();
        message = string.IsNullOrWhiteSpace(message)
            ? $"API trả về lỗi {(int)response.StatusCode}."
            : message.Trim('"');

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException(message);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException(message);
        }

        response.EnsureSuccessStatusCode();
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

    public async Task ArchiveDocumentAsync(long id)
    {
        var response = await _httpClient.PostAsync($"api/documents/{id}/archive", null);

        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task RestoreArchivedDocumentAsync(long id)
    {
        var response = await _httpClient.PostAsync($"api/documents/{id}/restore-from-archive", null);

        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<DocumentManagement.Contracts.Reports.ReportSummaryDto> GetReportSummaryAsync()
    {
        var response = await _httpClient.GetAsync("api/reports/summary");
        await EnsureSuccessWithMessageAsync(response);
        var result = await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.Reports.ReportSummaryDto>();

        return result ?? new DocumentManagement.Contracts.Reports.ReportSummaryDto();
    }

    public async Task<List<DocumentManagement.Contracts.Catalog.CatalogItemDto>> GetCatalogCategoriesAsync(bool includeInactive = true)
    {
        var response = await _httpClient.GetAsync(
            $"api/catalog/categories?includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        await EnsureSuccessWithMessageAsync(response);
        var result = await response.Content.ReadFromJsonAsync<List<DocumentManagement.Contracts.Catalog.CatalogItemDto>>();

        return result ?? new List<DocumentManagement.Contracts.Catalog.CatalogItemDto>();
    }

    public async Task<List<DocumentManagement.Contracts.Catalog.CatalogItemDto>> GetCatalogStatusesAsync(bool includeInactive = true)
    {
        var response = await _httpClient.GetAsync(
            $"api/catalog/statuses?includeInactive={includeInactive.ToString().ToLowerInvariant()}");
        await EnsureSuccessWithMessageAsync(response);
        var result = await response.Content.ReadFromJsonAsync<List<DocumentManagement.Contracts.Catalog.CatalogItemDto>>();

        return result ?? new List<DocumentManagement.Contracts.Catalog.CatalogItemDto>();
    }

    public async Task<DocumentManagement.Contracts.Catalog.CatalogItemDto> CreateCategoryAsync(
        DocumentManagement.Contracts.Catalog.SaveCatalogItemRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalog/categories", request);
        await EnsureSuccessWithMessageAsync(response);

        return await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.Catalog.CatalogItemDto>()
               ?? new DocumentManagement.Contracts.Catalog.CatalogItemDto();
    }

    public async Task<DocumentManagement.Contracts.Catalog.CatalogItemDto> UpdateCategoryAsync(
        long id,
        DocumentManagement.Contracts.Catalog.SaveCatalogItemRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalog/categories/{id}", request);
        await EnsureSuccessWithMessageAsync(response);

        return await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.Catalog.CatalogItemDto>()
               ?? new DocumentManagement.Contracts.Catalog.CatalogItemDto();
    }

    public async Task DeleteCategoryAsync(long id)
    {
        var response = await _httpClient.DeleteAsync($"api/catalog/categories/{id}");
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<DocumentManagement.Contracts.Catalog.CatalogItemDto> CreateStatusAsync(
        DocumentManagement.Contracts.Catalog.SaveCatalogItemRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/catalog/statuses", request);
        await EnsureSuccessWithMessageAsync(response);

        return await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.Catalog.CatalogItemDto>()
               ?? new DocumentManagement.Contracts.Catalog.CatalogItemDto();
    }

    public async Task<DocumentManagement.Contracts.Catalog.CatalogItemDto> UpdateStatusAsync(
        long id,
        DocumentManagement.Contracts.Catalog.SaveCatalogItemRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/catalog/statuses/{id}", request);
        await EnsureSuccessWithMessageAsync(response);

        return await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.Catalog.CatalogItemDto>()
               ?? new DocumentManagement.Contracts.Catalog.CatalogItemDto();
    }

    public async Task DeleteStatusAsync(long id)
    {
        var response = await _httpClient.DeleteAsync($"api/catalog/statuses/{id}");
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<List<DocumentManagement.Contracts.System.UserAdminDto>> SearchUsersAsync(
        string? keyword,
        bool includeInactive = true)
    {
        var query = $"includeInactive={includeInactive.ToString().ToLowerInvariant()}";

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query += $"&keyword={Uri.EscapeDataString(keyword)}";
        }

        var response = await _httpClient.GetAsync($"api/system/users?{query}");
        await EnsureSuccessWithMessageAsync(response);
        var result = await response.Content.ReadFromJsonAsync<List<DocumentManagement.Contracts.System.UserAdminDto>>();

        return result ?? new List<DocumentManagement.Contracts.System.UserAdminDto>();
    }

    public async Task<List<DocumentManagement.Contracts.System.RoleDto>> GetRolesAsync()
    {
        var response = await _httpClient.GetAsync("api/system/roles");
        await EnsureSuccessWithMessageAsync(response);
        var result = await response.Content.ReadFromJsonAsync<List<DocumentManagement.Contracts.System.RoleDto>>();

        return result ?? new List<DocumentManagement.Contracts.System.RoleDto>();
    }

    public async Task<DocumentManagement.Contracts.System.UserAdminDto> CreateUserAsync(
        DocumentManagement.Contracts.System.SaveUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/system/users", request);
        await EnsureSuccessWithMessageAsync(response);

        return await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.System.UserAdminDto>()
               ?? new DocumentManagement.Contracts.System.UserAdminDto();
    }

    public async Task<DocumentManagement.Contracts.System.UserAdminDto> UpdateUserAsync(
        long id,
        DocumentManagement.Contracts.System.SaveUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/system/users/{id}", request);
        await EnsureSuccessWithMessageAsync(response);

        return await response.Content.ReadFromJsonAsync<DocumentManagement.Contracts.System.UserAdminDto>()
               ?? new DocumentManagement.Contracts.System.UserAdminDto();
    }

    public async Task DeleteUserAsync(long id)
    {
        var response = await _httpClient.DeleteAsync($"api/system/users/{id}");
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<AutoFillDocumentResultDto> ExtractPdfAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Đường dẫn file PDF không hợp lệ.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Không tìm thấy file PDF.", filePath);
        }

        await using var stream = File.OpenRead(filePath);

        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync("api/documents/extract-pdf", content);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền trích xuất thông tin từ PDF.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(message);
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AutoFillDocumentResultDto>()
               ?? new AutoFillDocumentResultDto();
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
