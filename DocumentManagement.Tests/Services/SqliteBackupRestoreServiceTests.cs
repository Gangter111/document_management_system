using DocumentManagement.Infrastructure.Services;
using Xunit;

namespace DocumentManagement.Tests.Services;

public sealed class SqliteBackupRestoreServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dms-restore-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RestoreDatabaseAsync_RejectsUnsupportedExtension()
    {
        var service = new SqliteBackupRestoreService();
        Directory.CreateDirectory(_root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RestoreDatabaseAsync(
                new MemoryStream(new byte[] { 1, 2, 3 }),
                "restore.txt",
                Path.Combine(_root, "app.db"),
                Path.Combine(_root, "staging")));
    }

    [Fact]
    public async Task RestoreDatabaseAsync_RejectsInvalidDatabase()
    {
        var service = new SqliteBackupRestoreService();
        Directory.CreateDirectory(_root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RestoreDatabaseAsync(
                new MemoryStream(new byte[] { 1, 2, 3, 4 }),
                "restore.db",
                Path.Combine(_root, "app.db"),
                Path.Combine(_root, "staging")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // SQLite can release handles shortly after an invalid open attempt.
            }
        }
    }
}
