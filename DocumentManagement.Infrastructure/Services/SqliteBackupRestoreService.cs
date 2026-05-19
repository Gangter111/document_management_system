using DocumentManagement.Application.Interfaces;
using Microsoft.Data.Sqlite;

namespace DocumentManagement.Infrastructure.Services;

public sealed class SqliteBackupRestoreService : ISqliteBackupRestoreService
{
    private const long MaxRestoreBytes = 200_000_000;

    public async Task<string> CreateDatabaseBackupAsync(
        string sourceDatabasePath,
        string backupFolder,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceDatabasePath))
            throw new FileNotFoundException("Database file was not found.");

        Directory.CreateDirectory(backupFolder);
        var backupPath = Path.Combine(backupFolder, $"document_management_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        await BackupDatabaseFileAsync(sourceDatabasePath, backupPath, cancellationToken);
        return backupPath;
    }

    public async Task<SqliteRestoreResult> RestoreDatabaseAsync(
        Stream uploadedDatabase,
        string originalFileName,
        string targetDatabasePath,
        string stagingFolder,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!string.Equals(extension, ".db", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".sqlite", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Restore file must be .db or .sqlite.");

        var targetDirectory = Path.GetDirectoryName(targetDatabasePath)
            ?? throw new InvalidOperationException("Database target directory could not be resolved.");

        Directory.CreateDirectory(targetDirectory);
        Directory.CreateDirectory(stagingFolder);

        var stagingPath = Path.Combine(stagingFolder, $"restore_{Guid.NewGuid():N}{extension}");
        var safetyBackupPath = Path.Combine(targetDirectory, $"document_management_before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        var rollbackPath = Path.Combine(stagingFolder, $"rollback_{Guid.NewGuid():N}.db");

        try
        {
            await CopyBoundedAsync(uploadedDatabase, stagingPath, MaxRestoreBytes, cancellationToken);
            await ValidateSqliteDatabaseAsync(stagingPath, cancellationToken);

            SqliteConnection.ClearAllPools();

            if (File.Exists(targetDatabasePath))
            {
                await BackupDatabaseFileAsync(targetDatabasePath, safetyBackupPath, cancellationToken);
                File.Copy(targetDatabasePath, rollbackPath, overwrite: true);
            }

            File.Copy(stagingPath, targetDatabasePath, overwrite: true);
            DeleteIfExists(targetDatabasePath + "-wal");
            DeleteIfExists(targetDatabasePath + "-shm");

            await ValidateSqliteDatabaseAsync(targetDatabasePath, cancellationToken);
            return new SqliteRestoreResult(true, safetyBackupPath);
        }
        catch
        {
            if (File.Exists(rollbackPath))
            {
                File.Copy(rollbackPath, targetDatabasePath, overwrite: true);
            }

            throw;
        }
        finally
        {
            SafeDeleteIfExists(stagingPath);
            SafeDeleteIfExists(rollbackPath);
            SqliteConnection.ClearAllPools();
        }
    }

    private static async Task CopyBoundedAsync(
        Stream source,
        string targetPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        await using var target = new FileStream(
            targetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new InvalidOperationException("Restore file exceeds the configured size limit.");

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task BackupDatabaseFileAsync(
        string sourceDatabasePath,
        string backupPath,
        CancellationToken cancellationToken)
    {
        DeleteIfExists(backupPath);
        var backupDirectory = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrWhiteSpace(backupDirectory))
            Directory.CreateDirectory(backupDirectory);

        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        var backupConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        await using var source = new SqliteConnection(sourceConnectionString);
        await using var destination = new SqliteConnection(backupConnectionString);
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private static async Task ValidateSqliteDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT COUNT(*)
FROM sqlite_master
WHERE type = 'table'
  AND name IN ('documents', 'audit_logs', 'Users');";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var count = Convert.ToInt32(result ?? 0);
            if (count < 3)
                throw new InvalidOperationException("Restore file is not a valid Document Management database.");
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException("Restore file is not a valid SQLite database.", ex);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void SafeDeleteIfExists(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch
        {
            // Best effort cleanup for restore staging files.
        }
    }
}
