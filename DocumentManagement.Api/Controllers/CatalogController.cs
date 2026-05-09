using DocumentManagement.Application.Interfaces;
using DocumentManagement.Application.Models;
using DocumentManagement.Api.Security;
using DocumentManagement.Contracts.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;

    public CatalogController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
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
            return Ok(ToDto(await _catalogRepository.UpdateCategoryAsync(id, ToModel(request))));
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
            return Ok(ToDto(await _catalogRepository.UpdateStatusAsync(id, ToModel(request))));
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
}
