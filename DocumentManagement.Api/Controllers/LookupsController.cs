using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Common;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public LookupsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<LookupItemDto>>> GetCategories()
    {
        var categories = await _documentService.GetCategoriesAsync();

        var result = categories
            .Select(category => new LookupItemDto
            {
                Id = category.Id,
                Code = string.Empty,
                Name = category.Name
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("statuses")]
    public async Task<ActionResult<List<LookupItemDto>>> GetStatuses()
    {
        var statuses = await _documentService.GetStatusesAsync();

        var result = statuses
            .Select(status => new LookupItemDto
            {
                Id = status.Id,
                Code = GetStatusCode(status.Id),
                Name = status.Name
            })
            .ToList();

        return Ok(result);
    }

    private static string GetStatusCode(long statusId)
    {
        return statusId switch
        {
            1 => "DRAFT",
            2 => "PENDING_APPROVAL",
            3 => "APPROVED",
            4 => "ISSUED",
            5 => "ARCHIVED",
            6 => "REJECTED",
            _ => string.Empty
        };
    }
}