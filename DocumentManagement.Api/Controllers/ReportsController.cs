using DocumentManagement.Api.Security;
using DocumentManagement.Application.Interfaces;
using DocumentManagement.Contracts.Reports;
using DocumentManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentManagement.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ICatalogRepository _catalogRepository;

    public ReportsController(
        IDocumentService documentService,
        ICatalogRepository catalogRepository)
    {
        _documentService = documentService;
        _catalogRepository = catalogRepository;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> GetSummary()
    {
        var permission = RequireReportPermission();
        if (permission != null)
        {
            return permission;
        }

        var documents = (await _documentService.GetAllAsync())
            .Where(document => document.IsActive)
            .ToList();
        var categories = (await _catalogRepository.GetCategoriesAsync(includeInactive: true))
            .ToDictionary(category => category.Id, category => category.Name);

        var dto = new ReportSummaryDto
        {
            TotalDocuments = documents.Count,
            ArchivedDocuments = documents.Count(document => document.StatusId == 5),
            EffectiveDocuments = documents.Count(document => document.StatusId == 4 && !IsExpired(document)),
            ExpiredDocuments = documents.Count(IsExpired),
            DraftDocuments = documents.Count(document => document.StatusId == 1),
            ByStatus = documents
                .GroupBy(document => document.StatusText)
                .Select(group => new ReportChartItemDto { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value)
                .ToList(),
            ByCategory = documents
                .GroupBy(document => GetCategoryName(document, categories))
                .Select(group => new ReportChartItemDto { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value)
                .ToList(),
            ByDepartment = documents
                .GroupBy(GetDepartment)
                .Select(group => new ReportChartItemDto { Name = group.Key, Value = group.Count() })
                .OrderByDescending(item => item.Value)
                .Take(10)
                .ToList()
        };

        return Ok(dto);
    }

    private static bool IsExpired(Document document)
    {
        if (document.IsExpired)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(document.DueDate)
               && DateTime.TryParse(document.DueDate, out var dueDate)
               && dueDate.Date < DateTime.Today;
    }

    private static string GetDepartment(Document document)
    {
        if (!string.IsNullOrWhiteSpace(document.ProcessingDepartment))
        {
            return document.ProcessingDepartment.Trim();
        }

        if (!string.IsNullOrWhiteSpace(document.SenderName))
        {
            return document.SenderName.Trim();
        }

        return "Chưa xác định";
    }

    private static string GetCategoryName(Document document, IReadOnlyDictionary<long, string> categories)
    {
        if (document.CategoryId.HasValue
            && categories.TryGetValue(document.CategoryId.Value, out var categoryName)
            && !string.IsNullOrWhiteSpace(categoryName))
        {
            return categoryName;
        }

        return "Chưa phân loại";
    }

    private ActionResult? RequireReportPermission()
    {
        if (User.HasAnyRole("Admin", "Manager", "Publisher"))
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền xem báo cáo.");
    }
}
