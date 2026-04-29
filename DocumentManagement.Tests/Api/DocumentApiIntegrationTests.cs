using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentManagement.Contracts.AuditLogs;
using DocumentManagement.Contracts.Auth;
using DocumentManagement.Contracts.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DocumentManagement.Tests.Api;

public sealed class DocumentApiIntegrationTests : IClassFixture<DocumentApiFactory>
{
    private readonly DocumentApiFactory _factory;

    public DocumentApiIntegrationTests(DocumentApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DocumentFlow_ShouldEnforcePermissions_AndWriteAuditLogs()
    {
        using var client = _factory.CreateClient();

        var adminToken = await LoginAsync(client, "admin", "admin123", "ADMIN");
        var managerToken = await LoginAsync(client, "manager", "manager123", "MANAGER");
        var publisherToken = await LoginAsync(client, "publisher", "publisher123", "PUBLISHER");
        var staffToken = await LoginAsync(client, "staff", "staff123", "STAFF");

        SetBearerToken(client, staffToken);

        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var createResponse = await client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest
            {
                DocumentNumber = $"IT-{stamp}",
                Title = $"Integration Test Document {stamp}",
                Summary = "Created by integration test",
                ProcessingDepartment = "Phòng HCNS"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var documentId = await createResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(documentId > 0);

        var staffUpdateResponse = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}",
            CreateUpdateRequest(documentId, $"IT-{stamp}", "Staff blocked update"));

        Assert.Equal(HttpStatusCode.Forbidden, staffUpdateResponse.StatusCode);

        SetBearerToken(client, publisherToken);

        var publisherUpdateResponse = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}",
            CreateUpdateRequest(documentId, $"IT-{stamp}", "Publisher updated document"));

        Assert.Equal(HttpStatusCode.NoContent, publisherUpdateResponse.StatusCode);

        var publisherDeleteResponse = await client.DeleteAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.Forbidden, publisherDeleteResponse.StatusCode);

        SetBearerToken(client, managerToken);

        var managerUpdateResponse = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}",
            CreateUpdateRequest(documentId, $"IT-{stamp}", "Manager updated document"));

        Assert.Equal(HttpStatusCode.NoContent, managerUpdateResponse.StatusCode);

        var managerDeleteResponse = await client.DeleteAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.Forbidden, managerDeleteResponse.StatusCode);

        SetBearerToken(client, adminToken);

        var adminDeleteResponse = await client.DeleteAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, adminDeleteResponse.StatusCode);

        var deletedGetResponse = await client.GetAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NotFound, deletedGetResponse.StatusCode);

        var auditLogs = await client.GetFromJsonAsync<List<AuditLogDto>>(
            $"/api/audit-logs?entityName=Document&entityId={documentId}");

        Assert.NotNull(auditLogs);
        Assert.Contains(auditLogs, x => x.Action == "CREATE" && x.ChangedColumns == "CREATED");
        Assert.Contains(auditLogs, x => x.Action == "UPDATE" && ContainsChangedColumn(x, nameof(UpdateDocumentRequest.Title)));
        Assert.Contains(auditLogs, x => x.Action == "DELETE" && ContainsChangedColumn(x, "IsActive"));
        Assert.Contains(auditLogs, x => x.Action == "DELETE" && x.Username == "admin");
    }

    [Fact]
    public async Task Health_ShouldReturnOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldRejectInvalidPassword()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Username = "admin",
                Password = "wrong-password"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> LoginAsync(
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

        return login.Token;
    }

    private static UpdateDocumentRequest CreateUpdateRequest(long id, string documentNumber, string title)
    {
        return new UpdateDocumentRequest
        {
            Id = id,
            DocumentNumber = documentNumber,
            Title = title,
            Summary = "Updated by integration test",
            ProcessingDepartment = "Phòng HCNS",
            StatusId = 4,
            ConfidentialityLevel = "NORMAL",
            UrgencyLevel = "NORMAL",
            OcrStatus = "PENDING"
        };
    }

    private static bool ContainsChangedColumn(AuditLogDto log, string columnName)
    {
        return log.ChangedColumns?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(columnName, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}

public sealed class DocumentApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        "QuanLyVanBan.Tests",
        $"{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = _databasePath
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
