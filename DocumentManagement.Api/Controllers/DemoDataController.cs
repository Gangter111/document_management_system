using System.Security.Claims;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.DemoData;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/demo-data")]
[Authorize]
public class DemoDataController : ControllerBase
{
    private readonly IDemoDataService _demoDataService;
    private readonly IAuditLogRepository _auditLogRepository;

    public DemoDataController(
        IDemoDataService demoDataService,
        IAuditLogRepository auditLogRepository)
    {
        _demoDataService = demoDataService;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet("count")]
    public async Task<ActionResult<DemoDataResponse>> Count(CancellationToken cancellationToken)
    {
        var count = await _demoDataService.GetActiveDemoCountAsync(cancellationToken);
        return Ok(new DemoDataResponse
        {
            ActiveDemoCount = count,
            Message = "Demo data count loaded."
        });
    }

    [HttpPost("seed")]
    public async Task<ActionResult<DemoDataResponse>> Seed(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin();
        if (permissionResult != null)
            return permissionResult;

        var affected = await _demoDataService.EnsureSeededAsync(cancellationToken);
        var count = await _demoDataService.GetActiveDemoCountAsync(cancellationToken);
        await WriteAuditAsync("DEMO_DATA_SEED", affected, count);

        return Ok(new DemoDataResponse
        {
            ActiveDemoCount = count,
            AffectedCount = affected,
            Message = affected == 0
                ? "Demo data is already present."
                : "Demo data seeded."
        });
    }

    [HttpDelete]
    public async Task<ActionResult<DemoDataResponse>> Clear(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin();
        if (permissionResult != null)
            return permissionResult;

        var affected = await _demoDataService.ClearAsync(cancellationToken);
        var count = await _demoDataService.GetActiveDemoCountAsync(cancellationToken);
        await WriteAuditAsync("DEMO_DATA_CLEAR", affected, count);

        return Ok(new DemoDataResponse
        {
            ActiveDemoCount = count,
            AffectedCount = affected,
            Message = "Demo data cleared."
        });
    }

    private ActionResult? RequireAdmin()
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value
                   ?? User.FindFirst("role")?.Value
                   ?? string.Empty;

        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, "Administrator permission is required.");
    }

    private string GetUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.Identity?.Name
               ?? "system";
    }

    private async Task WriteAuditAsync(string action, int affected, int activeCount)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = "DemoData",
            EntityId = 0,
            Action = action,
            ChangedColumns = "SUCCESS",
            NewValues = $"correlationId={HttpContext.TraceIdentifier};affected={affected};activeCount={activeCount}",
            Username = GetUsername(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
