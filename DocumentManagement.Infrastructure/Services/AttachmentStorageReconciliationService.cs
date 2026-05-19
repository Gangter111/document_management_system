using DocumentManagement.Application.Interfaces;

namespace DocumentManagement.Infrastructure.Services;

public sealed class AttachmentStorageReconciliationService : IAttachmentStorageReconciliationService
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IFileStorageService _fileStorageService;

    public AttachmentStorageReconciliationService(
        IAttachmentRepository attachmentRepository,
        IFileStorageService fileStorageService)
    {
        _attachmentRepository = attachmentRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<AttachmentStorageReconciliationReport> ReconcileAsync(
        bool cleanVerifiedOrphans = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var attachments = await _attachmentRepository.GetAllAsync();
        var storedFiles = await _fileStorageService.ListStoredFilesAsync(cancellationToken);
        var metadataPaths = attachments
            .Where(x => !string.IsNullOrWhiteSpace(x.StoredFilePath))
            .Select(x => Path.GetFullPath(x.StoredFilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFiles = attachments
            .Where(x => string.IsNullOrWhiteSpace(x.StoredFilePath) || !File.Exists(x.StoredFilePath))
            .Select(x => new AttachmentStorageIssue("DB_WITHOUT_FILE", x.Id, x.StoredFileName, x.FileHash, x.FileSize))
            .ToList();

        var orphanFiles = storedFiles
            .Where(path => !metadataPaths.Contains(Path.GetFullPath(path)))
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new AttachmentStorageIssue("FILE_WITHOUT_DB", null, info.Name, null, info.Length);
            })
            .ToList();

        var cleaned = 0;
        if (cleanVerifiedOrphans && !dryRun)
        {
            foreach (var orphanPath in storedFiles.Where(path => !metadataPaths.Contains(Path.GetFullPath(path))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_fileStorageService.DeleteFile(orphanPath))
                    cleaned++;
            }
        }

        return new AttachmentStorageReconciliationReport(
            missingFiles,
            orphanFiles,
            cleaned,
            attachments.Count,
            storedFiles.Count,
            dryRun);
    }
}
