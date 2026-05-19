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
    Task<AutoFillDocumentResult> ExtractAndParseAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public interface IImagePreprocessor
{
    Task<string> PreprocessAsync(
        string imagePath,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}

public interface ILayoutAnalyzer
{
    DocumentLayout Analyze(OcrDocument ocrDocument);
}

public interface IOcrNormalizationService
{
    string NormalizeForComparison(string value);
    string NormalizeForDisplay(string value);
}

public interface ITextBlockSegmenter
{
    IReadOnlyList<TextBlock> Segment(IReadOnlyList<OcrLine> lines);
}

public interface IDocumentClassifier
{
    DocumentClassification Classify(DocumentLayout layout);
}

public interface IFieldExtractor
{
    IReadOnlyList<ExtractedFieldCandidate> ExtractCandidates(
        DocumentLayout layout,
        DocumentClassification classification);
}

public interface ISemanticParser
{
    SemanticParseResult Parse(
        DocumentLayout layout,
        DocumentClassification classification,
        IReadOnlyList<ExtractedFieldCandidate> candidates);
}

public interface IValidationService
{
    ValidationResult Validate(
        DocumentLayout layout,
        SemanticParseResult semanticResult);
}

public interface IConfidenceScorer
{
    double Score(ExtractedFieldCandidate candidate, DocumentLayout layout);
}

public interface IPdfExtractionWorker
{
    /// <summary>
    /// Extracts text and metadata from a PDF under caller-provided timeout/cancellation.
    /// A future implementation can replace the in-process worker with a process-isolated worker
    /// without changing API/controller contracts.
    /// </summary>
    Task<AutoFillDocumentResult> ExtractAsync(
        PdfExtractionJobRequest request,
        CancellationToken cancellationToken = default);

    Task<AutoFillDocumentResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public interface IPdfPageImageRenderer
{
    Task<IReadOnlyList<PdfRenderedPageImage>> RenderPagesAsync(
        PdfExtractionJobRequest request,
        string workingDirectory,
        int maxPages,
        int dpi,
        long maxImageBytes,
        CancellationToken cancellationToken = default);
}

public interface IPdfOcrEngine
{
    Task<PdfOcrPageResult> ReadTextAsync(
        string imagePath,
        string language,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public sealed record SqliteRestoreResult(bool Success, string SafetyBackupPath);

public interface ISqliteBackupRestoreService
{
    Task<string> CreateDatabaseBackupAsync(
        string sourceDatabasePath,
        string backupFolder,
        CancellationToken cancellationToken = default);

    Task<SqliteRestoreResult> RestoreDatabaseAsync(
        Stream uploadedDatabase,
        string originalFileName,
        string targetDatabasePath,
        string stagingFolder,
        CancellationToken cancellationToken = default);
}

public sealed record AttachmentStorageIssue(
    string IssueType,
    long? AttachmentId,
    string? StoredFileName,
    string? FileHash,
    long? FileSize);

public sealed record AttachmentStorageReconciliationReport(
    IReadOnlyList<AttachmentStorageIssue> MissingFiles,
    IReadOnlyList<AttachmentStorageIssue> OrphanFiles,
    int CleanedOrphanFiles,
    int TotalMetadataRecords,
    int TotalStoredFiles,
    bool DryRun);

public interface IAttachmentStorageReconciliationService
{
    Task<AttachmentStorageReconciliationReport> ReconcileAsync(
        bool cleanVerifiedOrphans = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default);
}
