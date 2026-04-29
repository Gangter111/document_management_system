using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Application.Services;
using DocumentManagement.Domain.Entities;
using Moq;
using Xunit;

namespace DocumentManagement.Tests.Services;

public sealed class DocumentServiceTests
{
    private readonly Mock<IDocumentRepository> _documentRepository = new();
    private readonly Mock<IHistoryRepository> _historyRepository = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepository = new();
    private readonly Mock<IAuthService> _authService = new();

    public DocumentServiceTests()
    {
        _authService
            .Setup(x => x.CurrentUser)
            .Returns(new UserSession
            {
                Id = 1,
                Username = "admin",
                DisplayName = "Admin",
                Roles = new List<string> { "ADMIN" }
            });
    }

    [Fact]
    public async Task CreateAsync_ShouldWriteAuditLog_WithCreatedMarker()
    {
        AuditLog? auditLog = null;

        _documentRepository
            .Setup(x => x.CreateAsync(It.IsAny<Document>()))
            .ReturnsAsync(10);

        _auditLogRepository
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(x => auditLog = x)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        var id = await service.CreateAsync(new Document
        {
            DocumentNumber = "VB-001",
            Title = "Van ban moi"
        });

        Assert.Equal(10, id);
        Assert.NotNull(auditLog);
        Assert.Equal("Document", auditLog.EntityName);
        Assert.Equal(10, auditLog.EntityId);
        Assert.Equal("CREATE", auditLog.Action);
        Assert.Equal("CREATED", auditLog.ChangedColumns);
        Assert.Equal("admin", auditLog.Username);
        Assert.Null(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
    }

    [Fact]
    public async Task UpdateAsync_ShouldWriteAuditLog_WithChangedColumns()
    {
        AuditLog? auditLog = null;

        var existing = new Document
        {
            Id = 10,
            DocumentNumber = "VB-001",
            Title = "Old title",
            Summary = "Old summary",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "admin",
            UpdatedBy = "old-user"
        };

        _documentRepository
            .Setup(x => x.GetByIdAsync(10))
            .ReturnsAsync(existing);

        _documentRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Document>()))
            .ReturnsAsync(true);

        _auditLogRepository
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(x => auditLog = x)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        await service.UpdateAsync(new Document
        {
            Id = 10,
            DocumentNumber = "VB-001",
            Title = "New title",
            Summary = "Old summary",
            IsActive = true
        });

        Assert.NotNull(auditLog);
        Assert.Equal("UPDATE", auditLog.Action);
        Assert.Contains(nameof(Document.Title), auditLog.ChangedColumns);
        Assert.Contains(nameof(Document.UpdatedAt), auditLog.ChangedColumns);
        Assert.Contains(nameof(Document.UpdatedBy), auditLog.ChangedColumns);
        Assert.DoesNotContain(nameof(Document.Summary), auditLog.ChangedColumns);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
    }

    [Fact]
    public async Task SoftDeleteAsync_ShouldWriteAuditLog_WithDeletedColumns()
    {
        AuditLog? auditLog = null;

        var existing = new Document
        {
            Id = 10,
            DocumentNumber = "VB-001",
            Title = "Van ban",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "admin",
            UpdatedBy = "old-user"
        };

        _documentRepository
            .Setup(x => x.GetByIdAsync(10))
            .ReturnsAsync(existing);

        _documentRepository
            .Setup(x => x.DeleteAsync(10))
            .ReturnsAsync(true);

        _auditLogRepository
            .Setup(x => x.AddAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(x => auditLog = x)
            .Returns(Task.CompletedTask);

        var service = CreateService();

        await service.SoftDeleteAsync(10);

        Assert.NotNull(auditLog);
        Assert.Equal("DELETE", auditLog.Action);
        Assert.Contains(nameof(Document.IsActive), auditLog.ChangedColumns);
        Assert.Contains(nameof(Document.UpdatedAt), auditLog.ChangedColumns);
        Assert.Contains(nameof(Document.UpdatedBy), auditLog.ChangedColumns);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
    }

    private DocumentService CreateService()
    {
        return new DocumentService(
            _documentRepository.Object,
            _historyRepository.Object,
            _auditLogRepository.Object,
            _authService.Object);
    }
}
