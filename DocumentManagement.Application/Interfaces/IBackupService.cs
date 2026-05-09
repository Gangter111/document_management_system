using System.Threading;

namespace DocumentManagement.Application.Interfaces;

public interface IBackupService
{
    Task<string> CreateBackupAsync(
        string databasePath,
        string storageRoot,
        string backupFolder,
        CancellationToken cancellationToken = default);

    Task RestoreBackupAsync(
        string backupZipPath,
        string targetDatabasePath,
        string targetStorageRoot,
        CancellationToken cancellationToken = default);
}
