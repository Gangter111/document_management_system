using DocumentManagement.Application.Models;
using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface IBackupService
{
    Task<string> CreateBackupAsync(string databasePath, string storageRoot, string backupFolder);

    Task RestoreBackupAsync(string backupZipPath, string targetDatabasePath, string targetStorageRoot);
}

public interface IHistoryRepository
{
    Task AddAsync(
        long? documentId,
        string actionType,
        string? actionDescription,
        string? oldValue,
        string? newValue,
        string? actionBy);

    Task<List<DocumentHistoryModel>> GetByDocumentIdAsync(long documentId);
}

public interface IReportService
{
    Task<string> ExportDocumentsToExcelAsync(List<Document> documents, string outputFolder);
}

public interface IOcrService
{
    Task<AutoFillDocumentResult> ExtractAndParseAsync(string filePath);
}