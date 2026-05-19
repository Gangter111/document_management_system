using System.Security.Claims;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin-operations")]
public sealed class AdminOperationsController : ControllerBase
{
    private readonly IAttachmentStorageReconciliationService _reconciliationService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IConfiguration _configuration;
    private readonly IFileStorageService _fileStorageService;
    private readonly IPdfExtractionWorker _pdfExtractionWorker;

    public AdminOperationsController(
        IAttachmentStorageReconciliationService reconciliationService,
        IAuditLogRepository auditLogRepository,
        IConfiguration configuration,
        IFileStorageService fileStorageService,
        IPdfExtractionWorker pdfExtractionWorker)
    {
        _reconciliationService = reconciliationService;
        _auditLogRepository = auditLogRepository;
        _configuration = configuration;
        _fileStorageService = fileStorageService;
        _pdfExtractionWorker = pdfExtractionWorker;
    }

    [HttpGet("maintenance/status")]
    public ActionResult<object> GetMaintenanceStatus()
    {
        var permission = RequireAdmin();
        if (permission != null)
            return permission;

        return Ok(new
        {
            maintenanceMode = _configuration.GetValue<bool>("Maintenance:Mode")
        });
    }

    [HttpGet("restore/status")]
    public ActionResult<object> GetRestoreStatus()
    {
        var permission = RequireAdmin();
        if (permission != null)
            return permission;

        return Ok(new
        {
            restoreEnabled = _configuration.GetValue<bool>("Maintenance:RestoreEnabled")
        });
    }

    [HttpGet("attachments/reconciliation")]
    public async Task<ActionResult<object>> GetAttachmentReconciliation(CancellationToken cancellationToken)
    {
        var permission = RequireAdmin();
        if (permission != null)
            return permission;

        var report = await _reconciliationService.ReconcileAsync(cleanVerifiedOrphans: false, dryRun: true, cancellationToken);
        return Ok(ToResponse(report));
    }

    [HttpPost("attachments/reconciliation/cleanup")]
    public async Task<ActionResult<object>> CleanupVerifiedAttachmentOrphans(
        [FromQuery] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var permission = RequireAdmin();
        if (permission != null)
            return permission;

        await WriteAuditAsync("ATTACHMENT_RECONCILIATION_CLEANUP_ATTEMPT", success: true, detail: null);

        var report = await _reconciliationService.ReconcileAsync(cleanVerifiedOrphans: true, dryRun, cancellationToken);

        await WriteAuditAsync(
            "ATTACHMENT_RECONCILIATION_CLEANUP_RESULT",
            success: true,
            detail: $"dryRun={report.DryRun};cleaned={report.CleanedOrphanFiles};missing={report.MissingFiles.Count};orphans={report.OrphanFiles.Count}");

        return Ok(ToResponse(report));
    }

    [HttpGet("operations/status")]
    public async Task<ActionResult<object>> GetOperationsStatus(CancellationToken cancellationToken)
    {
        var permission = RequireAdmin();
        if (permission != null)
            return permission;

        var storageAvailable = false;
        try
        {
            await _fileStorageService.ListStoredFilesAsync(cancellationToken);
            storageAvailable = true;
        }
        catch
        {
            storageAvailable = false;
        }

        var backupPath = _configuration["Backup:Path"];
        var backupConfigured = !string.IsNullOrWhiteSpace(backupPath);

        return Ok(new
        {
            storageAvailable,
            backupConfigured,
            extractionReady = _pdfExtractionWorker != null,
            maintenanceMode = _configuration.GetValue<bool>("Maintenance:Mode"),
            restoreEnabled = _configuration.GetValue<bool>("Maintenance:RestoreEnabled")
        });
    }

    private ActionResult? RequireAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value
                   ?? User.FindFirst("role")?.Value
                   ?? string.Empty;

        return IsAdmin(role)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, "Administrator permission is required.");
    }

    private static bool IsAdmin(string role)
    {
        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
    }

    private static object ToResponse(AttachmentStorageReconciliationReport report)
    {
        return new
        {
            missingFiles = report.MissingFiles.Select(ToIssue),
            orphanFiles = report.OrphanFiles.Select(ToIssue),
            cleanedOrphanFiles = report.CleanedOrphanFiles,
            totalMetadataRecords = report.TotalMetadataRecords,
            totalStoredFiles = report.TotalStoredFiles,
            dryRun = report.DryRun
        };
    }

    private static object ToIssue(AttachmentStorageIssue issue)
    {
        return new
        {
            issueType = issue.IssueType,
            attachmentId = issue.AttachmentId,
            storedFileName = issue.StoredFileName,
            fileHash = issue.FileHash,
            fileSize = issue.FileSize
        };
    }

    private string GetUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.Identity?.Name
               ?? "system";
    }

    private async Task WriteAuditAsync(string action, bool success, string? detail)
    {
        var auditDetail = $"correlationId={HttpContext.TraceIdentifier};result={(success ? "success" : "failure")}";
        if (!string.IsNullOrWhiteSpace(detail))
            auditDetail = $"{auditDetail};{detail}";

        await _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = "AttachmentStorage",
            EntityId = 0,
            Action = action,
            ChangedColumns = success ? "SUCCESS" : "FAILURE",
            NewValues = auditDetail,
            Username = GetUsername(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
