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
    public async Task DemoDataService_SeedsAndClearsExactlyFortyDemoDocuments()
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

        Assert.Equal(40, seeded);
        Assert.Equal(40, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';"));
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE status_id = 5;") > 0);
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE urgency_level IN ('URGENT', 'VERY_URGENT');") > 0);
        Assert.True(ExecuteScalar<long>(connection, "SELECT COUNT(DISTINCT category_id) FROM documents;") >= 4);

        var secondSeed = await demoDataService.EnsureSeededAsync();
        Assert.Equal(0, secondSeed);
        Assert.Equal(40, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM documents WHERE is_active = 1 AND document_number LIKE 'DEMO-2026-%' AND notes LIKE '%DEMO-QA-2026-05%';"));

        var cleared = await demoDataService.ClearAsync();
        Assert.Equal(40, cleared);
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
