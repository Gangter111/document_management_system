using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.AuditLogs;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogsController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> GetByEntity(
        [FromQuery] string entityName,
        [FromQuery] long entityId)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            return BadRequest("entityName không được để trống.");
        }

        if (entityId <= 0)
        {
            return BadRequest("entityId không hợp lệ.");
        }

        var logs = await _auditLogRepository.GetByEntityAsync(entityName.Trim(), entityId);

        return Ok(logs.Select(ToDto).ToList());
    }

    private static AuditLogDto ToDto(AuditLog log)
    {
        return new AuditLogDto
        {
            Id = log.Id,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            Action = log.Action,
            OldValues = log.OldValues,
            NewValues = log.NewValues,
            Username = log.Username,
            CreatedAt = log.CreatedAt
        };
    }
}