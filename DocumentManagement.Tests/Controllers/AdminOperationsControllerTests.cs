using System.Security.Claims;
using DocumentManagement.Api.Controllers;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace DocumentManagement.Tests.Controllers;

public sealed class AdminOperationsControllerTests
{
    [Fact]
    public async Task GetAttachmentReconciliation_ReturnsSanitizedReport_ForAdmin()
    {
        var reconciliation = new Mock<IAttachmentStorageReconciliationService>();
        var audit = new Mock<IAuditLogRepository>();
        var fileStorage = new Mock<IFileStorageService>();
        var configuration = new ConfigurationBuilder().Build();
        var worker = new PdfExtractionService();

        reconciliation
            .Setup(x => x.ReconcileAsync(false, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentStorageReconciliationReport(
                new List<AttachmentStorageIssue>
                {
                    new("DB_WITHOUT_FILE", 1, "hash.pdf", "hash", 10)
                },
                new List<AttachmentStorageIssue>
                {
                    new("FILE_WITHOUT_DB", null, "orphan.pdf", null, 20)
                },
                0,
                1,
                1,
                true));

        var controller = CreateController(reconciliation.Object, audit.Object, configuration, fileStorage.Object, worker, "Admin");

        var result = await controller.GetAttachmentReconciliation(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
        Assert.DoesNotContain(":", ok.Value.ToString());
    }

    [Fact]
    public async Task CleanupVerifiedAttachmentOrphans_BlocksNonAdmin()
    {
        var reconciliation = new Mock<IAttachmentStorageReconciliationService>();
        var audit = new Mock<IAuditLogRepository>();
        var fileStorage = new Mock<IFileStorageService>();
        var configuration = new ConfigurationBuilder().Build();
        var worker = new PdfExtractionService();
        var controller = CreateController(reconciliation.Object, audit.Object, configuration, fileStorage.Object, worker, "Staff");

        var result = await controller.CleanupVerifiedAttachmentOrphans(dryRun: true, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        reconciliation.Verify(x => x.ReconcileAsync(It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupVerifiedAttachmentOrphans_AuditsAttemptAndResult()
    {
        var reconciliation = new Mock<IAttachmentStorageReconciliationService>();
        var audit = new Mock<IAuditLogRepository>();
        var fileStorage = new Mock<IFileStorageService>();
        var configuration = new ConfigurationBuilder().Build();
        var worker = new PdfExtractionService();

        reconciliation
            .Setup(x => x.ReconcileAsync(true, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentStorageReconciliationReport(
                Array.Empty<AttachmentStorageIssue>(),
                Array.Empty<AttachmentStorageIssue>(),
                0,
                0,
                0,
                true));

        var controller = CreateController(reconciliation.Object, audit.Object, configuration, fileStorage.Object, worker, "Admin");

        var result = await controller.CleanupVerifiedAttachmentOrphans(dryRun: true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        audit.Verify(x => x.AddAsync(It.Is<AuditLog>(log => log.Action == "ATTACHMENT_RECONCILIATION_CLEANUP_ATTEMPT")), Times.Once);
        audit.Verify(x => x.AddAsync(It.Is<AuditLog>(log => log.Action == "ATTACHMENT_RECONCILIATION_CLEANUP_RESULT")), Times.Once);
    }

    private static AdminOperationsController CreateController(
        IAttachmentStorageReconciliationService reconciliation,
        IAuditLogRepository audit,
        IConfiguration configuration,
        IFileStorageService fileStorage,
        IPdfExtractionWorker worker,
        string role)
    {
        var controller = new AdminOperationsController(reconciliation, audit, configuration, fileStorage, worker);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "admin"),
                    new Claim(ClaimTypes.Role, role)
                }, "Test"))
            }
        };

        return controller;
    }
}
