using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Services;
using Moq;
using Xunit;

namespace DocumentManagement.Tests.Services;

public sealed class AttachmentStorageReconciliationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_ReportsMissingDbFilesAndOrphanStoredFiles()
    {
        var repository = new Mock<IAttachmentRepository>();
        var storage = new Mock<IFileStorageService>();
        var orphanPath = Path.Combine(Path.GetTempPath(), $"orphan-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(orphanPath, new byte[] { 1, 2, 3 });

        repository
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<DocumentAttachment>
            {
                new()
                {
                    Id = 10,
                    StoredFileName = "missing.pdf",
                    StoredFilePath = Path.Combine(Path.GetTempPath(), "missing-file-does-not-exist.pdf"),
                    FileHash = "hash",
                    FileSize = 12
                }
            });

        storage
            .Setup(x => x.ListStoredFilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { orphanPath });

        var service = new AttachmentStorageReconciliationService(repository.Object, storage.Object);

        try
        {
            var report = await service.ReconcileAsync();

            Assert.Single(report.MissingFiles);
            Assert.Single(report.OrphanFiles);
            Assert.Equal(0, report.CleanedOrphanFiles);
            Assert.Equal(1, report.TotalMetadataRecords);
            Assert.Equal(1, report.TotalStoredFiles);
            Assert.True(report.DryRun);
            storage.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(orphanPath);
        }
    }
}
