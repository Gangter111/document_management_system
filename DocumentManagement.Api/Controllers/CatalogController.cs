using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Api.Security;
using DocumentManagement.Contracts.Catalog;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public CatalogController(
        ICatalogRepository catalogRepository,
        IAuditLogRepository auditLogRepository)
    {
        _catalogRepository = catalogRepository;
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> GetCategories(
        [FromQuery] bool includeInactive = false)
    {
        return Ok((await _catalogRepository.GetCategoriesAsync(includeInactive)).Select(ToDto).ToList());
    }

    [HttpPost("categories")]
    public async Task<ActionResult<CatalogItemDto>> CreateCategory([FromBody] SaveCatalogItemRequest request)
    {
        var permission = RequireCatalogWritePermission();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            var item = await _catalogRepository.CreateCategoryAsync(ToModel(request));
            await AddCatalogAuditAsync("Category", item.Id, "CATEGORY_CREATE", "CREATED", item);
            return CreatedAtAction(nameof(GetCategories), new { includeInactive = true }, ToDto(item));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("categories/{id:long}")]
    public async Task<ActionResult<CatalogItemDto>> UpdateCategory(
        long id,
        [FromBody] SaveCatalogItemRequest request)
    {
        var permission = RequireCatalogWritePermission();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            var item = await _catalogRepository.UpdateCategoryAsync(id, ToModel(request));
            await AddCatalogAuditAsync("Category", item.Id, "CATEGORY_UPDATE", "UPDATED", item);
            return Ok(ToDto(item));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("categories/{id:long}")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        var permission = RequireCatalogWritePermission();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            await _catalogRepository.DeleteCategoryAsync(id);
            await AddCatalogAuditAsync("Category", id, "CATEGORY_DELETE", "IsActive", null);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("statuses")]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> GetStatuses(
        [FromQuery] bool includeInactive = false)
    {
        return Ok((await _catalogRepository.GetStatusesAsync(includeInactive)).Select(ToDto).ToList());
    }

    [HttpPost("statuses")]
    public async Task<ActionResult<CatalogItemDto>> CreateStatus([FromBody] SaveCatalogItemRequest request)
    {
        var permission = RequireCatalogWritePermission();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            var item = await _catalogRepository.CreateStatusAsync(ToModel(request));
            await AddCatalogAuditAsync("Status", item.Id, "STATUS_CREATE", "CREATED", item);
            return CreatedAtAction(nameof(GetStatuses), new { includeInactive = true }, ToDto(item));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("statuses/{id:long}")]
    public async Task<ActionResult<CatalogItemDto>> UpdateStatus(
        long id,
        [FromBody] SaveCatalogItemRequest request)
    {
        var permission = RequireCatalogWritePermission();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            var item = await _catalogRepository.UpdateStatusAsync(id, ToModel(request));
            await AddCatalogAuditAsync("Status", item.Id, "STATUS_UPDATE", "UPDATED", item);
            return Ok(ToDto(item));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("statuses/{id:long}")]
    public async Task<IActionResult> DeleteStatus(long id)
    {
        var permission = RequireCatalogWritePermission();
        if (permission != null)
        {
            return permission;
        }

        try
        {
            await _catalogRepository.DeleteStatusAsync(id);
            await AddCatalogAuditAsync("Status", id, "STATUS_DELETE", "IsActive", null);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private ActionResult? RequireCatalogWritePermission()
    {
        if (User.HasAnyRole("Admin", "Manager"))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền quản lý danh mục.");
    }

    private static SaveCatalogItemModel ToModel(SaveCatalogItemRequest request)
    {
        return new SaveCatalogItemModel
        {
            Code = request.Code,
            Name = request.Name,
            IsActive = request.IsActive
        };
    }

    private static CatalogItemDto ToDto(CatalogItemModel item)
    {
        return new CatalogItemDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            IsActive = item.IsActive
        };
    }

    private Task AddCatalogAuditAsync(
        string entityName,
        long entityId,
        string action,
        string changedColumns,
        CatalogItemModel? item)
    {
        return _auditLogRepository.AddAsync(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ChangedColumns = changedColumns,
            NewValues = item == null
                ? "Catalog item deactivated"
                : $"Code={item.Code};Name={item.Name};IsActive={item.IsActive}",
            Username = User.GetUsername(),
            CreatedAt = DateTime.UtcNow
        });
    }
}
