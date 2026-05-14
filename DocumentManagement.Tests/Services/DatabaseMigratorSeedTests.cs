using DocumentManagement.Infrastructure.Data;
using DocumentManagement.Infrastructure.Repositories;
using DocumentManagement.Infrastructure.Services;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DocumentManagement.Tests.Services;

public sealed class DatabaseMigratorSeedTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        "QuanLyVanBan.Tests",
        $"{Guid.NewGuid():N}.db");

    [Fact]
    public async Task DemoDataService_SeedsAndClearsExactlyOneHundredDemoDocuments()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        var factory = new SqliteConnectionFactory($"Data Source={_databasePath}");

        DatabaseMigrator.Migrate(factory);
        var dialect = new SqliteDialect();
        var authService = new AuthService(factory, new TestCurrentUserService());
        var documentService = new DocumentService(
            new DocumentRepository(factory, dialect),
            new HistoryRepository(factory, dialect),
            new AuditLogRepository(factory),
            authService);
        var demoDataService = new DemoDataService(factory, documentService);

        var seeded = await demoDataService.EnsureSeededAsync();

        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();

        Assert.Equal(100, seeded);
        Assert.Equal(100, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';"));
        Assert.Equal(68, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%' AND status_id = 4;"));
        Assert.Equal(45, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%' AND status_id = 4 AND is_expired = 0;"));
        Assert.Equal(23, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%' AND status_id = 4 AND is_expired = 1;"));
        Assert.Equal(24, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%' AND status_id = 5;"));
        Assert.Equal(8, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%' AND status_id = 1;"));
        Assert.Equal(12, ExecuteScalar<long>(connection, "SELECT COUNT(DISTINCT substr(issue_date, 1, 7)) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%' AND status_id = 4;"));
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(DISTINCT processing_department) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';") >= 7);
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(DISTINCT substr(reference_number, 1, instr(reference_number, '-') - 1)) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';") >= 8);
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE urgency_level IN ('URGENT', 'VERY_URGENT');") > 0);
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(DISTINCT category_id) FROM documents;") >= 4);

        var secondSeed = await demoDataService.EnsureSeededAsync();
        Assert.Equal(0, secondSeed);
        Assert.Equal(100, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';"));

        var cleared = await demoDataService.ClearAsync();
        Assert.Equal(100, cleared);
        Assert.Equal(0, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static T ExecuteScalar<T>(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId => "1";

        public string? Username => "demo-seed";

        public string? Role => "ADMIN";

        public string? Department => "QA";
    }
}
