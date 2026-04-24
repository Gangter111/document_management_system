namespace DocumentManagement.Application.Interfaces;

public interface IFileStorageService
{
    Task<(string StoredFileName, string StoredFilePath, long FileSize, string FileHash)> SaveFileAsync(string sourceFilePath);
    bool DeleteFile(string storedFilePath);
}
