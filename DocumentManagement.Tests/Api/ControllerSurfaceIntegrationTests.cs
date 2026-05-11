using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentManagement.Contracts.AuditLogs;
using DocumentManagement.Contracts.Auth;
using DocumentManagement.Contracts.Catalog;
using DocumentManagement.Contracts.Common;
using DocumentManagement.Contracts.Dashboard;
using DocumentManagement.Contracts.Documents;
using DocumentManagement.Contracts.Reports;
using DocumentManagement.Contracts.System;
using Xunit;

namespace DocumentManagement.Tests.Api;

public sealed class ControllerSurfaceIntegrationTests : IClassFixture<DocumentApiFactory>
{
    private readonly DocumentApiFactory _factory;

    public ControllerSurfaceIntegrationTests(DocumentApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthController_ShouldLoginAndChangeOwnPassword()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var staffRole = await GetRoleAsync(client, "STAFF");
        var username = UniqueName("auth_user");
        var createUserResponse = await client.PostAsJsonAsync(
            "/api/system/users",
            new SaveUserRequest
            {
                Username = username,
                FullName = "Auth Integration User",
                Department = "QA",
                RoleId = staffRole.Id,
                Password = "Password123",
                IsActive = true
            });

        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        var user = await createUserResponse.Content.ReadFromJsonAsync<UserAdminDto>();
        Assert.NotNull(user);

        var userLogin = await LoginResponseAsync(client, username, "Password123", "STAFF");
        SetBearerToken(client, userLogin.Token);

        var changePasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest
            {
                UserId = user.Id,
                OldPassword = "Password123",
                NewPassword = "NewPassword123"
            });

        Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);

        var oldPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = username,
                Password = "Password123"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);
        await LoginAsync(client, username, "NewPassword123", "STAFF");
    }

    [Fact]
    public async Task DocumentsController_ShouldExerciseCrudArchiveSearchAndExtractValidation()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var stamp = UniqueName("DOC");
        var createResponse = await client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest
            {
                DocumentNumber = stamp,
                Title = $"Document controller integration {stamp}",
                Summary = "Created by controller surface test",
                ProcessingDepartment = "QA",
                StatusId = 4
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var documentId = await createResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(documentId > 0);

        var getResponse = await client.GetAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var document = await getResponse.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(document);
        Assert.Equal(stamp, document.DocumentNumber);

        var listResponse = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var documents = await listResponse.Content.ReadFromJsonAsync<List<DocumentDto>>();
        Assert.Contains(documents!, x => x.Id == documentId);

        var searchResponse = await client.GetAsync($"/api/documents/search?keyword={Uri.EscapeDataString(stamp)}&pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var search = await searchResponse.Content.ReadFromJsonAsync<PagedResultDto<DocumentDto>>();
        Assert.NotNull(search);
        Assert.Contains(search.Items, x => x.Id == documentId);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}",
            new UpdateDocumentRequest
            {
                Id = documentId,
                DocumentNumber = stamp,
                Title = $"Updated {stamp}",
                Summary = "Updated by controller surface test",
                ProcessingDepartment = "QA",
                StatusId = 4,
                ConfidentialityLevel = "NORMAL",
                UrgencyLevel = "NORMAL",
                OcrStatus = "PENDING"
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var archiveResponse = await client.PostAsync($"/api/documents/{documentId}/archive", content: null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var restoreArchiveResponse = await client.PostAsync($"/api/documents/{documentId}/restore-from-archive", content: null);
        Assert.Equal(HttpStatusCode.NoContent, restoreArchiveResponse.StatusCode);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("not a pdf"u8.ToArray()), "file", "not-a-pdf.txt");
        var extractResponse = await client.PostAsync("/api/documents/extract-pdf", form);
        Assert.Equal(HttpStatusCode.BadRequest, extractResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DocumentsSearch_ShouldScopeTotalCountForNonAdmin()
    {
        using var client = _factory.CreateClient();
        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var stamp = UniqueName("RBACCOUNT");
        var readableResponse = await client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest
            {
                DocumentNumber = $"{stamp}-READ",
                Title = $"Readable {stamp}",
                ProcessingDepartment = "Finance",
                AssignedTo = "staff",
                StatusId = 4
            });
        Assert.Equal(HttpStatusCode.Created, readableResponse.StatusCode);

        var hiddenResponse = await client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest
            {
                DocumentNumber = $"{stamp}-HIDDEN",
                Title = $"Hidden {stamp}",
                ProcessingDepartment = "Finance",
                StatusId = 4
            });
        Assert.Equal(HttpStatusCode.Created, hiddenResponse.StatusCode);

        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");
        SetBearerToken(client, staffToken);

        var searchResponse = await client.GetAsync(
            $"/api/documents/search?keyword={Uri.EscapeDataString(stamp)}&pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var result = await searchResponse.Content.ReadFromJsonAsync<PagedResultDto<DocumentDto>>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Contains(result.Items, x => x.DocumentNumber == $"{stamp}-READ");
        Assert.DoesNotContain(result.Items, x => x.DocumentNumber == $"{stamp}-HIDDEN");
    }

    [Fact]
    public async Task BackupController_ShouldExposeAdminHealthAndRejectInvalidRestoreUpload()
    {
        using var client = _factory.CreateClient();

        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");
        SetBearerToken(client, staffToken);
        var forbiddenHealth = await client.GetAsync("/api/backup/health");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenHealth.StatusCode);

        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var healthResponse = await client.GetAsync("/api/backup/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("not sqlite"u8.ToArray()), "file", "backup.zip");

        var restoreResponse = await client.PostAsync("/api/backup/restore", form);
        Assert.Equal(HttpStatusCode.BadRequest, restoreResponse.StatusCode);
    }

    [Fact]
    public async Task CatalogController_ShouldReadAndManageCategoriesAndStatuses()
    {
        using var client = _factory.CreateClient();

        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");
        SetBearerToken(client, staffToken);

        var categoriesRead = await client.GetAsync("/api/catalog/categories");
        Assert.Equal(HttpStatusCode.OK, categoriesRead.StatusCode);
        var categories = await categoriesRead.Content.ReadFromJsonAsync<List<CatalogItemDto>>();
        Assert.NotEmpty(categories!);

        var staffCreate = await client.PostAsJsonAsync(
            "/api/catalog/categories",
            new SaveCatalogItemRequest { Name = UniqueName("blocked_category") });
        Assert.Equal(HttpStatusCode.Forbidden, staffCreate.StatusCode);

        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var categoryName = UniqueName("Category");
        var createCategory = await client.PostAsJsonAsync(
            "/api/catalog/categories",
            new SaveCatalogItemRequest { Name = categoryName, IsActive = true });
        Assert.Equal(HttpStatusCode.Created, createCategory.StatusCode);
        var category = await createCategory.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.NotNull(category);

        var updateCategory = await client.PutAsJsonAsync(
            $"/api/catalog/categories/{category.Id}",
            new SaveCatalogItemRequest { Name = $"{categoryName} Updated", IsActive = true });
        Assert.Equal(HttpStatusCode.OK, updateCategory.StatusCode);

        var deleteCategory = await client.DeleteAsync($"/api/catalog/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteCategory.StatusCode);

        var statusCode = UniqueName("STATUS").ToUpperInvariant();
        var createStatus = await client.PostAsJsonAsync(
            "/api/catalog/statuses",
            new SaveCatalogItemRequest { Code = statusCode, Name = UniqueName("Status"), IsActive = true });
        Assert.Equal(HttpStatusCode.Created, createStatus.StatusCode);
        var status = await createStatus.Content.ReadFromJsonAsync<CatalogItemDto>();
        Assert.NotNull(status);

        var updateStatus = await client.PutAsJsonAsync(
            $"/api/catalog/statuses/{status.Id}",
            new SaveCatalogItemRequest { Code = status.Code, Name = $"{status.Name} Updated", IsActive = true });
        Assert.Equal(HttpStatusCode.OK, updateStatus.StatusCode);

        var deleteStatus = await client.DeleteAsync($"/api/catalog/statuses/{status.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteStatus.StatusCode);
    }

    [Fact]
    public async Task DashboardLookupsAndReportsControllers_ShouldReturnReadModelsWithExpectedAuthorization()
    {
        using var client = _factory.CreateClient();

        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");
        SetBearerToken(client, staffToken);

        var dashboardResponse = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<DashboardDto>();
        Assert.NotNull(dashboard);
        Assert.NotNull(dashboard.Summary);

        var lookupCategoriesResponse = await client.GetAsync("/api/lookups/categories");
        Assert.Equal(HttpStatusCode.OK, lookupCategoriesResponse.StatusCode);
        var lookupCategories = await lookupCategoriesResponse.Content.ReadFromJsonAsync<List<LookupItemDto>>();
        Assert.NotEmpty(lookupCategories!);

        var lookupStatusesResponse = await client.GetAsync("/api/lookups/statuses");
        Assert.Equal(HttpStatusCode.OK, lookupStatusesResponse.StatusCode);
        var lookupStatuses = await lookupStatusesResponse.Content.ReadFromJsonAsync<List<LookupItemDto>>();
        Assert.Contains(lookupStatuses!, x => x.Code == "ISSUED");

        var staffReport = await client.GetAsync("/api/reports/summary");
        Assert.Equal(HttpStatusCode.Forbidden, staffReport.StatusCode);

        var publisherToken = await LoginAsync(client, "publisher", "publisher123", "PUBLISHER");
        SetBearerToken(client, publisherToken);

        var reportResponse = await client.GetAsync("/api/reports/summary");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        var report = await reportResponse.Content.ReadFromJsonAsync<ReportSummaryDto>();
        Assert.NotNull(report);
    }

    [Fact]
    public async Task SystemController_ShouldManageUsersAndEnforceAdminOnlyAccess()
    {
        using var client = _factory.CreateClient();

        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");
        SetBearerToken(client, staffToken);

        var forbiddenRoles = await client.GetAsync("/api/system/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRoles.StatusCode);

        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var rolesResponse = await client.GetAsync("/api/system/roles");
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var roles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleDto>>();
        var staffRole = Assert.Single(roles!, x => x.Name.Equals("STAFF", StringComparison.OrdinalIgnoreCase));

        var username = UniqueName("system_user");
        var createUser = await client.PostAsJsonAsync(
            "/api/system/users",
            new SaveUserRequest
            {
                Username = username,
                FullName = "System Controller User",
                Department = "QA",
                RoleId = staffRole.Id,
                Password = "Password123",
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        var user = await createUser.Content.ReadFromJsonAsync<UserAdminDto>();
        Assert.NotNull(user);

        var searchUsers = await client.GetAsync($"/api/system/users?keyword={Uri.EscapeDataString(username)}&includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, searchUsers.StatusCode);
        var users = await searchUsers.Content.ReadFromJsonAsync<List<UserAdminDto>>();
        Assert.Contains(users!, x => x.Id == user.Id);

        var updateUser = await client.PutAsJsonAsync(
            $"/api/system/users/{user.Id}",
            new SaveUserRequest
            {
                Username = username,
                FullName = "System Controller User Updated",
                Department = "QA",
                RoleId = staffRole.Id,
                IsActive = true
            });
        Assert.Equal(HttpStatusCode.OK, updateUser.StatusCode);

        var deleteUser = await client.DeleteAsync($"/api/system/users/{user.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteUser.StatusCode);
    }

    [Fact]
    public async Task AuditLogsController_ShouldReturnDocumentAuditLogsForAdminOnly()
    {
        using var client = _factory.CreateClient();

        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        SetBearerToken(client, adminToken);

        var stamp = UniqueName("AUDIT");
        var createDocument = await client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest
            {
                DocumentNumber = stamp,
                Title = $"Audit test {stamp}",
                ProcessingDepartment = "QA"
            });
        Assert.Equal(HttpStatusCode.Created, createDocument.StatusCode);
        var documentId = await createDocument.Content.ReadFromJsonAsync<long>();

        var auditResponse = await client.GetAsync($"/api/audit-logs?entityName=Document&entityId={documentId}");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditLogs = await auditResponse.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        Assert.Contains(auditLogs!, x => x.Action == "CREATE");

        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");
        SetBearerToken(client, staffToken);

        var forbiddenAuditResponse = await client.GetAsync($"/api/audit-logs?entityName=Document&entityId={documentId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAuditResponse.StatusCode);
    }

    private static async Task<RoleDto> GetRoleAsync(HttpClient client, string roleName)
    {
        var roles = await client.GetFromJsonAsync<List<RoleDto>>("/api/system/roles");
        return Assert.Single(roles!, x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string username,
        string password,
        string expectedRole)
    {
        return (await LoginResponseAsync(client, username, password, expectedRole)).Token;
    }

    private static async Task<LoginResponse> LoginResponseAsync(
        HttpClient client,
        string username,
        string password,
        string expectedRole)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = username,
                Password = password
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(login);
        Assert.True(login.Success);
        Assert.Equal(username, login.Username);
        Assert.Equal(expectedRole, login.Role);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));

        return login;
    }

    private static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static string UniqueName(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}
