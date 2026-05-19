namespace DocumentManagement.Application.Interfaces;

public interface IFileStorageService
{
    Task<(string StoredFileName, string StoredFilePath, long FileSize, string FileHash)> SaveFileAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default);

    bool DeleteFile(string storedFilePath);

    Task<IReadOnlyList<string>> ListStoredFilesAsync(CancellationToken cancellationToken = default);
}
