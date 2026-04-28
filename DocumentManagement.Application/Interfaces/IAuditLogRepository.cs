using DocumentManagement.Domain.Entities;

namespace DocumentManagement.Application.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);

    Task<List<AuditLog>> GetByEntityAsync(string entityName, long entityId);

    Task<int> CountByEntityActionAsync(string entityName, long entityId, string action);
}