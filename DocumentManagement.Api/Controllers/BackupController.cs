using System.Security.Claims;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Api.Security;
using DocumentManagement.Domain.Entities;
using DocumentManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BackupController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ISqliteBackupRestoreService _backupRestoreService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly RuntimePaths _runtimePaths;

    public BackupController(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ISqliteBackupRestoreService backupRestoreService,
        IAuditLogRepository auditLogRepository,
        RuntimePaths runtimePaths)
    {
        _configuration = configuration;
        _environment = environment;
        _backupRestoreService = backupRestoreService;
        _auditLogRepository = auditLogRepository;
        _runtimePaths = runtimePaths;
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download()
    {
        if (!IsAdmin(GetRole()))
            return StatusCode(StatusCodes.Status403Forbidden, "Backup requires administrator permission.");

        if (!IsSqliteProvider())
            return BadRequest("Direct API backup is supported only for SQLite.");

        var databasePath = ResolveDatabasePath();
        if (!System.IO.File.Exists(databasePath))
            return NotFound("Database file was not found.");

        var backupPath = await _backupRestoreService.CreateDatabaseBackupAsync(
            databasePath,
            ResolveBackupPath(),
            HttpContext.RequestAborted);

        await WriteAuditAsync("BACKUP_CREATE", success: true);
        var bytes = await System.IO.File.ReadAllBytesAsync(backupPath, HttpContext.RequestAborted);

        return File(bytes, "application/octet-stream", Path.GetFileName(backupPath));
    }

    [HttpPost("restore")]
    [RequestSizeLimit(200_000_000)]
    public async Task<IActionResult> Restore(IFormFile file)
    {
        if (!IsAdmin(GetRole()))
            return StatusCode(StatusCodes.Status403Forbidden, "Restore requires administrator permission.");

        if (!_configuration.GetValue<bool>("Maintenance:RestoreEnabled"))
        {
            await WriteAuditAsync("RESTORE_REJECTED", success: false, reason: "maintenance_required");
            return StatusCode(StatusCodes.Status409Conflict, "Restore requires Maintenance:RestoreEnabled=true.");
        }

        if (!IsSqliteProvider())
            return BadRequest("Direct API restore is supported only for SQLite.");

        if (file == null || file.Length <= 0)
            return BadRequest("Restore file is invalid.");

        await WriteAuditAsync("RESTORE_ATTEMPT", success: true);

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _backupRestoreService.RestoreDatabaseAsync(
                stream,
                file.FileName,
                ResolveDatabasePath(),
                Path.Combine(_environment.ContentRootPath, "restore-temp"),
                HttpContext.RequestAborted);

            await WriteAuditAsync("RESTORE_SUCCESS", success: true);

            return Ok(new
            {
                success = result.Success,
                message = "Restore completed.",
                safetyBackup = Path.GetFileName(result.SafetyBackupPath)
            });
        }
        catch
        {
            await WriteAuditAsync("RESTORE_FAILURE", success: false, reason: "restore_failed");
            throw;
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        if (!IsSqliteProvider())
        {
            return Ok(new
            {
                provider = DatabaseProvider.SqlServer.ToString(),
                backupMode = "external"
            });
        }

        var databasePath = ResolveDatabasePath();
        return Ok(new
        {
            provider = DatabaseProvider.Sqlite.ToString(),
            exists = System.IO.File.Exists(databasePath)
        });
    }

    private string ResolveDatabasePath()
    {
        var databasePath = _configuration["Database:Path"];
        if (string.IsNullOrWhiteSpace(databasePath))
            databasePath = Path.Combine("database", "app.db");

        if (Path.IsPathRooted(databasePath))
            return databasePath;

        var contentRootPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, databasePath));
        return System.IO.File.Exists(contentRootPath)
            ? contentRootPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, databasePath));
    }

    private string ResolveBackupPath()
    {
        return _runtimePaths.BackupPath;
    }

    private bool IsSqliteProvider()
    {
        var provider = _configuration.GetSection("Database").Get<DatabaseOptions>()?.GetProvider()
                       ?? DatabaseProvider.Sqlite;

        return provider == DatabaseProvider.Sqlite;
    }

    private string GetRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value
               ?? User.FindFirst("role")?.Value
               ?? string.Empty;
    }

    private string GetUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.Identity?.Name
               ?? "system";
    }

    private static bool IsAdmin(string role)
    {
        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase);
    }

    private async Task WriteAuditAsync(string action, bool success, string? reason = null)
    {
        var detail = $"correlationId={HttpContext.TraceIdentifier};result={(success ? "success" : "failure")}";
        if (!string.IsNullOrWhiteSpace(reason))
            detail = $"{detail};reason={reason}";

        await _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = "Backup",
            EntityId = 0,
            Action = action,
            ChangedColumns = success ? "SUCCESS" : "FAILURE",
            NewValues = detail,
            Username = GetUsername(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
